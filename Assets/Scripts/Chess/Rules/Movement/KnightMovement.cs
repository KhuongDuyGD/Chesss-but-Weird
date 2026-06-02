using UnityEngine;

public class KnightMovement : IMovementRule
{
    public bool IsLegalMove(ChessPiece piece, Vector2Int from, Vector2Int to, ChessPiece[,] board)
    {
        Vector2Int delta = to - from;
        int absoluteX = Mathf.Abs(delta.x);
        int absoluteY = Mathf.Abs(delta.y);
        
        bool isLPattern = (absoluteX == 1 && absoluteY == 2) || (absoluteX == 2 && absoluteY == 1);
        if (!isLPattern) return false;

        ChessPiece targetPiece = board[to.x, to.y];
        if (targetPiece != null && targetPiece.team == piece.team)
            return false;

        return true;
    }
}
