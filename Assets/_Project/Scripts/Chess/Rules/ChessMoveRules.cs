using UnityEngine;

public static class ChessMoveRules
{
    public static bool IsLegalMove(
        ChessPiece piece,
        Vector2Int from,
        Vector2Int to,
        ChessPiece[,] board)
    {
        if (!piece || piece.BoardPosition != from)
            return false;

        return piece.IsLegalMove(to, board);
    }

    public static bool IsInsideBoard(Vector2Int position)
    {
        return position.x >= 0 &&
            position.x < 8 &&
            position.y >= 0 &&
            position.y < 8;
    }
}
