using ChessButWeird.Domain;

internal readonly struct UnityBuffContextAdapter : IBuffContextProvider
{
    private readonly ChessPiece[,] board;
    private readonly AramBuffRuntime runtime;
    public UnityBuffContextAdapter(ChessPiece[,] board, AramBuffRuntime runtime) { this.board = board; this.runtime = runtime; }
    public AramPieceContext GetContext(PieceState piece)
    {
        if (!runtime || !runtime.IsActive || piece.IsEmpty || piece.Id > 64) return default;
        int index = piece.Id - 1;
        // Transitional IDs identify the source square, even in a hypothetical move view.
        ChessPiece original = board[index % 8, index / 8];
        return original ? runtime.CreateDomainContext(original) : default;
    }
}
