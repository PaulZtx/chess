using System.Runtime.CompilerServices;
using System.Security.Claims;
using BackChess.Configurations;
using BackChess.Models;
using BackChess.Models.Events;
using BackChess.Services;
using ChessInfrastructure;
using ChessInfrastructure.Entities;
using ChessTrainer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackChess.Controllers;

[Authorize]
[Route("api/games")]
public class GameController : RootController
{
    /// <summary>
    /// Контекст хранения участников и партий
    /// </summary>
    private readonly Storage _storage;

    /// <summary>
    /// Общая с worker доставка игровых событий в SSE-подписки
    /// </summary>
    private readonly ChessBridgeService _chessBridgeService;

    /// <summary>
    /// Создает контроллер игровых операций
    /// </summary>
    /// <param name="storage">Контекст хранения участников и партий</param>
    /// <param name="chessBridgeService">Доставка игровых событий подписчикам</param>
    /// <param name="logger">Логгер игровых операций</param>
    public GameController(Storage storage, ChessBridgeService chessBridgeService, ILogger<GameController> logger) : base(logger)
    {
        _storage = storage;
        _chessBridgeService = chessBridgeService;
    }

    /// <summary>
    /// Создает тренировочную партию и при необходимости задание на первый ход бота
    /// </summary>
    /// <param name="gameDto">Участник, сторона, сложность и начальная позиция партии</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Идентификатор созданной партии или ошибка проверки участников</returns>
    [HttpPost("create/training-game")]
    public async Task<IActionResult> StartTrainingGame([FromBody] TrainingGameDto gameDto,
        CancellationToken cancellationToken)
    {
        //TODO: сделать проверки на права доступа пользователя к боту
        
        // Идентификатор игрового профиля из авторизации текущего пользователя
        if (!TryGetPlayerId(User, out var playerId))
            return Unauthorized();

        // Игровой профиль текущего пользователя
        var humanPlayer = await _storage.Players.FirstOrDefaultAsync(
            _ => _.Id == playerId && _.IsHuman, cancellationToken);
       
        if (humanPlayer is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid user player"
            });
        }
        
        // Существующий бот, выбранный соперником
        var botPlayer = await _storage.Players.FirstOrDefaultAsync(
            _ => _.Id == gameDto.BotId && !_.IsHuman, cancellationToken);
        
        if (botPlayer is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid bot id"
            });
        }
        
        // Новая тренировочная партия с настройками выбранного соперника
        var game = new Game()
        {
            StartFen = string.IsNullOrWhiteSpace(gameDto.StartFen) ? GameConfig.BaseStartFen : gameDto.StartFen,
            WhitePlayer = gameDto.IsWhite ? humanPlayer : botPlayer,
            BlackPlayer = !gameDto.IsWhite ? humanPlayer : botPlayer,
            Mode = GameMode.Training,
            OpponentSkillLevel = gameDto.OpponentSkillLevel,
            CreatedAtDateTime = DateTime.UtcNow
        };

        // Начальная позиция определяет очередь независимо от выбранного пользователем цвета
        if (!Chess.ChessBoard.TryLoadFromFen(game.StartFen, out var board))
            return BadRequest(new { message = "Некорректная начальная FEN" });

        _storage.Games.Add(game);

        // В начале партии PositionMoveId остается null, а версия равна нулю
        var nextPlayer = board.Turn == Chess.PieceColor.White ? game.WhitePlayer : game.BlackPlayer;
        if (!board.IsEndGame && !nextPlayer.IsHuman)
            AddBotMoveJob(game, game.StartFen, game.CreatedAtDateTime);

        await _storage.SaveChangesAsync(cancellationToken);
        
        return Ok(game.Id);
    }

    /// <summary>
    /// Принимает легальный ход и сохраняет позицию с необходимыми заданиями анализа и ответа бота
    /// </summary>
    /// <param name="playerMove">Партия и предложенный ход</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Сохраненный ход, новая позиция и версия партии или ошибка проверки хода</returns>
    [HttpPost("make-move")]
    public async Task<IActionResult> MakeMove([FromBody] PlayerMove playerMove,
        CancellationToken cancellationToken)
    {
        if (!TryGetPlayerId(User, out var playerId))
            return Unauthorized();

        // Доступная игроку незавершенная партия
        var game = await _storage.Games
            .Include(_ => _.CurrentMove)
            .Include(_ => _.WhitePlayer)
            .Include(_ => _.BlackPlayer)
            .FirstOrDefaultAsync(
            _ => _.Id == playerMove.GameId
                && (_.BlackPlayerId == playerId || _.WhitePlayerId == playerId)
                && _.GameStatus == WinStatus.None, cancellationToken);
        
        if (game is null)
            return BadRequest();

        if (game.Version != playerMove.ExpectedVersion)
            return Conflict(new { message = "Позиция партии изменилась", version = game.Version });

        // Сохраненная позиция, из которой пользователь пытается сделать ход
        var fen = game.CurrentMove is null ? game.StartFen : game.CurrentMove.FenAfter;

        // Очередь задается исходной FEN, в том числе при старте с позиции за черных
        if (!Chess.ChessBoard.TryLoadFromFen(fen, out var board))
            return Problem("Не удалось загрузить сохраненную позицию партии");

        // Автор следующего хода определяется стороной, а не номером узла или данными клиента
        var movingPlayerId = board.Turn == Chess.PieceColor.White ? game.WhitePlayerId : game.BlackPlayerId;
        if (movingPlayerId != playerId)
            return Conflict(new { message = "Сейчас ход другого игрока" });

        if (!ChessMoveValidator.IsLegalMove(fen, playerMove.SanMove, out var fenAfter) ||
            !ChessMoveConverter.TrySanToUci(fen, playerMove.SanMove, out var uciMove)) 
        {
            return BadRequest();
        }

        // Очередь и завершение проверяются по позиции после принятого хода
        if (!Chess.ChessBoard.TryLoadFromFen(fenAfter, out var boardAfter))
            return Problem("Не удалось загрузить позицию после хода");

        // Момент принятия хода и время с последнего изменения позиции
        var dateTimeNow = DateTime.UtcNow;
        var thinkTimeMs = Math.Max(0, (long)(dateTimeNow - game.UpdateDateTime).TotalMilliseconds);

        // Новый неизменяемый узел дерева, который нужно явно добавить в контекст
        var move = game.CurrentMove is null
            ? Move.CreateFirst(game, movingPlayerId, uciMove, fenAfter, thinkTimeMs)
            : Move.CreateChild(game, game.CurrentMove, movingPlayerId, uciMove, fenAfter, thinkTimeMs);

        _storage.Moves.Add(move);
        game.CurrentMove = move;
        game.CurrentMoveId = move.Id;
        game.UpdateDateTime = dateTimeNow;
        game.Version = checked(game.Version + 1);

        if (boardAfter.IsEndGame)
        {
            // Итоговая ветка фиксируется отдельно от указателя текущей позиции
            game.FinalMoveId = move.Id;
            game.FinalMove = move;
            // Победившая сторона или null при ничьей
            var winner = boardAfter.EndGame!.WonSide;
            game.GameStatus = winner == Chess.PieceColor.White ? WinStatus.WhiteWin
                : winner == Chess.PieceColor.Black ? WinStatus.BlackWin : WinStatus.Draw;
        }

        if (game.Mode == GameMode.Training)
            AddMoveAnalysisJob(game, move, playerId, fen, dateTimeNow);

        // Ответ зависит от следующего участника, а не от режима партии
        var nextPlayer = boardAfter.Turn == Chess.PieceColor.White ? game.WhitePlayer : game.BlackPlayer;
        if (!boardAfter.IsEndGame && !nextPlayer.IsHuman)
            AddBotMoveJob(game, fenAfter, dateTimeNow);

        try
        {
            await _storage.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Партия изменилась во время сохранения хода" });
        }

        return Ok(new
        {
            gameId = game.Id,
            moveId = move.Id,
            playerId = move.PlayerId,
            uciMove = move.UciMove,
            fenAfter = move.FenAfter,
            version = game.Version
        });
    }
    
    /// <summary>
    /// Открывает SSE-поток новых игровых событий для участника указанной партии
    /// </summary>
    /// <param name="gameId">Идентификатор партии, доступ к которой проверяется на сервере</param>
    /// <param name="cancellationToken">Токен отключения клиента</param>
    /// <returns>Поток игровых событий или ошибка авторизации и доступа к партии</returns>
    [HttpGet("game-events")]
    public async Task<Results<ServerSentEventsResult<GameEvent>, UnauthorizedHttpResult, NotFound, BadRequest>> GameCallbackEvents([FromQuery] Guid gameId,
        CancellationToken cancellationToken)
    {
        if (!TryGetPlayerId(User, out var playerId))
            return TypedResults.Unauthorized();

        if (gameId == Guid.Empty)
            return TypedResults.BadRequest();

        // Подписка получает только события авторизованного участника выбранной партии
        var hasAccess = await _storage.Games.AsNoTracking().AnyAsync(game => game.Id == gameId &&
            (game.WhitePlayerId == playerId || game.BlackPlayerId == playerId), cancellationToken);
        if (!hasAccess)
            return TypedResults.NotFound();
        
        return TypedResults.ServerSentEvents(
            GetEventsAsync(gameId, playerId, cancellationToken),
            eventType: "message"
        );
    }
    
    /// <summary>
    /// Читает новые события отдельного подключения и удаляет подписку при отключении
    /// </summary>
    /// <param name="gameId">Идентификатор проверенной партии</param>
    /// <param name="playerId">Идентификатор авторизованного игрока</param>
    /// <param name="cancellationToken">Токен отключения клиента</param>
    /// <returns>Асинхронная последовательность новых игровых событий без восстановления пропущенных</returns>
    private async IAsyncEnumerable<GameEvent> GetEventsAsync(Guid gameId, Guid playerId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Каждая вкладка и каждое переподключение получают собственный канал
        var connectionId = Guid.NewGuid();
        var reader = _chessBridgeService.RegisterClient(gameId, playerId, connectionId);
        try
        {
            await foreach (var gameEvent in reader.ReadAllAsync(cancellationToken))
                yield return gameEvent;
        }
        finally
        {
            _chessBridgeService.UnregisterClient(gameId, playerId, connectionId);
        }
    }

    /// <summary>
    /// Добавляет задание разбора сыгранного хода со снимками обеих позиций
    /// </summary>
    /// <param name="game">Партия сыгранного хода</param>
    /// <param name="move">Уже созданный узел хода</param>
    /// <param name="recipientPlayerId">Идентификатор получателя анализа из проверенного контекста запроса</param>
    /// <param name="fenBefore">Каноническая позиция до хода</param>
    /// <param name="createdAtUtc">Время создания задания в UTC</param>
    private void AddMoveAnalysisJob(Game game, Move move, Guid recipientPlayerId, string fenBefore, DateTime createdAtUtc)
    {
        _storage.MoveAnalysisJobs.Add(new MoveAnalysisJob
        {
            GameId = game.Id,
            MoveId = move.Id,
            RecipientPlayerId = recipientPlayerId,
            ParentMoveId = move.ParentMoveId,
            FenBefore = fenBefore,
            FenAfter = move.FenAfter,
            SearchDepth = GameConfig.DefaultSearchDepth,
            CreatedAtUtc = createdAtUtc
        });
    }

    /// <summary>
    /// Добавляет задание поиска ответа бота с зафиксированными позицией и сложностью
    /// </summary>
    /// <param name="game">Партия с актуальными узлом, версией и настройкой бота</param>
    /// <param name="fen">Каноническая позиция для поиска ответа</param>
    /// <param name="createdAtUtc">Время создания задания в UTC</param>
    private void AddBotMoveJob(Game game, string fen, DateTime createdAtUtc)
    {
        _storage.BotMoveJobs.Add(new BotMoveJob
        {
            GameId = game.Id,
            PositionMoveId = game.CurrentMoveId,
            Fen = fen,
            ExpectedGameVersion = game.Version,
            SkillLevel = game.OpponentSkillLevel
                ?? throw new InvalidOperationException("Для партии с ботом не задан уровень силы"),
            SearchDepth = GameConfig.DefaultSearchDepth,
            CreatedAtUtc = createdAtUtc
        });
    }

    /// <summary>
    /// Читает непустой идентификатор игрового профиля из авторизации
    /// </summary>
    /// <param name="user">Авторизация текущего пользователя</param>
    /// <param name="playerId">Идентификатор профиля при успешном чтении</param>
    /// <returns>Признак корректного идентификатора профиля</returns>
    private static bool TryGetPlayerId(ClaimsPrincipal user, out Guid playerId)
    {
        return Guid.TryParse(user.FindFirstValue(AppClaimTypes.PlayerId), out playerId)
            && playerId != Guid.Empty;
    }
    
}
