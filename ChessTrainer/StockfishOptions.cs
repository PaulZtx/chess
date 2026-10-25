namespace ChessTrainer;

/// <summary>Настройки запуска и обмена с процессом Stockfish</summary>
public sealed class StockfishOptions
{
    /// <summary>Имя UCI-опции количества вариантов поиска</summary>
    public const string MultiPV = nameof(MultiPV);

    /// <summary>Количество вариантов от 1 до 500, GetBestMoveAsync возвращает только основной</summary>
    public int CountOfBestMoves { get; set; } = 1;

    /// <summary>Путь к исполняемому файлу Stockfish</summary>
    public string PathToStockFish { get; set; } = string.Empty;

    /// <summary>Таймаут одной операции, включая поиск заданной глубины</summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
}