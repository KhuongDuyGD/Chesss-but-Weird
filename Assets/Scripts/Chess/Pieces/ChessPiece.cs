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
public class ChessPiece : MonoBehaviour
{
    public PieceType type;
    public PieceTeam team;
    public Vector2Int boardPosition;
    public bool hasMoved;

    public IMovementRule MovementRule { get; private set; }

    private void Awake()
    {
        SetMovementRule(type);
    }

    public void SetMovementRule(PieceType newType)
    {
        type = newType;
        switch (type)
        {
            case PieceType.Pawn: MovementRule = new PawnMovement(); break;
            case PieceType.Knight: MovementRule = new KnightMovement(); break;
            case PieceType.Bishop: MovementRule = new BishopMovement(); break;
            case PieceType.Rook: MovementRule = new RookMovement(); break;
            case PieceType.Queen: MovementRule = new QueenMovement(); break;
            case PieceType.King: MovementRule = new KingMovement(); break;
        }
    }

    public void SetMovementRule(IMovementRule customRule)
    {
        MovementRule = customRule;
    }
}
