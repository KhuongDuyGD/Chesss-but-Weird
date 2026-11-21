using UnityEngine;

[CreateAssetMenu(fileName = "AnalysisBoardAssets", menuName = "Chess/Analysis Board Assets")]
public sealed class AnalysisBoardAssetCatalog : ScriptableObject
{
    public Texture2D board;
    public Texture2D moveList;
    public Texture2D replayButton;
    public Texture2D reportButton;
    public Texture2D shareButton;
    public Shader transparentWhiteShader;

    public static AnalysisBoardAssetCatalog Load()
    {
        return Resources.Load<AnalysisBoardAssetCatalog>("GameplayUI/AnalysisBoardAssets");
    }
}
