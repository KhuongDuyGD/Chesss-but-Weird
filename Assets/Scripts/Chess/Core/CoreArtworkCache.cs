using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// Core UI scope lasts for the app session; it never contains cosmetic model assets.
public static class CoreArtworkCache
{
    private static readonly Dictionary<string, AssetLoader> scopes = new Dictionary<string, AssetLoader>();
    private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private static readonly HashSet<string> readyLabels = new HashSet<string>();

    public static IEnumerator Prepare(string label)
    {
        if (readyLabels.Contains(label)) yield break;
        yield return AssetLoader.Initialize();
        if (!AssetLoader.IsInitialized) yield break;
        var assets = new AssetLoader();
        var acquiredKeys = new List<string>();
        bool complete = false;
        var locations = Addressables.LoadResourceLocationsAsync(label, typeof(Texture2D));
        try
        {
            yield return locations;
            if (locations.Status == AsyncOperationStatus.Succeeded)
            {
                foreach (var location in locations.Result)
                {
                    string key = location.PrimaryKey;
                    if (!textures.ContainsKey(key))
                        yield return assets.Load<Texture2D>(key, texture => { if (texture) { textures[key] = texture; acquiredKeys.Add(key); } });
                }
                readyLabels.Add(label);
                scopes[label] = assets;
                complete = true;
            }
        }
        finally
        {
            if (locations.IsValid()) Addressables.Release(locations);
            if (!complete) { foreach (string key in acquiredKeys) textures.Remove(key); assets.Dispose(); }
        }
    }

    public static Texture2D GetTexture(string path)
    {
        textures.TryGetValue(NormalizePath(path), out var texture);
        return texture;
    }

    public static Sprite GetSprite(string path, bool trimTransparent = false)
    {
        path = NormalizePath(path);
        string cacheKey = path + (trimTransparent ? "#trim" : "#full");
        if (sprites.TryGetValue(cacheKey, out var sprite)) return sprite;
        var texture = GetTexture(path);
        if (!texture) return null;
        Rect rect = trimTransparent ? MenuArtworkBounds.GetRect(path, texture) : new Rect(0, 0, texture.width, texture.height);
        sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
        sprites[cacheKey] = sprite;
        return sprite;
    }

    private static string NormalizePath(string path)
    {
        path = path.Replace('\\', '/');
        int assets = path.IndexOf("/Assets/", System.StringComparison.OrdinalIgnoreCase);
        return assets >= 0 ? path.Substring(assets + 1) : path;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        foreach (var sprite in sprites.Values) if (sprite) Object.Destroy(sprite);
        sprites.Clear(); textures.Clear(); readyLabels.Clear();
        foreach (var scope in scopes.Values) scope.Dispose();
        scopes.Clear();
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterEditorCleanup()
    {
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
    {
        // Release session handles after consumers have been destroyed, including
        // when Enter Play Mode has domain reload disabled.
        if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) Reset();
    }
#endif
}
