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
}
