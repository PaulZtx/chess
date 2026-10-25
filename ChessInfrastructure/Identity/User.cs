using Microsoft.AspNetCore.Identity;

namespace ChessInfrastructure.Identity;

/// <summary>
/// Учетная запись пользователя в ASP.NET Core Identity
/// </summary>
public class User : IdentityUser<Guid>
{
    /// <summary>
    /// Признак активной учетной записи
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Время создания учетной записи в UTC
    /// </summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
}
