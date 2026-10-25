namespace ChessInfrastructure.Entities.Enums;

/// <summary>
/// Состояние фонового задания партии
/// </summary>
public enum StatusJob
{
    /// <summary>
    /// Ожидает обработки
    /// </summary>
    Pending, 
    /// <summary>
    /// Выполняется
    /// </summary>
    Running, 
    /// <summary>
    /// Успешно завершено
    /// </summary>
    Completed, 
    /// <summary>
    /// Завершено с ошибкой
    /// </summary>
    Failed
}