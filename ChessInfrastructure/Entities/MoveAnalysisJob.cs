using System.ComponentModel.DataAnnotations.Schema;
using ChessInfrastructure.Entities.Enums;

namespace ChessInfrastructure.Entities;

/// <summary>
/// Задание разбора сохраненного хода со снимками обеих позиций
/// </summary>
public class MoveAnalysisJob : Entity
{
    /// <summary>
    /// Идентификатор партии для группировки заданий и уведомлений
    /// </summary>
    public Guid GameId { get; init; }

    /// <summary>
    /// Партия анализируемого хода
    /// </summary>
    [ForeignKey(nameof(GameId))]
    public Game Game { get; set; } = null!;

    /// <summary>
    /// Идентификатор уже сыгранного хода, который нужно разобрать
    /// </summary>
    public Guid MoveId { get; init; }

    /// <summary>
    /// Уже сыгранный ход
    /// </summary>
    [ForeignKey(nameof(MoveId))]
    public Move Move { get; set; } = null!;

    /// <summary>
    /// Идентификатор получателя персонального анализа, который может отличаться от автора хода
    /// </summary>
    public required Guid RecipientPlayerId { get; init; }

    /// <summary>
    /// Игрок, для которого выполняется анализ
    /// </summary>
    [ForeignKey(nameof(RecipientPlayerId))]
    public Player RecipientPlayer { get; set; } = null!;

    /// <summary>
    /// Родительский узел для поиска готового анализа или null для первого хода
    /// </summary>
    public Guid? ParentMoveId { get; init; }

    /// <summary>
    /// Родительский узел при наличии
    /// </summary>
    [ForeignKey(nameof(ParentMoveId))]
    public Move? ParentMove { get; set; }

    /// <summary>
    /// Снимок канонической FEN до хода, включая Game.StartFen для первого хода
    /// </summary>
    public required string FenBefore { get; init; }

    /// <summary>
    /// Снимок канонической FEN после хода
    /// </summary>
    public required string FenAfter { get; init; }

    /// <summary>
    /// Глубина анализа в полуходах, зафиксированная при создании задания
    /// </summary>
    public required int SearchDepth { get; init; }
    
    /// <summary>
    /// Ошибка при обработке
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Состояние обработки задания
    /// </summary>
    public StatusJob Status { get; set; } = StatusJob.Pending;

    /// <summary>
    /// Время создания задания в UTC
    /// </summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
