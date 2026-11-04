using System.IO;
using UnityEditor;
using UnityEngine;

public static class ShopAssetBuilder
{
    public const string Folder = "Assets/Resources/Shop";
    [MenuItem("Chess/Shop/Build Shop Assets")]
    public static void Build()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Build shop assets in Edit Mode.");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        GameObject card = null, screen = null;
        try
        {
            card = new GameObject("Shop Product Card", typeof(RectTransform));
            ((RectTransform)card.transform).sizeDelta = new Vector2(316, 198);
            card.AddComponent<ShopProductCard>().Build();
            var cardAsset = PrefabUtility.SaveAsPrefabAsset(card, Folder + "/ShopProductCard.prefab");
            var catalog = AssetDatabase.LoadAssetAtPath<ShopPresentationCatalog>(Folder + "/ShopPresentation.asset");
            if (!catalog) { catalog = ScriptableObject.CreateInstance<ShopPresentationCatalog>(); AssetDatabase.CreateAsset(catalog, Folder + "/ShopPresentation.asset"); }
            catalog.cosmetics = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/Game/Data/CosmeticCatalog.asset");
            catalog.productCard = cardAsset.GetComponent<ShopProductCard>(); EditorUtility.SetDirty(catalog);
            screen = new GameObject("Shop Screen", typeof(RectTransform));
            ((RectTransform)screen.transform).sizeDelta = new Vector2(1672, 941);
            screen.AddComponent<ShopMenuController>().BuildLayout(catalog);
            PrefabUtility.SaveAsPrefabAsset(screen, Folder + "/ShopScreen.prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("[Shop] Built screen, product card and presentation catalog using the existing sketchbook UI and cosmetic art.");
        }
        finally { if (screen) Object.DestroyImmediate(screen); if (card) Object.DestroyImmediate(card); }
    }
}
