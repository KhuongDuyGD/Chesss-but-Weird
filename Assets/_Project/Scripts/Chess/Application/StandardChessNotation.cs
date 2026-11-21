using System;
using System.Collections.Generic;
using System.Text;
using ChessButWeird.Domain;

namespace ChessButWeird.Application
{
    /// <summary>Export validation and SAN formatting; never changes gameplay rules.</summary>
    public static class StandardChessNotation
    {
        public static string SingleLine(string value)
        {
            var text = new StringBuilder();
            foreach (char c in value ?? string.Empty) text.Append(char.IsControl(c) || char.IsWhiteSpace(c) ? ' ' : c);
            return text.ToString().Trim();
        }

        public static MatchState ParsePosition(string fen)
        {
            MatchState state = FenCodec.Parse(fen);
            string[] fields = fen.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (char symbol in fields[0])
                if ("PNBRQKpnbrqk12345678/".IndexOf(symbol) < 0)
                    throw new FormatException("FEN piece placement must use standard ASCII symbols.");
            foreach (string rank in fields[0].Split('/'))
                for (int i = 1; i < rank.Length; i++)
                    if (char.IsDigit(rank[i]) && char.IsDigit(rank[i - 1])) throw new FormatException("Noncanonical empty squares in FEN.");
            int[] kings = new int[2], counts = new int[2], pawns = new int[2];
            var material = new int[2, 7];
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            {
                PieceState piece = state.Board.GetPiece(new Square(x, y));
                if (piece.IsEmpty) continue;
                counts[(int)piece.Team]++;
                int materialIndex = piece.Kind == PieceKind.Bishop ? 5 + (x + y) % 2 : (int)piece.Kind;
                if (piece.Kind != PieceKind.Pawn) material[(int)piece.Team, materialIndex]++;
                if (piece.Kind == PieceKind.King) kings[(int)piece.Team]++;
                if (piece.Kind == PieceKind.Pawn)
                {
                    pawns[(int)piece.Team]++;
                    if (y == 0 || y == 7) throw new FormatException("An unpromoted pawn occupies its last rank.");
                }
            }
            for (int side = 0; side < 2; side++)
            {
                if (kings[side] != 1 || counts[side] > 16 || pawns[side] > 8) throw new FormatException("Unsupported chess material or missing king.");
                int promotions = Math.Max(0, material[side, 1] - 1) + Math.Max(0, material[side, 2] - 2) +
                    Math.Max(0, material[side, 4] - 2) + Math.Max(0, material[side, 5] - 1) + Math.Max(0, material[side, 6] - 1);
                if (promotions > 8 - pawns[side]) throw new FormatException("Promoted material has no corresponding missing pawns.");
            }
            if (ClassicRules.IsInCheck(state.Board, ClassicRules.Opponent(state.Turn)))
                throw new FormatException("The side that just moved is still in check.");
            // Parsing must not silently discard claimed castling rights or move counters.
            string[] written = FenCodec.Write(state).Split(' ');
            foreach (char right in fields[2])
                if (right != '-' && written[2].IndexOf(right) < 0) throw new FormatException("Castling rights do not match the king and rook positions.");
            if (state.HalfMoveClock == int.MaxValue || state.FullMoveNumber == int.MaxValue)
                throw new FormatException("Move counters are outside the supported range.");
            if (state.EnPassantTarget.IsValid)
            {
                Square ep = state.EnPassantTarget;
                int forward = state.Turn == Team.White ? 1 : -1;
                PieceState pawn = state.Board.GetPiece(new Square(ep.File, ep.Rank - forward));
                if (!state.Board.GetPiece(ep).IsEmpty || pawn.IsEmpty || pawn.Kind != PieceKind.Pawn || pawn.Team == state.Turn ||
                    !state.Board.GetPiece(new Square(ep.File, ep.Rank + forward)).IsEmpty || state.HalfMoveClock != 0)
                    throw new FormatException("The en passant target has no matching double pawn move.");
            }
            return state;
        }

        public static string WriteSan(MatchState state, Move move, out MatchState next)
        {
            if (!ClassicRules.TryApply(state, move, out MoveResult applied))
                throw new FormatException("The move record contains an illegal or incomplete move.");
            next = applied.State;
            PieceState piece = state.Board.GetPiece(move.From);
            bool capture = false;
            foreach (var change in applied.Changes) if (change.Kind == BoardChangeKind.Capture) capture = true;
            var san = new StringBuilder();
            if (piece.Kind == PieceKind.King && Math.Abs(move.To.File - move.From.File) == 2)
                san.Append(move.To.File > move.From.File ? "O-O" : "O-O-O");
            else
            {
                if (piece.Kind != PieceKind.Pawn)
                {
                    san.Append(Symbol(piece.Kind));
                    bool ambiguous = false, sameFile = false, sameRank = false;
                    // Only LEGAL alternatives count. Pinned pieces cannot disambiguate SAN.
                    for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
                    {
                        var from = new Square(x, y); var other = state.Board.GetPiece(from);
                        if (from == move.From || other.IsEmpty || other.Team != piece.Team || other.Kind != piece.Kind ||
                            !ClassicRules.TryApply(state, new Move(from, move.To), out _)) continue;
                        ambiguous = true; sameFile |= x == move.From.File; sameRank |= y == move.From.Rank;
                    }
                    if (ambiguous)
                    {
                        if (!sameFile) san.Append((char)('a' + move.From.File));
                        else if (!sameRank) san.Append((char)('1' + move.From.Rank));
                        else san.Append(move.From.ToString());
                    }
                }
                else if (capture) san.Append((char)('a' + move.From.File));
                if (capture) san.Append('x');
                san.Append(move.To.ToString());
                if (move.Promotion.HasValue) san.Append('=').Append(Symbol(move.Promotion.Value));
            }
            if (ClassicRules.IsInCheck(next.Board, next.Turn))
                san.Append(ClassicRules.LegalMoves(next).Count == 0 ? '#' : '+');
            return san.ToString();
        }

        private static char Symbol(PieceKind kind) => "KQRBNP"[(int)kind];
    }
}
