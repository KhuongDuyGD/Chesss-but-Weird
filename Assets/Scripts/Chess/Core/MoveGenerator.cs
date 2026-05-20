using System.Collections.Generic;

namespace ChessButWeird
{
    /// <summary>
    /// Generates all LEGAL moves for a given board position.
    /// A move is legal if the active player's king is not in check after it.
    /// </summary>
    public static class MoveGenerator
    {
        // ─────────────────────────────── Public ────────────────────────────────────

        public static List<Move> GetLegalMoves(ChessBoard board)
        {
            var pseudoLegal = GetPseudoLegalMoves(board);
            var legal       = new List<Move>(pseudoLegal.Count);

            PieceColor mover = board.ActiveColor;

            foreach (var move in pseudoLegal)
            {
                var copy = new ChessBoard(board);
                copy.ApplyMove(move);
                // After apply the active color flips, so check the colour that just moved
                if (!copy.IsInCheck(mover))
                    legal.Add(move);
            }

            return legal;
        }

        // ─────────────────────────────── Private ───────────────────────────────────

        private static List<Move> GetPseudoLegalMoves(ChessBoard board)
        {
            var moves = new List<Move>(40);
            var color = board.ActiveColor;

            for (int r = 0; r < 8; r++)
            for (int f = 0; f < 8; f++)
            {
                var piece = board.GetPiece(r, f);
                if (piece.IsEmpty || piece.Color != color) continue;

                var from = new Square(r, f);
                switch (piece.Type)
                {
                    case PieceType.Pawn:   GeneratePawnMoves  (board, from, color, moves); break;
                    case PieceType.Knight: GenerateKnightMoves(board, from, color, moves); break;
                    case PieceType.Bishop: GenerateSliding    (board, from, color, moves, diagonal: true,  orthogonal: false); break;
                    case PieceType.Rook:   GenerateSliding    (board, from, color, moves, diagonal: false, orthogonal: true);  break;
                    case PieceType.Queen:  GenerateSliding    (board, from, color, moves, diagonal: true,  orthogonal: true);  break;
                    case PieceType.King:   GenerateKingMoves  (board, from, color, moves); break;
                }
            }

            return moves;
        }

        // ──────────────────────────── Pawn ─────────────────────────────────────────

        private static void GeneratePawnMoves(ChessBoard board, Square from, PieceColor color, List<Move> moves)
        {
            int dir       = color == PieceColor.White ? 1 : -1;
            int startRank = color == PieceColor.White ? 1 : 6;
            int promoRank = color == PieceColor.White ? 7 : 0;

            // Forward one square
            var oneStep = new Square(from.Rank + dir, from.File);
            if (oneStep.IsValid && board.GetPiece(oneStep).IsEmpty)
            {
                AddPawnMove(from, oneStep, promoRank, moves);

                // Forward two squares from starting rank
                if (from.Rank == startRank)
                {
                    var twoStep = new Square(from.Rank + 2 * dir, from.File);
                    if (board.GetPiece(twoStep).IsEmpty)
                        moves.Add(new Move(from, twoStep));
                }
            }

            // Diagonal captures (+ en passant)
            for (int df = -1; df <= 1; df += 2)
            {
                var capSq = new Square(from.Rank + dir, from.File + df);
                if (!capSq.IsValid) continue;

                if (capSq == board.EnPassantSquare)
                {
                    moves.Add(new Move(from, capSq, enPassant: true));
                    continue;
                }

                var target = board.GetPiece(capSq);
                if (!target.IsEmpty && target.Color != color)
                    AddPawnMove(from, capSq, promoRank, moves);
            }
        }

        private static readonly PieceType[] PromoTypes = { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };

        private static void AddPawnMove(Square from, Square to, int promoRank, List<Move> moves)
        {
            if (to.Rank == promoRank)
                foreach (var pt in PromoTypes)
                    moves.Add(new Move(from, to, pt));
            else
                moves.Add(new Move(from, to));
        }

        // ──────────────────────────── Knight ───────────────────────────────────────

        private static readonly int[] KnightDr = { -2, -2, -1, -1,  1,  1,  2,  2 };
        private static readonly int[] KnightDf = { -1,  1, -2,  2, -2,  2, -1,  1 };

        private static void GenerateKnightMoves(ChessBoard board, Square from, PieceColor color, List<Move> moves)
        {
            for (int i = 0; i < 8; i++)
            {
                var to = new Square(from.Rank + KnightDr[i], from.File + KnightDf[i]);
                if (!to.IsValid) continue;
                var target = board.GetPiece(to);
                if (target.IsEmpty || target.Color != color)
                    moves.Add(new Move(from, to));
            }
        }

        // ──────────────────────────── Sliding ──────────────────────────────────────

        private static readonly int[][] SlideDirs =
        {
            new[]{ 1, 1}, new[]{ 1,-1}, new[]{-1, 1}, new[]{-1,-1},  // diagonal [0..3]
            new[]{ 1, 0}, new[]{-1, 0}, new[]{ 0, 1}, new[]{ 0,-1}   // orthogonal [4..7]
        };

        private static void GenerateSliding(ChessBoard board, Square from, PieceColor color,
                                             List<Move> moves, bool diagonal, bool orthogonal)
        {
            int dStart = diagonal   ? 0 : 4;
            int dEnd   = orthogonal ? 8 : 4;

            for (int d = dStart; d < dEnd; d++)
            {
                int r = from.Rank + SlideDirs[d][0];
                int f = from.File + SlideDirs[d][1];

                while (r >= 0 && r < 8 && f >= 0 && f < 8)
                {
                    var to     = new Square(r, f);
                    var target = board.GetPiece(to);

                    if (target.IsEmpty)
                    {
                        moves.Add(new Move(from, to));
                    }
                    else
                    {
                        if (target.Color != color) moves.Add(new Move(from, to));
                        break; // blocked
                    }

                    r += SlideDirs[d][0];
                    f += SlideDirs[d][1];
                }
            }
        }

        // ──────────────────────────── King ─────────────────────────────────────────

        private static void GenerateKingMoves(ChessBoard board, Square from, PieceColor color, List<Move> moves)
        {
            var opponent = color == PieceColor.White ? PieceColor.Black : PieceColor.White;

            // Normal one-square moves
            for (int dr = -1; dr <= 1; dr++)
            for (int df = -1; df <= 1; df++)
            {
                if (dr == 0 && df == 0) continue;
                var to = new Square(from.Rank + dr, from.File + df);
                if (!to.IsValid) continue;
                var target = board.GetPiece(to);
                if (target.IsEmpty || target.Color != color)
                    moves.Add(new Move(from, to));
            }

            // Cannot castle while in check
            if (board.IsSquareAttackedBy(from, opponent)) return;

            int backRank = color == PieceColor.White ? 0 : 7;

            // Kingside castling
            var ksRight = color == PieceColor.White ? CastlingRights.WhiteKingside : CastlingRights.BlackKingside;
            if ((board.CastlingRights & ksRight) != 0)
            {
                var f5 = new Square(backRank, 5);
                var f6 = new Square(backRank, 6);
                if (board.GetPiece(f5).IsEmpty && board.GetPiece(f6).IsEmpty
                    && !board.IsSquareAttackedBy(f5, opponent)
                    && !board.IsSquareAttackedBy(f6, opponent))
                {
                    moves.Add(new Move(from, new Square(backRank, 6), castling: true));
                }
            }

            // Queenside castling
            var qsRight = color == PieceColor.White ? CastlingRights.WhiteQueenside : CastlingRights.BlackQueenside;
            if ((board.CastlingRights & qsRight) != 0)
            {
                var f3 = new Square(backRank, 3);
                var f2 = new Square(backRank, 2);
                var f1 = new Square(backRank, 1);
                if (board.GetPiece(f3).IsEmpty && board.GetPiece(f2).IsEmpty && board.GetPiece(f1).IsEmpty
                    && !board.IsSquareAttackedBy(f3, opponent)
                    && !board.IsSquareAttackedBy(f2, opponent))
                {
                    moves.Add(new Move(from, new Square(backRank, 2), castling: true));
                }
            }
        }
    }
}