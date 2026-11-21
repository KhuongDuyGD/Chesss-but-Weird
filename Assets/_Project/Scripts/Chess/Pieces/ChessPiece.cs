using System.Collections.Generic;
using UnityEngine;

public enum PieceType
{
    King,
    Queen,
    Rook,
    Bishop,
    Knight,
    Pawn
}

public enum PieceTeam
{
    White,
    Black
}

[DisallowMultipleComponent]
public abstract class ChessPiece : MonoBehaviour
{
    [SerializeField] private PieceTeam team;
    [SerializeField] private Vector2Int boardPosition;
    [SerializeField] private bool hasMoved;
    [SerializeField] private int forwardDirection = 1;

    public abstract PieceType Type { get; }
    public PieceTeam Team => team;
    public Vector2Int BoardPosition => boardPosition;
    public bool HasMoved => hasMoved;
    public int ForwardDirection => forwardDirection;
    public event System.Action<ChessPiece> Destroyed;

    public void Initialize(PieceTeam initialTeam, Vector2Int initialBoardPosition, int initialForwardDirection = 1)
    {
        team = initialTeam;
        boardPosition = initialBoardPosition;
        forwardDirection = Mathf.Clamp(initialForwardDirection, -1, 1);
        if (forwardDirection == 0)
            forwardDirection = 1;

        hasMoved = false;
    }

    public void SetBoardPosition(Vector2Int newBoardPosition)
    {
        boardPosition = newBoardPosition;
    }

    public void SetStartingBoardPosition(Vector2Int newBoardPosition, int newForwardDirection)
    {
        boardPosition = newBoardPosition;
        forwardDirection = Mathf.Clamp(newForwardDirection, -1, 1);
        if (forwardDirection == 0)
            forwardDirection = 1;

        hasMoved = false;
    }

    public void MarkMoved()
    {
        hasMoved = true;
    }

    public bool IsLegalMove(Vector2Int destination, ChessPiece[,] board)
    {
        if (!CanUseDestination(destination, board))
            return false;

        return IsLegalMovePattern(destination, board);
    }

    public IReadOnlyList<Vector2Int> GetLegalMoves(ChessPiece[,] board)
    {
        List<Vector2Int> legalMoves = new List<Vector2Int>();
        CollectLegalMoves(board, legalMoves);
        return legalMoves;
    }

    public void CollectLegalMoves(ChessPiece[,] board, List<Vector2Int> legalMoves)
    {
        if (legalMoves == null)
            return;

        legalMoves.Clear();
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                Vector2Int destination = new Vector2Int(x, y);
                if (IsLegalMove(destination, board))
                    legalMoves.Add(destination);
            }
    }

    protected abstract bool IsLegalMovePattern(Vector2Int destination, ChessPiece[,] board);

    protected bool IsPathClear(Vector2Int destination, ChessPiece[,] board)
    {
        Vector2Int step = new Vector2Int(
            Mathf.Clamp(destination.x - boardPosition.x, -1, 1),
            Mathf.Clamp(destination.y - boardPosition.y, -1, 1));

        Vector2Int current = boardPosition + step;
        while (current != destination)
        {
            if (board[current.x, current.y])
                return false;

            current += step;
        }

        return true;
    }

    protected bool IsStraight(Vector2Int delta)
    {
        return delta.x == 0 || delta.y == 0;
    }

    protected bool IsDiagonal(Vector2Int delta)
    {
        return Mathf.Abs(delta.x) == Mathf.Abs(delta.y);
    }

    protected bool IsEmptyAt(Vector2Int position, ChessPiece[,] board)
    {
        return ChessMoveRules.IsInsideBoard(position) && board[position.x, position.y] == null;
    }

    protected bool HasEnemyAt(Vector2Int position, ChessPiece[,] board)
    {
        if (!ChessMoveRules.IsInsideBoard(position))
            return false;

        ChessPiece targetPiece = board[position.x, position.y];
        return targetPiece && targetPiece.Team != team;
    }

    private bool CanUseDestination(Vector2Int destination, ChessPiece[,] board)
    {
        if (board == null || !ChessMoveRules.IsInsideBoard(boardPosition) ||
            !ChessMoveRules.IsInsideBoard(destination) || boardPosition == destination)
            return false;

        ChessPiece targetPiece = board[destination.x, destination.y];
        return !targetPiece || targetPiece.Team != team;
    }

    private void OnDestroy()
    {
        Destroyed?.Invoke(this);
    }
}
