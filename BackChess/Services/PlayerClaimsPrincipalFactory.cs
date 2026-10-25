using System.Security.Claims;
using BackChess.Configurations;
using ChessInfrastructure;
using ChessInfrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackChess.Services;

/// <summary>
/// Дополняет стандартную авторизацию Identity идентификатором игрового профиля
/// </summary>
public sealed class PlayerClaimsPrincipalFactory : UserClaimsPrincipalFactory<User, IdentityRole<Guid>>
{
    /// <summary>
    /// Хранилище связи аккаунта с игровым профилем
    /// </summary>
    private readonly Storage _storage;

    /// <summary>
    /// Создает фабрику с поддержкой стандартных claims пользователя и ролей
    /// </summary>
    /// <param name="userManager">Менеджер учетных записей</param>
    /// <param name="roleManager">Менеджер ролей</param>
    /// <param name="options">Настройки claims Identity</param>
    /// <param name="storage">Контекст игровых профилей</param>
    public PlayerClaimsPrincipalFactory(
        UserManager<User> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<IdentityOptions> options,
        Storage storage) : base(userManager, roleManager, options)
    {
        _storage = storage;
    }

    /// <summary>
    /// Добавляет актуальный PlayerId, сохраняя стандартные идентификатор аккаунта и роли
    /// </summary>
    /// <param name="user">Аккаунт, для которого создается авторизация</param>
    /// <returns>Identity с единственным PlayerId при наличии связанного человеческого профиля</returns>
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        // Стандартные claims, включая NameIdentifier, роли и security stamp
        var identity = await base.GenerateClaimsAsync(user);

        // PlayerId определяется только связью профиля с аккаунтом, а не сохраненными claims
        foreach (var claim in identity.FindAll(AppClaimTypes.PlayerId).ToArray())
            identity.RemoveClaim(claim);

        // Проекция не загружает навигации и не отслеживает профиль
        var playerId = await _storage.Players.AsNoTracking()
            .Where(player => player.UserId == user.Id && player.IsHuman)
            .Select(player => (Guid?)player.Id)
            .SingleOrDefaultAsync();

        if (playerId.HasValue && playerId.Value != Guid.Empty)
            identity.AddClaim(new Claim(AppClaimTypes.PlayerId, playerId.Value.ToString()));

        return identity;
    }
}
