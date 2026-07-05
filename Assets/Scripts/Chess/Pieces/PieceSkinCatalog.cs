using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PieceSkinDefinition
{
    private readonly Dictionary<PieceType, string> prefabNames;
    private readonly Dictionary<PieceType, PieceSkinPieceTuning> pieceTunings;

    public PieceSkinDefinition(
        string id,
        string displayName,
        Color swatchColor,
        string prefabFolder = null,
        string prefabPrefix = null,
        IReadOnlyDictionary<PieceType, string> prefabNameOverrides = null,
        IReadOnlyDictionary<PieceType, PieceSkinPieceTuning> pieceTuningOverrides = null)
    {
        Id = string.IsNullOrWhiteSpace(id) ? PieceSkinCatalog.DefaultSkinId : id.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
        SwatchColor = swatchColor;
        PrefabFolder = string.IsNullOrWhiteSpace(prefabFolder) ? null : prefabFolder.Trim().Replace('\\', '/');
        PrefabPrefix = string.IsNullOrWhiteSpace(prefabPrefix) ? Id : prefabPrefix.Trim();
        prefabNames = new Dictionary<PieceType, string>();
        pieceTunings = new Dictionary<PieceType, PieceSkinPieceTuning>();

        if (prefabNameOverrides != null)
        {
            foreach (KeyValuePair<PieceType, string> entry in prefabNameOverrides)
                if (!string.IsNullOrWhiteSpace(entry.Value))
                    prefabNames[entry.Key] = entry.Value.Trim();
        }

        if (pieceTuningOverrides == null)
            return;

        foreach (KeyValuePair<PieceType, PieceSkinPieceTuning> entry in pieceTuningOverrides)
            pieceTunings[entry.Key] = entry.Value;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public Color SwatchColor { get; }
    public string PrefabFolder { get; }
    public string PrefabPrefix { get; }
    public bool IsDefault => string.Equals(Id, PieceSkinCatalog.DefaultSkinId, StringComparison.OrdinalIgnoreCase);

    public string GetPrefabAssetPath(PieceType pieceType)
    {
        if (IsDefault || string.IsNullOrWhiteSpace(PrefabFolder))
            return null;

        return $"{PrefabFolder}/{GetPrefabName(pieceType)}.prefab";
    }

    public string GetResourcePath(PieceType pieceType)
    {
        if (IsDefault || string.IsNullOrWhiteSpace(PrefabFolder))
            return null;

        const string resourcesMarker = "/Resources/";
        int resourcesIndex = PrefabFolder.IndexOf(resourcesMarker, StringComparison.OrdinalIgnoreCase);
        if (resourcesIndex < 0)
            return null;

        string resourceFolder = PrefabFolder.Substring(resourcesIndex + resourcesMarker.Length);
        return $"{resourceFolder}/{GetPrefabName(pieceType)}";
    }

    public PieceSkinPieceTuning GetTuning(PieceType pieceType)
    {
        return pieceTunings.TryGetValue(pieceType, out PieceSkinPieceTuning tuning)
            ? tuning
            : PieceSkinPieceTuning.Default;
    }

    private string GetPrefabName(PieceType pieceType)
    {
        if (prefabNames.TryGetValue(pieceType, out string overriddenName))
            return overriddenName;

        return $"{PrefabPrefix}_{pieceType}";
    }
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

    private static readonly PieceSkinDefinition[] Skins =
    {
        new PieceSkinDefinition(DefaultSkinId, "Default Skin", new Color(0.96f, 0.93f, 0.84f, 1f)),
        new PieceSkinDefinition(
            "dc",
            "DC",
            new Color(0.62f, 0.82f, 1f, 1f),
            "Assets/Skins/DC/Prefabs",
            "DC",
            new Dictionary<PieceType, string>
            {
                [PieceType.Bishop] = "DC_Bishop",
                [PieceType.King] = "DC_KIng",
                [PieceType.Knight] = "DC_Knight",
                [PieceType.Pawn] = "DC_Pawn",
                [PieceType.Queen] = "DC_Queen",
                [PieceType.Rook] = "DC_Rook"
            },
            new Dictionary<PieceType, PieceSkinPieceTuning>
            {
                [PieceType.Knight] = new PieceSkinPieceTuning(new Vector3(0f, -90f, 0f), 1f, 1f),
                [PieceType.Pawn] = new PieceSkinPieceTuning(Vector3.zero, 1.08f, 1f),
                [PieceType.Rook] = new PieceSkinPieceTuning(Vector3.zero, 1.15f, 1f),
                [PieceType.Bishop] = new PieceSkinPieceTuning(Vector3.zero, 1f, 1f),
                [PieceType.Queen] = new PieceSkinPieceTuning(Vector3.zero, 0.96f, 1f),
                [PieceType.King] = new PieceSkinPieceTuning(Vector3.zero, 0.96f, 1f)
            }),
        new PieceSkinDefinition(
            "corn",
            "Corn",
            new Color(0.96f, 0.74f, 0.22f, 1f),
            "Assets/Skins/Corn/Prefabs",
            "Corn",
            new Dictionary<PieceType, string>
            {
                [PieceType.Bishop] = "Corn_Bishop",
                [PieceType.King] = "Corn_King",
                [PieceType.Knight] = "Corn_Knight",
                [PieceType.Pawn] = "Corn_Pawn",
                [PieceType.Queen] = "Corn_Queen",
                [PieceType.Rook] = "Corn_Rook"
            },
            new Dictionary<PieceType, PieceSkinPieceTuning>
            {
                [PieceType.Knight] = new PieceSkinPieceTuning(new Vector3(0f, -90f, 0f), 1f, 1f),
                [PieceType.Pawn] = new PieceSkinPieceTuning(Vector3.zero, 1f, 1f),
                [PieceType.Rook] = new PieceSkinPieceTuning(Vector3.zero, 1f, 1f),
                [PieceType.Bishop] = new PieceSkinPieceTuning(Vector3.zero, 1f, 1f),
                [PieceType.Queen] = new PieceSkinPieceTuning(Vector3.zero, 1f, 1f),
                [PieceType.King] = new PieceSkinPieceTuning(Vector3.zero, 1f, 1f)
            })
    };

    public static IReadOnlyList<PieceSkinDefinition> All => Skins;

    public static PieceSkinDefinition Get(string id)
    {
        string normalizedId = NormalizeId(id);
        for (int i = 0; i < Skins.Length; i++)
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
        bool hasActiveSkin = activeSkinVisual != null && !PieceSkinCatalog.IsDefault(activeSkinId);
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
