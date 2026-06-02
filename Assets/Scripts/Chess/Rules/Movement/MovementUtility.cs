using UnityEngine;

public static class MovementUtility
{
    public static bool IsPathClear(Vector2Int from, Vector2Int to, ChessPiece[,] board)
    {
        Vector2Int step = new Vector2Int(
            Mathf.Clamp(to.x - from.x, -1, 1),
            Mathf.Clamp(to.y - from.y, -1, 1));

        Vector2Int current = from + step;
        while (current != to)
        {
            if (board[current.x, current.y])
                return false;

            current += step;
        }

        return true;
    }
}
