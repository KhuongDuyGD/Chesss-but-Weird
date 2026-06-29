#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AnalysisBoardAssetBuilder
{
    private const string OutputFolder = "Assets/Resources/GameplayUI";
    private const string OutputPath = OutputFolder + "/AnalysisBoardAssets.asset";

    [MenuItem("Tools/Chess/Rebuild Analysis Board Assets")]
    public static void Rebuild()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(OutputFolder);

        AnalysisBoardAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<AnalysisBoardAssetCatalog>(OutputPath);
        if (!catalog)
        {
            catalog = ScriptableObject.CreateInstance<AnalysisBoardAssetCatalog>();
            AssetDatabase.CreateAsset(catalog, OutputPath);
        }

        catalog.board = Load("AnalysisBoardBlank.png");
        catalog.moveList = Load("MoveListUI.png");
        catalog.replayButton = Load("ReplyButton.png");
        catalog.reportButton = Load("ReportButton.png");
        catalog.shareButton = Load("ShareButton.png");
        catalog.transparentWhiteShader = Shader.Find("Chess/Analysis Transparent White UI");

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AnalysisBoard] Asset catalog rebuilt at {OutputPath}.");
    }

    private static Texture2D Load(string fileName)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Materials/GameplayUI/{fileName}");
        if (!texture)
            Debug.LogError($"[AnalysisBoard] Missing texture: {fileName}");
        return texture;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;
        int slash = folder.LastIndexOf('/');
        AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
    }
}
#endif
