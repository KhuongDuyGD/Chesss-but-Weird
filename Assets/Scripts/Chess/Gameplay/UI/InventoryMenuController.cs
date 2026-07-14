using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class InventoryMenuController : MonoBehaviour
{
    private const float DesignWidth = 1672f;
    private const float DesignHeight = 941f;
    private const float MenuViewportScale = 0.86f;
    private const string AssetFolder = "Assets/Materials/Inventory";
    private static readonly Vector2 DesignSize = new Vector2(DesignWidth, DesignHeight);
    private static readonly Vector2 ItemGridFirstSlot = new Vector2(193f, 325f);
    private static readonly Vector2 ItemGridSlotSpacing = new Vector2(259f, 173f);
    private static readonly Vector2 ItemGridSlotSize = new Vector2(226f, 138f);
    private const int ItemGridColumns = 6;
    private const int ItemGridRows = 3;
    private static readonly Vector2 StatusPosition = new Vector2(836f, 857f);
    private static readonly Vector2 StatusSize = new Vector2(1120f, 44f);
    private const float StatusFontSize = 26f;
    private static readonly HashSet<string> PlaceholderItemIds = new HashSet<string>
    {
        "effect_paper_spark",
        "skin_classic_white",
        "music_main_theme",
        "effect_check_flash",
        "skin_midnight_rook",
        "music_endgame"
    };
    private static readonly InventoryItemData[] BuiltInMusicItems =
    {
        new InventoryItemData("music_pack_cbw", "CBW Official", InventoryItemCategory.Music, 5, "Official")
        {
            musicPackId = "default",
            imageAssetPath = "Assets/Audio/MusicPackage/DefaultMusic/CBWMusicDisco.png",
            builtIn = true
        },
        new InventoryItemData("music_pack_angry_birds_epic", "Angry Birds Epic", InventoryItemCategory.Music, 5, "Collab")
        {
            musicPackId = "angry_birds_epic",
            imageAssetPath = "Assets/Audio/MusicPackage/AngryBirdsCollab/CBW_AngryBirdsEpicDisco.png",
            builtIn = true
        },
        new InventoryItemData("music_pack_epic_seven", "Epic Seven", InventoryItemCategory.Music, 5, "Collab")
        {
            musicPackId = "epic_seven",
            imageAssetPath = "Assets/Audio/MusicPackage/EpicSevenCollab/CBWEpicSevenDisco.png",
            builtIn = true
        },
        new InventoryItemData("music_pack_csgo", "CSGO Main Theme", InventoryItemCategory.Music, 5, "Collab")
        {
            musicPackId = "csgo",
            imageAssetPath = "Assets/Audio/MusicPackage/CSGOcollab/CBWCCSGODisco.png",
            builtIn = true
        },
        new InventoryItemData("music_pack_eye_of_the_dragon", "Eye of the Dragon", InventoryItemCategory.Music, 5, "Collab")
        {
            musicPackId = "eye_of_the_dragon",
            imageAssetPath = "Assets/Audio/MusicPackage/CSGOcollab/CBWEyeOfTheDragonDisco.png",
            builtIn = true
        }
    };

    private static readonly InventoryItemData[] BuiltInSkinItems = CreateBuiltInSkinItems();

    private static readonly InventoryButtonLayout[] ButtonLayouts =
    {
        new InventoryButtonLayout("Music Inventory", "MusicButton.png", InventoryButtonAction.Music, new Vector2(164f, 137f), new Vector2(225f, 160f), 1.045f, new Color(0.95f, 0.86f, 1f, 1f)),
        new InventoryButtonLayout("Skin Inventory", "SkinButton.png", InventoryButtonAction.Skin, new Vector2(413f, 137f), new Vector2(225f, 160f), 1.045f, new Color(0.86f, 0.92f, 1f, 1f)),
        new InventoryButtonLayout("Effect Inventory", "EffectButton.png", InventoryButtonAction.Effect, new Vector2(670f, 137f), new Vector2(225f, 160f), 1.045f, new Color(1f, 0.98f, 0.72f, 1f)),
        new InventoryButtonLayout("Sort Inventory", "SortButton.png", InventoryButtonAction.Sort, new Vector2(1339f, 137f), new Vector2(178f, 162f), 1.045f, new Color(0.82f, 1f, 0.87f, 1f)),
        new InventoryButtonLayout("Exit Inventory", "ExitButton.png", InventoryButtonAction.Close, new Vector2(1536f, 137f), new Vector2(158f, 158f), 1.04f, new Color(1f, 0.86f, 0.9f, 1f))
    };

    private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private readonly List<Sprite> runtimeSprites = new List<Sprite>();

    private RectTransform root;
    private RectTransform contentRoot;
    private RectTransform gridRoot;
    private TextMeshProUGUI statusLabel;
    private UnityAction closeAction;
    private InventorySaveData saveData;
    private InventoryItemCategory? activeCategory;

    public void Initialize(RectTransform newRoot, UnityAction newCloseAction)
    {
        root = newRoot;
        closeAction = newCloseAction;
        LoadSprites();
        LoadSave();
        Build();
    }

    public void Open()
    {
        RefreshGrid();
    }

    private void Build()
    {
        Image blocker = root.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.01f);
        blocker.raycastTarget = true;

        contentRoot = CreateChild(root, "Inventory Content Root", Vector2.zero, DesignSize);
        contentRoot.gameObject.AddComponent<InventoryContentRootFitter>().Configure(DesignWidth, DesignHeight, MenuViewportScale);

        AddFullImage(contentRoot, "Inventory Blank", GetSprite("InventoryBlank.png"));

        for (int i = 0; i < ButtonLayouts.Length; i++)
        {
            InventoryButtonLayout layout = ButtonLayouts[i];
            AddButton(contentRoot, layout.name, GetSprite(layout.spriteFile), D(layout.designPosition), layout.size, GetAction(layout.action), layout.hoverScale, layout.hoverTint);
        }

        gridRoot = CreateChild(contentRoot, "Inventory Items", Vector2.zero, DesignSize);
        statusLabel = AddText(contentRoot, "Inventory Status", D(StatusPosition), StatusSize, StatusFontSize, TextAlignmentOptions.Center, new Color(0.18f, 0.14f, 0.1f, 0.82f));
    }

    private void ShowCategory(InventoryItemCategory category)
    {
        activeCategory = category;
        RefreshGrid();
    }

    private void SortInventory()
    {
        if (saveData == null || saveData.items == null)
            return;

        saveData.items.Sort(CompareInventoryItems);
        Save();
        RefreshGrid();
        SetStatus("Sorted by rarity, owned date, A-Z");
    }

    private static int CompareInventoryItems(InventoryItemData left, InventoryItemData right)
    {
        int rarity = right.rarity.CompareTo(left.rarity);
        if (rarity != 0)
            return rarity;

        int ownedDate = DateTime.Compare(ParseOwnedAt(right.ownedAtUtc), ParseOwnedAt(left.ownedAtUtc));
        if (ownedDate != 0)
            return ownedDate;

        return string.Compare(left.displayName, right.displayName, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshGrid()
    {
        if (!gridRoot || saveData == null)
            return;

        for (int i = gridRoot.childCount - 1; i >= 0; i--)
            Destroy(gridRoot.GetChild(i).gameObject);

        List<InventoryItemData> visibleItems = GetVisibleItems();
        int slotCount = ItemGridColumns * ItemGridRows;
        int visibleCount = Mathf.Min(visibleItems.Count, slotCount);
        for (int i = 0; i < visibleCount; i++)
            AddItemCard(gridRoot, visibleItems[i], GetSlotPosition(i), ItemGridSlotSize);

        string categoryName = activeCategory.HasValue ? CategoryDisplayName(activeCategory.Value) : "All items";
        SetStatus($"{categoryName}: {visibleItems.Count}/{slotCount}");
    }

    private List<InventoryItemData> GetVisibleItems()
    {
        List<InventoryItemData> visibleItems = new List<InventoryItemData>();
        if (!activeCategory.HasValue || activeCategory.Value == InventoryItemCategory.Music)
            visibleItems.AddRange(BuiltInMusicItems);
        if (!activeCategory.HasValue || activeCategory.Value == InventoryItemCategory.Skin)
            visibleItems.AddRange(BuiltInSkinItems);

        if (saveData.items == null)
            return visibleItems;

        for (int i = 0; i < saveData.items.Count; i++)
        {
            InventoryItemData item = saveData.items[i];
            if (!activeCategory.HasValue || item.category == activeCategory.Value)
                visibleItems.Add(item);
        }
        return visibleItems;
    }

    private void AddItemCard(RectTransform parent, InventoryItemData item, Vector2 position, Vector2 size)
    {
        if (item != null && item.category == InventoryItemCategory.Music && TryGetMusicPack(item, out GameMusicPack pack))
        {
            AddMusicPackCard(parent, item, pack, position, size);
            return;
        }

        if (item != null && item.category == InventoryItemCategory.Skin && TryGetPieceSkin(item, out PieceSkinDefinition skin))
        {
            AddPieceSkinCard(parent, item, skin, position, size);
            return;
        }

        RectTransform card = CreateChild(parent, item.displayName, position, size);
        Image background = card.gameObject.AddComponent<Image>();
        background.color = RarityColor(item.rarity, 0.46f);
        background.raycastTarget = false;

        Outline outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = RarityColor(item.rarity, 0.78f);
        outline.effectDistance = new Vector2(2f, -2f);

        AddText(card, "Rarity", new Vector2(-82f, 42f), new Vector2(60f, 30f), 23f, TextAlignmentOptions.Center, Color.black).text = $"{item.rarity}*";
        AddText(card, "Category", new Vector2(50f, 42f), new Vector2(118f, 30f), 18f, TextAlignmentOptions.Right, new Color(0.16f, 0.12f, 0.08f, 0.9f)).text = CategoryDisplayName(item.category);
        AddText(card, "Name", new Vector2(0f, 3f), new Vector2(size.x - 24f, 46f), 25f, TextAlignmentOptions.Center, Color.black).text = item.displayName;
        AddText(card, "Owned", new Vector2(0f, -42f), new Vector2(size.x - 24f, 27f), 16f, TextAlignmentOptions.Center, new Color(0.2f, 0.16f, 0.12f, 0.85f)).text = FormatOwnedDate(item.ownedAtUtc);
    }

    private void AddMusicPackCard(RectTransform parent, InventoryItemData item, GameMusicPack pack, Vector2 position, Vector2 size)
    {
        RectTransform card = CreateChild(parent, item.displayName, position, size);
        Image background = card.gameObject.AddComponent<Image>();
        background.color = RarityColor(item.rarity, 0.34f);
        background.raycastTarget = true;

        Sprite cover = GetSpriteFromProjectPath(item.imageAssetPath, false);
        Vector2 coverSize = new Vector2(size.x - 14f, size.y - 10f);
        Image image = AddImage(card, "Music Pack Cover", cover, Vector2.zero, coverSize);
        image.preserveAspect = true;
        image.raycastTarget = false;

        Outline outline = card.gameObject.AddComponent<Outline>();
        bool selected = GameMusicManager.ActivePack == pack;
        outline.effectColor = selected ? new Color(1f, 0.92f, 0.22f, 0.95f) : RarityColor(item.rarity, 0.78f);
        outline.effectDistance = selected ? new Vector2(4f, -4f) : new Vector2(2f, -2f);

        Image labelBacking = AddImage(card, "Music Pack Label Backing", null, new Vector2(0f, -44f), new Vector2(size.x, 50f));
        labelBacking.color = new Color(0f, 0f, 0f, 0.55f);
        labelBacking.preserveAspect = false;

        AddText(card, "Music Pack Name", new Vector2(0f, -44f), new Vector2(size.x - 18f, 42f), 21f, TextAlignmentOptions.Center, Color.white).text =
            selected ? $"{item.displayName} - Active" : item.displayName;

        Button button = card.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;
        button.onClick.AddListener(() => SelectMusicPack(pack));

        HandDrawnPressable pressable = card.gameObject.AddComponent<HandDrawnPressable>();
        pressable.Configure(1.035f, 0.95f, 0.85f, new Color(1f, 0.97f, 0.86f, 1f));
    }

    private void AddPieceSkinCard(RectTransform parent, InventoryItemData item, PieceSkinDefinition skin, Vector2 position, Vector2 size)
    {
        RectTransform card = CreateChild(parent, item.displayName, position, size);
        Image background = card.gameObject.AddComponent<Image>();
        background.color = new Color(1f, 0.98f, 0.9f, 0.88f);
        background.raycastTarget = true;

        Outline outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = skin.IsDefault ? new Color(0f, 0f, 0f, 0.42f) : skin.SwatchColor;
        outline.effectDistance = new Vector2(3f, -3f);

        Image swatch = AddImage(card, "Skin Swatch", null, new Vector2(-68f, 10f), new Vector2(64f, 64f));
        swatch.color = skin.SwatchColor;
        swatch.preserveAspect = false;

        Outline swatchOutline = swatch.gameObject.AddComponent<Outline>();
        swatchOutline.effectColor = Color.black;
        swatchOutline.effectDistance = new Vector2(2f, -2f);

        AddText(card, "Rarity", new Vector2(-82f, 42f), new Vector2(60f, 30f), 23f, TextAlignmentOptions.Center, Color.black).text = $"{item.rarity}*";
        AddText(card, "Category", new Vector2(50f, 42f), new Vector2(118f, 30f), 18f, TextAlignmentOptions.Right, new Color(0.16f, 0.12f, 0.08f, 0.9f)).text = "Skin";
        AddText(card, "Skin Name", new Vector2(38f, 8f), new Vector2(130f, 42f), 24f, TextAlignmentOptions.Center, GetReadableTextColor(new Color(1f, 0.98f, 0.9f, 1f))).text = skin.DisplayName;
        AddText(card, "Skin Status", new Vector2(38f, -38f), new Vector2(138f, 28f), 16f, TextAlignmentOptions.Center, new Color(0.18f, 0.14f, 0.1f, 0.85f)).text =
            skin.IsDefault ? "Always owned" : "Available";

        Button button = card.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;
        button.onClick.AddListener(() => SetStatus($"Skin: {skin.DisplayName}. Select it before starting a match."));

        HandDrawnPressable pressable = card.gameObject.AddComponent<HandDrawnPressable>();
        pressable.Configure(1.035f, 0.95f, 0.85f, new Color(1f, 0.97f, 0.86f, 1f));
    }

    private Vector2 GetSlotPosition(int index)
    {
        int column = index % ItemGridColumns;
        int row = index / ItemGridColumns;
        Vector2 designPosition = ItemGridFirstSlot + new Vector2(ItemGridSlotSpacing.x * column, ItemGridSlotSpacing.y * row);
        return D(designPosition);
    }

    private UnityAction GetAction(InventoryButtonAction action)
    {
        switch (action)
        {
            case InventoryButtonAction.Music:
                return () => ShowCategory(InventoryItemCategory.Music);
            case InventoryButtonAction.Skin:
                return () => ShowCategory(InventoryItemCategory.Skin);
            case InventoryButtonAction.Effect:
                return () => ShowCategory(InventoryItemCategory.Effect);
            case InventoryButtonAction.Sort:
                return SortInventory;
            case InventoryButtonAction.Close:
                return () => closeAction?.Invoke();
            default:
                return null;
        }
    }

    private void SetStatus(string message)
    {
        if (statusLabel)
            statusLabel.text = message;
    }

    private Button AddButton(RectTransform parent, string name, Sprite sprite, Vector2 position, Vector2 size, UnityAction action, float hoverScale, Color hoverTint)
    {
        Image image = AddImage(parent, name, sprite, position, size);
        image.raycastTarget = true;

        Button button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        HandDrawnPressable pressable = image.gameObject.AddComponent<HandDrawnPressable>();
        pressable.Configure(hoverScale, 0.95f, 0.85f, hoverTint);
        return button;
    }

    private Image AddFullImage(RectTransform parent, string name, Sprite sprite)
    {
        Image image = AddImage(parent, name, sprite, Vector2.zero, DesignSize);
        image.preserveAspect = false;
        return image;
    }

    private Image AddImage(RectTransform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
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
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(11f, fontSize * 0.55f);
        text.fontSizeMax = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.text = string.Empty;
        return text;
    }

    private RectTransform CreateChild(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private Vector2 D(Vector2 designPosition)
    {
        return new Vector2(designPosition.x - DesignWidth * 0.5f, DesignHeight * 0.5f - designPosition.y);
    }

    private Sprite GetSprite(string fileName)
    {
        if (sprites.TryGetValue(fileName, out Sprite sprite))
            return sprite;

        sprite = LoadSprite(fileName, !string.Equals(fileName, "InventoryBlank.png", StringComparison.Ordinal));
        sprites[fileName] = sprite;
        return sprite;
    }

    private Sprite GetSpriteFromProjectPath(string assetPath, bool trimTransparent)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        if (sprites.TryGetValue(assetPath, out Sprite sprite))
            return sprite;

        sprite = LoadSpriteFromPath(ProjectAssetPath(assetPath), trimTransparent);
        sprites[assetPath] = sprite;
        return sprite;
    }

    private void LoadSprites()
    {
        LoadSpriteToCache("InventoryBlank.png", false);
        LoadSpriteToCache("MusicButton.png", true);
        LoadSpriteToCache("SkinButton.png", true);
        LoadSpriteToCache("EffectButton.png", true);
        LoadSpriteToCache("SortButton.png", true);
        LoadSpriteToCache("ExitButton.png", true);
    }

    private void LoadSpriteToCache(string fileName, bool trimTransparent)
    {
        sprites[fileName] = LoadSprite(fileName, trimTransparent);
    }

    private Sprite LoadSprite(string fileName, bool trimTransparent)
    {
        return LoadSpriteFromPath(FullAssetPath(fileName), trimTransparent);
    }

    private Sprite LoadSpriteFromPath(string path, bool trimTransparent)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[InventoryMenu] Missing asset: {path}");
            return null;
        }

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        if (!texture.LoadImage(bytes))
        {
            Destroy(texture);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(path);
        texture.Apply(true, false);
        texture.filterMode = FilterMode.Trilinear;
        texture.anisoLevel = 4;
        Rect rect = trimTransparent ? FindOpaqueBounds(texture) : new Rect(0f, 0f, texture.width, texture.height);
        Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
        runtimeSprites.Add(sprite);
        return sprite;
    }

    private Rect FindOpaqueBounds(Texture2D texture)
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

    private void LoadSave()
    {
        string json = PlayerPrefs.GetString(SaveKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                saveData = JsonUtility.FromJson<InventorySaveData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[InventoryMenu] Failed to load save data: {exception.Message}");
            }
        }

        if (saveData == null)
            saveData = InventorySaveData.CreateDefault();
        if (saveData.items == null)
            saveData.items = new List<InventoryItemData>();

        if (saveData.items.RemoveAll(IsPlaceholderInventoryItem) > 0)
            Save();
    }

    private void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saveData));
        PlayerPrefs.Save();
    }

    private string SaveKey
    {
        get
        {
            string user = string.IsNullOrWhiteSpace(PlayerAuthService.Username) ? PlayerAuthService.CurrentDisplayName : PlayerAuthService.Username;
            return $"chess_but_weird_inventory_{user}";
        }
    }

    private static string FullAssetPath(string fileName)
    {
        return Path.Combine(Directory.GetCurrentDirectory(), AssetFolder, fileName);
    }

    private static string ProjectAssetPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
    }

    private static bool TryGetMusicPack(InventoryItemData item, out GameMusicPack pack)
    {
        if (item != null && GameMusicManager.TryParsePackId(item.musicPackId, out pack))
            return true;

        pack = GameMusicPack.Default;
        return false;
    }

    private static InventoryItemData[] CreateBuiltInSkinItems()
    {
        IReadOnlyList<PieceSkinDefinition> skins = PieceSkinCatalog.All;
        InventoryItemData[] items = new InventoryItemData[skins.Count];
        for (int i = 0; i < skins.Count; i++)
        {
            PieceSkinDefinition skin = skins[i];
            items[i] = new InventoryItemData($"piece_skin_{skin.Id}", skin.DisplayName, InventoryItemCategory.Skin, skin.IsDefault ? 3 : 5, "Built-in")
            {
                skinId = skin.Id,
                builtIn = true
            };
        }

        return items;
    }

    private static bool TryGetPieceSkin(InventoryItemData item, out PieceSkinDefinition skin)
    {
        if (item != null && !string.IsNullOrWhiteSpace(item.skinId))
        {
            skin = PieceSkinCatalog.Get(item.skinId);
            return true;
        }

        skin = PieceSkinCatalog.Get(PieceSkinCatalog.DefaultSkinId);
        return false;
    }

    private void SelectMusicPack(GameMusicPack pack)
    {
        GameMusicManager.SetActivePack(pack);
        RefreshGrid();
        SetStatus($"Music pack: {GameMusicManager.GetPackDisplayName(pack)}");
    }

    private static bool IsPlaceholderInventoryItem(InventoryItemData item)
    {
        return item != null && !string.IsNullOrWhiteSpace(item.id) && PlaceholderItemIds.Contains(item.id);
    }

    private static DateTime ParseOwnedAt(string value)
    {
        if (DateTime.TryParse(value, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime date))
            return date;
        return DateTime.MinValue;
    }

    private static string FormatOwnedDate(string value)
    {
        DateTime date = ParseOwnedAt(value);
        return date == DateTime.MinValue ? "Owned: unknown" : $"Owned: {date:yyyy-MM-dd}";
    }

    private static string CategoryDisplayName(InventoryItemCategory category)
    {
        switch (category)
        {
            case InventoryItemCategory.Effect:
                return "Effects";
            case InventoryItemCategory.Music:
                return "Music";
            case InventoryItemCategory.Skin:
                return "Skins";
            default:
                return category.ToString();
        }
    }

    private static Color RarityColor(int rarity, float alpha)
    {
        if (rarity >= 5)
            return new Color(1f, 0.78f, 0.22f, alpha);
        if (rarity >= 4)
            return new Color(0.76f, 0.54f, 1f, alpha);
        if (rarity >= 3)
            return new Color(0.58f, 0.8f, 1f, alpha);
        return new Color(0.74f, 0.9f, 0.68f, alpha);
    }

    private static Color GetReadableTextColor(Color background)
    {
        float luminance = background.r * 0.299f + background.g * 0.587f + background.b * 0.114f;
        return luminance < 0.45f ? Color.white : Color.black;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < runtimeSprites.Count; i++)
        {
            if (!runtimeSprites[i])
                continue;

            Texture2D texture = runtimeSprites[i].texture;
            Destroy(runtimeSprites[i]);
            if (texture)
                Destroy(texture);
        }
        runtimeSprites.Clear();
    }
}

public sealed class InventoryContentRootFitter : MonoBehaviour
{
    private RectTransform rectTransform;
    private float referenceWidth = 1672f;
    private float referenceHeight = 941f;
    private float maxViewportScale = 1f;

    public void Configure(float width, float height, float viewportScale)
    {
        referenceWidth = Mathf.Max(1f, width);
        referenceHeight = Mathf.Max(1f, height);
        maxViewportScale = Mathf.Clamp(viewportScale, 0.1f, 1f);
        Apply();
    }

    private void Awake()
    {
        rectTransform = transform as RectTransform;
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        Apply();
    }

    private void Apply()
    {
        if (!rectTransform)
            rectTransform = transform as RectTransform;
        if (!rectTransform)
            return;

        RectTransform parent = rectTransform.parent as RectTransform;
        if (!parent)
            return;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(referenceWidth, referenceHeight);

        float scale = Mathf.Min(parent.rect.width / referenceWidth, parent.rect.height / referenceHeight) * maxViewportScale;
        if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale))
            scale = maxViewportScale;
        rectTransform.localScale = new Vector3(scale, scale, 1f);
    }
}

internal readonly struct InventoryButtonLayout
{
    public readonly string name;
    public readonly string spriteFile;
    public readonly InventoryButtonAction action;
    public readonly Vector2 designPosition;
    public readonly Vector2 size;
    public readonly float hoverScale;
    public readonly Color hoverTint;

    public InventoryButtonLayout(string name, string spriteFile, InventoryButtonAction action, Vector2 designPosition, Vector2 size, float hoverScale, Color hoverTint)
    {
        this.name = name;
        this.spriteFile = spriteFile;
        this.action = action;
        this.designPosition = designPosition;
        this.size = size;
        this.hoverScale = hoverScale;
        this.hoverTint = hoverTint;
    }
}

internal enum InventoryButtonAction
{
    Music,
    Skin,
    Effect,
    Sort,
    Close
}

[Serializable]
public sealed class InventorySaveData
{
    public List<InventoryItemData> items = new List<InventoryItemData>();

    public static InventorySaveData CreateDefault()
    {
        return new InventorySaveData
        {
            items = new List<InventoryItemData>()
        };
    }
}

[Serializable]
public sealed class InventoryItemData
{
    public string id;
    public string displayName;
    public InventoryItemCategory category;
    public int rarity;
    public string ownedAtUtc;
    public string musicPackId;
    public string skinId;
    public string imageAssetPath;
    public bool builtIn;

    public InventoryItemData()
    {
    }

    public InventoryItemData(string id, string displayName, InventoryItemCategory category, int rarity, string ownedAtUtc)
    {
        this.id = id;
        this.displayName = displayName;
        this.category = category;
        this.rarity = Mathf.Clamp(rarity, 1, 5);
        this.ownedAtUtc = ownedAtUtc;
    }
}

public enum InventoryItemCategory
{
    Effect,
    Music,
    Skin
}
