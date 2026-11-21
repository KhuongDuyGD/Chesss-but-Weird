using UnityEngine;

[CreateAssetMenu(fileName = "PauseMenuAssets", menuName = "Chess/Pause Menu Assets")]
public sealed class PauseMenuAssetCatalog : ScriptableObject
{
    public Texture2D menuBlank;
    public Texture2D resumeButton;
    public Texture2D restartButton;
    public Texture2D settingsButton;
    public Texture2D quitButton;

    public static PauseMenuAssetCatalog Load()
    {
        return Resources.Load<PauseMenuAssetCatalog>("Chess/PauseMenuAssets");
    }
}
