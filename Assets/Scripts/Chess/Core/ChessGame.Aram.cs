using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class ChessGame
{
    internal ChessPiece[,] AramBoard => pieces;
    internal Camera AramCamera => GetGameplayCamera();
    internal ChessPiece AramKing(PieceTeam team) => FindKing(team);
    internal bool AramInCheck(PieceTeam team) => IsTeamInCheck(team);
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
        int forward = AramKing(team) ? AramKing(team).ForwardDirection : team == PieceTeam.White ? 1 : -1;
        var piece = CreateVisualPieceObject(team,type,square,forward);
        if (!piece) return null;
        piece.MarkMoved();
        pieces[square.x,square.y]=piece;
        MovePieceToTile(piece,square,0f);
        if (drop) { piece.transform.position += Vector3.up * 3f; AnimatePieceToTile(piece,square,0f,.4f,0f); }
        return piece;
    }

    internal void AramRemove(ChessPiece piece, bool notify = true)
    {
        if (!AramIsAlive(piece)) return;
        if (notify) aramCoordinator.Runtime.OnPieceRemoved(piece);
        var p=piece.BoardPosition;
        pieces[p.x,p.y]=null;
        pieceAnimator?.Cancel(piece);
        piece.gameObject.SetActive(false);
        Destroy(piece.gameObject);
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
        ClearSelection(); return true;
    }

    internal bool AramSafeDestination(ChessPiece piece, Vector2Int to)
    {
        return AramIsAlive(piece) && ChessMoveRules.IsInsideBoard(to) && !pieces[to.x,to.y] &&
            DoesMoveKeepTeamKingSafe(piece,piece.BoardPosition,to,piece.Team);
    }

    internal bool AramFinishIfKingMissing()
    {
        if (!FindKing(PieceTeam.White) && !FindKing(PieceTeam.Black)) { FinishDraw("Both Kings were destroyed"); return true; }
        if (!FindKing(PieceTeam.White)) { FinishGame(PieceTeam.Black); return true; }
        if (!FindKing(PieceTeam.Black)) { FinishGame(PieceTeam.White); return true; }
        return gameOver;
    }

    internal void AramCompleteAbilityTurn()
    {
        if (gameOver || !aramMode || serverAuthoritativeMode) return;
        ClearSelection(); lastMoveWasPawnDoubleStep=false;
        currentTurn=currentTurn==PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        aramCoordinator.OnTurnStarted(currentTurn);
        turnSelectionUI?.SetTurn(currentTurn);
        AramRefreshPosition();
    }

    internal void AramRefreshPosition()
    {
        if (AramFinishIfKingMissing()) return;
        if (aramCoordinator.Runtime.HasPendingDecision) return;
        if (IsCheckmate(currentTurn) && !aramCoordinator.Runtime.TryOfferEscape(currentTurn))
            FinishGame(currentTurn==PieceTeam.White ? PieceTeam.Black : PieceTeam.White);
        UpdateCheckWarningForCurrentTurn(); RefreshLocalInteractionState();
    }
}
