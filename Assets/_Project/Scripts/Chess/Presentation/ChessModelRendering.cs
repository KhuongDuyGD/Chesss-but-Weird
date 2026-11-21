using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

internal static class ChessModelRendering
{
    private static readonly HashSet<Renderer> models = new HashSet<Renderer>();

    internal static void ApplySettings(GameObject root)
    {
        if (!root) return;
        models.RemoveWhere(renderer => !renderer);
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            models.Add(renderer);
            Apply(renderer, GameRuntimeSettings.ShadowsEnabled);
        }
    }
    internal static void RefreshShadows(bool enabled)
    {
        models.RemoveWhere(renderer => !renderer);
        foreach (var renderer in models) Apply(renderer, enabled);
    }
    private static void Apply(Renderer renderer, bool enabled)
    {
        renderer.shadowCastingMode = enabled ? ShadowCastingMode.On : ShadowCastingMode.Off;
        renderer.receiveShadows = enabled;
    }
}
