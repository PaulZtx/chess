using System.Diagnostics.CodeAnalysis;
using Chess;

namespace ChessTrainer;

/// <summary>
/// Проверяет шахматную легальность ходов без запуска движка
/// </summary>
public static class ChessMoveValidator
{
    /// <summary>
    /// Проверяет и применяет ход в SAN на отдельной доске с учетом очереди хода и безопасности короля
    /// </summary>
    /// <param name="fen">Исходная позиция партии в формате FEN</param>
    /// <param name="sanMove">Ход в SAN, например e4, Nf3, O-O или e8=Q</param>
    /// <param name="fenAfter">Позиция после легального хода или null при ошибке</param>
    /// <returns>Признак легального хода или false при пустых данных, ошибке загрузки FEN или разбора хода</returns>
    /// <remarks>
    /// Проверяет только позицию и ход, без прав пользователя и истории для определения повторений
    /// </remarks>
    public static bool IsLegalMove(string fen, string sanMove, [NotNullWhen(true)] out string? fenAfter)
    {
        fenAfter = null;
        if (string.IsNullOrWhiteSpace(fen) || string.IsNullOrWhiteSpace(sanMove))
            return false;

        // Отдельная доска для проверки, не изменяющая сохраненную позицию партии
        if (!ChessBoard.TryLoadFromFen(fen, out var board) || board.IsEndGame)
            return false;

        if (!board.TryParseFromSan(sanMove, out var move) || !board.Move(move))
            return false;

        fenAfter = board.ToFen();
        return true;
    }
}
