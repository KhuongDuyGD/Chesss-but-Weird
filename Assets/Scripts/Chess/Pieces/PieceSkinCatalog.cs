using System;
using System.Collections.Generic;
using UnityEngine;

// Menu metadata only. Prefab references and per-piece tuning live in PieceSkinData.
public sealed class PieceSkinDefinition
{
    public PieceSkinDefinition(string id, string displayName, Color swatchColor)
    {
        Id = string.IsNullOrWhiteSpace(id) ? PieceSkinCatalog.DefaultSkinId : id.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
        SwatchColor = swatchColor;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public Color SwatchColor { get; }
    public bool IsDefault => PieceSkinCatalog.IsDefault(Id);
}

public readonly struct PieceSkinPieceTuning
{
    public static readonly PieceSkinPieceTuning Default = new PieceSkinPieceTuning(Vector3.zero, 1f, 1f);

    public PieceSkinPieceTuning(Vector3 rotationEuler, float heightMultiplier = 1f, float maxFootprintMultiplier = 1f)
    {
        RotationEuler = rotationEuler;
        HeightMultiplier = Mathf.Max(0.01f, heightMultiplier);
        MaxFootprintMultiplier = Mathf.Max(0.01f, maxFootprintMultiplier);
    }

    public Vector3 RotationEuler { get; }
    public float HeightMultiplier { get; }
    public float MaxFootprintMultiplier { get; }
}

public static class PieceSkinCatalog
{
    public const string DefaultSkinId = "default";

    private static readonly List<PieceSkinDefinition> Skins = new List<PieceSkinDefinition>
    {
        new PieceSkinDefinition(DefaultSkinId, "Classic", new Color(.96f, .93f, .84f))
    };

    public static void UseCatalog(CosmeticCatalog catalog)
    {
        Skins.Clear();
        Skins.Add(new PieceSkinDefinition(DefaultSkinId, "Classic", new Color(.96f, .93f, .84f)));
        if (!catalog || catalog.pieceSkins == null) return;
        foreach (var entry in catalog.pieceSkins)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.id) || IsDefault(entry.id)) continue;
            Skins.Add(new PieceSkinDefinition(entry.id, entry.displayName, new Color(.74f, .83f, .95f)));
        }
    }

    public static IReadOnlyList<PieceSkinDefinition> All => Skins;

    public static PieceSkinDefinition Get(string id)
    {
        string normalizedId = NormalizeId(id);
        for (int i = 0; i < Skins.Count; i++)
            if (string.Equals(Skins[i].Id, normalizedId, StringComparison.OrdinalIgnoreCase))
                return Skins[i];

        return Skins[0];
    }

    public static string NormalizeId(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? DefaultSkinId : id.Trim().ToLowerInvariant();
    }

    public static bool IsDefault(string id)
    {
        return string.Equals(NormalizeId(id), DefaultSkinId, StringComparison.OrdinalIgnoreCase);
    }
}

[DisallowMultipleComponent]
public sealed class PieceSkinVisualState : MonoBehaviour
{
    private readonly List<Renderer> originalRenderers = new List<Renderer>();
    private GameObject activeSkinVisual;
    public GameObject ActiveVisual => activeSkinVisual;
    private string activeSkinId;
    private PieceType activePieceType;
    private Bounds originalBounds;
    private bool hasOriginalBounds;

    public void CaptureOriginalRenderers(Transform root)
    {
        if (originalRenderers.Count > 0 || !root)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i])
                originalRenderers.Add(renderers[i]);

        hasOriginalBounds = TryGetRendererBounds(originalRenderers, out originalBounds);
    }

    public void ApplyDefault()
    {
        ClearSkinVisual();
        SetOriginalRenderersVisible(true);
        activeSkinId = PieceSkinCatalog.DefaultSkinId;
    }

    public void ApplyPrefabSkin(GameObject prefab, string skinId, PieceType pieceType, PieceSkinPieceTuning tuning, Transform parent)
    {
        if (!prefab || !parent)
        {
            ApplyDefault();
            return;
        }

        if (activeSkinVisual &&
            string.Equals(activeSkinId, skinId, StringComparison.OrdinalIgnoreCase) &&
            activePieceType == pieceType)
        {
            SetOriginalRenderersVisible(false);
            SetSkinVisualRenderersVisible(true);
            return;
        }

        ClearSkinVisual();
        SetOriginalRenderersVisible(false);

        activeSkinVisual = UnityEngine.Object.Instantiate(prefab, parent, false);
        activeSkinVisual.name = $"{prefab.name} Skin Visual";
        Transform skinTransform = activeSkinVisual.transform;
        skinTransform.localRotation = Quaternion.Euler(tuning.RotationEuler) * skinTransform.localRotation;
        FitSkinVisualToOriginalBounds(skinTransform, tuning);

        activeSkinId = skinId;
        activePieceType = pieceType;
    }

    public void SetRuntimeVisible(bool visible)
    {
        bool hasActiveSkin = activeSkinVisual != null;
        SetOriginalRenderersVisible(visible && !hasActiveSkin);
        SetSkinVisualRenderersVisible(visible && hasActiveSkin);
    }

    private void FitSkinVisualToOriginalBounds(Transform skinTransform, PieceSkinPieceTuning tuning)
    {
        if (!hasOriginalBounds || !skinTransform)
            return;

        Renderer[] skinRenderers = skinTransform.GetComponentsInChildren<Renderer>(true);
        if (!TryGetRendererBounds(skinRenderers, out Bounds skinBounds))
            return;

        float targetHeight = Mathf.Max(0.001f, originalBounds.size.y * tuning.HeightMultiplier);
        float maxFootprint = Mathf.Max(0.001f, Mathf.Max(originalBounds.size.x, originalBounds.size.z) * tuning.MaxFootprintMultiplier);
        float skinHeight = Mathf.Max(0.001f, skinBounds.size.y);
        float skinFootprint = Mathf.Max(0.001f, Mathf.Max(skinBounds.size.x, skinBounds.size.z));
        float scale = targetHeight / skinHeight;
        float scaledFootprint = skinFootprint * scale;
        if (scaledFootprint > maxFootprint)
            scale *= maxFootprint / scaledFootprint;

        skinTransform.localScale *= scale;

        if (!TryGetRendererBounds(skinRenderers, out skinBounds))
            return;

        Vector3 originalBottomCenter = new Vector3(originalBounds.center.x, originalBounds.min.y, originalBounds.center.z);
        Vector3 skinBottomCenter = new Vector3(skinBounds.center.x, skinBounds.min.y, skinBounds.center.z);
        skinTransform.position += originalBottomCenter - skinBottomCenter;
    }

    private static bool TryGetRendererBounds(IReadOnlyList<Renderer> renderers, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer currentRenderer = renderers[i];
            if (!currentRenderer || !currentRenderer.enabled)
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

    private static bool TryGetRendererBounds(Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer currentRenderer = renderers[i];
            if (!currentRenderer || !currentRenderer.enabled)
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

    private void SetOriginalRenderersVisible(bool visible)
    {
        for (int i = 0; i < originalRenderers.Count; i++)
            if (originalRenderers[i])
                originalRenderers[i].enabled = visible;
    }

    private void SetSkinVisualRenderersVisible(bool visible)
    {
        if (!activeSkinVisual)
            return;

        Renderer[] renderers = activeSkinVisual.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i])
                renderers[i].enabled = visible;
    }

    private void ClearSkinVisual()
    {
        if (!activeSkinVisual)
            return;

        UnityEngine.Object.DestroyImmediate(activeSkinVisual);
        activeSkinVisual = null;
    }
}
