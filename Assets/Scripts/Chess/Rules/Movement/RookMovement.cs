using UnityEngine;

public class RookMovement : IMovementRule
{
    public bool IsLegalMove(ChessPiece piece, Vector2Int from, Vector2Int to, ChessPiece[,] board)
    {
        Vector2Int delta = to - from;
        
        if (delta.x != 0 && delta.y != 0) return false;

        if (!MovementUtility.IsPathClear(from, to, board)) return false;

        ChessPiece targetPiece = board[to.x, to.y];
        if (targetPiece != null && targetPiece.team == piece.team)
            return false;

        return true;
    }
}
