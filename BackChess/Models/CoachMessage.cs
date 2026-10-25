namespace BackChess.Models;

/// <summary>
/// Вопрос для тренера
/// </summary>
public class CoachMessage
{
    public Guid GameId { get; set; }
    
    public Guid? MoveId { get; set; }
    
    public string Message { get; set; }
}