namespace ChessInfrastructure;

public static class GameConfig
{
    /// <summary>
    /// Базовая позиция
    /// </summary>
    public const string BaseStartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    /// <summary>
    /// Начальная глубина поиска в полуходах, копируемая в каждое создаваемое задание
    /// </summary>
    public const int DefaultSearchDepth = 20;
}
