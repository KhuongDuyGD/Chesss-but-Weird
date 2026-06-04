using UnityEngine;

public class HandDrawnMenuAssets : MonoBehaviour
{
    [Header("Title Menu")]
    public Sprite logo;
    public Sprite startButton;
    public Sprite settingsButton;
    public Sprite creditsButton;
    public Sprite mascot;
    public Sprite crown;
    public Sprite hearts;
    public Sprite punchDoodle;
    public Sprite earlyAccess;
    public Sprite stars;

    [Header("Mode Select")]
    public Sprite slogan;
    public Sprite localButton;
    public Sprite onlineButton;
    public Sprite aramButton;
    public Sprite shopButton;
    public Sprite inventoryIcon;
    public Sprite gachaIcon;
    public Sprite playerProfileIcon;
    public Sprite settingsIcon;

    [Header("Side Select")]
    public Sprite chooseSideTitle;
    public Sprite whiteCard;
    public Sprite blackCard;

    public bool HasRequiredSprites => logo && startButton && localButton && whiteCard && blackCard;

    public void LoadFromResources()
    {
        logo = LoadSprite("logo", new SpriteCrop(1448f, 1086f, 82f, 148f, 1294f, 715f));
        startButton = LoadSprite("start_button", new SpriteCrop(1448f, 1086f, 49f, 363f, 1361f, 326f));
        settingsButton = LoadSprite("settings_button", new SpriteCrop(1448f, 1086f, 136f, 390f, 1226f, 271f));
        creditsButton = LoadSprite("credits_button", new SpriteCrop(1448f, 1086f, 62f, 357f, 1339f, 333f));
        mascot = LoadSprite("mascot", new SpriteCrop(1254f, 1254f, 57f, 155f, 1176f, 865f));
        crown = LoadSprite("crown", new SpriteCrop(1254f, 1254f, 137f, 229f, 978f, 800f));
        hearts = LoadSprite("hearts", new SpriteCrop(1254f, 1254f, 209f, 191f, 808f, 832f));
        punchDoodle = LoadSprite("punch_doodle", new SpriteCrop(1254f, 1254f, 123f, 249f, 1033f, 670f));
        earlyAccess = LoadSprite("early_access", new SpriteCrop(1448f, 1086f, 91f, 226f, 1265f, 568f));
        stars = LoadSprite("stars", new SpriteCrop(1254f, 1254f, 240f, 429f, 781f, 333f));

        slogan = LoadSprite("slogan", new SpriteCrop(2508f, 627f, 45f, 172f, 2432f, 315f));
        localButton = LoadSprite("local_button", new SpriteCrop(1448f, 1086f, 111f, 306f, 1246f, 441f));
        onlineButton = LoadSprite("online_button", new SpriteCrop(1448f, 1086f, 74f, 300f, 1302f, 451f));
        aramButton = LoadSprite("aram_button", new SpriteCrop(1448f, 1086f, 88f, 328f, 1285f, 402f));
        shopButton = LoadSprite("shop_button", new SpriteCrop(1448f, 1086f, 139f, 314f, 1186f, 454f));
        inventoryIcon = LoadSprite("inventory_icon", new SpriteCrop(1254f, 1254f, 211f, 207f, 840f, 869f));
        gachaIcon = LoadSprite("gacha_icon", new SpriteCrop(1254f, 1254f, 185f, 251f, 914f, 728f));
        playerProfileIcon = LoadSprite("player_profile_icon", new SpriteCrop(1254f, 1254f, 138f, 169f, 987f, 917f));
        settingsIcon = LoadSprite("settings_icon", new SpriteCrop(1254f, 1254f, 169f, 153f, 885f, 916f));

        chooseSideTitle = LoadSprite("choose_side_title", new SpriteCrop(2172f, 724f, 216f, 218f, 1797f, 253f));
        whiteCard = LoadSprite("white_card", new SpriteCrop(1086f, 1448f, 109f, 167f, 876f, 1093f));
        blackCard = LoadSprite("black_card", new SpriteCrop(1086f, 1448f, 151f, 176f, 811f, 1002f));
    }

    private static Sprite LoadSprite(string resourceName, SpriteCrop crop)
    {
        Texture2D texture = Resources.Load<Texture2D>($"Main_Menu/{resourceName}");
        if (!texture)
        {
            Debug.LogWarning($"[HandDrawnMenu] Missing Resources/Main_Menu/{resourceName}.png");
            return null;
        }

        texture.filterMode = FilterMode.Bilinear;
        Rect sourceRect = crop.ToUnityRect(texture.width, texture.height);
        return Sprite.Create(texture, sourceRect, new Vector2(0.5f, 0.5f), 100f);
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
