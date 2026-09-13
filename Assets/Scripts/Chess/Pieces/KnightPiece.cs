using UnityEngine;

public class KnightPiece : ChessPiece
{
    public override PieceType Type => PieceType.Knight;

    protected override bool IsLegalMovePattern(Vector2Int destination, ChessPiece[,] board)
    {
        return UnityBoardAdapter.IsLegalPattern(this, destination, board);
    }
}
