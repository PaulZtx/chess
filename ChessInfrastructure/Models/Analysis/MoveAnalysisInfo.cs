namespace ChessInfrastructure.Models.Analysis;

/// <summary>
/// Разбор одного сыгранного хода с анализами обеих позиций и оценками со стороны белых
/// </summary>
public sealed record MoveAnalysisInfo
{
    /// <summary>
    /// Исходный анализ до хода со стороны очереди хода, включая рекомендованный ход и линию
    /// </summary>
    public required EngineMoveInfo Before { get; init; }

    /// <summary>
    /// Исходный анализ после хода со стороны очереди хода, пригодный для следующего разбора
    /// В терминальной позиции линия пуста, BestMove отсутствует, мат обозначен MateIn равным нулю
    /// </summary>
    public required EngineMoveInfo After { get; init; }

    /// <summary>
    /// Признак того, что разобранный ход сделали белые
    /// </summary>
    public required bool IsWhiteMove { get; init; }

    /// <summary>
    /// Оценка до хода в сотых пешки со стороны белых или null для матовой оценки
    /// </summary>
    public int? ScoreCpBefore => IsWhiteMove ? Before.ScoreCp : -Before.ScoreCp;

    /// <summary>
    /// Оценка после хода в сотых пешки со стороны белых или null для матовой оценки
    /// </summary>
    public int? ScoreCpAfter => IsWhiteMove ? -After.ScoreCp : After.ScoreCp;

    /// <summary>
    /// Матовая оценка до хода со стороны белых, положительная при матовании белыми
    /// </summary>
    public int? MateInBefore => IsWhiteMove ? Before.MateIn : -Before.MateIn;

    /// <summary>
    /// Матовая оценка после хода со стороны белых, ноль означает уже поставленный мат
    /// </summary>
    public int? MateInAfter => IsWhiteMove ? -After.MateIn : After.MateIn;

    /// <summary>
    /// Неотрицательная потеря оценки ходившей стороны в сотых пешки
    /// Отсутствует при матовой оценке или границе оценки вместо точного значения
    /// </summary>
    public int? CentipawnLoss => Before.ScoreBound is null && After.ScoreBound is null &&
        ScoreCpBefore.HasValue && ScoreCpAfter.HasValue
            ? Math.Max(0, IsWhiteMove
                ? ScoreCpBefore.Value - ScoreCpAfter.Value
                : ScoreCpAfter.Value - ScoreCpBefore.Value)
            : null;
}
