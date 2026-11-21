using UnityEngine;

[CreateAssetMenu(fileName = "BotDifficultyAssets", menuName = "Chess/Bot Difficulty Assets")]
public sealed class BotDifficultyAssetCatalog : ScriptableObject
{
    public Texture2D background;
    public Texture2D beginnerButton;
    public Texture2D easyButton;
    public Texture2D mediumButton;
    public Texture2D hardButton;
    public Texture2D expertButton;

    public bool HasRequiredTextures =>
        background && beginnerButton && easyButton && mediumButton && hardButton && expertButton;

    public static BotDifficultyAssetCatalog Load()
    {
        return Resources.Load<BotDifficultyAssetCatalog>("Chess/BotDifficultyAssets");
    }
}
