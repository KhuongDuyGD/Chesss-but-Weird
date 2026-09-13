using System;
using ChessButWeird.Domain;

namespace ChessButWeird.Application
{
    /// <summary>Converts Stockfish UCI output into the same domain command used by local classic.</summary>
    public static class StockfishMoveAdapter
    {
        public static bool TryParseUci(string uciMove, out Move move)
        {
            move = default;
            uciMove = uciMove?.Trim();
            if (string.IsNullOrWhiteSpace(uciMove) || (uciMove.Length != 4 && uciMove.Length != 5))
                return false;

            if (!TryParseSquare(uciMove.Substring(0, 2), out Square from) ||
                !TryParseSquare(uciMove.Substring(2, 2), out Square to))
                return false;

            if (uciMove.Length == 4)
            {
                move = new Move(from, to);
                return true;
            }

            PieceKind promotion;
            switch (char.ToLowerInvariant(uciMove[4]))
            {
                case 'q': promotion = PieceKind.Queen; break;
                case 'r': promotion = PieceKind.Rook; break;
                case 'b': promotion = PieceKind.Bishop; break;
                case 'n': promotion = PieceKind.Knight; break;
                default: return false;
            }

            move = new Move(from, to, promotion);
            return true;
        }

        private static bool TryParseSquare(string square, out Square position)
        {
            position = new Square(-1, -1);
            if (square == null || square.Length != 2)
                return false;

            char file = char.ToLowerInvariant(square[0]);
            char rank = square[1];
            if (file < 'a' || file > 'h' || rank < '1' || rank > '8')
                return false;

            position = new Square(file - 'a', rank - '1');
            return true;
        }
    }
}
