using UnityEngine;

public class QueenMovement : IMovementRule
{
    public bool IsLegalMove(ChessPiece piece, Vector2Int from, Vector2Int to, ChessPiece[,] board)
    {
        Vector2Int delta = to - from;
        int absoluteX = Mathf.Abs(delta.x);
        int absoluteY = Mathf.Abs(delta.y);
        
        bool isStraight = delta.x == 0 || delta.y == 0;
        bool isDiagonal = absoluteX == absoluteY;
        
        if (!isStraight && !isDiagonal) return false;

        if (!MovementUtility.IsPathClear(from, to, board)) return false;

        ChessPiece targetPiece = board[to.x, to.y];
        if (targetPiece != null && targetPiece.team == piece.team)
            return false;

        return true;
    }
}
