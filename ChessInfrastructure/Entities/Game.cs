using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChessInfrastructure.Entities;

public enum WinStatus
{
    None,
    WhiteWin,
    BlackWin,
    Draw
}

/// <summary>
/// Партия игроков
/// </summary>
public class Game : Entity
{
    /// <summary>
    /// Идентификатор белого игрока
    /// </summary>
    public Guid WhitePlayerId { get; set; }

    /// <summary>
    /// Белый игрок
    /// </summary>
    [ForeignKey(nameof(WhitePlayerId))]
    public Player WhitePlayer { get; set; } = null!;

    /// <summary>
    /// Идентификатор черного игрока
    /// </summary>
    public Guid BlackPlayerId { get; set; }

    /// <summary>
    /// Черный игрок
    /// </summary>
    [ForeignKey(nameof(BlackPlayerId))]
    public Player BlackPlayer { get; set; } = null!;
    
    /// <summary>
    /// Итог партии
    /// </summary>
    public WinStatus GameStatus { get; set; }
    
    /// <summary>
    /// Все узлы дерева партии, включая отмененные ходы и альтернативные ветки
    /// </summary>
    public ICollection<Move> Moves { get; } = [];
    
    /// <summary>
    /// Начальная позиция партии в формате FEN
    /// </summary>
    public required string StartFen { get; init; }
    
    /// <summary>
    /// Режим игры
    /// </summary>
    public required GameMode Mode { get; set; }
    
    /// <summary>
    /// Уровень партии при наличии
    /// </summary>
    public int? OpponentSkillLevel { get; set; }
    
    /// <summary>
    /// Текущий ход, null - начало партии
    /// </summary>
    public Guid? CurrentMoveId { get; set; }
    
    /// <summary>
    /// Текущий ход, null - начало партии
    /// </summary>
    [ForeignKey(nameof(CurrentMoveId))]
    public Move? CurrentMove { get; set; }

    /// <summary>
    /// Конечный узел сохраненной итоговой ветки, не меняется при просмотре других веток
    /// Null до завершения партии или при завершении без сыгранных ходов
    /// </summary>
    public Guid? FinalMoveId { get; set; }

    /// <summary>
    /// Конечный узел итоговой партии, чья цепочка родителей задает сохраненную последовательность
    /// </summary>
    [ForeignKey(nameof(FinalMoveId))]
    public Move? FinalMove { get; set; }
    
    /// <summary>
    /// Версия состояния для защиты от одновременных изменений
    /// </summary>
    [ConcurrencyCheck]
    public int Version { get; set; }
    
    /// <summary>
    /// Время последнего обновления (хода) в UTC
    /// </summary>
    public DateTime UpdateDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Время начала партии в UTC
    /// </summary>
    public DateTime CreatedAtDateTime { get; set; } = DateTime.UtcNow;

    //TODO: можно будет добавить какую-то статистику в будущем
}
