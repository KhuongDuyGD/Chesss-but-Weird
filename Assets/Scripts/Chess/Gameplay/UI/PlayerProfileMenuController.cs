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
    private const float MenuViewportScale = 0.92f;
    private const string AssetFolder = "Assets/Materials/PlayerProfile";
    private static readonly Vector2 DesignSize = new Vector2(DesignWidth, DesignHeight);

    private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private readonly List<UnityEngine.Object> runtimeAssets = new List<UnityEngine.Object>();
    private readonly TextMeshProUGUI[] valueLabels = new TextMeshProUGUI[7];
    private readonly List<TextMeshProUGUI> historyRows = new List<TextMeshProUGUI>();
    private readonly List<Button> avatarButtons = new List<Button>();

    private RectTransform root;
    private RectTransform contentRoot;
    private RectTransform editPanel;
    private TMP_InputField displayNameInput;
    private Image avatarImage;
    private TextMeshProUGUI streakLabel;
    private TextMeshProUGUI achievementLabel;
    private TextMeshProUGUI statusLabel;
    private UnityAction closeAction;
    private int pendingAvatarIndex;

    public void Initialize(RectTransform newRoot, UnityAction newCloseAction)
    {
        root = newRoot;
        closeAction = newCloseAction;
        LoadSprites();
        Build();
    }

    public void Open()
    {
        PlayerProfileStore.EnsureLoaded();
        Refresh();
        if (editPanel)
            editPanel.gameObject.SetActive(false);
    }

    private void Build()
    {
        Image blocker = root.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.01f);
        blocker.raycastTarget = true;

        contentRoot = CreateChild(root, "Player Profile Content Root", Vector2.zero, DesignSize);
        contentRoot.gameObject.AddComponent<InventoryContentRootFitter>().Configure(DesignWidth, DesignHeight, MenuViewportScale);

        AddFullImage(contentRoot, "Player Profile Blank", GetSprite("PlayerProfileMainBlank.png"));
        AddImage(contentRoot, "Basic Profile Panel", GetSprite("BasicProfileData.png"), D(620f, 250f), new Vector2(1088f, 365f));
        AddImage(contentRoot, "Match History Panel", GetSprite("MatchHistory.png"), D(620f, 625f), new Vector2(1078f, 382f));
        AddImage(contentRoot, "Achievement Title", GetSprite("AchievementTitle.png"), D(1352f, 514f), new Vector2(420f, 190f));
        AddImage(contentRoot, "Streak Fire", GetSprite("Streaks.png"), D(1350f, 800f), new Vector2(205f, 205f));

        avatarImage = AddImage(contentRoot, "Current Avatar", GetSprite("Avatar0.png"), D(1360f, 172f), new Vector2(205f, 205f));
        AddButton(contentRoot, "Change Profile", GetSprite("ChangeDataProfileButton.png"), D(1360f, 340f), new Vector2(320f, 91f), ShowEditPanel, 1.035f);
        AddButton(contentRoot, "Back", GetSprite("Back.png"), D(205f, 860f), new Vector2(235f, 86f), Close, 1.035f);

        AddProfileValues();
        AddHistoryRows();
        achievementLabel = AddText(contentRoot, "Achievement Value", D(1352f, 556f), new Vector2(330f, 50f), 32f, TextAlignmentOptions.Center, Color.black);
        streakLabel = AddText(contentRoot, "Streak Value", D(1350f, 810f), new Vector2(150f, 66f), 40f, TextAlignmentOptions.Center, Color.black);
        statusLabel = AddText(contentRoot, "Profile Status", D(835f, 890f), new Vector2(840f, 38f), 24f, TextAlignmentOptions.Center, new Color(0.18f, 0.14f, 0.1f, 0.82f));
        BuildEditPanel();
    }

    private void AddProfileValues()
    {
        valueLabels[0] = AddText(contentRoot, "Name Value", D(585f, 140f), new Vector2(430f, 44f), 31f, TextAlignmentOptions.MidlineLeft, Color.black);
        valueLabels[1] = AddText(contentRoot, "Level Value", D(442f, 232f), new Vector2(150f, 42f), 31f, TextAlignmentOptions.MidlineLeft, Color.black);
        valueLabels[2] = AddText(contentRoot, "Gold Value", D(478f, 302f), new Vector2(225f, 42f), 31f, TextAlignmentOptions.MidlineLeft, Color.black);
        valueLabels[3] = AddText(contentRoot, "Id Value", D(500f, 372f), new Vector2(370f, 40f), 25f, TextAlignmentOptions.MidlineLeft, Color.black);
        valueLabels[4] = AddText(contentRoot, "Exp Value", D(930f, 232f), new Vector2(170f, 42f), 31f, TextAlignmentOptions.MidlineLeft, Color.black);
        valueLabels[5] = AddText(contentRoot, "Diamonds Value", D(1000f, 302f), new Vector2(160f, 42f), 31f, TextAlignmentOptions.MidlineLeft, Color.black);
        valueLabels[6] = AddText(contentRoot, "Tickets Value", D(970f, 372f), new Vector2(170f, 42f), 31f, TextAlignmentOptions.MidlineLeft, Color.black);
    }

    private void AddHistoryRows()
    {
        for (int i = 0; i < 6; i++)
        {
            TextMeshProUGUI row = AddText(contentRoot, $"Match History Row {i}", D(735f, 604f + i * 43f), new Vector2(745f, 38f), 25f, TextAlignmentOptions.Left, Color.black);
            historyRows.Add(row);
        }
    }

    private void BuildEditPanel()
    {
        editPanel = CreateChild(contentRoot, "Edit Profile Panel", D(836f, 520f), new Vector2(980f, 610f));
        Image panel = editPanel.gameObject.AddComponent<Image>();
        panel.color = new Color(0.985f, 0.965f, 0.91f, 0.97f);
        panel.raycastTarget = true;
        Outline outline = editPanel.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);

        AddText(editPanel, "Edit Title", new Vector2(0f, 250f), new Vector2(720f, 70f), 46f, TextAlignmentOptions.Center, Color.black).text = "Change Profile";
        AddText(editPanel, "Display Name Label", new Vector2(-310f, 158f), new Vector2(260f, 54f), 34f, TextAlignmentOptions.Left, Color.black).text = "Name:";
        displayNameInput = CreateInputField(editPanel, "Display Name Input", new Vector2(85f, 158f), new Vector2(560f, 68f));

        AddText(editPanel, "Avatar Label", new Vector2(-300f, 62f), new Vector2(280f, 54f), 34f, TextAlignmentOptions.Left, Color.black).text = "Avatar:";
        for (int i = 0; i < 6; i++)
        {
            int index = i;
            Vector2 position = new Vector2(-305f + i * 122f, -55f);
            Button button = AddButton(editPanel, $"Avatar {i}", GetSprite($"Avatar{i}.png"), position, new Vector2(98f, 98f), () => SelectAvatar(index), 1.06f);
            avatarButtons.Add(button);
        }

        AddButton(editPanel, "Save Profile", null, new Vector2(-145f, -240f), new Vector2(255f, 86f), SaveEditPanel, 1.035f, "SAVE");
        AddButton(editPanel, "Cancel Profile", null, new Vector2(165f, -240f), new Vector2(255f, 86f), HideEditPanel, 1.035f, "CANCEL");
        editPanel.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        PlayerProfileSaveData data = PlayerProfileStore.Data;
        valueLabels[0].text = Limit(data.displayName, 18);
        valueLabels[1].text = data.level.ToString(CultureInfo.InvariantCulture);
        valueLabels[2].text = data.gold.ToString("N0", CultureInfo.InvariantCulture);
        valueLabels[3].text = Limit(data.playerId, 18);
        valueLabels[4].text = $"{data.experience % 100}/100";
        valueLabels[5].text = data.diamonds.ToString("N0", CultureInfo.InvariantCulture);
        valueLabels[6].text = data.tickets.ToString("N0", CultureInfo.InvariantCulture);

        achievementLabel.text = Limit(data.achievementTitle, 18);
        streakLabel.text = $"{data.loginStreakDays}d";
        avatarImage.sprite = GetSprite($"Avatar{Mathf.Clamp(data.avatarIndex, 0, 5)}.png");
        RefreshHistory(data);
        SetStatus("Profile synced locally.");
    }

    private void RefreshHistory(PlayerProfileSaveData data)
    {
        for (int i = 0; i < historyRows.Count; i++)
        {
            if (data.matchHistory == null || i >= data.matchHistory.Count)
            {
                historyRows[i].text = i == 0 ? "No matches yet." : string.Empty;
                continue;
            }

            PlayerMatchHistoryEntry entry = data.matchHistory[i];
            string date = FormatDate(entry.playedAtUtc);
            historyRows[i].text = $"{date}  {Limit(entry.mode, 10)}  {Limit(entry.result, 6)}  vs {Limit(entry.opponent, 14)}";
        }
    }

    private void ShowEditPanel()
    {
        PlayerProfileSaveData data = PlayerProfileStore.Data;
        pendingAvatarIndex = Mathf.Clamp(data.avatarIndex, 0, 5);
        displayNameInput.text = data.displayName;
        RefreshAvatarSelection();
        editPanel.gameObject.SetActive(true);
        displayNameInput.ActivateInputField();
    }

    private void HideEditPanel()
    {
        editPanel.gameObject.SetActive(false);
    }

    private void SelectAvatar(int index)
    {
        pendingAvatarIndex = Mathf.Clamp(index, 0, 5);
        RefreshAvatarSelection();
    }

    private void RefreshAvatarSelection()
    {
        for (int i = 0; i < avatarButtons.Count; i++)
        {
            Image image = avatarButtons[i].targetGraphic as Image;
            if (image)
                image.color = i == pendingAvatarIndex ? new Color(1f, 0.95f, 0.55f, 1f) : Color.white;
        }
    }

    private void SaveEditPanel()
    {
        PlayerProfileStore.SetIdentity(displayNameInput.text, pendingAvatarIndex);
        HideEditPanel();
        Refresh();
        SetStatus("Profile updated.");
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

    private TMP_InputField CreateInputField(RectTransform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject rootObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        RectTransform rect = rootObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image background = rootObject.GetComponent<Image>();
        background.color = Color.white;
        background.raycastTarget = true;
        Outline outline = rootObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_InputField input = rootObject.GetComponent<TMP_InputField>();
        input.characterLimit = 18;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;

        TextMeshProUGUI text = AddText(rect, "Text", Vector2.zero, new Vector2(size.x - 34f, size.y - 10f), 34f, TextAlignmentOptions.MidlineLeft, Color.black);
        TextMeshProUGUI placeholder = AddText(rect, "Placeholder", Vector2.zero, new Vector2(size.x - 34f, size.y - 10f), 30f, TextAlignmentOptions.MidlineLeft, new Color(0f, 0f, 0f, 0.28f));
        placeholder.text = "Display name";
        input.textViewport = rect;
        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
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
            "Streaks.png",
            "AchievementTitle.png",
            "ChangeDataProfileButton.png",
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

        Rect rect = trimTransparent ? FindVisibleRect(texture) : new Rect(0f, 0f, texture.width, texture.height);
        Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
        runtimeAssets.Add(sprite);
        runtimeAssets.Add(texture);
        return sprite;
    }

    private static Rect FindVisibleRect(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        int minX = texture.width;
        int minY = texture.height;
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < texture.height; y++)
        {
            int row = y * texture.width;
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[row + x].a <= 8)
                    continue;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
            return new Rect(0f, 0f, texture.width, texture.height);

        const int padding = 3;
        minX = Mathf.Max(0, minX - padding);
        minY = Mathf.Max(0, minY - padding);
        maxX = Mathf.Min(texture.width - 1, maxX + padding);
        maxY = Mathf.Min(texture.height - 1, maxY + padding);
        return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static string FormatDate(string utc)
    {
        if (DateTime.TryParse(utc, null, DateTimeStyles.RoundtripKind, out DateTime parsed))
            return parsed.ToLocalTime().ToString("MM/dd", CultureInfo.InvariantCulture);
        return "--/--";
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
