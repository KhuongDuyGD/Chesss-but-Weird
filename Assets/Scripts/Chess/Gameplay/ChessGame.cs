using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ChessButWeird
{
    /// <summary>
    /// Core Chess game manager. Attach to a GameObject.
    /// ARAM mode derives from this class; UI only needs to talk to this interface.
    /// </summary>
    public class ChessGame : MonoBehaviour
    {
        // ───────────────────────────── State ───────────────────────────────────────

        public ChessBoard  Board       { get; protected set; }
        public List<Move>  LegalMoves  { get; private set; } = new();
        public GameResult  Result      { get; private set; } = GameResult.InProgress;
        public bool        IsInCheck   => Board.IsInCheck(Board.ActiveColor);

        // Full-move number doubles as a rough move counter for phase detection
        public int MoveNumber => Board?.FullmoveNumber ?? 1;

        // ───────────────────────────── Events ──────────────────────────────────────

        /// <summary>Fired after every successful move (pre‑result check).</summary>
        public UnityEvent<Move>       OnMoveMade     = new();
        /// <summary>Fired when the game ends.</summary>
        public UnityEvent<GameResult> OnGameOver     = new();
        /// <summary>Fired whenever the board changes (move applied or new game).</summary>
        public UnityEvent             OnBoardChanged = new();

        // ───────────────────────────── Private ─────────────────────────────────────

        private readonly List<string> _positionHistory = new();

        // ───────────────────────────── Unity ───────────────────────────────────────

        protected virtual void Start() => InitGame();

        // ───────────────────────────── Public API ──────────────────────────────────

        public virtual void InitGame()
        {
            Board = new ChessBoard();
            Result = GameResult.InProgress;
            _positionHistory.Clear();
            RefreshLegalMoves();
            OnBoardChanged.Invoke();
        }

        /// <summary>
        /// Attempt to make a move. Returns true if the move was legal and applied.
        /// For pawn promotion, pass the desired piece in <paramref name="promotion"/>
        /// (defaults to Queen).
        /// </summary>
        public bool TryMakeMove(Square from, Square to, PieceType promotion = PieceType.Queen)
        {
            if (Result != GameResult.InProgress) return false;

            Move? found = FindLegalMove(from, to, promotion);
            if (found == null) return false;

            ExecuteMove(found.Value);
            return true;
        }

        /// <summary>All legal moves originating from <paramref name="sq"/>.</summary>
        public List<Move> GetMovesFrom(Square sq) =>
            LegalMoves.FindAll(m => m.From == sq);

        public GamePhase GetCurrentPhase()
        {
            int n = MoveNumber;
            if (n <= 15) return GamePhase.Opening;
            if (n <= 40) return GamePhase.Midgame;
            return GamePhase.Endgame;
        }

        // ───────────────────────────── Protected ───────────────────────────────────

        protected virtual void ExecuteMove(Move move)
        {
            Board.ApplyMove(move);

            // Record position (FEN piece placement only, for threefold‑repetition check)
            _positionHistory.Add(Board.ToFEN().Split(' ')[0]);

            OnMoveMade.Invoke(move);

            RefreshLegalMoves();
            CheckGameOver();
            OnBoardChanged.Invoke();
        }

        // ───────────────────────────── Private ─────────────────────────────────────

        private Move? FindLegalMove(Square from, Square to, PieceType promotion)
        {
            foreach (var m in LegalMoves)
            {
                if (m.From != from || m.To != to) continue;
                // For promotion moves match the requested piece; for all others Promotion == None
                if (m.Promotion == PieceType.None || m.Promotion == promotion)
                    return m;
            }
            return null;
        }

        private void RefreshLegalMoves() =>
            LegalMoves = MoveGenerator.GetLegalMoves(Board);

        private void CheckGameOver()
        {
            if (LegalMoves.Count == 0)
            {
                // Checkmate or stalemate
                Result = Board.IsInCheck(Board.ActiveColor)
                    ? (Board.ActiveColor == PieceColor.White ? GameResult.BlackWins : GameResult.WhiteWins)
                    : GameResult.Draw;

                OnGameOver.Invoke(Result);
                return;
            }

            // 50-move rule (100 half-moves)
            if (Board.HalfmoveClock >= 100)
            {
                Result = GameResult.Draw;
                OnGameOver.Invoke(Result);
                return;
            }

            // Threefold repetition
            string cur   = Board.ToFEN().Split(' ')[0];
            int    count = 0;
            foreach (var pos in _positionHistory)
                if (pos == cur) count++;

            if (count >= 3)
            {
                Result = GameResult.Draw;
                OnGameOver.Invoke(Result);
            }
        }
    }
}