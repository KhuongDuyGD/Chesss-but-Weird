using System;
using System.Collections.Generic;
using ChessButWeird.Domain;

namespace ChessButWeird.Application
{
    public enum ClassicMoveStatus
    {
        Rejected,
        PromotionRequired,
        Applied
    }

    /// <summary>
    /// Coordinates one local classic command stream around MatchSession.
    /// The coordinator owns the session and the pending promotion command so
    /// presentation code cannot create a competing mutable match state.
    /// </summary>
    public sealed class ClassicMatchCoordinator
    {
        private readonly MatchSession session;
        private Move pendingPromotion;
        private bool hasPendingPromotion;

        private ClassicMatchCoordinator(MatchSession ownedSession)
        {
            session = ownedSession ?? throw new ArgumentNullException(nameof(ownedSession));
        }

        public static ClassicMatchCoordinator CreateClassic(Team firstTurn, BoardOrientation orientation)
        {
            return new ClassicMatchCoordinator(MatchSession.CreateClassic(firstTurn, orientation));
        }

        public static ClassicMatchCoordinator FromSnapshot(MatchState snapshot, BoardOrientation orientation)
        {
            return new ClassicMatchCoordinator(MatchSession.FromSnapshot(snapshot, orientation));
        }

        public BoardOrientation Orientation => session.Orientation;
        public Team Turn => session.Turn;
        public bool HasPendingPromotion => hasPendingPromotion;
        public MatchState Snapshot => session.Snapshot;

        public PieceState GetPiece(Square runtimeSquare) => session.GetPiece(runtimeSquare);

        public List<Move> LegalMoves() => hasPendingPromotion
            ? new List<Move>()
            : session.LegalMoves();

        public List<Move> LegalMovesFrom(Square runtimeFrom) => hasPendingPromotion
            ? new List<Move>()
            : session.LegalMovesFrom(runtimeFrom);

        public bool CanApply(Square runtimeFrom, Square runtimeTo, PieceKind? promotion = null)
        {
            return !hasPendingPromotion && session.CanApply(runtimeFrom, runtimeTo, promotion);
        }

        public bool TryApply(Square runtimeFrom, Square runtimeTo, out MatchSessionMoveResult result)
        {
            if (hasPendingPromotion)
            {
                result = null;
                return false;
            }

            return session.TryApply(runtimeFrom, runtimeTo, out result);
        }

        /// <summary>
        /// Submits one command to the owned session. A pawn move to its last rank
        /// is held as a pending command when no promotion choice is supplied;
        /// every other command is validated and committed atomically.
        /// </summary>
        public ClassicMoveStatus Submit(Move command, out MatchSessionMoveResult result)
        {
            result = null;

            if (hasPendingPromotion)
            {
                if (!command.Promotion.HasValue || command.From != pendingPromotion.From ||
                    command.To != pendingPromotion.To)
                    return ClassicMoveStatus.Rejected;

                return TryCompletePromotion(command.Promotion.Value, out result)
                    ? ClassicMoveStatus.Applied
                    : ClassicMoveStatus.Rejected;
            }

            PieceState piece = session.GetPiece(command.From);
            bool reachesPromotionRank = !piece.IsEmpty && piece.Kind == PieceKind.Pawn &&
                command.To.IsValid && command.To.Rank == (piece.Forward > 0 ? 7 : 0);
            if (!command.Promotion.HasValue && reachesPromotionRank)
            {
                return BeginPromotion(command.From, command.To)
                    ? ClassicMoveStatus.PromotionRequired
                    : ClassicMoveStatus.Rejected;
            }

            return session.TryApply(command.From, command.To, command.Promotion, out result)
                ? ClassicMoveStatus.Applied
                : ClassicMoveStatus.Rejected;
        }

        public bool BeginPromotion(Square runtimeFrom, Square runtimeTo)
        {
            if (hasPendingPromotion || !session.CanApply(runtimeFrom, runtimeTo, PieceKind.Queen))
                return false;

            pendingPromotion = new Move(runtimeFrom, runtimeTo);
            hasPendingPromotion = true;
            return true;
        }

        public bool TryCompletePromotion(PieceKind promotion, out MatchSessionMoveResult result)
        {
            result = null;
            if (!hasPendingPromotion)
                return false;

            if (promotion == PieceKind.King || promotion == PieceKind.Pawn ||
                !Enum.IsDefined(typeof(PieceKind), promotion))
                return false;

            if (!session.TryApply(pendingPromotion.From, pendingPromotion.To, promotion, out result))
                return false;

            ClearPendingCommand();
            return true;
        }

        public void ClearPendingCommand()
        {
            pendingPromotion = default;
            hasPendingPromotion = false;
        }
    }
}
