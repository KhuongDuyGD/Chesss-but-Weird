using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

[InitializeOnLoad]
public static class SchoolbellFontSetup
{
    private const string SourceFontPath = "Assets/Fonts/Schoolbell/Schoolbell-Regular.ttf";
    private const string TmpFontAssetPath = "Assets/Fonts/Schoolbell/Schoolbell TMP.asset";
    private const string CatalogAssetPath = "Assets/Resources/Chess/ChessFontCatalog.asset";
    private const string SeedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?+-*/=_()[]{}<>\"'@#$%^&`~|\\\n\r\t…";

    private static bool queued;

    static SchoolbellFontSetup()
    {
        QueueSync();
    }

    [MenuItem("Tools/Chess/Fonts/Sync Schoolbell Font")]
    public static void SyncMenuItem()
    {
        EnsureSchoolbellConfigured();
    }

    private static void QueueSync()
    {
        if (queued)
            return;

        queued = true;
        EditorApplication.delayCall += () =>
        {
            queued = false;
            EnsureSchoolbellConfigured();
        };
    }

    private static void EnsureSchoolbellConfigured()
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
            return;

        TMP_FontAsset tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontAssetPath);
        if (!HasValidAtlas(tmpFont))
        {
            tmpFont = RebuildFontAsset(sourceFont);
            if (tmpFont == null)
                return;
        }

        tmpFont.TryAddCharacters(SeedCharacters, true);

        ChessFontCatalog catalog = AssetDatabase.LoadAssetAtPath<ChessFontCatalog>(CatalogAssetPath);
        if (catalog == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Chess"))
                AssetDatabase.CreateFolder("Assets/Resources", "Chess");
            catalog = ScriptableObject.CreateInstance<ChessFontCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        SerializedObject catalogObject = new SerializedObject(catalog);
        catalogObject.FindProperty("runtimeFont").objectReferenceValue = sourceFont;
        catalogObject.FindProperty("tmpFont").objectReferenceValue = tmpFont;
        catalogObject.ApplyModifiedPropertiesWithoutUndo();

        TMP_Settings settings = TMP_Settings.instance;
        if (settings != null && TMP_Settings.defaultFontAsset != tmpFont)
        {
            TMP_Settings.defaultFontAsset = tmpFont;
            EditorUtility.SetDirty(settings);
        }

        EditorUtility.SetDirty(catalog);
        EditorUtility.SetDirty(tmpFont);
        AssetDatabase.SaveAssets();
    }

    private static bool HasValidAtlas(TMP_FontAsset tmpFont)
    {
        if (tmpFont == null || tmpFont.material == null)
            return false;

        Texture mainTexture = tmpFont.material.GetTexture(ShaderUtilities.ID_MainTex);
        if (mainTexture == null)
            return false;

        if (tmpFont.atlasTextures == null || tmpFont.atlasTextures.Length == 0 || tmpFont.atlasTextures[0] == null)
            return false;

        return true;
    }

    private static TMP_FontAsset RebuildFontAsset(Font sourceFont)
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontAssetPath) != null)
            AssetDatabase.DeleteAsset(TmpFontAssetPath);

        TMP_FontAsset tmpFont = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            90,
            9,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic,
            true);

        if (tmpFont == null)
        {
            Debug.LogWarning("[SchoolbellFontSetup] Failed to create TMP font asset.");
            return null;
        }

        tmpFont.name = "Schoolbell TMP";
        AssetDatabase.CreateAsset(tmpFont, TmpFontAssetPath);

        if (tmpFont.atlasTextures != null)
        {
            for (int i = 0; i < tmpFont.atlasTextures.Length; i++)
            {
                Texture2D atlasTexture = tmpFont.atlasTextures[i];
                if (atlasTexture == null)
                    continue;

                atlasTexture.name = i == 0 ? "Schoolbell Atlas" : $"Schoolbell Atlas {i}";
                if (AssetDatabase.GetAssetPath(atlasTexture) == string.Empty)
                    AssetDatabase.AddObjectToAsset(atlasTexture, tmpFont);
            }
        }

        if (tmpFont.material != null)
        {
            tmpFont.material.name = "Schoolbell Material";
            if (tmpFont.atlasTextures != null && tmpFont.atlasTextures.Length > 0 && tmpFont.atlasTextures[0] != null)
                tmpFont.material.SetTexture(ShaderUtilities.ID_MainTex, tmpFont.atlasTextures[0]);
            if (AssetDatabase.GetAssetPath(tmpFont.material) == string.Empty)
                AssetDatabase.AddObjectToAsset(tmpFont.material, tmpFont);
        }

        EditorUtility.SetDirty(tmpFont);
        AssetDatabase.SaveAssets();
        return tmpFont;
    }
}
