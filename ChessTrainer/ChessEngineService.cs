using System.Globalization;
using Chess;
using ChessInfrastructure.Models.Analysis;

namespace ChessTrainer;

/// <summary>
/// Получает ходы соперника и анализ тренера через собственный экземпляр Stockfish
/// </summary>
/// <remarks>
/// После ошибки обмена или отмены активного поиска сервис следует освободить и создать заново
/// </remarks>
public sealed class ChessEngineService : IAsyncDisposable
{
    /// <summary>
    /// Движок, доступный только через операции сервиса
    /// </summary>
    private readonly StockfishEngine _engine;

    /// <summary>
    /// Блокировка всей последовательности настройки, установки позиции и поиска
    /// </summary>
    private readonly SemaphoreSlim _searchLock = new(1, 1);

    /// <summary>
    /// Признак завершенной инициализации процесса
    /// </summary>
    private bool _started;

    /// <summary>
    /// Последняя партия движка или null до первого успешного переключения
    /// </summary>
    private Guid? _currentGameId;

    /// <summary>
    /// Признак окончательного освобождения сервиса
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Создает сервис с отдельным процессом, запускаемым при первом поиске
    /// </summary>
    /// <param name="options">Параметры запуска и обмена со Stockfish</param>
    public ChessEngineService(StockfishOptions options)
    {
        _engine = new StockfishEngine(options);
    }


    /// <summary>
    /// Анализирует позиции до и после сыгранного хода и сравнивает оценки с учетом стороны игрока
    /// </summary>
    /// <param name="gameId">Идентификатор анализируемой партии</param>
    /// <param name="fenBefore">Сохраненная позиция до легального хода</param>
    /// <param name="fenAfter">Сохраненная позиция после того же хода</param>
    /// <param name="depth">Положительная глубина поиска в полуходах</param>
    /// <param name="previousMoveInfo">Анализ fenBefore без ослабления со стороны очереди хода или null</param>
    /// <param name="cancellationToken">Токен отмены анализа</param>
    /// <returns>Оба анализа, оценки со стороны белых и потеря оценки ходившей стороны при наличии точных оценок в сотых пешки</returns>
    /// <remarks>
    /// Принадлежность предыдущего анализа позиции проверяет вызывающий код
    /// Более мелкий поиск или оценка с границей не переиспользуются
    /// История повторений из двух FEN не восстанавливается
    /// </remarks>
    public async Task<MoveAnalysisInfo> AnalyzeMoveAsync(Guid gameId, string fenBefore, string fenAfter, int depth,
        EngineMoveInfo? previousMoveInfo, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (gameId == Guid.Empty)
            throw new ArgumentException("Идентификатор партии не должен быть пустым", nameof(gameId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        ArgumentException.ThrowIfNullOrWhiteSpace(fenBefore);
        ArgumentException.ThrowIfNullOrWhiteSpace(fenAfter);

        // Доски определяют сторону игрока и завершение позиции после хода
        if (!ChessBoard.TryLoadFromFen(fenBefore, out var boardBefore) || boardBefore.IsEndGame)
            throw new ArgumentException("Ожидается корректная незавершенная позиция до хода", nameof(fenBefore));
        if (!ChessBoard.TryLoadFromFen(fenAfter, out var boardAfter) || boardBefore.Turn == boardAfter.Turn)
            throw new ArgumentException("Ожидается корректная позиция с другой очередью после хода", nameof(fenAfter));

        // Сохраненный анализ должен быть достаточно глубоким и содержать оценку без границы
        var before = previousMoveInfo;
        if (before is null || before.Depth is null || before.Depth < depth || before.ScoreBound is not null ||
            (!before.ScoreCp.HasValue && !before.MateIn.HasValue))
        {
            before = await AnalyzePositionAsync(gameId, fenBefore, depth, cancellationToken);
        }

        if (before is null)
            throw new InvalidOperationException("Движок не вернул анализ позиции до хода");

        // Один процесс выполняет поиски последовательно, дополнительная блокировка здесь не нужна
        EngineMoveInfo? after;
        if (boardAfter.IsEndGame)
        {
            // При мате нет оценки в пешках, при ничьей оценка равна нулю, продолжение отсутствует
            after = new EngineMoveInfo
            {
                ScoreCp = boardAfter.EndGame!.WonSide is null ? 0 : null,
                MateIn = boardAfter.EndGame.WonSide is null ? null : 0
            };
        }
        else
        {
            after = await AnalyzePositionAsync(gameId, fenAfter, depth, cancellationToken);
        }

        if (after is null)
            throw new InvalidOperationException("Движок не вернул анализ позиции после хода");

        return new MoveAnalysisInfo
        {
            Before = before,
            After = after,
            IsWhiteMove = boardBefore.Turn == PieceColor.White
        };
    }

    /// <summary>
    /// Выбирает ход соперника с заданным уровнем силы
    /// </summary>
    /// <param name="gameId">Идентификатор партии для сохранения кэша между ее поисками</param>
    /// <param name="fen">Корректная FEN позиции без истории предыдущих ходов</param>
    /// <param name="skillLevel">Уровень силы от 0 до 20, где 20 означает игру без ослабления</param>
    /// <param name="depth">Положительная глубина поиска в полуходах</param>
    /// <param name="cancellationToken">Токен отмены ожидания или поиска</param>
    /// <returns>Ход в UCI-нотации или null, если допустимых ходов нет</returns>
    public async Task<string?> GetOpponentMoveAsync(
        Guid gameId, string fen, int skillLevel, int depth, CancellationToken cancellationToken = default)
    {
        // Оценка основной линии может не соответствовать ослабленному ходу и наружу не передается
        var result = await SearchAsync(gameId, fen, skillLevel, depth, cancellationToken);
        return result?.BestMove;
    }

    /// <summary>
    /// Анализирует позицию без ослабления силы движка
    /// </summary>
    /// <param name="gameId">Идентификатор партии для сохранения кэша между ее поисками</param>
    /// <param name="fen">Корректная FEN позиции без истории предыдущих ходов</param>
    /// <param name="depth">Положительная глубина поиска в полуходах</param>
    /// <param name="cancellationToken">Токен отмены ожидания или поиска</param>
    /// <returns>Основная линия и оценка со стороны очереди хода или null, если допустимых ходов нет</returns>
    public Task<EngineMoveInfo?> AnalyzePositionAsync(
        Guid gameId, string fen, int depth, CancellationToken cancellationToken = default) =>
        SearchAsync(gameId, fen, 20, depth, cancellationToken);

    /// <summary>
    /// Последовательно переключает партию при необходимости, настраивает движок и выполняет поиск
    /// </summary>
    /// <param name="gameId">Непустой идентификатор партии текущего поиска</param>
    /// <param name="fen">Корректная FEN позиции, шахматная легальность здесь не проверяется</param>
    /// <param name="skillLevel">Уровень силы от 0 до 20</param>
    /// <param name="depth">Положительная глубина поиска в полуходах</param>
    /// <param name="cancellationToken">Токен отмены ожидания или поиска</param>
    /// <returns>Результат поиска или null при отсутствии допустимых ходов</returns>
    private async Task<EngineMoveInfo?> SearchAsync(
        Guid gameId, string fen, int skillLevel, int depth, CancellationToken cancellationToken)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("Идентификатор партии не должен быть пустым", nameof(gameId));
        ArgumentException.ThrowIfNullOrWhiteSpace(fen);
        if (fen.Contains('\r') || fen.Contains('\n'))
            throw new ArgumentException("FEN должна занимать одну строку", nameof(fen));
        ArgumentOutOfRangeException.ThrowIfLessThan(skillLevel, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(skillLevel, 20);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);

        await _searchLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_started)
            {
                await _engine.StartEngineAsync(cancellationToken);
                _started = true;
            }

            if (_currentGameId != gameId)
            {
                await _engine.NewGameAsync(cancellationToken);
                _currentGameId = gameId;
            }

            await _engine.SetOptionAsync(new OptionCommand("UCI_LimitStrength", "false"), cancellationToken);
            await _engine.SetOptionAsync(new OptionCommand(
                "Skill Level", skillLevel.ToString(CultureInfo.InvariantCulture)), cancellationToken);
            await _engine.SetPositionAsync($"position fen {fen}", cancellationToken);
            return await _engine.GetBestMoveAsync(depth, cancellationToken);
        }
        finally
        {
            _searchLock.Release();
        }
    }

    /// <summary>
    /// Дожидается текущей операции и закрывает принадлежащий сервису движок
    /// </summary>
    /// <returns>Задача освобождения процесса движка</returns>
    public async ValueTask DisposeAsync()
    {
        await _searchLock.WaitAsync();
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            await _engine.DisposeAsync();
        }
        finally
        {
            _searchLock.Release();
        }
    }
}
