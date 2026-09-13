using System;
using System.Collections.Generic;

namespace ChessButWeird.Application
{
    /// <summary>Transport-independent authoritative match snapshot.</summary>
    public sealed class NetworkMatchSnapshot
    {
        public string MatchId { get; internal set; }
        public string GameMode { get; internal set; }
        public string Fen { get; internal set; }
        public int MoveNumber { get; internal set; }
        public bool IsActive { get; internal set; }

        public NetworkMatchSnapshot Clone()
        {
            return new NetworkMatchSnapshot
            {
                MatchId = MatchId,
                GameMode = GameMode,
                Fen = Fen,
                MoveNumber = MoveNumber,
                IsActive = IsActive
            };
        }
    }

    /// <summary>
    /// Owns client-side command bookkeeping around a server-authoritative match.
    /// It never adjudicates moves; it only rejects stale/duplicate responses and
    /// replaces the presentation snapshot at an explicit sequence boundary.
    /// </summary>
    public sealed class NetworkMatchSession : IDisposable
    {
        private readonly HashSet<string> pendingRequestIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> completedRequestIds = new HashSet<string>(StringComparer.Ordinal);
        private NetworkMatchSnapshot snapshot;
        private bool completed;

        public bool IsActive => snapshot != null && snapshot.IsActive;
        public bool HasSnapshot => snapshot != null;
        public bool IsCompleted => completed;
        public int Generation { get; private set; }
        public string MatchId => snapshot != null ? snapshot.MatchId : string.Empty;
        public string GameMode => snapshot != null ? snapshot.GameMode : string.Empty;
        public string AuthoritativeFen => snapshot != null ? snapshot.Fen : string.Empty;
        public int ConfirmedMoveNumber => snapshot != null ? snapshot.MoveNumber : -1;
        public int PendingCommandCount => pendingRequestIds.Count;

        public void Begin(string matchId, string gameMode, string fen, int moveNumber = 0, bool active = true)
        {
            ResetInternal();
            Generation++;
            snapshot = new NetworkMatchSnapshot
            {
                MatchId = matchId ?? string.Empty,
                GameMode = gameMode ?? string.Empty,
                Fen = fen ?? string.Empty,
                MoveNumber = Math.Max(0, moveNumber),
                IsActive = active
            };
            completed = !active;
        }

        public bool TryQueueCommand(string requestId, string matchId)
        {
            if (!IsActive || string.IsNullOrWhiteSpace(requestId) || !MatchesMatch(matchId))
                return false;

            if (completedRequestIds.Contains(requestId) || pendingRequestIds.Contains(requestId))
                return false;

            pendingRequestIds.Add(requestId);
            return true;
        }

        public bool CanAcceptResult(string requestId, string matchId, int moveNumber)
        {
            return IsActive && MatchesMatch(matchId) && moveNumber > ConfirmedMoveNumber &&
                (string.IsNullOrWhiteSpace(requestId) || !completedRequestIds.Contains(requestId));
        }

        public bool TryAcceptResult(string requestId, string matchId, int moveNumber, string fen)
        {
            if (!CanAcceptResult(requestId, matchId, moveNumber))
                return false;

            if (!string.IsNullOrWhiteSpace(requestId))
            {
                if (completedRequestIds.Contains(requestId))
                    return false;

                pendingRequestIds.Remove(requestId);
                completedRequestIds.Add(requestId);
            }

            snapshot.Fen = fen ?? string.Empty;
            snapshot.MoveNumber = moveNumber;
            return true;
        }

        public bool RejectCommand(string requestId, string matchId)
        {
            if (!IsActive || !MatchesMatch(matchId) || string.IsNullOrWhiteSpace(requestId))
                return false;

            return pendingRequestIds.Remove(requestId);
        }

        public bool ApplySnapshot(string matchId, string gameMode, string fen, int moveNumber, bool active)
        {
            if (snapshot != null && !MatchesMatch(matchId))
                return false;

            if (snapshot == null)
                Begin(matchId, gameMode, fen, moveNumber, active);
            else
            {
                if (moveNumber < snapshot.MoveNumber)
                    return false;

                if (completed && active)
                    return false;

                if (!string.IsNullOrWhiteSpace(matchId))
                    snapshot.MatchId = matchId;
                if (!string.IsNullOrWhiteSpace(gameMode))
                    snapshot.GameMode = gameMode;
                snapshot.Fen = fen ?? string.Empty;
                snapshot.MoveNumber = Math.Max(0, moveNumber);
                snapshot.IsActive = active;
                completed |= !active;
            }

            pendingRequestIds.Clear();
            completedRequestIds.Clear();
            return true;
        }

        /// <summary>
        /// Checks a replacement snapshot without changing pending requests or
        /// authoritative state. Callers use this before mode-specific payload
        /// validation that may update presentation input state.
        /// </summary>
        public bool CanApplySnapshot(string matchId, int moveNumber, bool active)
        {
            return snapshot != null && MatchesMatch(matchId) &&
                moveNumber >= snapshot.MoveNumber && !(completed && active);
        }

        public NetworkMatchSnapshot GetSnapshot()
        {
            return snapshot?.Clone();
        }

        public void Complete()
        {
            if (snapshot != null)
            {
                snapshot.IsActive = false;
                completed = true;
            }
            pendingRequestIds.Clear();
            completedRequestIds.Clear();
        }

        public void Reset()
        {
            ResetInternal();
            Generation++;
        }

        public void Dispose()
        {
            Reset();
        }

        private bool MatchesMatch(string matchId)
        {
            return snapshot != null &&
                (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(snapshot.MatchId) ||
                 string.Equals(snapshot.MatchId, matchId, StringComparison.Ordinal));
        }

        private void ResetInternal()
        {
            snapshot = null;
            completed = false;
            pendingRequestIds.Clear();
            completedRequestIds.Clear();
        }
    }
}
