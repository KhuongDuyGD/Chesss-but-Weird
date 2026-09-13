using UnityEngine;

public class KingPiece : ChessPiece
{
    public override PieceType Type => PieceType.King;

    protected override bool IsLegalMovePattern(Vector2Int destination, ChessPiece[,] board)
    {
        return UnityBoardAdapter.IsLegalPattern(this, destination, board);
    }
}
