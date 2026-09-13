using System;
using System.Collections.Generic;

namespace ChessButWeird.Domain
{
    /// <summary>
    /// Owns one classic match state and accepts moves in the board coordinates shown
    /// to the player. The owned MatchState never escapes; snapshots and move results
    /// are detached copies for presentation, persistence, or tests.
    /// </summary>
    public sealed class MatchSession
    {
        private MatchState state;

        private MatchSession(MatchState initialState, BoardOrientation orientation)
        {
            state = initialState ?? throw new ArgumentNullException(nameof(initialState));
            Orientation = orientation;
        }

        public BoardOrientation Orientation { get; }
        public Team Turn => state.Turn;
        public MatchState Snapshot => state.Clone();

        public static MatchSession CreateClassic(Team firstTurn, BoardOrientation orientation)
        {
            MatchState canonical = FenCodec.Parse(FenCodec.InitialPosition);
            canonical.Turn = firstTurn;
            return new MatchSession(canonical, orientation);
        }

        public static MatchSession FromSnapshot(MatchState canonicalState, BoardOrientation orientation)
        {
            return new MatchSession(canonicalState?.Clone() ?? throw new ArgumentNullException(nameof(canonicalState)), orientation);
        }

        public PieceState GetPiece(Square runtimeSquare)
        {
            PieceState canonical = state.Board.GetPiece(Orientation.ToCanonical(runtimeSquare));
            if (canonical.IsEmpty)
                return canonical;
            return new PieceState(canonical.Id, canonical.Kind, canonical.Team, canonical.HasMoved,
                Orientation.ToRuntimeForward(canonical.Forward));
        }

        public bool CanApply(Square runtimeFrom, Square runtimeTo, PieceKind? promotion = null)
        {
            return ClassicRules.TryApply(state,
                ToCanonicalMove(runtimeFrom, runtimeTo, promotion), out _);
        }

        public bool TryApply(Square runtimeFrom, Square runtimeTo, out MatchSessionMoveResult result)
        {
            return TryApply(runtimeFrom, runtimeTo, null, out result);
        }

        public bool TryApply(Square runtimeFrom, Square runtimeTo, PieceKind? promotion,
            out MatchSessionMoveResult result)
        {
            result = null;
            if (!runtimeFrom.IsValid || !runtimeTo.IsValid)
                return false;

            Move canonicalMove = ToCanonicalMove(runtimeFrom, runtimeTo, promotion);
            if (!ClassicRules.TryApply(state, canonicalMove, out MoveResult applied))
                return false;

            state = applied.State;
            result = MatchSessionMoveResult.Create(applied, new Move(runtimeFrom, runtimeTo, promotion), Orientation);
            return true;
        }

        public List<Move> LegalMoves()
        {
            List<Move> canonicalMoves = ClassicRules.LegalMoves(state);
            return ToRuntimeMoves(canonicalMoves);
        }

        public List<Move> LegalMovesFrom(Square runtimeFrom)
        {
            List<Move> canonicalMoves = ClassicRules.LegalMovesFrom(state, Orientation.ToCanonical(runtimeFrom));
            return ToRuntimeMoves(canonicalMoves);
        }

        private Move ToCanonicalMove(Square runtimeFrom, Square runtimeTo, PieceKind? promotion)
        {
            return new Move(Orientation.ToCanonical(runtimeFrom), Orientation.ToCanonical(runtimeTo), promotion);
        }

        private List<Move> ToRuntimeMoves(List<Move> canonicalMoves)
        {
            var runtimeMoves = new List<Move>(canonicalMoves.Count);
            for (int i = 0; i < canonicalMoves.Count; i++)
            {
                Move move = canonicalMoves[i];
                runtimeMoves.Add(new Move(Orientation.ToRuntime(move.From), Orientation.ToRuntime(move.To), move.Promotion));
            }
            return runtimeMoves;
        }
    }

    public sealed class MatchSessionMoveResult
    {
        private MatchSessionMoveResult(Move move, MatchState snapshot, IReadOnlyList<BoardChange> changes)
        {
            Move = move;
            State = snapshot;
            Changes = changes;
        }

        /// <summary>Move in runtime/player coordinates.</summary>
        public Move Move { get; }
        /// <summary>Detached canonical snapshot. Use MatchSession.GetPiece for runtime coordinates.</summary>
        public MatchState State { get; }
        /// <summary>Changes in runtime/player coordinates, including mapped castle/en-passant squares.</summary>
        public IReadOnlyList<BoardChange> Changes { get; }
        public bool IsCapture
        {
            get
            {
                for (int i = 0; i < Changes.Count; i++)
                    if (Changes[i].Kind == BoardChangeKind.Capture)
                        return true;
                return false;
            }
        }

        public bool IsCastle => Changes.Count == 2 &&
            Changes[0].Kind == BoardChangeKind.Move && Changes[1].Kind == BoardChangeKind.Move;

        internal static MatchSessionMoveResult Create(MoveResult applied, Move runtimeMove, BoardOrientation orientation)
        {
            var mapped = new List<BoardChange>(applied.Changes.Count);
            for (int i = 0; i < applied.Changes.Count; i++)
            {
                BoardChange change = applied.Changes[i];
                mapped.Add(new BoardChange(change.Kind, change.PieceId, change.PieceKind,
                    orientation.ToRuntime(change.From), orientation.ToRuntime(change.To)));
            }
            return new MatchSessionMoveResult(runtimeMove, applied.State.Clone(), mapped.AsReadOnly());
        }
    }
}
