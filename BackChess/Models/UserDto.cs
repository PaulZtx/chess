using System.ComponentModel.DataAnnotations;

namespace BackChess.Models;

/// <summary>
/// Модель пользователя
/// </summary>
public class UserDto
{
    /// <summary>
    /// Имя пользователя
    /// </summary>
    [Required]
    [MinLength(4)]
    [MaxLength(32)]
    public string UserName { get; set; } = string.Empty;
    
    /// <summary>
    /// Пароль
    /// </summary>
    [Required]
    [MinLength(8)]
    [MaxLength(64)]
    public string Password { get; set; } = string.Empty;
    
    /// <summary>
    /// Электронная почта
    /// </summary>
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// Запомнить ли для входа
    /// </summary>
    public bool? RememberMe { get; set; }
}
