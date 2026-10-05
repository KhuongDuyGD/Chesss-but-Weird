using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class ChessGame
{
    private int lastAramRepetitionMove=-1;
    internal ChessPiece[,] AramBoard => pieces;
    internal Camera AramCamera => GetGameplayCamera();
    internal ChessPiece AramKing(PieceTeam team) => FindKing(team);
    internal bool AramInCheck(PieceTeam team) => IsTeamInCheck(team);
    internal void AramWin(PieceTeam winner) => FinishGame(winner);
    internal bool AramIsMate(PieceTeam team) => IsCheckmate(team);
    internal bool AramHasMove(ChessPiece piece) => GetSafeLegalMoves(piece).Count>0;
    internal bool AramIsAlive(ChessPiece piece) => piece && ChessMoveRules.IsInsideBoard(piece.BoardPosition) && pieces[piece.BoardPosition.x,piece.BoardPosition.y] == piece;
    internal Vector3 AramTile(Vector2Int square) => chessboard.GetTileCenterWorld(square);
    internal void AramHighlight(List<Vector2Int> squares)
    { if(squares==null)chessboard.ClearLegalMoveHighlights();else chessboard.SetLegalMoveHighlights(squares); }
    internal bool AramSafeRemoval(ChessPiece piece,PieceTeam team)
    {
        if(!AramIsAlive(piece))return false;
        var p=piece.BoardPosition;pieces[p.x,p.y]=null;
        bool safe=!IsTeamInCheck(team);pieces[p.x,p.y]=piece;return safe;
    }
    internal bool AramSafeSnipe(ChessPiece victim,PieceTeam team)
    {
        if(!AramIsAlive(victim))return false;
        if(aramCoordinator.Runtime.IsKingless(team))return true;
        var projected=new ChessButWeird.Domain.RemovalBoardView<UnityBoardAdapter>(new UnityBoardAdapter(pieces),UnityBoardAdapter.ToSquare(victim.BoardPosition),false);
        var contexts=new UnityBuffContextAdapter(pieces,aramCoordinator.Runtime);
        if(aramCoordinator.WouldQueenExplode(victim))
        {
            var chained=new ChessButWeird.Domain.ExplosionChainBoardView<ChessButWeird.Domain.RemovalBoardView<UnityBoardAdapter>>(projected,UnityBoardAdapter.ToSquare(victim.BoardPosition),contexts);
            return !ChessButWeird.Domain.KingSafetyRules.IsInCheck(chained,(ChessButWeird.Domain.Team)team,contexts,aramCoordinator.DomainRules);
        }
        return !ChessButWeird.Domain.KingSafetyRules.IsInCheck(projected,(ChessButWeird.Domain.Team)team,
            contexts,aramCoordinator.DomainRules);
    }
    internal void AramRestoreFormation(Dictionary<ChessPiece,Vector2Int> formation)
    {
        foreach(var p in formation.Keys)if(AramIsAlive(p))pieces[p.BoardPosition.x,p.BoardPosition.y]=null;
        foreach(var pair in formation)if(pair.Key)
        { pieces[pair.Value.x,pair.Value.y]=pair.Key;pair.Key.SetBoardPosition(pair.Value);MovePieceToTile(pair.Key,pair.Value,0f); }
    }

    internal bool AramReadPointer(out Vector2Int square)
    {
        square = new Vector2Int(-1,-1);
        if (Mouse.current==null || !AramCamera) return false;
        Ray ray = AramCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray,out var hit,BoardClickRaycastDistance,boardClickRaycastMask)) return false;
        var piece = hit.collider.GetComponentInParent<ChessPiece>();
        if (piece) { square=piece.BoardPosition; return true; }
        return chessboard.TryGetTileFromObject(hit.collider.gameObject,out square);
    }

    internal ChessPiece AramSpawn(PieceType type, PieceTeam team, Vector2Int square, bool drop = false)
    {
        if (!aramMode || serverAuthoritativeMode || !ChessMoveRules.IsInsideBoard(square) || pieces[square.x,square.y]) return null;
        int forward = aramCoordinator.Runtime.ForwardFor(team);
        var piece = CreateVisualPieceObject(team,type,square,forward);
        if (!piece) return null;
        piece.MarkMoved();
        pieces[square.x,square.y]=piece;
        aramCoordinator.Runtime.RegisterSpawn(piece);
        RecordStatisticsEvent(team, type + " spawned on " + FormatSquare(square));
        MovePieceToTile(piece,square,0f);
        if (drop) { piece.transform.position += Vector3.up * 3f; AnimatePieceToTile(piece,square,0f,.4f,0f); }
        return piece;
    }

    internal void AramRemove(ChessPiece piece, bool notify = true)
    {
        if (!AramIsAlive(piece)) return;
        if (notify) aramCoordinator.Runtime.OnPieceRemoved(piece);
        var p=piece.BoardPosition;
        RecordStatisticsEvent(piece.Team, piece.Type + " removed from " + FormatSquare(p));
        pieces[p.x,p.y]=null;
        pieceAnimator?.Cancel(piece);
        piece.gameObject.SetActive(false);
        Destroy(piece.gameObject);
        if(notify)aramCoordinator.Runtime.OtherQueenRemoved(piece,p);
    }

    internal ChessPiece AramReplace(ChessPiece old, PieceType type, PieceTeam team)
    {
        if (!AramIsAlive(old)) return null;
        var p=old.BoardPosition;
        AramRemove(old,false);
        return AramSpawn(type,team,p);
    }

    internal bool AramRelocate(ChessPiece piece, Vector2Int to, bool requireSafe = true, bool swap = false, bool markMoved = true)
    {
        if (!AramIsAlive(piece) || !ChessMoveRules.IsInsideBoard(to)) return false;
        var other=pieces[to.x,to.y]; var from=piece.BoardPosition;
        if (from==to || (other && (!swap || other.Team!=piece.Team))) return false;
        pieces[from.x,from.y]=other; pieces[to.x,to.y]=piece;
        piece.SetBoardPosition(to); if(other) other.SetBoardPosition(from);
        if (requireSafe && IsTeamInCheck(piece.Team))
        {
            pieces[from.x,from.y]=piece; pieces[to.x,to.y]=other;
            piece.SetBoardPosition(from); if(other)other.SetBoardPosition(to); return false;
        }
        pieceAnimator?.Cancel(piece); MovePieceToTile(piece,to,0f); if(markMoved)piece.MarkMoved();
        if(other){ pieceAnimator?.Cancel(other); MovePieceToTile(other,from,0f); if(markMoved)other.MarkMoved(); }
        lastMoveWasPawnDoubleStep=false;
        RecordStatisticsEvent(piece.Team, piece.Type + " relocated " + FormatSquare(from) + " → " + FormatSquare(to));
        ClearSelection(); return true;
    }

    internal bool AramSafeDestination(ChessPiece piece, Vector2Int to)
    {
        return AramIsAlive(piece) && ChessMoveRules.IsInsideBoard(to) && !pieces[to.x,to.y] &&
            DoesMoveKeepTeamKingSafe(piece,piece.BoardPosition,to,piece.Team);
    }

    internal bool AramFinishIfKingMissing()
    {
        bool whiteMissing=AramLostRoyalCondition(PieceTeam.White);
        bool blackMissing=AramLostRoyalCondition(PieceTeam.Black);
        if (whiteMissing && blackMissing) { FinishDraw("Both teams were eliminated"); return true; }
        if (whiteMissing) { FinishGame(PieceTeam.Black); return true; }
        if (blackMissing) { FinishGame(PieceTeam.White); return true; }
        return gameOver;
    }
    private bool AramLostRoyalCondition(PieceTeam team) => aramCoordinator.Runtime.IsKingless(team)
        ? GetActivePiecesForAram(team).Count==0 : !FindKing(team);

    internal void AramCompleteAbilityTurn()
    {
        if (gameOver || !aramMode || serverAuthoritativeMode) return;
        ClearSelection(); lastMoveWasPawnDoubleStep=false;
        halfMoveClock=0;
        currentTurn=currentTurn==PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        aramCoordinator.OnTurnStarted(currentTurn);
        turnSelectionUI?.SetTurn(currentTurn);
        AramRefreshPosition();
    }

    internal void AramRefreshPosition()
    {
        aramCoordinator.Runtime.ObserveCheckConditions();
        if (AramFinishIfKingMissing()) return;
        if (aramCoordinator.Runtime.HasPendingDecision) return;
        RecordCurrentPosition();
        if (IsCheckmate(currentTurn))
        {
            if(!aramCoordinator.Runtime.TryOfferEscape(currentTurn))FinishGame(currentTurn==PieceTeam.White ? PieceTeam.Black : PieceTeam.White);
            return;
        }
        if(aramCoordinator.Runtime.TryFinishTurnDeadline())return;
        if(TryGetDrawReason(out var reason)){FinishDraw(reason);return;}
        UpdateCheckWarningForCurrentTurn(); RefreshLocalInteractionState();
    }
    internal void AramInitializeDrawTracking()
    {
        if(serverAuthoritativeMode||statistics.ConfirmedMoveNumber>0)return;
        positionHistory.Clear();lastAramRepetitionMove=-1;RecordCurrentPosition();
    }

    internal bool AramCanPlace(PieceType kind,PieceTeam team,Vector2Int destination,bool forbidEnemyCheck=false,ChessPiece removed=null)
    {
        if(!aramCoordinator.Runtime.SquareAvailable(destination))return false;
        var view=new ChessButWeird.Domain.PlacementBoardView<UnityBoardAdapter>(new UnityBoardAdapter(pieces),
            UnityBoardAdapter.ToSquare(destination),new ChessButWeird.Domain.PieceState(int.MaxValue,(ChessButWeird.Domain.PieceKind)kind,(ChessButWeird.Domain.Team)team,true,aramCoordinator.Runtime.ForwardFor(team)),
            removed?UnityBoardAdapter.ToSquare(removed.BoardPosition):new ChessButWeird.Domain.Square(-1,-1));
        var contexts=new AramPlacementContexts(new UnityBuffContextAdapter(pieces,aramCoordinator.Runtime),
            kind==PieceType.Pawn&&destination.y==(aramCoordinator.Runtime.ForwardFor(team)>0?7:0)&&
            aramCoordinator.Runtime.HasBuff(team==PieceTeam.White?PieceTeam.Black:PieceTeam.White,AramBuffId.DoubleEdgedTrap));
        if(!aramCoordinator.Runtime.IsKingless(team)&&ChessButWeird.Domain.KingSafetyRules.IsInCheck(view,(ChessButWeird.Domain.Team)team,contexts,aramCoordinator.DomainRules))return false;
        var enemy=team==PieceTeam.White?PieceTeam.Black:PieceTeam.White;
        return !forbidEnemyCheck||aramCoordinator.Runtime.IsKingless(enemy)||!ChessButWeird.Domain.KingSafetyRules.IsInCheck(view,(ChessButWeird.Domain.Team)enemy,contexts,aramCoordinator.DomainRules);
    }

    private readonly struct AramPlacementContexts : ChessButWeird.Domain.IBuffContextProvider
    {
        private readonly UnityBuffContextAdapter existing;
        private readonly bool trapped;
        public AramPlacementContexts(UnityBuffContextAdapter existing,bool trapped){this.existing=existing;this.trapped=trapped;}
        public ChessButWeird.Domain.AramPieceContext GetContext(ChessButWeird.Domain.PieceState piece) => piece.Id==int.MaxValue?
            new ChessButWeird.Domain.AramPieceContext(ChessButWeird.Domain.AramBuffs.None,trapPawn:trapped):existing.GetContext(piece);
    }

    internal void AramAbilityCapture(PieceTeam actor,ChessPiece victim,string detail,bool spendsMove=false)
    {
        if(!AramIsAlive(victim))return;
        ChessButWeird.Domain.PieceKind? kind=victim.Team!=actor?(ChessButWeird.Domain.PieceKind?)victim.Type:null;
        if(spendsMove)statistics.AddLocal((ChessButWeird.Domain.Team)actor,detail,false,kind);
        else statistics.AddEvent((ChessButWeird.Domain.Team)actor,detail,kind);
        if(kind.HasValue)GetCapturedPieceList(actor).Add((PieceType)kind.Value);
    }
}
