namespace BackChess.Models.Events;

/// <summary>
/// Уведомление о сохраненном результате анализа хода без передачи сущностей EF
/// </summary>
public sealed record MoveAnalyzedEvent : GameEvent
{
    /// <summary>
    /// Идентификатор завершенного задания анализа
    /// </summary>
    public required Guid JobId { get; init; }

    /// <summary>
    /// Идентификатор разобранного хода
    /// </summary>
    public required Guid MoveId { get; init; }

    /// <summary>
    /// Идентификатор сохраненного результата анализа
    /// </summary>
    public required Guid AnalysisId { get; init; }
}
