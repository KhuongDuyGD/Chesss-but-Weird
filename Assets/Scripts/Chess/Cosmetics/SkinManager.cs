using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class SkinManager
{
    private sealed class LoadedSkin
    {
        public PieceSkinData data;
        public readonly Dictionary<PieceType, GameObject> prefabs = new Dictionary<PieceType, GameObject>();
    }
    private readonly AssetLoader assets;
    private readonly Dictionary<string, LoadedSkin> loaded = new Dictionary<string, LoadedSkin>(StringComparer.OrdinalIgnoreCase);
    private LoadedSkin white;
    private LoadedSkin black;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    public SkinManager(AssetLoader assets) { this.assets = assets ?? throw new ArgumentNullException(nameof(assets)); }
    public PieceSkinData GetData(PieceTeam team) => (team == PieceTeam.White ? white : black)?.data;

    public IEnumerator Load(CosmeticCatalog catalog, string whiteSkinId, string blackSkinId, Action<float> progress = null)
    {
        float highest = 0;
        Action<float> report = value => { highest = Mathf.Max(highest, value); progress?.Invoke(highest); };
        yield return LoadSide(catalog, whiteSkinId, result => white = result, p => report(p * 0.5f));
        yield return LoadSide(catalog, blackSkinId, result => black = result, p => report(0.5f + p * 0.5f));
        report(1);
    }

    private IEnumerator LoadSide(CosmeticCatalog catalog, string id, Action<LoadedSkin> completed, Action<float> progress)
    {
        string fallback = catalog ? catalog.defaultPieceSkinId : "default";
        LoadedSkin result = null;
        yield return LoadSkin(catalog, id, value => result = value, progress);
        if (result == null && !string.Equals(id, fallback, StringComparison.OrdinalIgnoreCase))
            yield return LoadSkin(catalog, fallback, value => result = value, null);
        completed(result); // null deliberately selects the existing procedural default visual.
        progress?.Invoke(1);
    }

    private IEnumerator LoadSkin(CosmeticCatalog catalog, string id, Action<LoadedSkin> completed, Action<float> progress)
    {
        id = id?.Trim() ?? "";
        if (loaded.TryGetValue(id, out LoadedSkin cached)) { completed(cached); progress?.Invoke(1); yield break; }
        CosmeticCatalogEntry entry = catalog ? catalog.FindPiece(id) : null;
        if (entry == null) { loaded[id] = null; completed(null); yield break; }
        PieceSkinData data = null;
        yield return assets.Load<PieceSkinData>(entry.data, value => data = value, p => progress?.Invoke(p / 7));
        if (!data) { loaded[id] = null; completed(null); yield break; }
        var skin = new LoadedSkin { data = data };
        int index = 0;
        foreach (PieceType type in Enum.GetValues(typeof(PieceType)))
        {
            GameObject prefab = null;
            int stage = ++index;
            yield return assets.Load<GameObject>(data.GetPrefab(type), value => prefab = value, p => progress?.Invoke((stage + p) / 7f));
            if (!prefab)
            {
                Debug.LogWarning("[Cosmetics] Incomplete piece set '" + id + "'. Entire side uses the default set.");
                loaded[id] = null;
                completed(null);
                yield break;
            }
            skin.prefabs[type] = prefab;
        }
        loaded[id] = skin;
        completed(skin);
        progress?.Invoke(1);
    }

    public bool TryGetPrefab(PieceTeam team, PieceType type, out GameObject prefab)
    {
        prefab = null;
        LoadedSkin skin = team == PieceTeam.White ? white : black;
        return !assets.IsDisposed && skin != null && skin.prefabs.TryGetValue(type, out prefab) && prefab;
    }

    public GameObject InstantiatePiece(PieceTeam team, PieceType type, Transform parent)
    {
        if (!TryGetPrefab(team, type, out GameObject prefab)) return null;
        GameObject instance = UnityEngine.Object.Instantiate(prefab, parent, false);
        ApplyColor(instance, team);
        return instance;
    }

    public void ApplyColor(GameObject instance, PieceTeam team)
    {
        PieceSkinData data = GetData(team);
        if (!instance || !data || !data.applyTeamColor) return;
        Color color = team == PieceTeam.White ? data.whiteColor : data.blackColor;
        var block = new MaterialPropertyBlock();
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            block.SetColor(ColorProperty, color);
            renderer.SetPropertyBlock(block);
            block.Clear();
        }
    }
}
