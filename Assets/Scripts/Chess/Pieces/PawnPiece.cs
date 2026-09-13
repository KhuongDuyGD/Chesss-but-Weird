using UnityEngine;

public class PawnPiece : ChessPiece
{
    public override PieceType Type => PieceType.Pawn;

    protected override bool IsLegalMovePattern(Vector2Int destination, ChessPiece[,] board)
    {
        return UnityBoardAdapter.IsLegalPattern(this, destination, board);
    }
}
