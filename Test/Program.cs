using ChessTrainer;


for(int i = 0; i < 10; ++i)
{
    Task.Run(() => Console.WriteLine(i));
    await Task.Delay(10);
}
return;

// Путь к локальному движку можно передать первым аргументом запуска
var enginePath = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../stockfish/src/stockfish"));

// await using закрывает процесс после ручного запуска анализа
await using var engine = new StockfishEngine(new StockfishOptions
{
    PathToStockFish = enginePath,
    CountOfBestMoves = 3
});

await engine.StartEngineAsync();
await engine.SetPositionAsync("position startpos moves e2e4 e7e5");

// Лучший ход и оценка основной линии после достижения заданной глубины
var result = await engine.GetBestMoveAsync(10);
Console.WriteLine(result is null
    ? "В позиции нет допустимых ходов"
    : $"Лучший ход: {result.BestMove}, глубина: {result.Depth}, " +
      $"оценка: {result.ScoreCp} cp, мат: {result.MateIn}, PV: {string.Join(' ', result.Pv)}");
