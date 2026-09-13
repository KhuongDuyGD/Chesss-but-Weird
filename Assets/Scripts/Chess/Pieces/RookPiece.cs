using UnityEngine;

public class RookPiece : ChessPiece
{
    public override PieceType Type => PieceType.Rook;

    protected override bool IsLegalMovePattern(Vector2Int destination, ChessPiece[,] board)
    {
        return UnityBoardAdapter.IsLegalPattern(this, destination, board);
    }
}
