using System.Text;
using BackChess.Configurations;
using BackChess.Models;
using ChessInfrastructure.Entities;
using ChessInfrastructure;
using ChessInfrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace BackChess.Controllers;

/// <summary>
/// Регистрация пользователей и создание начального игрового профиля
/// </summary>
public class UserController : RootController
{
    /// <summary>
    /// URL страницы подтверждения регистрации из настроек Email
    /// </summary>
    private readonly string _confirmationUrl;
    
    /// <summary>
    /// Менеджер учетных записей Identity
    /// </summary>
    private readonly UserManager<User> _userManager;

    /// <summary>
    /// Контекст, общий с хранилищем Identity в рамках запроса
    /// </summary>
    private readonly Storage _storage;

    /// <summary>
    /// Отправщик писем подтверждения учетной записи
    /// </summary>
    private readonly IEmailSender<User> _emailSender;

    /// <summary>
    /// Проверяющий вход пользователя
    /// </summary>
    private readonly SignInManager<User> _signInManager;

    /// <summary>
    /// Создает контроллер регистрации с общим контекстом хранения
    /// </summary>
    /// <param name="userManager">Менеджер учетных записей</param>
    /// <param name="storage">Контекст хранения учетных записей и профилей</param>
    /// <param name="emailSender">Отправщик писем подтверждения</param>
    /// <param name="signInManager">Проверяющий вход пользователя</param>
    /// <param name="logger">Логгер контроллера</param>
    public UserController(
        UserManager<User> userManager,
        Storage storage,
        IEmailSender<User> emailSender,
        SignInManager<User> signInManager,
        ILogger<UserController> logger) : base(logger)
    {
        _userManager = userManager;
        _storage = storage;
        _emailSender = emailSender;
        _signInManager = signInManager;
        _confirmationUrl = Config.GetSection<Email>().ConfirmationUrl;
    }

    /// <summary>
    /// Создает учетную запись, назначает роль и сохраняет игровой профиль в одной транзакции
    /// </summary>
    /// <param name="userDto">Данные регистрации</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Данные зарегистрированного пользователя или ошибки регистрации</returns>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> RegisterUser([FromBody] UserDto userDto,
        CancellationToken cancellationToken)
    {
        // Новая учетная запись, с которой будет связан игровой профиль
        var user = new User()
        {
            Email = userDto.Email,
            UserName = userDto.UserName,
        };
        
        // Общая транзакция для учетной записи, роли и начального профиля
        await using (var transaction = await _storage.Database.BeginTransactionAsync(cancellationToken))
        {
            // Результат создания учетной записи и последующего назначения роли
            var result = await _userManager.CreateAsync(user, userDto.Password);
            if (!result.Succeeded)
                return IdentityValidationProblem(result, "Registration failed");

            result = await _userManager.AddToRoleAsync(user, AppRoles.User);
            if (!result.Succeeded)
                return IdentityValidationProblem(result, "Role assignment failed");

            _storage.Players.Add(new Player
            {
                UserId = user.Id,
                IsHuman = true,
                Nickname = userDto.UserName
            });
            await _storage.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        
        // Токен подтверждения создается после успешного сохранения аккаунта и профиля
        var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        
        var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(confirmationToken));
        var userId = user.Id.ToString();
        
        var confirmationLink = QueryHelpers.AddQueryString(
            _confirmationUrl,
            new Dictionary<string, string?>
            {
                [nameof(userId)] = userId,
                [nameof(token)] = token
            });
        
        await _emailSender.SendConfirmationLinkAsync(user, user.Email, confirmationLink);
        
        return Ok(new
        {
            userId = user.Id,
            userName = user.UserName,
            email = user.Email,
            message = "Registration completed. Check your email to confirm account."
        });
    }

    /// <summary>
    /// Подтверждение почты
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="token">Токен подтверждения</param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmUser([FromQuery]Guid userId, [FromQuery]string token)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid confirmation link"
            });
        }
        
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid confirmation link"
            });
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid confirmation token"
            });
        }

        var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
        if (!result.Succeeded)
        {
            return IdentityValidationProblem(result, "Confirmation Error");
        }

        return Ok(new
        {
            message = "Email confirmed"
        });
    }

    /// <summary>
    /// Аутентификация пользователя
    /// </summary>
    /// <param name="userDto">Модель пользователя</param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> LoginUser([FromBody] UserDto userDto)
    {
        var user = await _userManager.FindByEmailAsync(userDto.Email);
        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid credentials"
            });
        }
        
        var signInResult = await _signInManager.PasswordSignInAsync(
            user,
            userDto.Password,
            userDto.RememberMe ?? false,
            lockoutOnFailure: true);

        if (signInResult.Succeeded)
        {
            return NoContent();
        }
        
        if (signInResult.IsLockedOut)
        {
            return Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "User is locked out",
                detail: "Too many failed attempts. Try again later.");
        }
        
        if (signInResult.IsNotAllowed && !await _userManager.IsEmailConfirmedAsync(user))
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Email is not confirmed",
                detail: "Confirm your email before signing in.");
        }

        return Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Invalid credentials"
        });
    }

    /// <summary>
    /// Преобразует ошибки Identity в ответ валидации
    /// </summary>
    /// <param name="result">Ошибки операции Identity</param>
    /// <param name="title">Заголовок ответа</param>
    /// <returns>Ответ с ошибками валидации</returns>
    private IActionResult IdentityValidationProblem(IdentityResult result, string title)
    {
        // Ошибки, сгруппированные по коду Identity
        var errors = result.Errors
            .Select(_ => new KeyValuePair<string, string[]>(_.Code, [_.Description])).ToDictionary();
        
        return ValidationProblem(new ValidationProblemDetails(errors)
        {
            Title = title
        });
    }
}
