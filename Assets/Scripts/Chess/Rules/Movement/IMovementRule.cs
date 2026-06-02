using UnityEngine;

public interface IMovementRule
{
    bool IsLegalMove(ChessPiece piece, Vector2Int from, Vector2Int to, ChessPiece[,] board);
}
