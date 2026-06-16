using System;
using System.Globalization;
using UnityEngine;

[Serializable]
public struct ChessLanMove
{
    public Vector2Int from;
    public Vector2Int to;
    public bool hasPromotion;
    public PieceType promotionType;

    public ChessLanMove(Vector2Int from, Vector2Int to)
    {
        this.from = from;
        this.to = to;
        hasPromotion = false;
        promotionType = PieceType.Queen;
    }

    public ChessLanMove(Vector2Int from, Vector2Int to, PieceType promotionType)
    {
        this.from = from;
        this.to = to;
        hasPromotion = true;
        this.promotionType = promotionType;
    }

    public string ToProtocolPayload()
    {
        int promotionValue = hasPromotion ? (int)promotionType : -1;
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2},{3},{4}",
            from.x,
            from.y,
            to.x,
            to.y,
            promotionValue);
    }

    public static bool TryParse(string payload, out ChessLanMove move)
    {
        move = default;
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        string[] tokens = payload.Split(',');
        if (tokens.Length != 5)
            return false;

        if (!int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int fromX) ||
            !int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int fromY) ||
            !int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int toX) ||
            !int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int toY) ||
            !int.TryParse(tokens[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int promotionValue))
            return false;

        Vector2Int from = new Vector2Int(fromX, fromY);
        Vector2Int to = new Vector2Int(toX, toY);
        if (promotionValue < 0)
        {
            move = new ChessLanMove(from, to);
            return true;
        }

        if (!Enum.IsDefined(typeof(PieceType), promotionValue))
            return false;

        move = new ChessLanMove(from, to, (PieceType)promotionValue);
        return true;
    }
}
