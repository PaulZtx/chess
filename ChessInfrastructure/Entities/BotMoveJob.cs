using System.ComponentModel.DataAnnotations.Schema;
using ChessInfrastructure.Entities.Enums;

namespace ChessInfrastructure.Entities;

/// <summary>
/// Задание поиска ответного хода бота из зафиксированной позиции партии
/// </summary>
public class BotMoveJob : Entity
{
    /// <summary>
    /// Идентификатор партии, в которую нужно применить найденный ход
    /// </summary>
    public Guid GameId { get; init; }

    /// <summary>
    /// Партия бота
    /// </summary>
    [ForeignKey(nameof(GameId))]
    public Game Game { get; set; } = null!;

    /// <summary>
    /// Узел исходной позиции или null для первого хода из Game.StartFen
    /// </summary>
    public Guid? PositionMoveId { get; init; }

    /// <summary>
    /// Узел исходной позиции при наличии
    /// </summary>
    [ForeignKey(nameof(PositionMoveId))]
    public Move? PositionMove { get; set; }

    /// <summary>
    /// Каноническая FEN, из которой бот должен выбрать ход
    /// </summary>
    public required string Fen { get; init; }

    /// <summary>
    /// Версия партии, которая вместе с PositionMoveId проверяется перед применением ответа
    /// </summary>
    public int ExpectedGameVersion { get; init; }

    /// <summary>
    /// Уровень силы от 0 до 20, зафиксированный для данного поиска
    /// </summary>
    public required int SkillLevel { get; init; }

    /// <summary>
    /// Глубина поиска в полуходах, зафиксированная при создании задания
    /// </summary>
    public required int SearchDepth { get; init; }

    /// <summary>
    /// Состояние обработки задания
    /// </summary>
    public StatusJob Status { get; set; } = StatusJob.Pending;

    /// <summary>
    /// Время создания задания в UTC
    /// </summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
