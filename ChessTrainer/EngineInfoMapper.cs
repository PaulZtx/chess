namespace ChessTrainer;

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using ChessInfrastructure.Models.Analysis;

//TODO: вынести строки из кода в константы класса
public static class EngineInfoMapper
{
    private static readonly Regex FieldRegex = new(
        @"\b(?:" +
            @"(?<field>depth|seldepth|multipv|nodes|nps|hashfull|tbhits|time)" +
                @"\s+(?<number>\d+)\b" +
            @"|score\s+(?<scoreType>cp|mate)\s+(?<score>[+-]?\d+)\b" +
                @"(?:\s+(?<bound>lowerbound|upperbound)\b)?" +
            @"|pv\s+(?<pv>.*)$" +
        @")",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public static EngineMoveInfo? Parse(string line)
    {
        // Эти строки не содержат структурированный результат поиска.
        if (!line.StartsWith("info ", StringComparison.Ordinal) ||
            line.StartsWith("info string ", StringComparison.Ordinal))
        {
            return null;
        }

        var result = new EngineMoveInfo();

        foreach (Match match in FieldRegex.Matches(line))
        {
            if (match.Groups["scoreType"].Success)
            {
                var score = int.Parse(
                    match.Groups["score"].Value,
                    CultureInfo.InvariantCulture);

                result = result with
                {
                    ScoreCp = match.Groups["scoreType"].Value == "cp"
                        ? score : null,
                    MateIn = match.Groups["scoreType"].Value == "mate"
                        ? score : null,
                    ScoreBound = match.Groups["bound"].Success
                        ? match.Groups["bound"].Value : null
                };

                continue;
            }

            if (match.Groups["pv"].Success)
            {
                result = result with
                {
                    Pv = match.Groups["pv"].Value.Split(
                        (char[]?)null,
                        StringSplitOptions.RemoveEmptyEntries)
                };

                continue;
            }

            var value = long.Parse(
                match.Groups["number"].Value,
                CultureInfo.InvariantCulture);

            result = match.Groups["field"].Value switch
            {
                "depth"   => result with { Depth = checked((int)value) },
                "seldepth"=> result with { SelectiveDepth = checked((int)value) },
                "multipv" => result with { MultiPv = checked((int)value) },
                "nodes"   => result with { Nodes = value },
                "nps"     => result with { NodesPerSecond = value },
                "hashfull"=> result with { HashFull = checked((int)value) },
                "tbhits"  => result with { TablebaseHits = value },
                "time"    => result with { TimeMs = value },
                _         => result
            };
        }

        return result;
    }
}
