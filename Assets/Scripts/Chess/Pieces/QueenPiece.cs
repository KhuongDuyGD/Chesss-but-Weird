using UnityEngine;

public class QueenPiece : ChessPiece
{
    public override PieceType Type => PieceType.Queen;

    protected override bool IsLegalMovePattern(Vector2Int destination, ChessPiece[,] board)
    {
        Vector2Int delta = destination - BoardPosition;
        return (IsStraight(delta) || IsDiagonal(delta)) && IsPathClear(destination, board);
    }
}
