using System;
using System.Text;

namespace ChessButWeird
{
    /// <summary>
    /// Immutable‑style board state. Call ApplyMove() to mutate in place.
    /// Use the copy constructor to branch positions without changing the original.
    /// </summary>
    public class ChessBoard
    {
        // [rank, file]  rank 0 = white's back rank
        private readonly Piece[,] _board = new Piece[8, 8];

        public PieceColor    ActiveColor    { get; private set; }
        public CastlingRights CastlingRights { get; private set; }
        public Square        EnPassantSquare { get; private set; }
        public int           HalfmoveClock  { get; private set; }
        public int           FullmoveNumber  { get; private set; }

        public const string StartFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        // ─────────────────────────────── Constructors ──────────────────────────────

        public ChessBoard()                   => LoadFEN(StartFEN);
        public ChessBoard(string fen)         => LoadFEN(fen);

        /// <summary>Deep copy.</summary>
        public ChessBoard(ChessBoard src)
        {
            Array.Copy(src._board, _board, 64);
            ActiveColor     = src.ActiveColor;
            CastlingRights  = src.CastlingRights;
            EnPassantSquare = src.EnPassantSquare;
            HalfmoveClock   = src.HalfmoveClock;
            FullmoveNumber  = src.FullmoveNumber;
        }

        // ─────────────────────────────── Accessors ─────────────────────────────────

        public Piece GetPiece(Square sq)        => sq.IsValid ? _board[sq.Rank, sq.File] : Piece.Empty;
        public Piece GetPiece(int rank, int file) => _board[rank, file];
        public void  SetPiece(Square sq, Piece p) => _board[sq.Rank, sq.File] = p;

        // ─────────────────────────────── FEN ───────────────────────────────────────

        public void LoadFEN(string fen)
        {
            for (int r = 0; r < 8; r++)
                for (int f = 0; f < 8; f++)
                    _board[r, f] = Piece.Empty;

            var parts = fen.Split(' ');
            var rows  = parts[0].Split('/');

            for (int row = 0; row < 8; row++)
            {
                int rank = 7 - row; // FEN row 0 = rank 8
                int file = 0;
                foreach (char c in rows[row])
                {
                    if (char.IsDigit(c)) file += c - '0';
                    else { _board[rank, file] = CharToPiece(c); file++; }
                }
            }

            ActiveColor = parts.Length > 1 && parts[1] == "b" ? PieceColor.Black : PieceColor.White;

            CastlingRights = CastlingRights.None;
            if (parts.Length > 2)
            {
                if (parts[2].Contains('K')) CastlingRights |= CastlingRights.WhiteKingside;
                if (parts[2].Contains('Q')) CastlingRights |= CastlingRights.WhiteQueenside;
                if (parts[2].Contains('k')) CastlingRights |= CastlingRights.BlackKingside;
                if (parts[2].Contains('q')) CastlingRights |= CastlingRights.BlackQueenside;
            }

            EnPassantSquare = parts.Length > 3 ? Square.FromAlgebraic(parts[3]) : Square.Invalid;
            HalfmoveClock   = parts.Length > 4 ? int.Parse(parts[4]) : 0;
            FullmoveNumber  = parts.Length > 5 ? int.Parse(parts[5]) : 1;
        }

        public string ToFEN()
        {
            var sb = new StringBuilder();

            for (int r = 7; r >= 0; r--)
            {
                int empty = 0;
                for (int f = 0; f < 8; f++)
                {
                    var p = _board[r, f];
                    if (p.IsEmpty) { empty++; }
                    else
                    {
                        if (empty > 0) { sb.Append(empty); empty = 0; }
                        sb.Append(p.ToString());
                    }
                }
                if (empty > 0) sb.Append(empty);
                if (r > 0) sb.Append('/');
            }

            sb.Append(' ');
            sb.Append(ActiveColor == PieceColor.White ? 'w' : 'b');

            string cr = "";
            if ((CastlingRights & CastlingRights.WhiteKingside)  != 0) cr += 'K';
            if ((CastlingRights & CastlingRights.WhiteQueenside) != 0) cr += 'Q';
            if ((CastlingRights & CastlingRights.BlackKingside)  != 0) cr += 'k';
            if ((CastlingRights & CastlingRights.BlackQueenside) != 0) cr += 'q';
            if (cr == "") cr = "-";

            sb.Append(' ').Append(cr);
            sb.Append(' ').Append(EnPassantSquare.ToString());
            sb.Append(' ').Append(HalfmoveClock);
            sb.Append(' ').Append(FullmoveNumber);
            return sb.ToString();
        }

        // ─────────────────────────────── Apply Move ────────────────────────────────

        /// <summary>
        /// Mutates the board. Returns false only for obvious invalid input
        /// (wrong color, empty square). Legal‑move validation is done by MoveGenerator.
        /// </summary>
        public bool ApplyMove(Move move)
        {
            var piece = GetPiece(move.From);
            if (piece.IsEmpty || piece.Color != ActiveColor) return false;

            bool isPawnMove  = piece.Type == PieceType.Pawn;
            bool isCapture   = !GetPiece(move.To).IsEmpty;

            // En passant – remove the captured pawn
            if (move.IsEnPassant)
            {
                int capturedRank = ActiveColor == PieceColor.White ? move.To.Rank - 1 : move.To.Rank + 1;
                SetPiece(new Square(capturedRank, move.To.File), Piece.Empty);
                isCapture = true;
            }

            // Castling – move rook as well
            if (move.IsCastling)
            {
                bool kingSide   = move.To.File > move.From.File;
                int  rookFromF  = kingSide ? 7 : 0;
                int  rookToF    = kingSide ? 5 : 3;
                var  rook       = GetPiece(new Square(move.From.Rank, rookFromF));
                SetPiece(new Square(move.From.Rank, rookFromF), Piece.Empty);
                SetPiece(new Square(move.From.Rank, rookToF),   rook);
            }

            // Move the piece
            SetPiece(move.To,   piece);
            SetPiece(move.From, Piece.Empty);

            // Pawn promotion
            if (move.Promotion != PieceType.None)
                SetPiece(move.To, new Piece(move.Promotion, ActiveColor));

            // Update castling rights
            UpdateCastlingRights(move, piece);

            // Update en passant target
            EnPassantSquare = Square.Invalid;
            if (isPawnMove && Math.Abs(move.To.Rank - move.From.Rank) == 2)
                EnPassantSquare = new Square((move.From.Rank + move.To.Rank) / 2, move.From.File);

            // Clocks
            if (isCapture || isPawnMove) HalfmoveClock = 0;
            else                         HalfmoveClock++;

            if (ActiveColor == PieceColor.Black) FullmoveNumber++;

            ActiveColor = ActiveColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
            return true;
        }

        private void UpdateCastlingRights(Move move, Piece piece)
        {
            if (piece.Type == PieceType.King)
            {
                if (piece.Color == PieceColor.White)
                    CastlingRights &= ~(CastlingRights.WhiteKingside | CastlingRights.WhiteQueenside);
                else
                    CastlingRights &= ~(CastlingRights.BlackKingside | CastlingRights.BlackQueenside);
            }

            // If a rook moves or is captured its castling right is lost
            void RevokeSq(Square sq, CastlingRights right)
            {
                if (move.From == sq || move.To == sq) CastlingRights &= ~right;
            }
            RevokeSq(new Square(0, 0), CastlingRights.WhiteQueenside);
            RevokeSq(new Square(0, 7), CastlingRights.WhiteKingside);
            RevokeSq(new Square(7, 0), CastlingRights.BlackQueenside);
            RevokeSq(new Square(7, 7), CastlingRights.BlackKingside);
        }

        // ─────────────────────────────── Queries ───────────────────────────────────

        public Square FindKing(PieceColor color)
        {
            for (int r = 0; r < 8; r++)
                for (int f = 0; f < 8; f++)
                    if (_board[r, f].Type == PieceType.King && _board[r, f].Color == color)
                        return new Square(r, f);
            return Square.Invalid;
        }

        public bool IsInCheck(PieceColor color)
        {
            var kingSq   = FindKing(color);
            if (!kingSq.IsValid) return false;
            var opponent = color == PieceColor.White ? PieceColor.Black : PieceColor.White;
            return IsSquareAttackedBy(kingSq, opponent);
        }

        /// <summary>Is <paramref name="sq"/> attacked by any piece of <paramref name="attacker"/>?</summary>
        public bool IsSquareAttackedBy(Square sq, PieceColor attacker)
        {
            // ── Pawns ──
            int pawnSrcRank = sq.Rank - (attacker == PieceColor.White ? 1 : -1);
            for (int df = -1; df <= 1; df += 2)
            {
                var psq = new Square(pawnSrcRank, sq.File + df);
                if (psq.IsValid)
                {
                    var p = GetPiece(psq);
                    if (p.Type == PieceType.Pawn && p.Color == attacker) return true;
                }
            }

            // ── Knights ──
            int[] nDr = { -2, -2, -1, -1,  1,  1,  2,  2 };
            int[] nDf = { -1,  1, -2,  2, -2,  2, -1,  1 };
            for (int i = 0; i < 8; i++)
            {
                var ksq = new Square(sq.Rank + nDr[i], sq.File + nDf[i]);
                if (!ksq.IsValid) continue;
                var p = GetPiece(ksq);
                if (p.Type == PieceType.Knight && p.Color == attacker) return true;
            }

            // ── Sliding pieces ──
            int[][] dirs =
            {
                new[]{ 1, 0}, new[]{-1, 0}, new[]{ 0, 1}, new[]{ 0,-1},  // orthogonal
                new[]{ 1, 1}, new[]{ 1,-1}, new[]{-1, 1}, new[]{-1,-1}   // diagonal
            };
            for (int d = 0; d < 8; d++)
            {
                int r = sq.Rank + dirs[d][0];
                int f = sq.File + dirs[d][1];
                while (r >= 0 && r < 8 && f >= 0 && f < 8)
                {
                    var p = _board[r, f];
                    if (!p.IsEmpty)
                    {
                        if (p.Color == attacker)
                        {
                            bool ortho = d < 4;
                            if (ortho  && (p.Type == PieceType.Rook   || p.Type == PieceType.Queen)) return true;
                            if (!ortho && (p.Type == PieceType.Bishop  || p.Type == PieceType.Queen)) return true;
                        }
                        break;
                    }
                    r += dirs[d][0]; f += dirs[d][1];
                }
            }

            // ── King ──
            for (int dr = -1; dr <= 1; dr++)
            for (int df = -1; df <= 1; df++)
            {
                if (dr == 0 && df == 0) continue;
                var ksq = new Square(sq.Rank + dr, sq.File + df);
                if (!ksq.IsValid) continue;
                var p = GetPiece(ksq);
                if (p.Type == PieceType.King && p.Color == attacker) return true;
            }

            return false;
        }

        // ─────────────────────────────── Helpers ───────────────────────────────────

        private static Piece CharToPiece(char c)
        {
            PieceColor color = char.IsUpper(c) ? PieceColor.White : PieceColor.Black;
            PieceType  type  = char.ToLower(c) switch
            {
                'p' => PieceType.Pawn,
                'n' => PieceType.Knight,
                'b' => PieceType.Bishop,
                'r' => PieceType.Rook,
                'q' => PieceType.Queen,
                'k' => PieceType.King,
                _   => PieceType.None
            };
            return new Piece(type, color);
        }
    }
}