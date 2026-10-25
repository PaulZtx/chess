using System.ComponentModel.DataAnnotations.Schema;
using ChessInfrastructure.Models.Analysis;

namespace ChessInfrastructure.Entities;

/// <summary>
/// Актуальный разбор сыгранного хода с исходными анализами обеих позиций
/// </summary>
public class MoveAnalysis : Entity
{
    /// <summary>
    /// Идентификатор задания, для которого сохранен результат
    /// </summary>
    public Guid MoveAnalysisJobId { get; set; }
    
    /// <summary>
    /// Задание анализа сыгранного хода
    /// </summary>
    [ForeignKey(nameof(MoveAnalysisJobId))]
    public MoveAnalysisJob MoveAnalysisJob { get; set; } = null!;
    
    /// <summary>
    /// Краткое пояснение результата при наличии
    /// </summary>
    public string? AnalyzeSummary { get; set; }
    
    /// <summary>
    /// Полный исходный анализ позиции до хода, сохраняемый в JSONB
    /// При отсутствии анализа родителя рассчитывается заново перед сохранением результата
    /// Оценка остается со стороны очереди хода, Pv содержит рекомендуемое продолжение
    /// </summary>
    public EngineMoveInfo BeforeAnalysis { get; set; } = null!;
    
    /// <summary>
    /// Полный исходный анализ позиции после хода, сохраняемый в JSONB
    /// Пригоден для повторного использования как анализ до следующего хода
    /// </summary>
    public EngineMoveInfo AfterAnalysis { get; set; } = null!;
}
