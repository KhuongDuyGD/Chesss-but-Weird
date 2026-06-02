using UnityEngine;

public class BishopMovement : IMovementRule
{
    public bool IsLegalMove(ChessPiece piece, Vector2Int from, Vector2Int to, ChessPiece[,] board)
    {
        Vector2Int delta = to - from;
        int absoluteX = Mathf.Abs(delta.x);
        int absoluteY = Mathf.Abs(delta.y);
        
        if (absoluteX != absoluteY) return false;

        if (!MovementUtility.IsPathClear(from, to, board)) return false;

        ChessPiece targetPiece = board[to.x, to.y];
        if (targetPiece != null && targetPiece.team == piece.team)
            return false;

        return true;
    }
}
