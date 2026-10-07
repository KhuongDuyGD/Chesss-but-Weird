using UnityEngine;

public partial class ChessGame
{
    internal Camera PieceHoverCamera => GetGameplayCamera();

    internal ChessPiece PickPieceForHover(Ray ray, float maxDistance)
    {
        ChessPiece result = null;
        float nearest = maxDistance;
        foreach (var hit in Physics.RaycastAll(ray, maxDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.distance >= nearest) continue;
            var piece = hit.collider.GetComponentInParent<ChessPiece>();
            if (!piece && chessboard && chessboard.TryGetTileFromObject(hit.collider.gameObject, out var tile))
                piece = pieces[tile.x, tile.y];
            // Only current playable views qualify; decorations and stale models do not.
            if (!piece || !piece.gameObject.activeInHierarchy || !chessboard || !chessboard.IsValidTile(piece.BoardPosition)) continue;
            var square = piece.BoardPosition;
            if (pieces[square.x, square.y] != piece) continue;
            result = piece; nearest = hit.distance;
        }
        var prisoners = GetComponent<CapturedPieceDisplay>();
        if (prisoners && prisoners.TryPickForHover(ray, nearest, out var prisoner)) result = prisoner;
        return result;
    }
}
