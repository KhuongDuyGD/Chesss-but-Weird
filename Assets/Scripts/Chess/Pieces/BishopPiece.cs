using UnityEngine;

public class BishopPiece : ChessPiece
{
    public override PieceType Type => PieceType.Bishop;

    protected override bool IsLegalMovePattern(Vector2Int destination, ChessPiece[,] board)
    {
        return UnityBoardAdapter.IsLegalPattern(this, destination, board);
    }
}
