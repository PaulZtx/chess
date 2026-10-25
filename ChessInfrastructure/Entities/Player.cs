using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ChessInfrastructure.Identity;

namespace ChessInfrastructure.Entities;

/// <summary>
/// Участник партий, которым может быть человек или бот
/// </summary>
public class Player : Entity
{
    /// <summary>
    /// Признак игрока-человека
    /// </summary>
    public bool IsHuman { get; set; }
    
    /// <summary>
    /// Идентификатор связанной учетной записи при наличии
    /// </summary>
    public Guid? UserId { get; set; }
    
    /// <summary>
    /// Связанная учетная запись при наличии
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>
    /// Отображаемое имя игрока
    /// </summary>
    [MaxLength(64)] 
    public string Nickname { get; set; } = null!;
}
