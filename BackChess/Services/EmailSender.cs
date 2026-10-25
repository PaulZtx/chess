using System.Net;
using System.Net.Mail;
using System.Text;
using BackChess.Configurations;
using ChessInfrastructure;
using ChessInfrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace BackChess.Services;

/// <summary>
/// Формирует письма Identity и отправляет их через SMTP
/// </summary>
public sealed class EmailSender : IEmailSender<User>
{
    /// <summary>
    /// Настройки подключения и отправителя
    /// </summary>
    private readonly Email _settings;

    /// <summary>
    /// Логгер отправки без содержимого писем и токенов
    /// </summary>
    private readonly ILogger<EmailSender> _logger;

    /// <summary>
    /// Создает отправщик с настройками SMTP
    /// </summary>
    /// <param name="logger">Логгер отправки</param>
    public EmailSender(ILogger<EmailSender> logger)
    {
        _settings = Config.GetSection<Email>();
        _logger = logger;
    }

    /// <summary>
    /// Отправляет письмо подтверждения с пользовательским шаблоном
    /// </summary>
    /// <param name="user">Учетная запись получателя</param>
    /// <param name="email">Адрес получателя</param>
    /// <param name="confirmationLink">Полный URL подтверждения почты</param>
    /// <returns>Задача отправки письма</returns>
    public Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)
    {
        // Шаблон письма с экранированными именем пользователя и ссылкой
        var html = string.Format(Email.RegistrationText,
            WebUtility.HtmlEncode(user.UserName), WebUtility.HtmlEncode(confirmationLink));

        return SendEmailAsync(email, "Подтверждение почты", html);
    }

    /// <summary>
    /// Отправляет ссылку сброса пароля
    /// </summary>
    /// <param name="user">Учетная запись получателя</param>
    /// <param name="email">Адрес получателя</param>
    /// <param name="resetLink">Полный URL сброса пароля</param>
    /// <returns>Задача отправки письма</returns>
    public Task SendPasswordResetLinkAsync(User user, string email, string resetLink) =>
        SendEmailAsync(email, "Сброс пароля",
            $"<p>Для сброса пароля <a href=\"{WebUtility.HtmlEncode(resetLink)}\">перейдите по ссылке</a></p>");

    /// <summary>
    /// Отправляет код сброса пароля
    /// </summary>
    /// <param name="user">Учетная запись получателя</param>
    /// <param name="email">Адрес получателя</param>
    /// <param name="resetCode">Код сброса пароля</param>
    /// <returns>Задача отправки письма</returns>
    public Task SendPasswordResetCodeAsync(User user, string email, string resetCode) =>
        SendEmailAsync(email, "Код сброса пароля",
            $"<p>Ваш код сброса пароля: <strong>{WebUtility.HtmlEncode(resetCode)}</strong></p>");

    /// <summary>
    /// Передает HTML-письмо SMTP-серверу с ограничением времени отправки
    /// </summary>
    /// <param name="email">Адрес получателя</param>
    /// <param name="subject">Тема письма</param>
    /// <param name="html">Готовое HTML-содержимое письма</param>
    /// <returns>Задача отправки письма</returns>
    private async Task SendEmailAsync(string email, string subject, string html)
    {
        // Письмо с явно заданными отправителем и кодировкой
        using var message = new MailMessage
        {
            From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
            Subject = subject,
            Body = html,
            IsBodyHtml = true,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        message.To.Add(email);

        // Отдельное SMTP-подключение для текущего письма
        using var smtp = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = _settings.UseSsl
        };
        
        if (!string.IsNullOrWhiteSpace(_settings.UserName))
            smtp.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);

        // Интерфейс Identity не принимает CancellationToken, поэтому время отправки ограничено отдельно
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await smtp.SendMailAsync(message, timeout.Token);
        
        _logger.LogInformation("Письмо {Subject} передано SMTP-серверу", subject);
    }
}
