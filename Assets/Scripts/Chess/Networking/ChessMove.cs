using System;
using UnityEngine;

[Serializable]
public struct ChessMove
{
    public Vector2Int from;
    public Vector2Int to;
    public bool hasPromotion;
    public PieceType promotionType;

    public ChessMove(Vector2Int from, Vector2Int to)
    {
        this.from = from;
        this.to = to;
        hasPromotion = false;
        promotionType = PieceType.Queen;
    }

    public ChessMove(Vector2Int from, Vector2Int to, PieceType promotionType)
    {
        this.from = from;
        this.to = to;
        hasPromotion = true;
        this.promotionType = promotionType;
    }

}
