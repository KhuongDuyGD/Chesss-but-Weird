using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class HandDrawnMenuAssets : MonoBehaviour
{
    private const string MainMenuAssetFolder = "Assets/_Project/Art/UI/Main_Menu";

    [Header("Main Menu 1 and 2 - Preserved Original Logo")]
    public Sprite mainMenuLogoOriginal;

    // Main Menu 1/2 buttons and icons are now drawn by their presentation
    // components. These bitmap assets are still used by the other menu screens.
    [Header("Shared Submenu Artwork - Still In Use")]
    public Sprite backgroundDecoration1;
    public Sprite backgroundDecoration2;
    public Sprite backgroundDecoration3;
    public Sprite backgroundDecoration4;
    public Sprite backButton;

    public bool HasRequiredSprites => mainMenuLogoOriginal && backButton;

    public bool IsPrepared { get; private set; }
    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    private readonly List<Texture2D> ownedTextures = new List<Texture2D>();

    public IReadOnlyList<Action> CreatePreparationSteps()
    {
        if (IsPrepared) return Array.Empty<Action>();
        return new Action[]
        {
            () =>
            {
                mainMenuLogoOriginal = MainMenuLogoOriginal.LoadSprite();
                if (mainMenuLogoOriginal) spriteCache["MainMenuLogoOriginal"] = mainMenuLogoOriginal;
            },
            () => { backgroundDecoration1 = LoadRenamedSprite("BackgroundDecoration1.png", "punch_doodle", new SpriteCrop(1254f, 1254f, 123f, 249f, 1033f, 670f)); },
            () => { backgroundDecoration2 = LoadRenamedSprite("BackgroundDecoration2.png", "hearts", new SpriteCrop(1254f, 1254f, 209f, 191f, 808f, 832f)); },
            () => { backgroundDecoration3 = LoadRenamedSprite("BackgroundDecoration3.png", "stars", new SpriteCrop(1254f, 1254f, 240f, 429f, 781f, 333f)); },
            () => { backgroundDecoration4 = LoadRenamedSprite("BackgroundDecoration4.png", "crown", new SpriteCrop(1254f, 1254f, 137f, 229f, 978f, 800f)); },
            () => { backButton = LoadRenamedSprite("Back.png", "back_button", new SpriteCrop(2172f, 724f, 363f, 148f, 1448f, 418f)); },
            () => IsPrepared = true
        };
    }

    public void LoadFromResources()
    {
        foreach (Action step in CreatePreparationSteps()) step();
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in spriteCache.Values) if (sprite) Destroy(sprite);
        foreach (Texture2D texture in ownedTextures) if (texture) Destroy(texture);
        spriteCache.Clear();
        ownedTextures.Clear();
    }

    private Sprite LoadRenamedSprite(string fileName, string legacyResourceName, SpriteCrop? legacyCrop)
    {
        if (spriteCache.TryGetValue(fileName, out Sprite cached)) return cached;
        Texture2D texture = LoadTextureFromProjectFile(Path.Combine(MainMenuAssetFolder, fileName));
        if (texture)
        {
            MenuTextureSampling.Configure(texture);
            Rect projectSourceRect = legacyCrop.HasValue
                ? legacyCrop.Value.ToUnityRect(texture.width, texture.height)
                : new Rect(0f, 0f, texture.width, texture.height);
            return spriteCache[fileName] = Sprite.Create(texture, projectSourceRect, new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
        }

        if (!string.IsNullOrEmpty(legacyResourceName))
            texture = Resources.Load<Texture2D>($"Main_Menu/{legacyResourceName}");

        if (!texture)
        {
            Debug.LogWarning($"[HandDrawnMenu] Missing Main Menu asset: {fileName}");
            spriteCache[fileName] = null;
            return null;
        }

        MenuTextureSampling.Configure(texture);
        Rect sourceRect = legacyCrop.HasValue
            ? legacyCrop.Value.ToUnityRect(texture.width, texture.height)
            : new Rect(0f, 0f, texture.width, texture.height);
        return spriteCache[fileName] = Sprite.Create(texture, sourceRect, new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
    }

    private Texture2D LoadTextureFromProjectFile(string projectRelativePath)
    {
        Texture2D prepared = CoreArtworkCache.GetTexture(projectRelativePath);
        if (prepared) return prepared;
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath);
        if (!File.Exists(fullPath))
            return null;

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        if (!texture.LoadImage(bytes))
        {
            Destroy(texture);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(projectRelativePath);
        MenuTextureSampling.FinishRuntimeTexture(texture);
        ownedTextures.Add(texture);
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
