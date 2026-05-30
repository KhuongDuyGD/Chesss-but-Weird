using UnityEngine;

public class KingPiece : ChessPiece
{
    public override PieceType Type => PieceType.King;

    protected override bool IsLegalMovePattern(Vector2Int destination, ChessPiece[,] board)
    {
        Vector2Int delta = destination - BoardPosition;
        return Mathf.Abs(delta.x) <= 1 && Mathf.Abs(delta.y) <= 1;
    }
}
