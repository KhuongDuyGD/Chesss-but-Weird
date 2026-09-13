using ChessButWeird.Domain;
using UnityEngine;

/// <summary>Read-only bridge during migration. A struct avoids allocations per candidate move.</summary>
internal readonly struct UnityBoardAdapter : IReadOnlyBoard
{
    private readonly ChessPiece[,] board;
    public UnityBoardAdapter(ChessPiece[,] board) { this.board = board; }
    public PieceState GetPiece(Square square)
    {
        if (board == null || !square.IsValid) return default;
        return ToState(board[square.File, square.Rank]);
    }
    public static Square ToSquare(Vector2Int square) => new Square(square.x, square.y);
    public static PieceState ToState(ChessPiece piece)
    {
        if (!piece) return default;
        // Transitional board-square identity. A future MatchSession will supply persistent IDs.
        int id = piece.BoardPosition.y * 8 + piece.BoardPosition.x + 1;
        return new PieceState(id, (PieceKind)piece.Type, (Team)piece.Team, piece.HasMoved, piece.ForwardDirection);
    }
    public static bool IsLegalPattern(ChessPiece piece, Vector2Int to, ChessPiece[,] board)
    {
        return MovementRules.IsLegalPattern(new UnityBoardAdapter(board), ToState(piece),
            ToSquare(piece.BoardPosition), ToSquare(to));
    }
}
