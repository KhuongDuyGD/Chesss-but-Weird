using System;
using System.Collections.Generic;
using System.Globalization;
using ChessButWeird.Application;
using ChessButWeird.Domain;
using UnityEngine;

public partial class ChessGame
{
    private readonly MatchStatistics statistics = new MatchStatistics();
    private bool statisticsTimeKnown = true;
    private bool authoritativeStatisticsClock;
    private float authoritativeElapsed;
    private float authoritativeElapsedAt;
    private float StatisticsElapsed => authoritativeElapsed + (gameStarted ? Mathf.Max(0, Time.unscaledTime - authoritativeElapsedAt) : 0);
    public MatchStatistics Statistics => statistics;
    public bool IsNetworkGame => serverAuthoritativeMode;
    public string StatisticsConnectionState => turnSelectionUI ? turnSelectionUI.MatchConnectionState : "Disconnected";
    public bool StatisticsTimeKnown => statisticsTimeKnown;
    public float StatisticsHudBottom => aramMode && aramCoordinator != null && aramCoordinator.Runtime
        ? aramCoordinator.Runtime.StatisticsHudBottom : 0;
    public bool IsCurrentTeamChecked => checkedKing != null;
    public bool StatisticsBuffsVisible(PieceTeam team) => aramMode && aramCoordinator != null && aramCoordinator.Runtime &&
        (!aramCoordinator.Runtime.IsNetworkMatch || team == playerTeam);
    private PieceTeam StatisticsViewer => botMode || serverAuthoritativeMode ? playerTeam : currentTurn;
    public IReadOnlyList<AramBuffDefinition> StatisticsBuffs(PieceTeam team) => StatisticsBuffsVisible(team)
        ? aramCoordinator.Runtime.GetDisplayBuffs(team,StatisticsViewer) : Array.Empty<AramBuffDefinition>();
    public string StatisticsBuffProgress(PieceTeam team,AramBuffId id) => StatisticsBuffsVisible(team) &&
        !aramCoordinator.Runtime.DisguisesBuff(team,StatisticsViewer)
        ? aramCoordinator.Runtime.BuffProgress(team,id) : string.Empty;
    public string StatisticsMode => aramMode ? "ARAM" : botMode ? "Solo" : serverAuthoritativeMode ? "Multiplayer" : "Local";
    public string StatisticsPlayerName(PieceTeam team)
    {
        string supplied = turnSelectionUI ? turnSelectionUI.GetMatchPlayerName(team) : null;
        if (!string.IsNullOrWhiteSpace(supplied)) return supplied;
        if (botMode) return team == playerTeam ? "You" : "Bot · " + botDifficulty;
        if (serverAuthoritativeMode) return team == playerTeam ? "You" : "Opponent";
        return team == PieceTeam.White ? "White player" : "Black player";
    }

    private void ResetStatistics()
    {
        statistics.Reset(); statisticsTimeKnown = true; authoritativeStatisticsClock = false;
    }
    private void RecordStatisticsMove(PieceTeam actor, string notation, ChessPiece captured)
    {
        statistics.AddLocal((Team)actor, notation, serverAuthoritativeMode,
            captured && captured.Team != actor ? (PieceKind?)captured.Type : null);
    }
    internal void RecordStatisticsEvent(PieceTeam actor, string description)
    {
        // Never write private network buff state into a public journal.
        if (!aramMode || serverAuthoritativeMode || !gameStarted) return;
        statistics.AddEvent((Team)actor, description);
    }
    public void RejectStatisticsPrediction()
    {
        statistics.RejectPending(); SyncLegacyStatistics();
    }
    public void ConfirmStatisticsMove(BackendMoveResultPayload payload, string previousFen)
    {
        if (ApplyStatisticsSnapshot(payload.statistics)) return;
        Team actor = currentTurn == PieceTeam.White ? Team.Black : Team.White;
        PieceKind? captured = null;
        bool known = !aramMode && MatchStatistics.TryClassicCapture(previousFen, payload.from, payload.to, out actor, out captured);
        if (aramMode)
        {
            // Identity is taken from the board BEFORE the action, never inferred by alternating rows.
            MatchStatistics.TryClassicCapture(previousFen, payload.from, payload.to, out actor, out _);
        }
        statistics.Confirm(payload.moveNumber, actor,
            string.IsNullOrWhiteSpace(payload.notation) ? payload.from + "-" + payload.to : payload.notation,
            captured, known);
        SyncLegacyStatistics();
    }
    public void ObserveStatisticsSnapshot(int moveNumber)
    {
        statistics.ObserveSnapshot(moveNumber); SyncLegacyStatistics();
    }
    public void RestoreStatistics(BackendMatchDto match)
    {
        if (ApplyStatisticsSnapshot(match.statistics)) return;
        var records = new List<MatchStatistics.Entry>();
        string before = FenCodec.InitialPosition;
        int expected = 1;
        bool complete = match.moveCount == 0 || match.moves != null;
        bool capturesKnown = !aramMode;
        if (match.moves != null)
        {
            var moves = new List<BackendMatchMoveDto>(match.moves);
            moves.Sort((a, b) => a.moveNumber.CompareTo(b.moveNumber));
            foreach (var move in moves)
            {
                if (move.moveNumber < expected) continue;
                bool contiguous = move.moveNumber == expected;
                complete &= contiguous;
                Team actor = string.Equals(move.playerId, match.blackPlayerId, StringComparison.Ordinal) ? Team.Black : Team.White;
                PieceKind? capture = null;
                if (!aramMode && contiguous)
                {
                    bool valid = MatchStatistics.TryClassicCapture(before, move.from, move.to, out Team boardActor, out capture);
                    capturesKnown &= valid;
                    if (valid) actor = boardActor;
                }
                else capturesKnown = false;
                records.Add(new MatchStatistics.Entry(move.id, move.moveNumber, actor,
                    string.IsNullOrWhiteSpace(move.notation) ? move.from + "-" + move.to : move.notation,
                    true, false, capture));
                before = move.fenAfter;
                expected = move.moveNumber + 1;
            }
        }
        complete &= expected - 1 == match.moveCount;
        statistics.Restore(records, match.moveCount, complete, complete && capturesKnown);
        DateTimeOffset started=default;
        bool activeMatch=string.Equals(match.status,"ACTIVE",StringComparison.OrdinalIgnoreCase);
        statisticsTimeKnown = HasStatisticsTimeZone(match.startedAt) &&
            (activeMatch || HasStatisticsTimeZone(match.finishedAt)) &&
            DateTimeOffset.TryParse(match.startedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out started);
        if (statisticsTimeKnown)
        {
            DateTimeOffset end = DateTimeOffset.TryParse(match.finishedAt, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var finished) ? finished : DateTimeOffset.UtcNow;
            float seconds = (float)Math.Max(0, (end - started).TotalSeconds);
            matchStartedAt = Time.unscaledTime - seconds;
            accumulatedPausedSeconds = 0; matchPausedAt = pauseLocked ? Time.unscaledTime : -1;
            frozenMatchDurationSeconds = seconds;
        }
        SyncLegacyStatistics();
    }
    public void MarkStatisticsTimeUnavailable() { statisticsTimeKnown = false; }
    private static bool HasStatisticsTimeZone(string value)
    {
        if(string.IsNullOrWhiteSpace(value))return false;
        value=value.Trim();int time=value.IndexOf('T');
        return time>=0&&(value.EndsWith("Z",StringComparison.OrdinalIgnoreCase)||value.IndexOf('+',time)>=0||value.IndexOf('-',time)>=0);
    }
    public void MarkStatisticsRecoveryIncomplete(int moveNumber)
    {statistics.Reset(false);statistics.ObserveSnapshot(moveNumber);statisticsTimeKnown=false;SyncLegacyStatistics();}

    public bool ApplyStatisticsSnapshot(BackendMatchStatistics snapshot)
    {
        if (snapshot == null || snapshot.version != 1 || snapshot.entries == null || snapshot.moveCount < 0 ||
            snapshot.moveCount < statistics.ConfirmedMoveNumber || snapshot.elapsedSeconds < 0 ||
            (snapshot.capturesComplete && !snapshot.historyComplete))
            return false;
        var restored = new List<MatchStatistics.Entry>();
        var ids = new HashSet<string>();
        int lastMove=0,moveRows=0;
        foreach (var row in snapshot.entries)
        {
            if (row == null || string.IsNullOrEmpty(row.id) || !ids.Add(row.id) ||
                row.moveNumber < 0 || row.moveNumber > snapshot.moveCount ||
                !Enum.TryParse(row.actor, true, out Team actor) || !Enum.IsDefined(typeof(Team), actor)) return false;
            if(row.isMove)
            {
                if(row.moveNumber<=lastMove || (snapshot.historyComplete&&row.moveNumber!=lastMove+1))return false;
                lastMove=row.moveNumber;moveRows++;
            }
            else if(row.moveNumber!=lastMove || !string.IsNullOrEmpty(row.captured))return false;
            PieceKind? capture = null;
            if (!string.IsNullOrEmpty(row.captured))
            {
                if (!Enum.TryParse(row.captured, true, out PieceKind parsed) || !Enum.IsDefined(typeof(PieceKind), parsed)) return false;
                capture = parsed;
            }
            restored.Add(new MatchStatistics.Entry(row.id, row.moveNumber, actor, row.text, row.isMove, false, capture));
        }
        if(snapshot.historyComplete&&(lastMove!=snapshot.moveCount||moveRows!=snapshot.moveCount))return false;
        statistics.Restore(restored, snapshot.moveCount, snapshot.historyComplete, snapshot.capturesComplete);
        authoritativeElapsed = Mathf.Max(0, snapshot.elapsedSeconds);
        authoritativeElapsedAt = Time.unscaledTime;
        authoritativeStatisticsClock = statisticsTimeKnown = true;
        frozenMatchDurationSeconds = authoritativeElapsed;
        SyncLegacyStatistics();
        return true;
    }

    private void SyncLegacyStatistics()
    {
        moveHistory.Clear(); whiteCapturedPieces.Clear(); blackCapturedPieces.Clear();
        foreach (var entry in statistics.Entries)
        {
            if (entry.IsMove) moveHistory.Add(entry.Text);
            if (entry.Captured.HasValue) GetCapturedPieceList((PieceTeam)entry.Actor).Add((PieceType)entry.Captured.Value);
        }
        lastMoveSummary = moveHistory.Count > 0 ? moveHistory[moveHistory.Count - 1] : "No moves yet.";
    }
}
