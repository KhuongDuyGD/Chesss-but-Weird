using System;
using System.Collections.Generic;
using UnityEngine;

// Narrow dependencies for copied production HUD and ChessGame.Statistics sources.
// No gameplay, account, transport, actual match scene or server is loaded here.
public enum PieceTeam { White, Black }
public enum PieceType { King, Queen, Rook, Bishop, Knight, Pawn }
public sealed partial class ChessGame : MonoBehaviour
{
    public enum ChessGameStatus { Playing, Draw, Checkmate }
    private bool aramMode,botMode,serverAuthoritativeMode,gameStarted=true,pauseLocked;
    private StockfishDifficulty botDifficulty=StockfishDifficulty.Beginner;
    private string lastMoveSummary;
    private PieceTeam currentTurn,playerTeam;
    private ChessPiece checkedKing;
    private ChessTurnSelectionUI turnSelectionUI;
    private float matchStartedAt,accumulatedPausedSeconds,matchPausedAt=-1,frozenMatchDurationSeconds;
    private readonly List<string> moveHistory=new List<string>();
    private readonly List<PieceType> whiteCapturedPieces=new List<PieceType>(),blackCapturedPieces=new List<PieceType>();
    private readonly HudCoordinator aramCoordinator=new HudCoordinator();
    public bool IsAramGame { get=>aramMode; set=>aramMode=value; }
    public bool IsBotGame { get=>botMode; set=>botMode=value; }
    public bool IsBotPractice => false;
    public bool PauseLocked { get=>pauseLocked; set=>pauseLocked=value; }
    public PieceTeam CurrentTurn { get=>currentTurn;set=>currentTurn=value; }
    public PieceTeam PlayerTeam { get=>playerTeam;set=>playerTeam=value; }
    public float AramHudBottom { get=>aramCoordinator.Runtime?aramCoordinator.Runtime.StatisticsHudBottom:0;set { aramCoordinator.Runtime=GetComponent<AramBuffRuntime>();aramCoordinator.Runtime.StatisticsHudBottom=value; } }
    public bool UsesDotNetOnline;
    public string OnlineClockLabel(PieceTeam team) => "10:00";
    public string OnlineMatchPhase => "Playing";
    public bool TryGetOnlineBuffSummary(PieceTeam team, out string summary, out string details)
    { summary = details = string.Empty; return false; }
    public bool GameOver,HasPendingPromotion;
    public PieceTeam WinningTeam;
    public ChessGameStatus Status;
    public string DrawReason="Stalemate";
    public float MatchElapsedSeconds => gameStarted
        ? Mathf.Max(0f,Time.unscaledTime-matchStartedAt-accumulatedPausedSeconds-CurrentPauseDuration) : frozenMatchDurationSeconds;
    private float CurrentPauseDuration=>matchPausedAt>=0?Time.unscaledTime-matchPausedAt:0;
    private List<PieceType> GetCapturedPieceList(PieceTeam team)=>team==PieceTeam.White?whiteCapturedPieces:blackCapturedPieces;
    public IReadOnlyList<PieceType> GetCapturedPieces(PieceTeam team)=>GetCapturedPieceList(team);
    public int GetCapturedMaterialScore(PieceTeam team) {int value=0;foreach(var piece in GetCapturedPieces(team))value+=piece==PieceType.Queen?9:piece==PieceType.Rook?5:piece==PieceType.Knight||piece==PieceType.Bishop?3:piece==PieceType.Pawn?1:0;return value;}
}
// The existing HUD fixture models the idle avatar's reserved space without loading bot gameplay.
public static class BotCompanionView
{
    public static float BottomInset(ChessGame game)=>156;
}
public static class BotPracticeView { public static float ToolbarWidth(ChessGame game)=>0; }
public sealed class HudCoordinator { public AramBuffRuntime Runtime; public bool IsActive => Runtime && Runtime.IsActive; }
public sealed class ChessPiece : MonoBehaviour { public PieceTeam Team; public PieceType Type; }
public sealed class ChessTurnSelectionUI : MonoBehaviour
{
    public string MatchConnectionState="Connected";
    public string GetMatchPlayerName(PieceTeam team)=>team==PieceTeam.White?"You":"Bot · Beginner";
}
public sealed class AramBuffRuntime : MonoBehaviour
{
    public bool IsActive = true;
    public bool AbilityInstructionRequired;
    public readonly List<AramBuffDefinition> WhiteBuffs=new List<AramBuffDefinition>(),BlackBuffs=new List<AramBuffDefinition>();
    public IReadOnlyList<AramBuffDefinition> GetBuffs(PieceTeam team)=>team==PieceTeam.White?WhiteBuffs:BlackBuffs;
    public bool Disguised;
    public PieceTeam LastViewer;
    public IReadOnlyList<AramBuffDefinition> GetDisplayBuffs(PieceTeam team,PieceTeam viewer) {LastViewer=viewer;return GetBuffs(team);}
    public bool DisguisesBuff(PieceTeam team,PieceTeam viewer)=>Disguised&&team!=viewer;
    public string BuffProgressText="Tickets: 12 · Drop: 0/1";
    public string BuffProgress(PieceTeam team,AramBuffId id)=>BuffProgressText;
    public bool AbilityHudVisible=true,RifleActive;
    public string AbilityContext="turn-1",AbilityStatus="Choose an ability, or play your move.";
    public string AbilityBuffSummary="White · Battle Gacha\nTickets: 64 · Drop: 0/1",AbilityBuffDetails="Battle Gacha\nSpend tickets to deploy a piece.\nTickets: 64 · Drop: 0/1";
    public string RifleMouseHint="ALT: unlock mouse";
    public string AbilityPresentationContext="White:normal";
    public float AbilityPanelHeight,StatisticsHudBottom;
    public readonly List<AramAbilityView.AbilityAction> Actions=new List<AramAbilityView.AbilityAction>();
    public void GetAbilityActions(List<AramAbilityView.AbilityAction> destination) {destination.Clear();destination.AddRange(Actions);}
}
