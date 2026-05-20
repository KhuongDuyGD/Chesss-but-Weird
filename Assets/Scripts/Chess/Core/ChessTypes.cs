using System;

namespace ChessButWeird
{
    public enum PieceType   { None, Pawn, Knight, Bishop, Rook, Queen, King }
    public enum PieceColor  { None, White, Black }
    public enum GamePhase   { Opening, Midgame, Endgame }
    public enum GameResult  { InProgress, WhiteWins, BlackWins, Draw }

    [Serializable]
    public struct Piece
    {
        public PieceType  Type;
        public PieceColor Color;

        public static readonly Piece Empty = new Piece(PieceType.None, PieceColor.None);

        public bool IsEmpty => Type == PieceType.None;
        public bool IsWhite => Color == PieceColor.White;
        public bool IsBlack => Color == PieceColor.Black;

        public Piece(PieceType type, PieceColor color) { Type = type; Color = color; }

        public override string ToString()
        {
            if (IsEmpty) return ".";
            char c = Type switch
            {
                PieceType.Pawn   => 'p',
                PieceType.Knight => 'n',
                PieceType.Bishop => 'b',
                PieceType.Rook   => 'r',
                PieceType.Queen  => 'q',
                PieceType.King   => 'k',
                _                => '.'
            };
            return IsWhite ? char.ToUpper(c).ToString() : c.ToString();
        }
    }

    [Serializable]
    public struct Square
    {
        public int Rank; // 0‑7  (0 = rank 1, white side)
        public int File; // 0‑7  (0 = file a)

        public bool IsValid => Rank >= 0 && Rank < 8 && File >= 0 && File < 8;

        public Square(int rank, int file) { Rank = rank; File = file; }

        public static readonly Square Invalid = new Square(-1, -1);

        /// <summary>Parse algebraic notation like "e4". Returns Invalid for "-".</summary>
        public static Square FromAlgebraic(string s)
        {
            if (string.IsNullOrEmpty(s) || s == "-") return Invalid;
            return new Square(s[1] - '1', s[0] - 'a');
        }

        public string ToAlgebraic() => $"{(char)('a' + File)}{Rank + 1}";

        public static bool operator ==(Square a, Square b) => a.Rank == b.Rank && a.File == b.File;
        public static bool operator !=(Square a, Square b) => !(a == b);
        public override bool Equals(object obj) => obj is Square s && this == s;
        public override int GetHashCode() => Rank * 8 + File;
        public override string ToString() => IsValid ? ToAlgebraic() : "-";
    }

    [Flags]
    public enum CastlingRights
    {
        None          = 0,
        WhiteKingside = 1,
        WhiteQueenside= 2,
        BlackKingside = 4,
        BlackQueenside= 8,
        All           = 15
    }

    [Serializable]
    public struct Move
    {
        public Square   From;
        public Square   To;
        public PieceType Promotion;   // None unless pawn promo
        public bool     IsEnPassant;
        public bool     IsCastling;

        public Move(Square from, Square to,
                    PieceType promotion = PieceType.None,
                    bool enPassant = false,
                    bool castling  = false)
        {
            From        = from;
            To          = to;
            Promotion   = promotion;
            IsEnPassant = enPassant;
            IsCastling  = castling;
        }

        public override string ToString()
        {
            string s = $"{From}{To}";
            if (Promotion != PieceType.None) s += Promotion.ToString().ToLower()[0];
            return s;
        }
    }
}