using System.ComponentModel.DataAnnotations;

namespace BackChess.Models;

public class PlayerMove
{
    /// <summary>
    /// Ход пользователя
    /// </summary>
    public string SanMove { get; set; } = null!;
    
    /// <summary>
    /// Идентификатор игры
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Ожидаемая версия партии, полученная от сервера до отправки хода
    /// </summary>
    [Range(0, int.MaxValue)]
    public int ExpectedVersion { get; set; }
}
