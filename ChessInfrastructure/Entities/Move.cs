using System.ComponentModel.DataAnnotations.Schema;

namespace ChessInfrastructure.Entities;

/// <summary>
/// Сохраненный полуход и позиция после него в дереве партии
/// </summary>
public class Move : Entity
{
    /// <summary>
    /// Идентификатор партии
    /// </summary>
    public Guid GameId { get; private set; }
    
    /// <summary>
    /// Партия
    /// </summary>
    [ForeignKey(nameof(GameId))]
    public Game Game { get; private set; } = null!;

    /// <summary>
    /// Идентификатор игрока, сделавшего ход, включая игрока-бота
    /// </summary>
    public Guid PlayerId { get; private set; }

    /// <summary>
    /// Игрок, сделавший ход
    /// </summary>
    [ForeignKey(nameof(PlayerId))]
    public Player Player { get; private set; } = null!;
    
    /// <summary>
    /// Идентификатор предыдущего полухода, отсутствующий у первого хода
    /// </summary>
    public Guid? ParentMoveId { get; private set; }
    
    /// <summary>
    /// Предыдущий полуход, отсутствующий у первого хода
    /// </summary>
    [ForeignKey(nameof(ParentMoveId))]
    public Move? ParentMove { get; private set; }
    
    /// <summary>
    /// Сыгранный ход в формате UCI, например e2e4 или e7e8q
    /// </summary>
    public string UciMove { get; private set; } = null!;
    
    /// <summary>
    /// Номер полухода от начала ветки, начиная с единицы
    /// </summary>
    public int PlyNumber { get; private set; }
    
    /// <summary>
    /// Время размышлений в миллисекундах при наличии
    /// </summary>
    public long? ThinkTimeMs { get; private set; }

    /// <summary>
    /// Позиция на доске после хода в формате FEN
    /// </summary>
    public string FenAfter { get; private set; } = null!;
    
    /// <summary>
    /// Конструктор для восстановления сохраненного узла
    /// </summary>
    private Move()
    {
    }

    /// <summary>
    /// Создает узел с постоянными данными хода и родительской связью
    /// </summary>
    /// <param name="game">Партия, которой принадлежит узел</param>
    /// <param name="parentMove">Предыдущий полуход или null для первого хода</param>
    /// <param name="playerId">Идентификатор проверенного автора хода</param>
    /// <param name="uciMove">Сыгранный ход в формате UCI</param>
    /// <param name="fenAfter">Позиция после хода в формате FEN</param>
    /// <param name="thinkTimeMs">Время размышлений в миллисекундах при наличии</param>
    private Move(Game game, Move? parentMove, Guid playerId, string uciMove, string fenAfter, long? thinkTimeMs)
    {
        Game = game;
        GameId = game.Id;
        PlayerId = playerId;
        ParentMove = parentMove;
        ParentMoveId = parentMove?.Id;
        PlyNumber = parentMove is null ? 1 : checked(parentMove.PlyNumber + 1);
        UciMove = uciMove;
        FenAfter = fenAfter;
        ThinkTimeMs = thinkTimeMs;
    }

    /// <summary>
    /// Создает первый полуход от начальной позиции партии без добавления в коллекцию ходов
    /// </summary>
    /// <param name="game">Партия с начальной позицией</param>
    /// <param name="playerId">Идентификатор автора хода, очередь которого проверил вызывающий код</param>
    /// <param name="uciMove">Сыгранный ход в формате UCI</param>
    /// <param name="fenAfter">Позиция после хода, рассчитанная шахматным сервисом</param>
    /// <param name="thinkTimeMs">Время размышлений в миллисекундах при наличии</param>
    /// <returns>Новый узел без родителя с номером полухода 1</returns>
    public static Move CreateFirst(Game game, Guid playerId, string uciMove, string fenAfter, long? thinkTimeMs = null)
    {
        ValidateMoveData(game, playerId, uciMove, fenAfter, thinkTimeMs);
        return new Move(game, null, playerId, uciMove, fenAfter, thinkTimeMs);
    }

    /// <summary>
    /// Создает новое продолжение существующего узла без изменения дерева и добавления в коллекцию ходов
    /// </summary>
    /// <param name="game">Партия, которой принадлежит родительский узел</param>
    /// <param name="parentMove">Узел, от позиции которого сделан ход</param>
    /// <param name="playerId">Идентификатор автора хода, очередь которого проверил вызывающий код</param>
    /// <param name="uciMove">Сыгранный ход в формате UCI</param>
    /// <param name="fenAfter">Позиция после хода, рассчитанная шахматным сервисом</param>
    /// <param name="thinkTimeMs">Время размышлений в миллисекундах при наличии</param>
    /// <returns>Новый узел с номером полухода на единицу больше родительского</returns>
    public static Move CreateChild(Game game, Move parentMove, Guid playerId, string uciMove, string fenAfter,
        long? thinkTimeMs = null)
    {
        ArgumentNullException.ThrowIfNull(parentMove);
        ValidateMoveData(game, playerId, uciMove, fenAfter, thinkTimeMs);

        if (parentMove.GameId != game.Id)
            throw new ArgumentException("Родительский узел должен принадлежать этой партии", nameof(parentMove));

        if (parentMove.PlyNumber < 1)
            throw new ArgumentException("Родительский узел должен иметь положительный номер полухода", nameof(parentMove));

        return new Move(game, parentMove, playerId, uciMove, fenAfter, thinkTimeMs);
    }

    /// <summary>
    /// Проверяет данные узла и участие автора без проверки шахматной легальности и соответствия FEN ходу
    /// </summary>
    /// <param name="game">Партия с начальной позицией</param>
    /// <param name="playerId">Идентификатор участника партии, сделавшего ход</param>
    /// <param name="uciMove">Сыгранный ход в формате UCI</param>
    /// <param name="fenAfter">Позиция после хода в формате FEN</param>
    /// <param name="thinkTimeMs">Время размышлений в миллисекундах при наличии</param>
    private static void ValidateMoveData(Game game, Guid playerId, string uciMove, string fenAfter, long? thinkTimeMs)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (playerId == Guid.Empty || (playerId != game.WhitePlayerId && playerId != game.BlackPlayerId))
            throw new ArgumentException("Автор хода должен быть участником партии", nameof(playerId));

        ArgumentException.ThrowIfNullOrWhiteSpace(game.StartFen, nameof(Game.StartFen));
        ArgumentException.ThrowIfNullOrWhiteSpace(uciMove);
        ArgumentException.ThrowIfNullOrWhiteSpace(fenAfter);

        if (thinkTimeMs is < 0)
            throw new ArgumentOutOfRangeException(nameof(thinkTimeMs), thinkTimeMs,
                "Время размышлений не может быть отрицательным");
    }
}
