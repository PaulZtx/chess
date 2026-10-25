namespace ChessTrainer;

public record Command(string ExecuteCommand, string ExpectedResult);

public record IsReadyCommand() : Command("isready", "readyok");

public record UciOkCommand() : Command("uci", "uciok");

public record GoCommand(int Depth) : Command($"go depth {Depth}", "bestmove");

/// <summary>
/// 
/// </summary>
/// <param name="Name"></param>
/// <param name="Value"></param>
public record OptionCommand(string Name, string Value);


