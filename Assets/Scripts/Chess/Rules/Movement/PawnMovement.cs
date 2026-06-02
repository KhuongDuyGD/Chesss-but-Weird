using UnityEngine;

public class PawnMovement : IMovementRule
{
    public bool IsLegalMove(ChessPiece piece, Vector2Int from, Vector2Int to, ChessPiece[,] board)
    {
        Vector2Int delta = to - from;
        int direction = piece.team == PieceTeam.White ? 1 : -1;  // Trắng đi lên (+1), Đen đi xuống (-1)
        int startingRank = piece.team == PieceTeam.White ? 1 : 6; // hàng xuất phát
        ChessPiece targetPiece = board[to.x, to.y];

        // TRƯỜNG HỢP 1: Đi THẲNG (delta.x == 0)
        if (delta.x == 0)
        {
            if (delta.y == direction)              // đi 1 bước về phía trước
                return targetPiece == null;        // CHỈ đi được nếu ô TRỐNG (không ăn thẳng được)

            if (from.y == startingRank && delta.y == direction * 2) // đi 2 bước (chỉ nước đầu)
            {
                int middleY = from.y + direction;
                return targetPiece == null && board[from.x, middleY] == null; // cả 2 ô phải trống
            }
        }
        // TRƯỜNG HỢP 2: Đi CHÉO (ăn quân địch)
        else if (Mathf.Abs(delta.x) == 1 && delta.y == direction)
        {
            // Chỉ đi chéo được nếu có quân ĐỊCH ở ô chéo đó
            if (targetPiece != null && targetPiece.team != piece.team)
                return true;
        }

        return false;
    }
}
