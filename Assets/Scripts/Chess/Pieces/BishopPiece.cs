using UnityEngine;

public class BishopPiece : ChessPiece
{
    public override PieceType Type => PieceType.Bishop;

    protected override bool IsLegalMovePattern(Vector2Int destination, ChessPiece[,] board)
    {
        Vector2Int delta = destination - BoardPosition;
        return IsDiagonal(delta) && IsPathClear(destination, board);
    }
}
