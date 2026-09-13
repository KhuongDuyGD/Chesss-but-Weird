using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets.ResourceLocators;

// One scope per match. Destroy its instantiated GameObjects before disposing the scope.
public sealed class AssetLoader : IDisposable
{
    private sealed class Entry
    {
        public AsyncOperationHandle handle;
        public bool hasHandle;
        public bool complete;
        public UnityEngine.Object asset;
        public float progress;
    }
    private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
    private readonly List<AsyncOperationHandle> downloads = new List<AsyncOperationHandle>();
    private bool disposed;
    private static bool initialized;
    private static AsyncOperationHandle<IResourceLocator> initialization;
    public static bool IsInitialized => initialized;
    public bool IsDisposed => disposed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInitialization()
    {
        if (initialization.IsValid()) Addressables.Release(initialization);
        initialization = default;
        initialized = false;
    }

    public static IEnumerator Initialize(Action<float> progress = null)
    {
        if (initialized) { progress?.Invoke(1); yield break; }
        if (!initialization.IsValid())
        {
            try { initialization = Addressables.InitializeAsync(false); }
            catch (Exception error) { Debug.LogWarning("[Cosmetics] Initialization failed: " + error.Message); }
        }
        if (!initialization.IsValid()) yield break;
        while (initialization.IsValid() && !initialization.IsDone)
        {
            progress?.Invoke(initialization.PercentComplete);
            yield return null;
        }
        if (!initialization.IsValid()) { progress?.Invoke(initialized ? 1 : 0); yield break; }
        initialized = initialization.Status == AsyncOperationStatus.Succeeded;
        if (!initialized) Debug.LogWarning("[Cosmetics] Addressables initialization failed; using local fallback visuals.");
        Addressables.Release(initialization);
        initialization = default;
        progress?.Invoke(1);
    }

    // Invoke in Boot/MainMenu with no match scopes alive, then reload Cosmetics/Catalog.
    public static IEnumerator UpdateCatalogs()
    {
        yield return Initialize();
        if (!initialized) yield break;
        var check = Addressables.CheckForCatalogUpdates(false);
        List<string> catalogs = null;
        try
        {
            yield return check;
            if (check.Status == AsyncOperationStatus.Succeeded && check.Result != null)
                catalogs = new List<string>(check.Result);
        }
        finally { if (check.IsValid()) Addressables.Release(check); }
        if (catalogs == null || catalogs.Count == 0) yield break;
        var update = Addressables.UpdateCatalogs(catalogs, false);
        try
        {
            yield return update;
            if (update.Status != AsyncOperationStatus.Succeeded)
                Debug.LogWarning("[Cosmetics] Remote catalog update unavailable; retaining installed content.");
        }
        finally { if (update.IsValid()) Addressables.Release(update); }
    }

    public IEnumerator Load<T>(object key, Action<T> completed, Action<float> progress = null) where T : UnityEngine.Object
    {
        if (disposed || key == null) { completed?.Invoke(null); yield break; }
        if (key is AssetReference reference)
        {
            if (!reference.RuntimeKeyIsValid()) { completed?.Invoke(null); yield break; }
            key = reference.RuntimeKey;
        }
        string cacheKey = typeof(T).FullName + ":" + key;
        if (entries.TryGetValue(cacheKey, out Entry existing))
        {
            while (!disposed && !existing.complete) { progress?.Invoke(existing.progress); yield return null; }
            progress?.Invoke(1);
            completed?.Invoke(disposed ? null : existing.asset as T);
            yield break;
        }
        var entry = new Entry();
        entries.Add(cacheKey, entry);
        yield return Initialize();
        if (disposed || !initialized) { entry.complete = true; completed?.Invoke(null); yield break; }

        AsyncOperationHandle download = default;
        try { download = Addressables.DownloadDependenciesAsync(key, false); }
        catch (Exception error) { Debug.LogWarning("[Cosmetics] Dependency request failed: " + error.Message); }
        if (!download.IsValid()) { entry.complete = true; completed?.Invoke(null); yield break; }
        downloads.Add(download);
        while (!disposed && !download.IsDone)
        {
            entry.progress = download.GetDownloadStatus().Percent * 0.5f;
            progress?.Invoke(entry.progress);
            yield return null;
        }
        if (disposed) { completed?.Invoke(null); yield break; }
        bool downloaded = download.Status == AsyncOperationStatus.Succeeded;
        downloads.Remove(download);
        Addressables.Release(download);
        if (downloaded)
        {
            try { entry.handle = Addressables.LoadAssetAsync<T>(key); entry.hasHandle = true; }
            catch (Exception error) { Debug.LogWarning("[Cosmetics] Asset request failed: " + error.Message); }
            while (!disposed && entry.hasHandle && !entry.handle.IsDone)
            {
                entry.progress = 0.5f + entry.handle.PercentComplete * 0.5f;
                progress?.Invoke(entry.progress);
                yield return null;
            }
            if (disposed) { completed?.Invoke(null); yield break; }
            if (entry.hasHandle && entry.handle.Status == AsyncOperationStatus.Succeeded)
                entry.asset = entry.handle.Result as T;
        }
        if (!entry.asset)
        {
            Debug.LogWarning("[Cosmetics] Could not load '" + key + "'; using fallback.");
            if (entry.hasHandle && entry.handle.IsValid()) Addressables.Release(entry.handle);
            entry.hasHandle = false;
        }
        entry.complete = true;
        entry.progress = 1;
        progress?.Invoke(1);
        completed?.Invoke(entry.asset as T);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var download in downloads) if (download.IsValid()) Addressables.Release(download);
        downloads.Clear();
        foreach (var entry in entries.Values)
        {
            if (entry.hasHandle && entry.handle.IsValid()) Addressables.Release(entry.handle);
            entry.asset = null;
        }
        entries.Clear();
    }
}

