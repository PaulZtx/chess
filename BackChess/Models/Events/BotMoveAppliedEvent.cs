namespace BackChess.Models.Events;

/// <summary>
/// Контракт уведомления для будущей обработки сохраненного ответного хода бота
/// </summary>
public sealed record BotMoveAppliedEvent : GameEvent
{
    /// <summary>
    /// Идентификатор сохраненного хода бота
    /// </summary>
    public required Guid MoveId { get; init; }

    /// <summary>
    /// Сыгранный ход в UCI-нотации
    /// </summary>
    public required string UciMove { get; init; }

    /// <summary>
    /// Сохраненная позиция после хода бота
    /// </summary>
    public required string FenAfter { get; init; }

    /// <summary>
    /// Версия партии после применения хода
    /// </summary>
    public required int Version { get; init; }
}
