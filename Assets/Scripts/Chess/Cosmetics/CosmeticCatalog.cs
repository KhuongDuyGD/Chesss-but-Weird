using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

// Only IDs, labels and indirect references: opening a menu does not load model bundles.
[CreateAssetMenu(menuName = "Chess/Cosmetics/Catalog")]
public sealed class CosmeticCatalog : ScriptableObject
{
    public const string Address = "Cosmetics/Catalog";
    public string defaultPieceSkinId = CosmeticSelection.LowPolyId;
    public string defaultBoardId = CosmeticSelection.LowPolyId;
    public List<CosmeticCatalogEntry> pieceSkins = new List<CosmeticCatalogEntry>();
    public List<CosmeticCatalogEntry> boards = new List<CosmeticCatalogEntry>();
    public List<CosmeticCatalogEntry> environments = new List<CosmeticCatalogEntry>();

    public CosmeticCatalogEntry FindPiece(string id) => Find(pieceSkins, id);
    public CosmeticCatalogEntry FindBoard(string id) => Find(boards, id);
    public CosmeticCatalogEntry FindEnvironment(string id) => Find(environments, id);

    private static CosmeticCatalogEntry Find(List<CosmeticCatalogEntry> entries, string id)
    {
        if (entries == null || string.IsNullOrWhiteSpace(id)) return null;
        foreach (var entry in entries)
            if (entry != null && string.Equals(entry.id, id.Trim(), StringComparison.OrdinalIgnoreCase)) return entry;
        return null;
    }
}

[Serializable]
public sealed class CosmeticCatalogEntry
{
    public string id;
    public string displayName;
    public AssetReference data;
    // Small menu art lives with the catalog; model bundles stay unloaded until a match.
    public Sprite thumbnail;
    public string tier = "COMMON";
    public bool HasArt => thumbnail && data != null && data.RuntimeKeyIsValid();
}
