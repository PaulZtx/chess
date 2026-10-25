namespace ChessInfrastructure;

/// <summary>
/// Строки подключения из секции ConnectionStrings
/// </summary>
public class ConnectionStrings
{
    /// <summary>
    /// Строка подключения к PostgreSQL
    /// </summary>
    public string PostgresConnection { get; set; } = string.Empty;
}
