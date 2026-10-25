using System.Text.Json.Serialization;

namespace BackChess.Models.Events;

/// <summary>
/// Общий контракт игровых уведомлений с признаком конкретного типа в JSON
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MoveAnalyzedEvent), "move-analyzed")]
[JsonDerivedType(typeof(BotMoveAppliedEvent), "bot-move-applied")]
public abstract record GameEvent
{
    /// <summary>
    /// Идентификатор партии, к которой относится событие
    /// </summary>
    public required Guid GameId { get; init; }
}
