using System;
using System.Collections.Generic;
using ChessButWeird.Application;
using ChessButWeird.Domain;
using UnityEngine;

public partial class ChessGame
{
    private readonly MatchStatistics statistics = new MatchStatistics();
    private bool statisticsTimeKnown = true;
    public MatchStatistics Statistics => statistics;
    public bool IsNetworkGame => serverAuthoritativeMode;
    public string StatisticsConnectionState => turnSelectionUI ? turnSelectionUI.MatchConnectionState : "Disconnected";
    public bool StatisticsTimeKnown => statisticsTimeKnown;
    public float StatisticsHudBottom => UsesDotNetOnline ? 110f : aramMode && aramCoordinator != null && aramCoordinator.Runtime && aramCoordinator.IsActive
        ? aramCoordinator.Runtime.StatisticsHudBottom : 0;
    public bool IsCurrentTeamChecked => checkedKing != null;
    public bool StatisticsBuffsVisible(PieceTeam team) => aramMode && aramCoordinator != null && aramCoordinator.Runtime &&
        aramCoordinator.IsActive && !UsesDotNetOnline;
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
        statistics.Reset(); statisticsTimeKnown = true;
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
    public void MarkStatisticsTimeUnavailable() { statisticsTimeKnown = false; }
    public void MarkStatisticsRecoveryIncomplete(int moveNumber)
    {statistics.Reset(false);statistics.ObserveSnapshot(moveNumber);statisticsTimeKnown=false;SyncPresentationStatistics();}

    private void SyncPresentationStatistics()
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
