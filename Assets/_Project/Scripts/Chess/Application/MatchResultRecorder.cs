using System;

namespace ChessButWeird.Application
{
    /// <summary>
    /// Guards the profile/history write for one match identity.
    /// Presentation may receive the same terminal signal more than once (for
    /// example after a reconnect); the first accepted callback owns the write.
    /// </summary>
    public sealed class MatchResultRecorder
    {
        private string matchId = string.Empty;
        private bool recorded;

        public string MatchId => matchId;
        public bool HasRecorded => recorded;

        public void Begin(string newMatchId)
        {
            matchId = string.IsNullOrWhiteSpace(newMatchId) ? string.Empty : newMatchId;
            recorded = false;
        }

        public bool TryRecord(Action writeResult)
        {
            if (recorded || string.IsNullOrWhiteSpace(matchId) || writeResult == null)
                return false;

            writeResult();
            recorded = true;
            return true;
        }

        public void Reset()
        {
            matchId = string.Empty;
            recorded = false;
        }
    }
}
