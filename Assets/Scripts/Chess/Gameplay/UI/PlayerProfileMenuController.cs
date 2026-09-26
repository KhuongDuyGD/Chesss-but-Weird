using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class PlayerProfileMenuController : MonoBehaviour
{
    private const float DesignWidth = 1672f;
    private const float DesignHeight = 941f;
    private const float MenuViewportScale = 1f;
    private const string AssetFolder = "Assets/Materials/PlayerProfile";
    private static readonly Vector2 DesignSize = new Vector2(DesignWidth, DesignHeight);

    private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private readonly List<UnityEngine.Object> runtimeAssets = new List<UnityEngine.Object>();
    private readonly TextMeshProUGUI[] valueLabels = new TextMeshProUGUI[7];
    private readonly List<TextMeshProUGUI> historyRows = new List<TextMeshProUGUI>();

    private RectTransform root;
    private RectTransform contentRoot;
    private Image avatarImage;
    private TextMeshProUGUI streakLabel;
    private TextMeshProUGUI achievementLabel;
    private TextMeshProUGUI avatarIdLabel;
    private TextMeshProUGUI statusLabel;
    private UnityAction closeAction;
    private bool loadingProfile;

    public void Initialize(RectTransform newRoot, UnityAction newCloseAction)
    {
        root = newRoot;
        closeAction = newCloseAction;
        LoadSprites();
        Build();
    }

    public async void Open()
    {
        if (loadingProfile) return;
        loadingProfile = true;
        Refresh(PlayerAuthService.CurrentApiUser);
        SetStatus("Loading profile from server...");
        try
        {
            UserMeResponse user = await new UserService().GetMeAsync();
            if (!this || !root) return;
            PlayerAuthService.ApplyApiUser(user);
            Refresh(user);
            SetStatus("Profile updated from /api/users/me.");
        }
        catch (Exception error)
        {
            if (this) SetStatus("Could not load profile: " + error.Message);
        }
        finally
        {
            loadingProfile = false;
        }
    }

    private void Build()
    {
        Image blocker = root.gameObject.AddComponent<Image>();
        blocker.color = new Color(.985f, .965f, .91f, 1f);
        blocker.raycastTarget = true;

        contentRoot = CreateChild(root, "Player Profile Content Root", Vector2.zero, DesignSize);
        contentRoot.gameObject.AddComponent<InventoryContentRootFitter>().Configure(DesignWidth, DesignHeight, MenuViewportScale);

        AddFullImage(contentRoot, "Player Profile Blank", GetSprite("PlayerProfileMainBlank.png"));
        AddImage(contentRoot, "Basic Profile Panel", GetSprite("BasicProfileData.png"), D(626f, 258f), new Vector2(1096f, 360f)).preserveAspect = false;
        Image basicMask = AddImage(contentRoot, "Live Profile Labels Background", null,
            D(626f, 258f), new Vector2(1018f, 285f));
        basicMask.color = new Color(0.995f, 0.983f, 0.955f, 1f);
        basicMask.preserveAspect = false;
        AddText(contentRoot, "Display Name Caption", D(220f, 140f), new Vector2(240f, 44f), 31f, TextAlignmentOptions.MidlineLeft, Color.black).text = "Name:";
        AddText(contentRoot, "Elo Rating Caption", D(220f, 232f), new Vector2(240f, 44f), 31f, TextAlignmentOptions.MidlineLeft, Color.black).text = "ELO:";
        AddText(contentRoot, "Gold Caption", D(220f, 302f), new Vector2(240f, 44f), 31f, TextAlignmentOptions.MidlineLeft, Color.black).text = "Gold:";
        AddText(contentRoot, "User Id Caption", D(220f, 372f), new Vector2(240f, 44f), 31f, TextAlignmentOptions.MidlineLeft, Color.black).text = "ID:";
        AddText(contentRoot, "Games Caption", D(805f, 232f), new Vector2(220f, 44f), 31f, TextAlignmentOptions.MidlineLeft, Color.black).text = "Games:";
        AddText(contentRoot, "Diamonds Caption", D(805f, 302f), new Vector2(240f, 44f), 31f, TextAlignmentOptions.MidlineLeft, Color.black).text = "Diamonds:";
        AddText(contentRoot, "Tickets Caption", D(805f, 372f), new Vector2(240f, 44f), 31f, TextAlignmentOptions.MidlineLeft, Color.black).text = "Tickets:";
        AddImage(contentRoot, "Match History Panel", GetSprite("MatchHistory.png"), D(610f, 666f), new Vector2(1062f, 390f)).preserveAspect = false;
        Image detailsMask = AddImage(contentRoot, "Account Details Background", null,
            D(610f, 666f), new Vector2(985f, 310f));
        detailsMask.color = new Color(0.995f, 0.983f, 0.955f, 1f);
        detailsMask.preserveAspect = false;
        AddText(contentRoot, "Account Details Title", D(610f, 522f), new Vector2(840f, 55f),
            38f, TextAlignmentOptions.Center, Color.black).text = "ACCOUNT DETAILS";
        AddText(contentRoot, "Elo Caption", D(1360f, 500f), new Vector2(330f, 52f), 33f,
            TextAlignmentOptions.Center, Color.black).text = "ELO";
        AddText(contentRoot, "Games Caption", D(1365f, 742f), new Vector2(330f, 52f), 33f,
            TextAlignmentOptions.Center, Color.black).text = "GAMES PLAYED";

        avatarImage = AddImage(contentRoot, "Current Avatar", GetSprite("Avatar0.png"), D(1376f, 190f), new Vector2(247f, 232f));
        avatarIdLabel = AddText(contentRoot, "Avatar ID", D(1380f, 309f), new Vector2(340f, 43f),
            21f, TextAlignmentOptions.Center, Color.black);
        AddButton(contentRoot, "Refresh Profile", null, D(1384f, 362f), new Vector2(333f, 91f), Open, 1.035f, "REFRESH");
        AddButton(contentRoot, "Back", GetSprite("Back.png"), D(188f, 894f), new Vector2(180f, 44f), Close, 1.035f);

        AddProfileValues();
        AddHistoryRows();
        achievementLabel = AddText(contentRoot, "Achievement Value", D(1360f, 559f), new Vector2(330f, 50f), 32f, TextAlignmentOptions.Center, Color.black);
        streakLabel = AddText(contentRoot, "Streak Value", D(1365f, 808f), new Vector2(150f, 66f), 40f, TextAlignmentOptions.Center, Color.black);
        statusLabel = AddText(contentRoot, "Profile Status", D(835f, 890f), new Vector2(840f, 38f), 24f, TextAlignmentOptions.Center, new Color(0.18f, 0.14f, 0.1f, 0.82f));
    }

    private void AddProfileValues()
    {
        valueLabels[0] = AddText(contentRoot, "Name Value", D(585f, 140f), new Vector2(430f, 44f), 31f, TextAlignmentOptions.MidlineLeft, Color.black);
        valueLabels[1] = AddText(contentRoot, "Level Value", D(442f, 232f), new Vector2(150f, 42f), 31f, TextAlignmentOptions.MidlineLeft, Color.black);
        valueLabels[2] = AddText(contentRoot, "Gold Value", D(478f, 302f), new Vector2(225f, 42f), 31f, TextAlignmentOptions.MidlineLeft, Color.black);
        valueLabels[3] = AddText(contentRoot, "Id Value", D(500f, 372f), new Vector2(370f, 40f), 25f, TextAlignmentOptions.MidlineLeft, Color.black);
        valueLabels[4] = AddText(contentRoot, "Games Value", D(1020f, 232f), new Vector2(170f, 42f), 31f, TextAlignmentOptions.MidlineLeft, Color.black);
        valueLabels[5] = AddText(contentRoot, "Diamonds Value", D(1040f, 302f), new Vector2(160f, 42f), 31f, TextAlignmentOptions.MidlineLeft, Color.black);
        valueLabels[6] = AddText(contentRoot, "Tickets Value", D(1040f, 372f), new Vector2(170f, 42f), 31f, TextAlignmentOptions.MidlineLeft, Color.black);
    }

    private void AddHistoryRows()
    {
        for (int i = 0; i < 7; i++)
        {
            TextMeshProUGUI row = AddText(contentRoot, $"Match History Row {i}", D(610f, 589f + i * 44f), new Vector2(950f, 38f), 25f, TextAlignmentOptions.Left, Color.black);
            historyRows.Add(row);
        }
    }

    private void Refresh(UserMeResponse user)
    {
        valueLabels[0].text = Limit(user?.profile?.displayName ?? user?.username, 18);
        valueLabels[1].text = user?.stats?.elo.ToString(CultureInfo.InvariantCulture) ?? "--";
        valueLabels[2].text = user?.wallet?.golds.ToString("N0", CultureInfo.InvariantCulture) ?? "--";
        valueLabels[3].text = Limit(user?.userId, 18);
        valueLabels[4].text = user?.stats?.gamesPlayed.ToString(CultureInfo.InvariantCulture) ?? "--";
        valueLabels[5].text = user?.wallet?.diamonds.ToString("N0", CultureInfo.InvariantCulture) ?? "--";
        valueLabels[6].text = user?.wallet?.tickets.ToString("N0", CultureInfo.InvariantCulture) ?? "--";

        achievementLabel.text = user?.stats == null ? "--" : user.stats.elo.ToString(CultureInfo.InvariantCulture);
        streakLabel.text = user?.stats == null ? "--" : user.stats.gamesPlayed.ToString(CultureInfo.InvariantCulture);
        avatarImage.sprite = GetSprite("Avatar0.png");
        avatarIdLabel.text = "Avatar: " + (user?.profile?.avatarId ?? "default");
        RefreshDetails(user);
    }

    private void RefreshDetails(UserMeResponse user)
    {
        string chessSet = user?.equipped?.chessSkinId == "6aacc041e50e17c25a690158"
            ? "Tazji's Low Poly" : user?.equipped?.chessSkinId ?? "--";
        string board = user?.equipped?.boardSkinId == "6aacc04be50e17c25a69015a"
            ? "Tazji's Low Poly Arena" : user?.equipped?.boardSkinId ?? "--";
        string[] details =
        {
            "Username: " + (user?.username ?? "--"),
            "Email: " + (user?.email ?? "--"),
            user?.stats == null ? "ELO: --" : $"ELO: {user.stats.elo}    W {user.stats.wins}  L {user.stats.losses}  D {user.stats.draws}",
            "Games played: " + (user?.stats?.gamesPlayed.ToString(CultureInfo.InvariantCulture) ?? "--"),
            "Chess set: " + chessSet,
            "Board: " + board,
            "Created: " + (user?.createdAt ?? "--")
        };
        for (int i = 0; i < historyRows.Count; i++) historyRows[i].text = details[i];
    }

    private void Close()
    {
        closeAction?.Invoke();
    }

    private void SetStatus(string message)
    {
        if (statusLabel)
            statusLabel.text = message ?? string.Empty;
    }

    private Button AddButton(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, UnityAction action, float hoverScale, string text = null)
    {
        Image image = AddImage(parent, name, sprite, position, size);
        image.raycastTarget = true;
        if (!sprite)
        {
            image.color = new Color(0.84f, 0.94f, 0.72f, 1f);
            image.preserveAspect = false;
            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(3f, -3f);
            AddSolidFrame(image.rectTransform, $"{name} Border", size, 3f, Color.black);
        }

        Button button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        HandDrawnPressable pressable = image.gameObject.AddComponent<HandDrawnPressable>();
        pressable.Configure(hoverScale, 0.965f, 0.55f, new Color(1f, 0.96f, 0.72f, 1f));

        if (!string.IsNullOrWhiteSpace(text))
            AddText(image.rectTransform, $"{name} Label", Vector2.zero, size, 34f, TextAlignmentOptions.Center, Color.black).text = text;

        return button;
    }

    private void AddSolidFrame(RectTransform parent, string name, Vector2 size, float thickness, Color color)
    {
        AddFrameEdge(parent, $"{name} Top", new Vector2(0f, size.y * 0.5f - thickness * 0.5f), new Vector2(size.x, thickness), color);
        AddFrameEdge(parent, $"{name} Bottom", new Vector2(0f, -size.y * 0.5f + thickness * 0.5f), new Vector2(size.x, thickness), color);
        AddFrameEdge(parent, $"{name} Left", new Vector2(-size.x * 0.5f + thickness * 0.5f, 0f), new Vector2(thickness, size.y), color);
        AddFrameEdge(parent, $"{name} Right", new Vector2(size.x * 0.5f - thickness * 0.5f, 0f), new Vector2(thickness, size.y), color);
    }

    private void AddFrameEdge(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject edgeObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = edgeObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = edgeObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private Image AddFullImage(Transform parent, string name, Sprite sprite)
    {
        Image image = AddImage(parent, name, sprite, Vector2.zero, DesignSize);
        image.preserveAspect = false;
        return image;
    }

    private Image AddImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI AddText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = ChessFontCatalog.TmpFont != null ? ChessFontCatalog.TmpFont : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(13f, fontSize * 0.62f);
        text.fontSizeMax = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private RectTransform CreateChild(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private Sprite GetSprite(string fileName)
    {
        if (sprites.TryGetValue(fileName, out Sprite sprite))
            return sprite;
        sprite = LoadSprite(fileName, !string.Equals(fileName, "PlayerProfileMainBlank.png", StringComparison.OrdinalIgnoreCase));
        sprites[fileName] = sprite;
        return sprite;
    }

    private void LoadSprites()
    {
        string[] files =
        {
            "PlayerProfileMainBlank.png",
            "BasicProfileData.png",
            "MatchHistory.png",
            "Back.png",
            "Avatar0.png",
            "Avatar1.png",
            "Avatar2.png",
            "Avatar3.png",
            "Avatar4.png",
            "Avatar5.png"
        };

        for (int i = 0; i < files.Length; i++)
            sprites[files[i]] = LoadSprite(files[i], !string.Equals(files[i], "PlayerProfileMainBlank.png", StringComparison.OrdinalIgnoreCase));
    }

    private Sprite LoadSprite(string fileName, bool trimTransparent)
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), AssetFolder, fileName);
        if (CoreArtworkCache.GetSprite(path, trimTransparent) is Sprite prepared) return prepared;
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[PlayerProfile] Missing asset: {fileName}");
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = Path.GetFileNameWithoutExtension(fileName),
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        if (!texture.LoadImage(File.ReadAllBytes(path)))
        {
            Destroy(texture);
            return null;
        }

        Rect rect = trimTransparent ? MenuArtworkBounds.GetRect(AssetFolder + "/" + fileName, texture) : new Rect(0f, 0f, texture.width, texture.height);
        Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
        texture.Apply(false, true);
        runtimeAssets.Add(sprite);
        runtimeAssets.Add(texture);
        return sprite;
    }


    private static string Limit(string value, int maxCharacters)
    {
        return PlayerProfileStore.Limit(value, maxCharacters);
    }

    private static Vector2 D(float x, float y)
    {
        return new Vector2(x - DesignWidth * 0.5f, DesignHeight * 0.5f - y);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < runtimeAssets.Count; i++)
            if (runtimeAssets[i])
                Destroy(runtimeAssets[i]);
        runtimeAssets.Clear();
    }
}
