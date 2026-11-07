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
            ChessModelRendering.ApplySettings(piece.gameObject);
            PieceViewGeometry.UpdatePieceRootColliderFromVisibleRenderers(piece.gameObject);
            return;
        }

        var data = manager.GetData(piece.Team, piece.Type);
        float boardScale = 0f;
        if (data && data.useAuthoredBoardScale && game.Board)
        {
            float width = Vector3.Distance(game.Board.GetTileCenterWorld(Vector2Int.zero), game.Board.GetTileCenterWorld(Vector2Int.right));
            float depth = Vector3.Distance(game.Board.GetTileCenterWorld(Vector2Int.zero), game.Board.GetTileCenterWorld(Vector2Int.up));
            boardScale = Mathf.Sqrt(width * depth);
        }
        skinState.ApplyPrefabSkin(prefab, data ? data.skinId : skinId, piece.Type,
            data ? data.GetTuning(piece.Type) : PieceSkinPieceTuning.Default, piece.transform, boardScale);
        if (data && data.faceOpponent && game.Board && skinState.ActiveVisual)
        {
            Vector3 forward = game.Board.GetTileCenterWorld(Vector2Int.up) - game.Board.GetTileCenterWorld(Vector2Int.zero);
            forward *= piece.ForwardDirection;
            skinState.ActiveVisual.transform.rotation = Quaternion.LookRotation(forward, game.Board.transform.up);
        }
        manager.ApplyColor(skinState.ActiveVisual, piece.Team, piece.Type);
        ChessModelRendering.ApplySettings(piece.gameObject);
        PieceViewGeometry.UpdatePieceRootColliderFromVisibleRenderers(piece.gameObject);
    }

}
