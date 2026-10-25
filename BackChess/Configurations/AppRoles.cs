namespace BackChess.Configurations;

/// <summary>
/// Роли пользователей
/// </summary>
public static class AppRoles
{
    /// <summary>
    /// Пользователь
    /// </summary>
    public const string User = nameof(User);
    
    /// <summary>
    /// Премиум-пользователь
    /// </summary>
    public const string PremiumUser = nameof(PremiumUser);
    
    /// <summary>
    /// Модератор
    /// </summary>
    public const string Moderator = nameof(Moderator);

    /// <summary>
    /// Администратор
    /// </summary>
    public const string Administrator = nameof(Administrator);
}
