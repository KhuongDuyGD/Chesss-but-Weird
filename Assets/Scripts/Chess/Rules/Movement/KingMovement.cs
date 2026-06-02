using UnityEngine;

public class KingMovement : IMovementRule
{
    public bool IsLegalMove(ChessPiece piece, Vector2Int from, Vector2Int to, ChessPiece[,] board)
    {
        Vector2Int delta = to - from;
        int absoluteX = Mathf.Abs(delta.x);
        int absoluteY = Mathf.Abs(delta.y);
        
        if (absoluteX > 1 || absoluteY > 1) return false;  // chỉ được đi TỐI ĐA 1 ô mỗi hướng

        ChessPiece targetPiece = board[to.x, to.y];
        if (targetPiece != null && targetPiece.team == piece.team)
            return false;

        return true;
    }
}
