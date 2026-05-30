using UnityEngine;

public class PawnPiece : ChessPiece
{
    public override PieceType Type => PieceType.Pawn;

    protected override bool IsLegalMovePattern(Vector2Int destination, ChessPiece[,] board)
    {
        Vector2Int delta = destination - BoardPosition;
        int direction = ForwardDirection;
        int startingRank = direction > 0 ? 1 : 6;

        if (Mathf.Abs(delta.x) == 1 && delta.y == direction)
            return HasEnemyAt(destination, board);

        if (delta.x != 0)
            return false;

        if (delta.y == direction)
            return IsEmptyAt(destination, board);

        if (HasMoved || BoardPosition.y != startingRank || delta.y != direction * 2)
            return false;

        int middleY = BoardPosition.y + direction;
        return board[BoardPosition.x, middleY] == null && IsEmptyAt(destination, board);
    }
}
