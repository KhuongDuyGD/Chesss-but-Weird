using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "Chess/Font Catalog", fileName = "ChessFontCatalog")]
public sealed class ChessFontCatalog : ScriptableObject
{
    private const string ResourcePath = "Chess/ChessFontCatalog";

    [SerializeField] private Font runtimeFont;
    [SerializeField] private TMP_FontAsset tmpFont;

    private static ChessFontCatalog cachedCatalog;

    public static Font RuntimeFont
    {
        get
        {
            ChessFontCatalog catalog = LoadCatalog();
            if (catalog != null && catalog.runtimeFont != null)
                return catalog.runtimeFont;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    public static TMP_FontAsset TmpFont
    {
        get
        {
            ChessFontCatalog catalog = LoadCatalog();
            if (catalog != null && catalog.tmpFont != null)
                return catalog.tmpFont;
            return TMP_Settings.defaultFontAsset;
        }
    }

    private static ChessFontCatalog LoadCatalog()
    {
        if (cachedCatalog == null)
            cachedCatalog = Resources.Load<ChessFontCatalog>(ResourcePath);
        return cachedCatalog;
    }
}
