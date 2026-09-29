using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UI = SketchbookUI;

public sealed class InventoryMenuController : MonoBehaviour
{
    private const float DesignWidth = 1672f;
    private const float DesignHeight = 941f;
    private const int Columns = 3;
    private const int Rows = 2;
    private const int PageSize = Columns * Rows;
    private static readonly Color CommonTierColor = new Color32(202, 207, 214, 255);
    private static readonly Color RareTierColor = new Color32(126, 181, 240, 255);
    private static readonly Color EpicTierColor = new Color32(181, 145, 229, 255);
    private static readonly Color LegendaryTierColor = new Color32(251, 184, 82, 255);

    private enum SkinTab { Chess, Board }

    private sealed class InventoryEntry
    {
        public string itemId;
        public string name;
        public string type;
        public string rarity;
        public string unityAssetKey;
        public bool owned;
        public bool isEquipped;
        public bool isActive;
    }

    private readonly InventoryService inventory = new InventoryService();
    private readonly List<InventoryEntry> items = new List<InventoryEntry>();

    private RectTransform root;
    private RectTransform contentRoot;
    private RectTransform itemRoot;
    private HandDrawnRoundedGraphic chessLoadoutCard;
    private HandDrawnRoundedGraphic boardLoadoutCard;
    private TextMeshProUGUI chessLoadoutName;
    private TextMeshProUGUI chessLoadoutHint;
    private TextMeshProUGUI boardLoadoutName;
    private TextMeshProUGUI boardLoadoutHint;
    private TextMeshProUGUI statusLabel;
    private TextMeshProUGUI countLabel;
    private TextMeshProUGUI pageLabel;
    private TextMeshProUGUI chessTabLabel;
    private TextMeshProUGUI boardTabLabel;
    private Button chessTabButton;
    private Button boardTabButton;
    private Button previousButton;
    private Button nextButton;
    private UnityAction closeAction;
    private SkinTab activeTab = SkinTab.Chess;
    private int page;
    private bool loading;
    private bool equipping;
    private string loadedUserId;

    public void Initialize(RectTransform newRoot, UnityAction newCloseAction)
    {
        root = newRoot;
        closeAction = newCloseAction;
        Build();
    }

    public void Open()
    {
        page = 0;
        items.Clear();
        RefreshView();
        RefreshFromServer();
    }

    private void Build()
    {
        Image blocker = root.gameObject.GetComponent<Image>() ?? root.gameObject.AddComponent<AntialiasedMenuImage>();
        blocker.color = new Color(0f, 0f, 0f, 0.01f);
        blocker.raycastTarget = true;

        contentRoot = UI.Node(root, "Inventory Content", new Rect(0, 0, DesignWidth, DesignHeight));
        contentRoot.gameObject.AddComponent<InventoryContentRootFitter>()
            .Configure(DesignWidth, DesignHeight, 1f);
        UI.Background(contentRoot);

        UI.Text(contentRoot, "Eyebrow", "CHESS BUT WEIRD  /  COLLECTION",
            new Rect(78, 38, 700, 30), 21, UI.Muted);
        UI.Card(contentRoot, "Title Marker", new Rect(76, 132, 391, 22), UI.Yellow, -1f, false, 5f);
        UI.Text(contentRoot, "Title", "Inventory", new Rect(76, 68, 790, 86), 65);
        UI.Text(contentRoot, "Subtitle", "Choose your set for the next match",
            new Rect(594, 99, 730, 43), 26, UI.Muted);
        UI.Button(contentRoot, "Back to Menu", "BACK TO MENU",
            new Rect(1346, 79, 244, 62), UI.Pink, () => closeAction?.Invoke(), 24);

        RectTransform loadout = UI.Card(contentRoot, "Loadout Panel",
            new Rect(72, 184, 470, 650), UI.White).rectTransform;
        UI.Tape(loadout, 168, -11, 126, -3);
        UI.Text(loadout, "Loadout Heading", "LOADOUT", new Rect(30, 28, 390, 54), 42);
        UI.Text(loadout, "Loadout Note", "Your equipped skins", new Rect(31, 79, 390, 35),
            22, UI.Muted);
        BuildLoadoutSlot(loadout, SkinTab.Chess, new Rect(27, 138, 416, 215),
            UI.Paper, SketchbookDoodle.Shape.Pawn, out chessLoadoutCard,
            out chessLoadoutName, out chessLoadoutHint);
        BuildLoadoutSlot(loadout, SkinTab.Board, new Rect(27, 382, 416, 215),
            UI.Paper, SketchbookDoodle.Shape.Board, out boardLoadoutCard,
            out boardLoadoutName, out boardLoadoutHint);

        RectTransform collection = UI.Card(contentRoot, "Skin Collection",
            new Rect(568, 184, 1032, 650), UI.White).rectTransform;
        UI.Text(collection, "Collection Heading", "SKIN COLLECTION",
            new Rect(29, 26, 620, 50), 37);
        countLabel = UI.Text(collection, "Collection Count", "",
            new Rect(680, 32, 320, 38), 23, UI.Muted, TextAlignmentOptions.Right);

        chessTabButton = UI.Button(collection, "Chess Tab", "CHESS",
            new Rect(28, 103, 237, 68), UI.White,
            () => ShowTab(SkinTab.Chess), 26);
        boardTabButton = UI.Button(collection, "Board Tab", "BOARD",
            new Rect(283, 103, 237, 68), UI.White,
            () => ShowTab(SkinTab.Board), 26);
        chessTabLabel = chessTabButton.GetComponentInChildren<TextMeshProUGUI>();
        boardTabLabel = boardTabButton.GetComponentInChildren<TextMeshProUGUI>();
        UI.Text(collection, "Collection Help", "Tap an owned skin to equip it",
            new Rect(567, 119, 432, 36), 21, UI.Muted, TextAlignmentOptions.Right);

        itemRoot = UI.Node(collection, "Skin Items", new Rect(27, 195, 978, 390));
        previousButton = UI.Button(collection, "Previous Page", "<",
            new Rect(32, 584, 66, 52), UI.Paper, () => ChangePage(-1), 29);
        pageLabel = UI.Text(collection, "Page", "", new Rect(113, 591, 807, 38),
            23, UI.Muted, TextAlignmentOptions.Center);
        nextButton = UI.Button(collection, "Next Page", ">",
            new Rect(934, 584, 66, 52), UI.Paper, () => ChangePage(1), 29);

        statusLabel = UI.Text(contentRoot, "Inventory Status", "",
            new Rect(115, 852, 1442, 53), 23, UI.Muted, TextAlignmentOptions.Center, true);
        RefreshView();
    }

    private static void BuildLoadoutSlot(
        RectTransform parent, SkinTab tab, Rect area, Color fill, SketchbookDoodle.Shape icon,
        out HandDrawnRoundedGraphic slotCard, out TextMeshProUGUI itemName, out TextMeshProUGUI hint)
    {
        var graphic = UI.Card(parent, tab + " Loadout Slot", area, fill, 0f, true, 19f);
        slotCard = graphic;
        graphic.raycastTarget = true;
        var button = graphic.gameObject.AddComponent<Button>();
        button.targetGraphic = graphic;
        button.transition = Selectable.Transition.None;
        // The caller wires the tab action after the card is built.
        UI.Doodle(graphic.transform, tab + " Icon", new Rect(23, 51, 86, 95), icon, UI.Ink, -4);
        UI.Text(graphic.transform, tab + " Slot Label",
            tab == SkinTab.Chess ? "CHESS SKIN" : "BOARD SKIN",
            new Rect(124, 23, 266, 37), 24, UI.Muted);
        itemName = UI.Text(graphic.transform, tab + " Equipped Name", "No skin equipped",
            new Rect(122, 65, 270, 73), 31, UI.Ink, TextAlignmentOptions.MidlineLeft, true);
        hint = UI.Text(graphic.transform, tab + " Slot Hint", "Choose a skin",
            new Rect(123, 150, 269, 37), 19, UI.Muted);
        button.onClick.AddListener(() =>
        {
            InventoryMenuController controller = parent.GetComponentInParent<InventoryMenuController>();
            if (controller) controller.ShowTab(tab);
        });
    }

    private async void RefreshFromServer()
    {
        if (loading) return;
        string userId = PlayerAuthService.CurrentApiUser?.userId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            SetStatus("Sign in to load your inventory.");
            return;
        }

        loading = true;
        loadedUserId = userId;
        RefreshView();
        SetStatus("Loading your skins...");
        try
        {
            Task<List<ItemCatalogResponse>> catalogTask = inventory.GetItemsAsync();
            Task<List<InventoryItemResponse>> ownedTask = inventory.GetInventoryAsync();
            await Task.WhenAll(catalogTask, ownedTask);
            if (!this || userId != PlayerAuthService.CurrentApiUser?.userId) return;

            MergeItems(catalogTask.Result, ownedTask.Result);
            SyncEquippedVisuals();
            RefreshView();
            SetStatus("Choose an owned skin on the right to update your loadout.");
        }
        catch (Exception error)
        {
            if (this) SetStatus("Could not load inventory: " + PlayerNotificationText.FromException(error));
        }
        finally
        {
            if (this)
            {
                loading = false;
                if (root && root.gameObject.activeInHierarchy &&
                    loadedUserId != PlayerAuthService.CurrentApiUser?.userId)
                {
                    items.Clear();
                    RefreshView();
                    RefreshFromServer();
                }
            }
        }
    }

    private void MergeItems(List<ItemCatalogResponse> catalog, List<InventoryItemResponse> owned)
    {
        items.Clear();
        var ownership = new Dictionary<string, InventoryItemResponse>(StringComparer.OrdinalIgnoreCase);
        if (owned != null)
            foreach (InventoryItemResponse item in owned)
                if (item != null && !string.IsNullOrWhiteSpace(item.itemId))
                    ownership[item.itemId] = item;

        if (catalog != null)
            foreach (ItemCatalogResponse item in catalog)
            {
                if (item == null || !item.isActive || string.IsNullOrWhiteSpace(item.itemId)) continue;
                ownership.TryGetValue(item.itemId, out InventoryItemResponse playerItem);
                items.Add(new InventoryEntry
                {
                    itemId = item.itemId,
                    name = DisplayName(item.name, item.code, item.itemId),
                    type = item.type,
                    rarity = item.rarity,
                    unityAssetKey = item.unityAssetKey,
                    owned = playerItem != null,
                    isEquipped = playerItem != null && playerItem.isEquipped,
                    isActive = true
                });
                ownership.Remove(item.itemId);
            }

        // Keep owned legacy items visible if the catalog no longer lists them.
        foreach (InventoryItemResponse item in ownership.Values)
            items.Add(new InventoryEntry
            {
                itemId = item.itemId,
                name = DisplayName(item.name, item.code, item.itemId),
                type = item.type,
                rarity = item.rarity,
                unityAssetKey = item.unityAssetKey,
                owned = true,
                isEquipped = item.isEquipped,
                isActive = false
            });
    }

    private static string DisplayName(string name, string code, string id) =>
        !string.IsNullOrWhiteSpace(name) ? name :
        !string.IsNullOrWhiteSpace(code) ? code : id;

    private void ShowTab(SkinTab tab)
    {
        activeTab = tab;
        page = 0;
        RefreshView();
        SetStatus(tab == SkinTab.Chess ? "Choose a chess skin for your loadout." :
            "Choose a board skin for your loadout.");
    }

    private void ChangePage(int direction)
    {
        int maxPage = Mathf.Max(0, (GetVisibleItems().Count - 1) / PageSize);
        page = Mathf.Clamp(page + direction, 0, maxPage);
        RefreshView();
    }

    private List<InventoryEntry> GetVisibleItems()
    {
        var visible = new List<InventoryEntry>();
        foreach (InventoryEntry item in items)
            if (activeTab == SkinTab.Chess ? IsChessType(item.type) : IsBoardType(item.type))
                visible.Add(item);

        visible.Sort((left, right) =>
        {
            int equippedFirst = right.isEquipped.CompareTo(left.isEquipped);
            if (equippedFirst != 0) return equippedFirst;
            int ownedFirst = right.owned.CompareTo(left.owned);
            if (ownedFirst != 0) return ownedFirst;
            int rarity = RarityRank(right.rarity).CompareTo(RarityRank(left.rarity));
            return rarity != 0 ? rarity : string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase);
        });
        return visible;
    }

    private void RefreshView()
    {
        if (!contentRoot || !itemRoot) return;
        RefreshLoadout();
        RefreshTabs();
        RefreshItems();
    }

    private void RefreshLoadout()
    {
        UpdateLoadout(SkinTab.Chess, chessLoadoutName, chessLoadoutHint);
        UpdateLoadout(SkinTab.Board, boardLoadoutName, boardLoadoutHint);
    }

    private void UpdateLoadout(SkinTab tab, TextMeshProUGUI name, TextMeshProUGUI hint)
    {
        InventoryEntry equipped = null;
        foreach (InventoryEntry item in items)
            if (item.isEquipped && (tab == SkinTab.Chess ? IsChessType(item.type) : IsBoardType(item.type)))
            {
                equipped = item;
                break;
            }

        name.text = equipped != null ? equipped.name : loading ? "Loading..." : "No skin equipped";
        hint.text = equipped != null ? "EQUIPPED  /  TAP TO CHANGE" :
            tab == SkinTab.Chess ? "TAP TO CHOOSE CHESS SKIN" : "TAP TO CHOOSE BOARD SKIN";
        HandDrawnRoundedGraphic slotCard = tab == SkinTab.Chess ? chessLoadoutCard : boardLoadoutCard;
        slotCard.color = equipped != null ? RarityColor(equipped.rarity) : UI.Paper;
    }

    private void RefreshTabs()
    {
        int chessOwned = 0, chessTotal = 0, boardOwned = 0, boardTotal = 0;
        foreach (InventoryEntry item in items)
        {
            if (IsChessType(item.type))
            {
                chessTotal++;
                if (item.owned) chessOwned++;
            }
            else if (IsBoardType(item.type))
            {
                boardTotal++;
                if (item.owned) boardOwned++;
            }
        }

        chessTabLabel.text = $"CHESS  {chessOwned}/{chessTotal}";
        boardTabLabel.text = $"BOARD  {boardOwned}/{boardTotal}";
        chessTabButton.targetGraphic.color = activeTab == SkinTab.Chess ? UI.White : UI.Paper;
        boardTabButton.targetGraphic.color = activeTab == SkinTab.Board ? UI.White : UI.Paper;
        chessTabLabel.fontStyle = activeTab == SkinTab.Chess ? FontStyles.Bold : FontStyles.Normal;
        boardTabLabel.fontStyle = activeTab == SkinTab.Board ? FontStyles.Bold : FontStyles.Normal;
    }

    private void RefreshItems()
    {
        for (int i = itemRoot.childCount - 1; i >= 0; i--)
        {
            GameObject card = itemRoot.GetChild(i).gameObject;
            card.SetActive(false);
            Destroy(card);
        }

        List<InventoryEntry> visible = GetVisibleItems();
        page = Mathf.Clamp(page, 0, Mathf.Max(0, (visible.Count - 1) / PageSize));
        int first = page * PageSize;
        int count = Mathf.Min(PageSize, visible.Count - first);
        for (int i = 0; i < count; i++)
        {
            int column = i % Columns;
            int row = i / Columns;
            AddItemCard(visible[first + i], new Rect(column * 327f, row * 189f, 307f, 171f));
        }

        if (visible.Count == 0)
            UI.Text(itemRoot, "Empty Category",
                loading ? "Loading skins..." : "No skins in this category yet.",
                new Rect(45, 105, 890, 130), 32, UI.Muted, TextAlignmentOptions.Center, true);

        int owned = 0;
        foreach (InventoryEntry item in visible)
            if (item.owned) owned++;
        countLabel.text = $"{owned} OWNED  /  {visible.Count} ITEMS";
        int pages = Mathf.Max(1, (visible.Count + PageSize - 1) / PageSize);
        pageLabel.text = $"PAGE {page + 1} / {pages}";
        previousButton.interactable = page > 0;
        nextButton.interactable = page + 1 < pages;
    }

    private void AddItemCard(InventoryEntry item, Rect area)
    {
        Color fill = RarityColor(item.rarity);
        var graphic = UI.Card(itemRoot, "Item " + item.itemId, area, fill, 0f, true, 15f);
        RectTransform card = graphic.rectTransform;
        UI.Text(card, "Rarity", string.IsNullOrWhiteSpace(item.rarity) ? "ITEM" : item.rarity.ToUpperInvariant(),
            new Rect(18, 13, 270, 28), 18, UI.Muted);
        UI.Doodle(card, "Skin Icon", new Rect(20, 48, 66, 70),
            activeTab == SkinTab.Chess ? SketchbookDoodle.Shape.Pawn : SketchbookDoodle.Shape.Board,
            item.owned ? UI.Ink : UI.Muted);
        UI.Text(card, "Item Name", item.name, new Rect(99, 39, 190, 73),
            25, UI.Ink, TextAlignmentOptions.MidlineLeft, true);
        string state = item.isEquipped ? "EQUIPPED" : !item.owned ? "LOCKED" :
            !item.isActive ? "UNAVAILABLE" : "TAP TO EQUIP";
        UI.Text(card, "Item State", state, new Rect(99, 119, 190, 33),
            18, item.isEquipped ? UI.Ink : UI.Muted);

        if (!item.owned || !item.isActive || item.isEquipped) return;
        graphic.raycastTarget = true;
        Button button = graphic.gameObject.AddComponent<Button>();
        button.targetGraphic = graphic;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => EquipItem(item));
    }

    private async void EquipItem(InventoryEntry item)
    {
        if (equipping || loading || item == null || !item.owned || !item.isActive) return;
        string userId = PlayerAuthService.CurrentApiUser?.userId;
        if (string.IsNullOrWhiteSpace(userId)) return;

        equipping = true;
        SetStatus("Equipping " + item.name + "...");
        try
        {
            EquipItemResponse response = await inventory.EquipAsync(item.itemId);
            if (!this || userId != PlayerAuthService.CurrentApiUser?.userId) return;
            if (response == null || !response.success || response.equipped == null)
                throw new ApiException(response?.message ?? "The server did not confirm equipment.");

            PlayerAuthService.CurrentApiUser.equipped = response.equipped;
            foreach (InventoryEntry candidate in items)
                candidate.isEquipped = candidate.owned &&
                    (IsChessType(candidate.type)
                        ? string.Equals(candidate.itemId, response.equipped.chessSkinId, StringComparison.OrdinalIgnoreCase)
                        : IsBoardType(candidate.type) &&
                          string.Equals(candidate.itemId, response.equipped.boardSkinId, StringComparison.OrdinalIgnoreCase));

            bool hasVisual = ApplyLocalVisual(item);
            RefreshView();
            SetStatus(hasVisual ? "Equipped " + item.name + "." :
                "Equipped " + item.name + " on your account. Local art is unavailable.");
        }
        catch (Exception error)
        {
            if (this) SetStatus("Could not equip skin: " + PlayerNotificationText.FromException(error));
        }
        finally
        {
            if (this) equipping = false;
        }
    }

    private void SyncEquippedVisuals()
    {
        CosmeticSelection selection = CosmeticSelection.Load();
        selection.whiteSkinId = CosmeticSelection.LowPolyId;
        selection.blackSkinId = CosmeticSelection.LowPolyId;
        selection.boardId = CosmeticSelection.LowPolyId;
        foreach (InventoryEntry item in items)
        {
            if (!item.isEquipped) continue;
            if (IsChessType(item.type))
                selection.whiteSkinId = selection.blackSkinId =
                    IsKnownPieceSkin(item.unityAssetKey) ? item.unityAssetKey : CosmeticSelection.LowPolyId;
            else if (IsBoardType(item.type))
                selection.boardId = string.Equals(item.unityAssetKey, CosmeticSelection.LowPolyId,
                    StringComparison.OrdinalIgnoreCase) ? item.unityAssetKey : CosmeticSelection.LowPolyId;
        }
        selection.Save();
    }

    private static bool ApplyLocalVisual(InventoryEntry item)
    {
        string key = item.unityAssetKey;
        CosmeticSelection selection = CosmeticSelection.Load();
        bool hasVisual;
        if (IsChessType(item.type))
        {
            hasVisual = IsKnownPieceSkin(key);
            selection.whiteSkinId = selection.blackSkinId = hasVisual ? key : CosmeticSelection.LowPolyId;
        }
        else if (IsBoardType(item.type))
        {
            hasVisual = string.Equals(key, CosmeticSelection.LowPolyId, StringComparison.OrdinalIgnoreCase);
            selection.boardId = hasVisual ? key : CosmeticSelection.LowPolyId;
        }
        else return false;

        selection.Save();
        return hasVisual;
    }

    private static bool IsChessType(string type) =>
        !string.IsNullOrWhiteSpace(type) && type.IndexOf("CHESS", StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool IsBoardType(string type) =>
        !string.IsNullOrWhiteSpace(type) && type.IndexOf("BOARD", StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool IsKnownPieceSkin(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        foreach (PieceSkinDefinition skin in PieceSkinCatalog.All)
            if (string.Equals(skin.Id, key, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static int RarityRank(string rarity)
    {
        switch (rarity?.Trim().ToUpperInvariant())
        {
            case "LEGENDARY": return 5;
            case "EPIC": return 4;
            case "RARE": return 3;
            default: return 2;
        }
    }

    private static Color RarityColor(string rarity)
    {
        switch (RarityRank(rarity))
        {
            case 5: return LegendaryTierColor;
            case 4: return EpicTierColor;
            case 3: return RareTierColor;
            default: return CommonTierColor;
        }
    }

    private void SetStatus(string message)
    {
        if (statusLabel) statusLabel.text = message ?? string.Empty;
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
