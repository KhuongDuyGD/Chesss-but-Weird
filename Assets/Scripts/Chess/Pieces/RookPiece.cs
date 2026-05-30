using UnityEngine;

public class RookPiece : ChessPiece
{
    public override PieceType Type => PieceType.Rook;

    protected override bool IsLegalMovePattern(Vector2Int destination, ChessPiece[,] board)
    {
        Vector2Int delta = destination - BoardPosition;
        return IsStraight(delta) && IsPathClear(destination, board);
    }
}
