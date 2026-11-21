using UnityEngine;

public class QueenPiece : ChessPiece
{
    public override PieceType Type => PieceType.Queen;

    protected override bool IsLegalMovePattern(Vector2Int destination, ChessPiece[,] board)
    {
        return UnityBoardAdapter.IsLegalPattern(this, destination, board);
    }
}
