using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Chess;

namespace ChessTrainer;

/// <summary>
/// Преобразует легальные ходы между SAN и UCI для стандартных шахмат
/// </summary>
public static class ChessMoveConverter
{
    /// <summary>
    /// Преобразует SAN в UCI с учетом исходной позиции и легальности хода
    /// </summary>
    /// <param name="fen">Исходная позиция партии в формате FEN</param>
    /// <param name="sanMove">Ход в SAN, например Nf3, O-O или e8=Q</param>
    /// <param name="uciMove">Ход в UCI при успешном преобразовании или null при ошибке</param>
    /// <returns>Признак успешного преобразования легального хода</returns>
    public static bool TrySanToUci(string fen, string sanMove, [NotNullWhen(true)] out string? uciMove)
    {
        uciMove = null;
        if (string.IsNullOrWhiteSpace(fen) || string.IsNullOrWhiteSpace(sanMove)
            || !ChessBoard.TryLoadFromFen(fen, out var board) || board.IsEndGame)
            return false;

        // В UCI стандартная рокировка записывается перемещением короля на c- или g-вертикаль
        board.StandardiseCastlingPositions = true;
        if (!board.TryParseFromSan(sanMove, out var move) || !board.IsValidMove(move))
            return false;

        uciMove = ToUci(move);
        return true;
    }

    /// <summary>
    /// Преобразует UCI в SAN с учетом взятия, превращения, неоднозначности, шаха и мата
    /// </summary>
    /// <param name="fen">Исходная позиция партии в формате FEN</param>
    /// <param name="uciMove">Ход в UCI, например g1f3, e1g1 или e7e8q</param>
    /// <param name="sanMove">Ход в SAN при успешном преобразовании или null при ошибке</param>
    /// <returns>Признак успешного преобразования легального хода</returns>
    public static bool TryUciToSan(string fen, string uciMove, [NotNullWhen(true)] out string? sanMove)
    {
        sanMove = null;
        if (string.IsNullOrWhiteSpace(fen) || string.IsNullOrWhiteSpace(uciMove)
            || !Regex.IsMatch(uciMove, @"\A[a-h][1-8][a-h][1-8][qrbn]?\z")
            || !ChessBoard.TryLoadFromFen(fen, out var board) || board.IsEndGame)
            return false;

        // Поле исходной фигуры уже проверено форматом UCI
        var from = new Position(uciMove[..2]);
        if (board[from] is null)
            return false;

        board.StandardiseCastlingPositions = true;

        // Библиотека генерирует отдельный легальный вариант для каждого типа превращения
        var move = board.Moves(from).FirstOrDefault(candidate => ToUci(candidate) == uciMove);
        if (move?.San is not { Length: > 0 } san)
            return false;

        sanMove = san;
        return true;
    }

    /// <summary>
    /// Записывает проверенный ход в UCI с необязательной буквой фигуры превращения
    /// </summary>
    /// <param name="move">Легальный ход с заполненными параметрами</param>
    /// <returns>Исходное и конечное поля с суффиксом превращения при наличии</returns>
    private static string ToUci(Move move)
    {
        return $"{move.OriginalPosition}{move.NewPosition}{move.Promotion?.Type.AsChar}";
    }
}
