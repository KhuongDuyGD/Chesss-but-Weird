using UnityEngine;

/// <summary>
/// Thuần C# static class: phát hiện Chiếu Tướng, Chiếu Bí, Hòa Bế Tắc.
/// Không phụ thuộc MonoBehaviour, có thể gọi từ bất kỳ đâu.
/// </summary>
public static class CheckDetector
{
    private const int BoardSize = 8;

    // ─── API chính ──────────────────────────────────────────────────

    /// <summary>Vua của phe <paramref name="team"/> có đang bị chiếu không?</summary>
    public static bool IsKingInCheck(PieceTeam team, ChessPiece[,] board)
    {
        Vector2Int kingPos = FindKing(team, board);
        if (kingPos.x < 0) return false; // Không tìm thấy Vua (không hợp lệ)

        return IsSquareAttackedByTeam(kingPos, Opponent(team), board);
    }

    /// <summary>Phe <paramref name="team"/> có ít nhất 1 nước đi hợp lệ không (sau khi lọc bỏ nước bị Chiếu)?</summary>
    public static bool HasAnyLegalMove(PieceTeam team, ChessPiece[,] board)
    {
        for (int x = 0; x < BoardSize; x++)
        {
            for (int y = 0; y < BoardSize; y++)
            {
                ChessPiece piece = board[x, y];
                if (piece == null || piece.team != team) continue;

                Vector2Int from = new Vector2Int(x, y);
                for (int tx = 0; tx < BoardSize; tx++)
                {
                    for (int ty = 0; ty < BoardSize; ty++)
                    {
                        Vector2Int to = new Vector2Int(tx, ty);
                        if (from == to) continue;
                        if (piece.MovementRule == null) continue;
                        if (!piece.MovementRule.IsLegalMove(piece, from, to, board)) continue;

                        // Mô phỏng nước đi trên bản sao bàn cờ
                        ChessPiece[,] simBoard = SimulateMove(board, from, to);
                        if (!IsKingInCheck(team, simBoard))
                            return true; // Tìm được ít nhất 1 nước hợp lệ
                    }
                }
            }
        }
        return false;
    }

    /// <summary>Chiếu Bí: Vua bị chiếu VÀ không còn nước đi nào.</summary>
    public static bool IsCheckmate(PieceTeam team, ChessPiece[,] board)
    {
        return IsKingInCheck(team, board) && !HasAnyLegalMove(team, board);
    }

    /// <summary>Hòa Bế Tắc: Vua KHÔNG bị chiếu nhưng cũng không còn nước đi nào.</summary>
    public static bool IsStalemate(PieceTeam team, ChessPiece[,] board)
    {
        return !IsKingInCheck(team, board) && !HasAnyLegalMove(team, board);
    }

    // ─── Helper: Kiểm tra sau khi di chuyển, Vua có bị chiếu không ─

    /// <summary>
    /// Sau khi di chuyển <paramref name="from"/> -> <paramref name="to"/>, Vua phe <paramref name="team"/> có bị chiếu không?
    /// Hoạt động trên bản sao bàn cờ, không ảnh hưởng bàn cờ thật.
    /// </summary>
    public static bool WouldLeaveKingInCheck(PieceTeam team, Vector2Int from, Vector2Int to, ChessPiece[,] board)
    {
        ChessPiece[,] simBoard = SimulateMove(board, from, to);
        return IsKingInCheck(team, simBoard);
    }

    // ─── Internal helpers ────────────────────────────────────────────

    private static Vector2Int FindKing(PieceTeam team, ChessPiece[,] board)
    {
        for (int x = 0; x < BoardSize; x++)
            for (int y = 0; y < BoardSize; y++)
            {
                ChessPiece p = board[x, y];
                if (p != null && p.team == team && p.type == PieceType.King)
                    return new Vector2Int(x, y);
            }
        return new Vector2Int(-1, -1); // Không tìm thấy
    }

    private static bool IsSquareAttackedByTeam(Vector2Int square, PieceTeam attackingTeam, ChessPiece[,] board)
    {
        for (int x = 0; x < BoardSize; x++)
        {
            for (int y = 0; y < BoardSize; y++)
            {
                ChessPiece attacker = board[x, y];
                if (attacker == null || attacker.team != attackingTeam) continue;
                if (attacker.MovementRule == null) continue;

                Vector2Int from = new Vector2Int(x, y);
                if (attacker.MovementRule.IsLegalMove(attacker, from, square, board))
                    return true;
            }
        }
        return false;
    }

    /// <summary>Tạo bản sao bàn cờ sau khi thực hiện nước đi (không thay đổi bàn cờ thật).</summary>
    private static ChessPiece[,] SimulateMove(ChessPiece[,] board, Vector2Int from, Vector2Int to)
    {
        ChessPiece[,] copy = (ChessPiece[,])board.Clone();
        copy[to.x, to.y] = copy[from.x, from.y];
        copy[from.x, from.y] = null;
        return copy;
    }

    private static PieceTeam Opponent(PieceTeam team)
    {
        return team == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
    }
}
