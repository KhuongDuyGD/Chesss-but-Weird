using System;

namespace ChessButWeird.Application
{
    /// <summary>
    /// Owns transport-independent lobby and match identity state. The Unity
    /// lobby facade can still render the legacy UI, but room/match lifecycle
    /// transitions have one state owner beside NetworkMatchSession.
    /// </summary>
    public sealed class NetworkLobbySession : IDisposable
    {
        public const string ClassicGameMode = "CLASSIC";

        public bool IsPanelVisible { get; private set; }
        public bool IsMatchActive { get; private set; }
        public string RoomCode { get; private set; } = string.Empty;
        public string MatchId { get; private set; } = string.Empty;
        public string GameMode { get; private set; } = ClassicGameMode;
        public bool LocalReady { get; private set; }
        public int ReadyCount { get; private set; }
        public int Generation { get; private set; }

        public void Configure(string gameMode)
        {
            GameMode = string.IsNullOrWhiteSpace(gameMode) ? ClassicGameMode : gameMode;
            IsPanelVisible = true;
        }

        public void SetPanelVisible(bool visible)
        {
            IsPanelVisible = visible;
        }

        public void SetMatchActive(bool active)
        {
            IsMatchActive = active;
        }

        public void SetRoomCode(string roomCode)
        {
            RoomCode = roomCode ?? string.Empty;
        }

        public void SetMatchId(string matchId)
        {
            MatchId = matchId ?? string.Empty;
        }

        public void SetGameMode(string gameMode)
        {
            GameMode = string.IsNullOrWhiteSpace(gameMode) ? ClassicGameMode : gameMode;
        }

        public void SetReady(bool ready, int readyCount)
        {
            LocalReady = ready;
            ReadyCount = Math.Max(0, readyCount);
        }

        public void SetReadyCount(int readyCount)
        {
            ReadyCount = Math.Max(0, readyCount);
        }

        public void BeginMatch(string matchId, string gameMode)
        {
            SetMatchId(matchId);
            SetGameMode(gameMode);
            IsMatchActive = true;
            IsPanelVisible = false;
            Generation++;
        }

        public void CompleteMatch()
        {
            IsMatchActive = false;
            LocalReady = false;
        }

        public void Reset(bool clearRoomIdentity)
        {
            IsPanelVisible = false;
            IsMatchActive = false;
            LocalReady = false;
            ReadyCount = 0;
            MatchId = string.Empty;
            if (clearRoomIdentity)
            {
                RoomCode = string.Empty;
                GameMode = ClassicGameMode;
            }

            Generation++;
        }

        public void Dispose()
        {
            Reset(true);
        }
    }
}
