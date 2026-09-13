using UnityEngine;

internal static class PieceViewGeometry
{
    public static void EnsurePieceCollider(GameObject pieceObject)
    {
        int chessPieceLayer = LayerMask.NameToLayer("ChessPiece");
        if (chessPieceLayer >= 0)
            SetLayerRecursively(pieceObject.transform, chessPieceLayer);

        if (pieceObject.GetComponentInChildren<Collider>())
            return;

        UpdatePieceRootColliderFromVisibleRenderers(pieceObject);
    }

    public static void UpdatePieceRootColliderFromVisibleRenderers(GameObject pieceObject)
    {
        if (!pieceObject)
            return;

        Renderer[] renderers = pieceObject.GetComponentsInChildren<Renderer>(true);
        if (!TryGetVisibleRendererBounds(renderers, out Bounds bounds))
            return;

        BoxCollider collider = pieceObject.GetComponent<BoxCollider>();
        if (!collider)
            collider = pieceObject.AddComponent<BoxCollider>();
        collider.center = pieceObject.transform.InverseTransformPoint(bounds.center);

        Vector3 localSize = pieceObject.transform.InverseTransformVector(bounds.size);
        collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
    }

    public static bool TryGetVisibleRendererBounds(Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer currentRenderer = renderers[i];
            if (!currentRenderer || !currentRenderer.enabled || !currentRenderer.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                bounds = currentRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(currentRenderer.bounds);
            }
        }

        return hasBounds;
    }

    public static void SetLayerRecursively(Transform root, int layer)
    {
        if (!root)
            return;

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }
}
