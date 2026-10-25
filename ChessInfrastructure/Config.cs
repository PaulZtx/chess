using Microsoft.Extensions.Configuration;

namespace ChessInfrastructure;

/// <summary>
/// Предоставляет доступ к типизированным секциям конфигурации приложения
/// </summary>
public static class Config
{
    /// <summary>
    /// Конфигурация, подготовленная приложением при запуске
    /// </summary>
    private static IConfiguration? _configuration;

    /// <summary>
    /// Подключает конфигурацию приложения вместе с настроенными провайдерами
    /// </summary>
    /// <param name="configuration">Конфигурация приложения</param>
    public static void Initialize(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
    }

    /// <summary>
    /// Читает секцию с именем класса настроек
    /// </summary>
    /// <typeparam name="T">Класс настроек с именем соответствующей секции</typeparam>
    /// <returns>Настройки секции или объект со значениями по умолчанию, если секция отсутствует</returns>
    public static T GetSection<T>() where T : class, new()
    {
        // Общая конфигурация должна быть подключена до первого чтения настроек
        var configuration = _configuration
            ?? throw new InvalidOperationException("Сначала вызовите Config.Initialize при запуске приложения");

        return configuration.GetSection(typeof(T).Name).Get<T>() ?? new T();
    }
}
