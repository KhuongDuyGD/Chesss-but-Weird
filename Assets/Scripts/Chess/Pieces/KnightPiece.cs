using UnityEngine;

public class KnightPiece : ChessPiece
{
    public override PieceType Type => PieceType.Knight;

    protected override bool IsLegalMovePattern(Vector2Int destination, ChessPiece[,] board)
    {
        Vector2Int delta = destination - BoardPosition;
        int absoluteX = Mathf.Abs(delta.x);
        int absoluteY = Mathf.Abs(delta.y);

        return (absoluteX == 1 && absoluteY == 2) || (absoluteX == 2 && absoluteY == 1);
    }
}
