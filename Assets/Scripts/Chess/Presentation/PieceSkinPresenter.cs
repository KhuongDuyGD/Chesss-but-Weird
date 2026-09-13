using UnityEngine;

internal static class PieceSkinPresenter
{
    public static void Apply(ChessPiece piece, string skinId)
    {
        if (!piece)
            return;

        var game = piece.GetComponentInParent<ChessGame>();
        var manager = game ? game.GetComponent<LoadingManager>()?.Skins : null;
        PieceSkinVisualState skinState = piece.GetComponent<PieceSkinVisualState>();
        if (!skinState)
            skinState = piece.gameObject.AddComponent<PieceSkinVisualState>();
        skinState.CaptureOriginalRenderers(piece.transform);

        if (manager == null || !manager.TryGetPrefab(piece.Team, piece.Type, out GameObject prefab))
        {
            skinState.ApplyDefault();
            PieceViewGeometry.UpdatePieceRootColliderFromVisibleRenderers(piece.gameObject);
            return;
        }

        var data = manager.GetData(piece.Team);
        skinState.ApplyPrefabSkin(prefab, data ? data.skinId : skinId, piece.Type,
            data ? data.GetTuning(piece.Type) : PieceSkinPieceTuning.Default, piece.transform);
        manager.ApplyColor(skinState.ActiveVisual, piece.Team);
        PieceViewGeometry.UpdatePieceRootColliderFromVisibleRenderers(piece.gameObject);
    }

}
