namespace BackChess.Configurations;

/// <summary>
/// SMTP-настройки и шаблон письма подтверждения
/// </summary>
public class Email
{
    /// <summary>
    /// URL страницы подтверждения почты из конфигурации Email:ConfirmationUrl
    /// </summary>
    public string ConfirmationUrl { get; set; } = string.Empty;

    /// <summary>
    /// Текст при регистрации пользователя
    /// </summary>
    public static readonly string RegistrationText = """
                                                     <p>Hello, {0}!</p>
                                                     <p>Confirm your email by clicking the link below:</p>
                                                     <p><a href="{1}">Confirm email</a></p>
                                                     <p>If you did not create this account, ignore this message.</p>
                                                     """;

    /// <summary>
    /// Хост smtp
    /// </summary>
    public string SmtpHost { get; set; } = "localhost";

    /// <summary>
    /// Порт smtp
    /// </summary>
    public int SmtpPort { get; set; } = 2525;

    /// <summary>
    /// Шифрование SMTP-соединения через STARTTLS
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Ящик отправителя письма
    /// </summary>
    public string SenderEmail { get; set; } = "no-reply@jibjob.local";

    /// <summary>
    /// Имя отправителя
    /// </summary>
    public string SenderName { get; set; } = "JibJob";

    /// <summary>
    /// Имя пользователя
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Пароль
    /// </summary>
    public string? Password { get; set; }
}
