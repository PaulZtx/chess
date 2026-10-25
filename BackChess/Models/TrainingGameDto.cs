using System.ComponentModel.DataAnnotations;

namespace BackChess.Models;

public class TrainingGameDto
{
    [Range(0, 20, ErrorMessage = "Несоответствие диапазона уровню")]
    public int OpponentSkillLevel { get; set; }
    
    public string? StartFen { get; set; }
    
    public bool IsWhite { get; set; }
    
    public Guid BotId { get; set; }
}