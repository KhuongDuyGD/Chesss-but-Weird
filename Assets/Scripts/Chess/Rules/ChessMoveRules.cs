using UnityEngine;

public static class ChessMoveRules
{
    public static bool IsLegalMove(
        PieceType type,
        PieceTeam team,
        Vector2Int from,
        Vector2Int to,
        ChessPiece[,] board)
    {
        if (board == null || !IsInsideBoard(from) || !IsInsideBoard(to) || from == to)
            return false;

        if (board[to.x, to.y])
            return false;

        Vector2Int delta = to - from;
        int absoluteX = Mathf.Abs(delta.x);
        int absoluteY = Mathf.Abs(delta.y);

        switch (type)
        {
            case PieceType.King:
                return absoluteX <= 1 && absoluteY <= 1;

            case PieceType.Queen:
                return IsStraight(delta) || IsDiagonal(absoluteX, absoluteY)
                    ? IsPathClear(from, to, board)
                    : false;

            case PieceType.Rook:
                return IsStraight(delta) && IsPathClear(from, to, board);

            case PieceType.Bishop:
                return IsDiagonal(absoluteX, absoluteY) && IsPathClear(from, to, board);

            case PieceType.Knight:
                return (absoluteX == 1 && absoluteY == 2) || (absoluteX == 2 && absoluteY == 1);

            case PieceType.Pawn:
                return IsLegalPawnMove(team, from, delta, board);

            default:
                return false;
        }
    }

    private static bool IsLegalPawnMove(PieceTeam team, Vector2Int from, Vector2Int delta, ChessPiece[,] board)
    {
        int direction = team == PieceTeam.White ? 1 : -1;
        int startingRank = team == PieceTeam.White ? 1 : 6;

        if (delta.x != 0)
            return false;

        if (delta.y == direction)
            return true;

        if (from.y != startingRank || delta.y != direction * 2)
            return false;

        int middleY = from.y + direction;
        return !board[from.x, middleY];
    }

    private static bool IsPathClear(Vector2Int from, Vector2Int to, ChessPiece[,] board)
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

    private static bool IsStraight(Vector2Int delta)
    {
        return delta.x == 0 || delta.y == 0;
    }

    private static bool IsDiagonal(int absoluteX, int absoluteY)
    {
        return absoluteX == absoluteY;
    }

    private static bool IsInsideBoard(Vector2Int position)
    {
        return position.x >= 0 &&
            position.x < 8 &&
            position.y >= 0 &&
            position.y < 8;
    }
}
