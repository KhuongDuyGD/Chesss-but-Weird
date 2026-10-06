using System;
using System.Collections.Generic;
using ChessButWeird.Application;
using ChessButWeird.Domain;
using UnityEngine;

// Account, transport, animation, audio and scene ports are inert. The copied ARAM
// runtime, board bridge, movement adapters and extracted Core rules are production.
public sealed partial class ChessGame : MonoBehaviour
{
    private readonly ChessPiece[,] pieces=new ChessPiece[8,8];
    private readonly MatchStatistics statistics=new MatchStatistics();
    private readonly Dictionary<string,int> positionHistory=new Dictionary<string,int>();
    private int halfMoveClock;
    private readonly List<PieceType>[] captures={new List<PieceType>(),new List<PieceType>()};
    private bool aramMode=true,serverAuthoritativeMode,gameOver,lastMoveWasPawnDoubleStep;
    private PieceTeam currentTurn;
    private readonly FixtureCoordinator aramCoordinator=new FixtureCoordinator();
    private FixtureBoard chessboard=new FixtureBoard();
    private FixtureAnimator pieceAnimator;
    private FixtureTurnUI turnSelectionUI;
    private ChessPiece whiteKing,blackKing;
    private const int BoardSize=8;
    private const float BoardClickRaycastDistance=500;
    private LayerMask boardClickRaycastMask=~0;
    public bool GameStarted=true,InputLocked,PauseLocked,HasPendingPromotion;
    public bool IsBotGame;
    public PieceTeam PlayerTeam;
    public bool GameOver=>gameOver;
    public PieceTeam CurrentTurn {get=>currentTurn;set=>currentTurn=value;}
    public PieceTeam Winner;
    public bool Drawn;
    public AramBuffRuntime Runtime=>aramCoordinator.Runtime;
    public MatchStatistics Statistics=>statistics;
    public ChessPiece[,] Board=>pieces;
    public void Connect(AramBuffRuntime value){aramCoordinator.Runtime=value;}
    public void SetAramInputLocked(bool value){InputLocked=value;}
    public void ClearSelection(){}
    public void RecordStatisticsEvent(PieceTeam team,string message){statistics.AddEvent((Team)team,message);}
    public List<ChessPiece> GetActivePiecesForAram(PieceTeam team)
    {var result=new List<ChessPiece>();foreach(var piece in pieces)if(piece&&piece.Team==team)result.Add(piece);return result;}
    private List<PieceType> GetCapturedPieceList(PieceTeam team)=>captures[(int)team];
    private Camera GetGameplayCamera()=>Camera.main;
    private bool IsCachedKingValid(ChessPiece p,PieceTeam team)=>AramIsAlive(p)&&p.Team==team&&p.Type==PieceType.King&&!p.GetComponent<AramDecoyTag>();
    private void CacheKing(ChessPiece p){if(p.Team==PieceTeam.White)whiteKing=p;else blackKing=p;}
    private void FinishGame(PieceTeam team){Winner=team;gameOver=true;GameStarted=false;}
    private void FinishDraw(string reason)
    {bool w=Runtime.DrawIsLoss(PieceTeam.White),b=Runtime.DrawIsLoss(PieceTeam.Black);if(w!=b&&!AramLostRoyalCondition(w?PieceTeam.Black:PieceTeam.White)){FinishGame(w?PieceTeam.Black:PieceTeam.White);return;}Drawn=true;gameOver=true;}
    private string FormatSquare(Vector2Int p)=>new Square(p.x,p.y).ToString();
    private string BuildPositionKey()
    {var key=new System.Text.StringBuilder(currentTurn.ToString());foreach(var p in pieces)key.Append(p?p.Team+":"+p.Type+":"+p.BoardPosition+":"+p.HasMoved:"-");return key.ToString();}
    private void MovePieceToTile(ChessPiece p,Vector2Int square,float height){p.transform.position=chessboard.GetTileCenterWorld(square);}
    private void AnimatePieceToTile(ChessPiece p,Vector2Int square,float a,float b,float c){MovePieceToTile(p,square,0);}
    private ChessPiece CreateVisualPieceObject(PieceTeam team,PieceType kind,Vector2Int square,int forward)
    {var go=new GameObject(team+" "+kind);go.transform.SetParent(transform,false);var p=go.AddComponent<FixturePiece>();p.Kind=kind;p.Initialize(team,square,forward);return p;}
    public ChessPiece Add(PieceTeam team,PieceType kind,int x,int y)
    {var p=CreateVisualPieceObject(team,kind,new Vector2Int(x,y),team==PieceTeam.White?1:-1);pieces[x,y]=p;return p;}
    private bool TryGetEnPassantCapture(ChessPiece p,Vector2Int from,Vector2Int to,out ChessPiece pawn,out Vector2Int square){pawn=null;square=default;return false;}
    private bool IsEnPassantMove(ChessPiece p,Vector2Int from,Vector2Int to)=>false;
    private bool IsCheckmate(PieceTeam team)=>IsTeamInCheck(team)&&!HasAnySafeLegalMove(team);
    private bool HasAnySafeLegalMove(PieceTeam team){foreach(var p in GetActivePiecesForAram(team))if(GetSafeLegalMoves(p).Count>0)return true;return false;}
    private List<Vector2Int> GetSafeLegalMoves(ChessPiece p)
    {
        var result=new List<Vector2Int>();var candidates=new List<Vector2Int>();
        if(!Runtime.SuppressesStandardMovement(p))p.CollectLegalMoves(pieces,candidates);
        AddSpecialCandidateMoves(p,candidates);Runtime.AddCandidateMoves(p,candidates,pieces);
        foreach(var to in candidates)if(Runtime.CanMove(p,to)&&(!pieces[to.x,to.y]||pieces[to.x,to.y].Type!=PieceType.King)&&DoesMoveKeepTeamKingSafe(p,p.BoardPosition,to,p.Team))result.Add(to);
        return result;
    }
    private bool TryGetDrawReason(out string reason){reason="Stalemate";return !IsTeamInCheck(currentTurn)&&!HasAnySafeLegalMove(currentTurn);}
    private void UpdateCheckWarningForCurrentTurn(){}
    private void RefreshLocalInteractionState(){}
}
public sealed class FixturePiece : ChessPiece
{
    public PieceType Kind;
    public override PieceType Type=>Kind;
    protected override bool IsLegalMovePattern(Vector2Int destination,ChessPiece[,] board)=>UnityBoardAdapter.IsLegalPattern(this,destination,board);
}
public sealed class FixtureCoordinator
{
    public AramBuffRuntime Runtime;
    public AramRules DomainRules=>Runtime.DomainRules;
    public bool WouldQueenExplode(ChessPiece p)=>Runtime.WouldQueenExplode(p);
    public void OnTurnStarted(PieceTeam team)=>Runtime.OnTurnStarted(team);
    public bool AllowsStrongFortressCastle(PieceTeam team)=>Runtime.AllowsStrongFortressCastle(team);
}
public sealed class FixtureBoard
{
    public static bool operator !(FixtureBoard board)=>board==null;
    public Vector3 GetTileCenterWorld(Vector2Int p)=>new Vector3(p.x,0,p.y);
    public void ClearLegalMoveHighlights(){}
    public void SetLegalMoveHighlights(List<Vector2Int> squares){}
    public bool TryGetTileFromObject(GameObject obj,out Vector2Int p){p=default;return false;}
    public bool IsValidTile(Vector2Int p)=>ChessMoveRules.IsInsideBoard(p);
}
public sealed class FixtureAnimator{public void Cancel(ChessPiece p){}}
public sealed class FixtureTurnUI{public void SetTurn(PieceTeam team){}}
