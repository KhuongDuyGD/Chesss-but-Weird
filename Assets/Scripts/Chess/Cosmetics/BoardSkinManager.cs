using System;
using System.Collections;
using UnityEngine;

public sealed class BoardSkinManager
{
    private readonly AssetLoader assets;
    public BoardSkinData Data { get; private set; }
    public EnvironmentData Environment { get; private set; }
    public GameObject BoardPrefab { get; private set; }
    public GameObject EnvironmentPrefab { get; private set; }
    public BoardSkinManager(AssetLoader assets) { this.assets = assets ?? throw new ArgumentNullException(nameof(assets)); }

    public IEnumerator Load(CosmeticCatalog catalog, string boardId, string environmentId, Action<float> progress = null)
    {
        Data = null;
        BoardPrefab = null;
        Environment = null;
        EnvironmentPrefab = null;
        float highest = 0;
        Action<float> report = p => { highest = Mathf.Max(highest, p); progress?.Invoke(highest); };
        yield return LoadBoard(catalog, boardId, p => report(p * 0.7f));
        string fallback = catalog ? catalog.defaultBoardId : "default";
        if (!BoardPrefab && !string.Equals(boardId, fallback, StringComparison.OrdinalIgnoreCase))
            yield return LoadBoard(catalog, fallback, null);
        report(0.7f);
        if (catalog && !string.IsNullOrWhiteSpace(environmentId))
        {
            CosmeticCatalogEntry entry = catalog.FindEnvironment(environmentId);
            if (entry != null)
            {
                yield return assets.Load<EnvironmentData>(entry.data, value => Environment = value, p => report(0.7f + p * 0.1f));
                if (Environment)
                    yield return assets.Load<GameObject>(Environment.environmentPrefab, value => EnvironmentPrefab = value, p => report(0.8f + p * 0.2f));
            }
        }
        report(1);
    }

    private IEnumerator LoadBoard(CosmeticCatalog catalog, string id, Action<float> progress)
    {
        CosmeticCatalogEntry entry = catalog ? catalog.FindBoard(id) : null;
        if (entry == null) yield break;
        yield return assets.Load<BoardSkinData>(entry.data, value => Data = value, p => progress?.Invoke(p * 0.25f));
        if (Data)
            yield return assets.Load<GameObject>(Data.boardPrefab, value => BoardPrefab = value, p => progress?.Invoke(0.25f + p * 0.75f));
    }

    public GameObject InstantiateBoard(Transform parent)
    {
        if (assets.IsDisposed || !BoardPrefab) return null;
        GameObject instance = UnityEngine.Object.Instantiate(BoardPrefab, parent, false);
        if (Data) SetTransform(instance.transform, Data.localPosition, Data.localRotation, Data.localScale);
        return instance;
    }

    public GameObject InstantiateEnvironment(Transform parent)
    {
        if (assets.IsDisposed || !EnvironmentPrefab) return null;
        GameObject instance = UnityEngine.Object.Instantiate(EnvironmentPrefab, parent, false);
        if (Environment) SetTransform(instance.transform, Environment.localPosition, Environment.localRotation, Environment.localScale);
        return instance;
    }

    private static void SetTransform(Transform target, Vector3 position, Vector3 rotation, Vector3 scale)
    {
        target.localPosition = position;
        target.localRotation = Quaternion.Euler(rotation);
        target.localScale = scale;
    }
}
