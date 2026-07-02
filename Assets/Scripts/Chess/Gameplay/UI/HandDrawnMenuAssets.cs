using System.IO;
using UnityEngine;

public class HandDrawnMenuAssets : MonoBehaviour
{
    private const string MainMenuAssetFolder = "Assets/Materials/Main_Menu";

    [Header("Shared Main Menu Artwork")]
    public Sprite gameLogo;
    public Sprite doodleFaceDecoration;
    public Sprite backgroundDecoration1;
    public Sprite backgroundDecoration2;
    public Sprite backgroundDecoration3;
    public Sprite backgroundDecoration4;
    public Sprite gameVersion;
    public Sprite logoutButton;

    [Header("Main Menu 1")]
    public Sprite startButton;
    public Sprite settingsButton;
    public Sprite creditsButton;

    [Header("Main Menu 2")]
    public Sprite gameSlogan;
    public Sprite localGameplayButton;
    public Sprite onlinePlayButton;
    public Sprite aramModeButton;
    public Sprite shopButton;
    public Sprite inventoryButton;
    public Sprite gachaButton;
    public Sprite playerProfile;
    public Sprite settingsIcon;
    public Sprite backButton;

    [Header("Multiplayer Mode")]
    public Sprite chooseMultiplayerMode;
    public Sprite lanButton;
    public Sprite multiplayerButton;

    [Header("Side Select")]
    public Sprite chooseYourSide;
    public Sprite whiteSideButton;
    public Sprite blackSideButton;

    public bool HasRequiredSprites =>
        gameLogo && startButton && settingsButton && creditsButton &&
        localGameplayButton && onlinePlayButton && logoutButton &&
        whiteSideButton && blackSideButton;

    public bool HasMultiplayerModeSprites =>
        chooseMultiplayerMode && lanButton && multiplayerButton;

    public void LoadFromResources()
    {
        // The readable project files are preferred so renamed artwork is reflected
        // immediately. Legacy Resources names remain as a player-build fallback.
        gameLogo = LoadRenamedSprite("GameLogo.png", "logo", new SpriteCrop(1448f, 1086f, 82f, 148f, 1294f, 715f));
        startButton = LoadRenamedSprite("StartButton.png", "start_button", new SpriteCrop(1448f, 1086f, 49f, 363f, 1361f, 326f));
        settingsButton = LoadRenamedSprite("SettingsButton.png", "settings_button", new SpriteCrop(1448f, 1086f, 136f, 390f, 1226f, 271f));
        creditsButton = LoadRenamedSprite("CreditsButton.png", "credits_button", new SpriteCrop(1448f, 1086f, 62f, 357f, 1339f, 333f));
        doodleFaceDecoration = LoadRenamedSprite("DoodleFaceDecorateBackground.png", "mascot", new SpriteCrop(1254f, 1254f, 57f, 155f, 1176f, 865f));
        backgroundDecoration1 = LoadRenamedSprite("BackgroundDecoration1.png", "punch_doodle", new SpriteCrop(1254f, 1254f, 123f, 249f, 1033f, 670f));
        backgroundDecoration2 = LoadRenamedSprite("BackgroundDecoration2.png", "hearts", new SpriteCrop(1254f, 1254f, 209f, 191f, 808f, 832f));
        backgroundDecoration3 = LoadRenamedSprite("BackgroundDecoration3.png", "stars", new SpriteCrop(1254f, 1254f, 240f, 429f, 781f, 333f));
        backgroundDecoration4 = LoadRenamedSprite("BackgroundDecoration4.png", "crown", new SpriteCrop(1254f, 1254f, 137f, 229f, 978f, 800f));
        gameVersion = LoadRenamedSprite("GameVersionFake.png", "early_access", new SpriteCrop(1448f, 1086f, 91f, 226f, 1265f, 568f));
        logoutButton = LoadRenamedSprite("LogoutButton.png", "logout_button", new SpriteCrop(2172f, 724f, 150f, 114f, 1877f, 477f));

        gameSlogan = LoadRenamedSprite("GameSlogan.png", "slogan", new SpriteCrop(2508f, 627f, 45f, 172f, 2432f, 315f));
        localGameplayButton = LoadRenamedSprite("LocalGameplayButton.png", "local_button", new SpriteCrop(1448f, 1086f, 111f, 306f, 1246f, 441f));
        onlinePlayButton = LoadRenamedSprite("OnlinePlayButton.png", "online_button", new SpriteCrop(1448f, 1086f, 74f, 300f, 1302f, 451f));
        aramModeButton = LoadRenamedSprite("ARAMModeButton.png", "aram_button", new SpriteCrop(1448f, 1086f, 88f, 328f, 1285f, 402f));
        shopButton = LoadRenamedSprite("ShopButton.png", "shop_button", new SpriteCrop(1448f, 1086f, 139f, 314f, 1186f, 454f));
        inventoryButton = LoadRenamedSprite("InventoryButton.png", "inventory_icon", new SpriteCrop(1254f, 1254f, 211f, 207f, 840f, 869f));
        gachaButton = LoadRenamedSprite("GachaButton.png", "gacha_icon", new SpriteCrop(1254f, 1254f, 185f, 251f, 914f, 728f));
        playerProfile = LoadRenamedSprite("PlayerProfile.png", "player_profile_icon", new SpriteCrop(1254f, 1254f, 138f, 169f, 987f, 917f));
        settingsIcon = LoadRenamedSprite("SettingsIcon.png", "settings_icon", new SpriteCrop(1254f, 1254f, 169f, 153f, 885f, 916f));
        backButton = LoadRenamedSprite("Back.png", "back_button", new SpriteCrop(2172f, 724f, 363f, 148f, 1448f, 418f));

        chooseMultiplayerMode = LoadRenamedSprite("ChooseYour MultiplayerMode.png", "multiplayer_mode_title", new SpriteCrop(2172f, 724f, 20f, 220f, 2130f, 300f));
        lanButton = LoadRenamedSprite("LANButton.png", "lan_card", new SpriteCrop(1086f, 1448f, 36f, 112f, 1010f, 1250f));
        multiplayerButton = LoadRenamedSprite("MultiplayerButton.png", "multiplayer_online_card", new SpriteCrop(1086f, 1448f, 36f, 112f, 1010f, 1250f));

        chooseYourSide = LoadRenamedSprite("ChooseYourSide.png", "choose_side_title", new SpriteCrop(2172f, 724f, 216f, 218f, 1797f, 253f));
        whiteSideButton = LoadRenamedSprite("WhiteSideButton.png", "white_card", new SpriteCrop(1086f, 1448f, 109f, 167f, 876f, 1093f));
        blackSideButton = LoadRenamedSprite("BlackSideButton.png", "black_card", new SpriteCrop(1086f, 1448f, 151f, 176f, 811f, 1002f));
    }

    private static Sprite LoadRenamedSprite(string fileName, string legacyResourceName, SpriteCrop? legacyCrop)
    {
        Texture2D texture = LoadTextureFromProjectFile(Path.Combine(MainMenuAssetFolder, fileName));
        if (texture)
        {
            texture.filterMode = FilterMode.Bilinear;
            Rect projectSourceRect = legacyCrop.HasValue
                ? legacyCrop.Value.ToUnityRect(texture.width, texture.height)
                : new Rect(0f, 0f, texture.width, texture.height);
            return Sprite.Create(texture, projectSourceRect, new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
        }

        if (!string.IsNullOrEmpty(legacyResourceName))
            texture = Resources.Load<Texture2D>($"Main_Menu/{legacyResourceName}");

        if (!texture)
        {
            Debug.LogWarning($"[HandDrawnMenu] Missing Main Menu asset: {fileName}");
            return null;
        }

        texture.filterMode = FilterMode.Bilinear;
        Rect sourceRect = legacyCrop.HasValue
            ? legacyCrop.Value.ToUnityRect(texture.width, texture.height)
            : new Rect(0f, 0f, texture.width, texture.height);
        return Sprite.Create(texture, sourceRect, new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
    }

    private static Texture2D LoadTextureFromProjectFile(string projectRelativePath)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath);
        if (!File.Exists(fullPath))
            return null;

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            Destroy(texture);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(projectRelativePath);
        return texture;
    }

    private readonly struct SpriteCrop
    {
        private readonly float sourceWidth;
        private readonly float sourceHeight;
        private readonly float left;
        private readonly float top;
        private readonly float width;
        private readonly float height;

        public SpriteCrop(float sourceWidth, float sourceHeight, float left, float top, float width, float height)
        {
            this.sourceWidth = sourceWidth;
            this.sourceHeight = sourceHeight;
            this.left = left;
            this.top = top;
            this.width = width;
            this.height = height;
        }

        public Rect ToUnityRect(float textureWidth, float textureHeight)
        {
            float scaleX = textureWidth / sourceWidth;
            float scaleY = textureHeight / sourceHeight;
            float scaledLeft = left * scaleX;
            float scaledTop = top * scaleY;
            float scaledWidth = width * scaleX;
            float scaledHeight = height * scaleY;
            float x = Mathf.Clamp(scaledLeft, 0f, textureWidth - 1f);
            float y = Mathf.Clamp(textureHeight - scaledTop - scaledHeight, 0f, textureHeight - 1f);
            float rectWidth = Mathf.Clamp(scaledWidth, 1f, textureWidth - x);
            float rectHeight = Mathf.Clamp(scaledHeight, 1f, textureHeight - y);
            return new Rect(x, y, rectWidth, rectHeight);
        }
    }
}
