using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI = SketchbookUI;

public sealed class InventoryMenuController : MonoBehaviour
{
    private const float DesignWidth = 1672f, DesignHeight = 941f;
    private const int PageSize = 6;
    private enum Category { All, Chess, Board, Effects }
    private enum Ownership { Owned, Equipped, All }
    private enum SortOrder { Equipped, Name, Rarity }
    private sealed class InventoryEntry
    {
        public string itemId, name, type, rarity, unityAssetKey;
        public string[] tags;
        public bool owned, isEquipped, isActive, localEffect;
    }

    private readonly InventoryService inventory = new InventoryService();
    private readonly List<InventoryEntry> items = new List<InventoryEntry>();
    private readonly HashSet<string> selectedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<Button> tagButtons = new List<Button>();
    private readonly Button[] categoryButtons = new Button[4];
    private readonly Button[] ownershipButtons = new Button[3];
    private readonly Button[] sortButtons = new Button[3];
    private readonly TextMeshProUGUI[] loadoutNames = new TextMeshProUGUI[3];
    private RectTransform root, contentRoot, itemRoot, filterOverlay, tagRoot;
    private CanvasGroup contentInput;
    private Button previousButton, nextButton, filterButton;
    private TMP_InputField searchInput;
    private TextMeshProUGUI statusLabel, countLabel, pageLabel, filterSummary;
    private UnityAction closeAction;
    private Category category;
    private Ownership ownership = Ownership.Owned;
    private SortOrder sort;
    private int page, requestVersion;
    private bool loading, equipping;
    private string loadedUserId;
    private LoadingManager cosmetics;

    public void Initialize(RectTransform newRoot, UnityAction newCloseAction, LoadingManager newCosmetics = null)
    {
        root = newRoot; closeAction = newCloseAction; cosmetics = newCosmetics; Build();
    }

    public void Open()
    {
        requestVersion++;
        page = 0; category = Category.All; ownership = Ownership.Owned; sort = SortOrder.Equipped;
        selectedTags.Clear();
        searchInput.SetTextWithoutNotify("");
        items.Clear();
        AddLocalEffects();
        RefreshView();
        RefreshFromServer();
    }

    private void Build()
    {
        var blocker = root.gameObject.GetComponent<Image>() ?? root.gameObject.AddComponent<AntialiasedMenuImage>();
        blocker.color = UI.Paper; blocker.raycastTarget = true;
        contentRoot = UI.Node(root, "Inventory Content", new Rect(0, 0, DesignWidth, DesignHeight));
        contentRoot.gameObject.AddComponent<InventoryContentRootFitter>().Configure(DesignWidth, DesignHeight, 1);
        UI.Background(contentRoot);
        var content = UI.Node(contentRoot, "Inventory Controls", new Rect(0, 0, DesignWidth, DesignHeight));
        contentInput = content.gameObject.AddComponent<CanvasGroup>();
        UI.Text(content, "Eyebrow", "CHESS BUT WEIRD  /  YOUR COLLECTION", new Rect(72, 34, 1020, 34), 21, UI.Muted);
        UI.Card(content, "Title Highlighter", new Rect(73, 132, 360, 18), UI.Yellow, -1, false, 4);
        UI.Text(content, "Title", "Inventory", new Rect(72, 70, 680, 88), 65);
        UI.Button(content, "Back to Menu", "<  BACK", new Rect(1436, 80, 164, 62), UI.Pink, () => closeAction?.Invoke(), 26);

        UI.Text(content, "Loadout Heading", "YOUR LOADOUT", new Rect(72, 197, 348, 48), 33);
        UI.Text(content, "Collection Heading", "COLLECTION", new Rect(452, 188, 420, 48), 33);
        countLabel = UI.Text(content, "Collection Count", "", new Rect(1132, 195, 468, 38), 22, UI.Muted, TextAlignmentOptions.Right);
        for (int i = 0; i < 3; i++)
        {
            Category slot = (Category)(i + 1);
            RectTransform card = UI.Card(content, slot + " Loadout", new Rect(72, 267 + i * 176, 348, 150),
                i == 0 ? UI.Blue : i == 1 ? UI.Green : UI.Pink, i == 1 ? 1 : -1, true, 8).rectTransform;
            card.GetComponent<HandDrawnRoundedGraphic>().raycastTarget = true;
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<HandDrawnRoundedGraphic>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => ShowCategory(slot));
            UI.Doodle(card, "Slot Icon", new Rect(18, 44, 66, 76), Icon(slot), UI.Ink);
            UI.Text(card, "Slot Label", slot == Category.Chess ? "CHESS SKIN" : slot == Category.Board ? "BOARD SKIN" : "MOVE EFFECT",
                new Rect(101, 15, 225, 30), 19, UI.Muted);
            loadoutNames[i] = UI.Text(card, "Equipped Name", "Classic", new Rect(101, 48, 225, 83), 29, UI.Ink, TextAlignmentOptions.MidlineLeft, true);
        }

        string[] labels = { "ALL", "CHESS", "BOARD", "EFFECTS" };
        for (int i = 0; i < labels.Length; i++)
        {
            Category value = (Category)i;
            categoryButtons[i] = UI.Button(content, labels[i] + " Category", labels[i],
                new Rect(452 + i * 194, 252, 176, 54), UI.White, () => ShowCategory(value), 23);
        }
        filterButton = UI.Button(content, "Filter Inventory", "FILTERS", new Rect(1256, 252, 152, 54), UI.Blue, OpenFilters, 23);
        searchInput = BuildSearch(content, new Rect(452, 323, 600, 53));
        filterSummary = UI.Text(content, "Active Filter Summary", "", new Rect(1078, 330, 522, 38), 20, UI.Muted, TextAlignmentOptions.Right);
        itemRoot = UI.Node(content, "Inventory Items", new Rect(452, 399, 1148, 430));
        previousButton = UI.Button(content, "Previous Page", "<", new Rect(452, 848, 60, 50), UI.White, () => ChangePage(-1), 29);
        pageLabel = UI.Text(content, "Page", "", new Rect(524, 852, 992, 40), 23, UI.Muted, TextAlignmentOptions.Center);
        nextButton = UI.Button(content, "Next Page", ">", new Rect(1540, 848, 60, 50), UI.White, () => ChangePage(1), 29);
        statusLabel = UI.Text(content, "Inventory Status", "", new Rect(72, 817, 344, 97), 21, UI.Muted, TextAlignmentOptions.MidlineLeft, true);
        foreach (Selectable control in content.GetComponentsInChildren<Selectable>())
        {
            if (control == previousButton || control == nextButton) continue;
            ColorBlock colors = control.colors; colors.disabledColor = Color.white; control.colors = colors;
        }
        BuildFilters();
        AddLocalEffects();
        RefreshView();
    }

    private TMP_InputField BuildSearch(Transform parent, Rect area)
    {
        var face = UI.Card(parent, "Search Inventory", area, UI.White, 0, false, 8);
        face.raycastTarget = true;
        var input = face.gameObject.AddComponent<TMP_InputField>();
        var viewport = UI.Node(face.transform, "Text Viewport", new Rect(18, 4, area.width - 36, area.height - 8));
        viewport.gameObject.AddComponent<RectMask2D>();
        var text = UI.Text(viewport, "Search Text", "", new Rect(0, 0, area.width - 36, area.height - 8), 23);
        var placeholder = UI.Text(viewport, "Search Placeholder", "Search collection...", new Rect(0, 0, area.width - 36, area.height - 8), 23, UI.Muted);
        input.textViewport = viewport; input.textComponent = text; input.placeholder = placeholder;
        input.targetGraphic = face; input.characterLimit = 80;
        input.onValueChanged.AddListener(_ => { page = 0; RefreshView(); });
        return input;
    }

    private void BuildFilters()
    {
        filterOverlay = UI.Node(contentRoot, "Inventory Filter Overlay", new Rect(0, 0, DesignWidth, DesignHeight));
        var shade = filterOverlay.gameObject.AddComponent<AntialiasedMenuImage>();
        shade.color = new Color(0, 0, 0, .15f);
        var dismiss = filterOverlay.gameObject.AddComponent<Button>();
        dismiss.targetGraphic = shade; dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(CloseFilters);
        var panel = UI.Card(filterOverlay, "Filter Sheet", new Rect(860, 184, 740, 704), UI.White, 0, true, 8).rectTransform;
        panel.GetComponent<HandDrawnRoundedGraphic>().raycastTarget = true;
        UI.Text(panel, "Filter Heading", "FILTER COLLECTION", new Rect(28, 22, 574, 49), 36);
        UI.Button(panel, "Close Filters", "x", new Rect(646, 22, 62, 50), UI.Pink, CloseFilters, 28);
        UI.Text(panel, "Ownership Heading", "SHOW", new Rect(28, 92, 660, 31), 21, UI.Muted);
        string[] ownershipLabels = { "OWNED", "EQUIPPED", "ALL ITEMS" };
        string[] sortLabels = { "EQUIPPED FIRST", "NAME A-Z", "RARITY" };
        for (int i = 0; i < 3; i++)
        {
            Ownership value = (Ownership)i;
            ownershipButtons[i] = UI.Button(panel, "Ownership " + value, ownershipLabels[i],
                new Rect(28 + i * 230, 133, 216, 52), UI.White, () => { ownership = value; FilterChanged(); }, 22);
            SortOrder order = (SortOrder)i;
            sortButtons[i] = UI.Button(panel, "Sort " + order, sortLabels[i], new Rect(28 + i * 230, 505, 216, 52), UI.White,
                () => { sort = order; FilterChanged(); }, 21);
        }
        UI.Text(panel, "Tags Heading", "TAGS", new Rect(28, 211, 660, 32), 21, UI.Muted);
        tagRoot = UI.ScrollArea(panel, "Filter Tags", new Rect(22, 259, 694, 171), out _);
        UI.Text(panel, "Sort Heading", "SORT BY", new Rect(28, 456, 660, 31), 21, UI.Muted);
        UI.Button(panel, "Reset Filters", "RESET", new Rect(28, 608, 214, 57), UI.Paper, () =>
        {
            ownership = Ownership.Owned; sort = SortOrder.Equipped; selectedTags.Clear();
            searchInput.SetTextWithoutNotify(""); FilterChanged();
        }, 23);
        UI.Button(panel, "Apply Filters", "DONE", new Rect(494, 608, 214, 57), UI.Green, CloseFilters, 25);
        filterOverlay.gameObject.SetActive(false);
    }

    private void OpenFilters()
    {
        RebuildTags();
        contentInput.interactable = contentInput.blocksRaycasts = false;
        filterOverlay.gameObject.SetActive(true);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(ownershipButtons[(int)ownership].gameObject);
    }

    private void CloseFilters()
    {
        filterOverlay.gameObject.SetActive(false);
        contentInput.interactable = contentInput.blocksRaycasts = true;
        if (EventSystem.current && filterButton.gameObject.activeInHierarchy) EventSystem.current.SetSelectedGameObject(filterButton.gameObject);
    }

    private void FilterChanged() { page = 0; RefreshView(); RefreshFilterControls(); }

    private void RebuildTags()
    {
        ClearChildren(tagRoot); tagButtons.Clear();
        var tags = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "COMMON", "RARE", "EPIC", "LEGENDARY", "BUILT-IN" };
        foreach (InventoryEntry item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.rarity)) tags.Add(item.rarity.ToUpperInvariant());
            if (item.tags != null) foreach (string tag in item.tags)
                if (!string.IsNullOrWhiteSpace(tag)) tags.Add(tag.Trim().ToUpperInvariant());
        }
        int index = 0;
        foreach (string tag in tags)
        {
            string value = tag;
            Button button = UI.Button(tagRoot, "Tag " + value, value,
                new Rect(6 + (index % 3) * 230, 5 + (index / 3) * 60, 215, 48),
                selectedTags.Contains(value) ? UI.Yellow : UI.White, () =>
                {
                    if (!selectedTags.Add(value)) selectedTags.Remove(value);
                    FilterChanged();
                }, 21);
            tagButtons.Add(button); index++;
        }
        tagRoot.sizeDelta = new Vector2(0, Mathf.Max(171, ((index + 2) / 3) * 60 + 8));
        RefreshFilterControls();
    }

    private void RefreshFilterControls()
    {
        for (int i = 0; i < 3; i++)
        {
            ownershipButtons[i].targetGraphic.color = (int)ownership == i ? UI.Blue : UI.White;
            sortButtons[i].targetGraphic.color = (int)sort == i ? UI.Green : UI.White;
        }
        foreach (Button button in tagButtons)
            button.targetGraphic.color = selectedTags.Contains(button.name.Substring(4)) ? UI.Yellow : UI.White;
    }

    private void ShowCategory(Category value) { category = value; page = 0; RefreshView(); }
    private void ChangePage(int direction)
    {
        page = Mathf.Clamp(page + direction, 0, Mathf.Max(0, (GetVisibleItems().Count - 1) / PageSize)); RefreshView();
    }

    private static Category ItemCategory(InventoryEntry item) =>
        item.localEffect ? Category.Effects : IsChessType(item.type) ? Category.Chess :
        IsBoardType(item.type) ? Category.Board : Category.All;

    private List<InventoryEntry> GetVisibleItems()
    {
        var visible = new List<InventoryEntry>();
        string query = searchInput ? searchInput.text.Trim() : "";
        foreach (InventoryEntry item in items)
        {
            Category type = ItemCategory(item);
            if (type == Category.All || (category != Category.All && category != type)) continue;
            if (ownership == Ownership.Owned && !item.owned || ownership == Ownership.Equipped && !item.isEquipped) continue;
            if (query.Length > 0 && item.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (selectedTags.Count > 0)
            {
                bool matches = selectedTags.Contains(item.rarity ?? "");
                if (item.tags != null) foreach (string tag in item.tags) matches |= selectedTags.Contains(tag?.Trim() ?? "");
                if (!matches) continue;
            }
            visible.Add(item);
        }
        visible.Sort((a, b) =>
        {
            int result = sort == SortOrder.Equipped ? b.isEquipped.CompareTo(a.isEquipped) :
                sort == SortOrder.Rarity ? RarityRank(b.rarity).CompareTo(RarityRank(a.rarity)) : 0;
            return result != 0 ? result : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        });
        return visible;
    }

    private void RefreshView()
    {
        if (!itemRoot) return;
        for (int i = 0; i < loadoutNames.Length; i++)
        {
            var equipped = items.Find(item => item.isEquipped && ItemCategory(item) == (Category)(i + 1));
            loadoutNames[i].text = equipped != null ? equipped.name : loading ? "Loading..." : "Default";
        }
        for (int i = 0; i < categoryButtons.Length; i++) categoryButtons[i].targetGraphic.color = (int)category == i ? UI.Yellow : UI.White;
        ClearChildren(itemRoot);
        List<InventoryEntry> visible = GetVisibleItems();
        page = Mathf.Clamp(page, 0, Mathf.Max(0, (visible.Count - 1) / PageSize));
        int first = page * PageSize;
        for (int i = first; i < Mathf.Min(first + PageSize, visible.Count); i++)
            AddItemCard(visible[i], new Rect(((i - first) % 3) * 386, ((i - first) / 3) * 216, 374, 198));
        if (visible.Count == 0)
            UI.Text(itemRoot, "Empty Collection", loading ? "Loading collection..." : "No items match these filters.",
                new Rect(60, 110, 1028, 110), 32, UI.Muted, TextAlignmentOptions.Center, true);
        countLabel.text = visible.Count + (visible.Count == 1 ? " ITEM" : " ITEMS");
        int pages = Mathf.Max(1, (visible.Count + PageSize - 1) / PageSize);
        pageLabel.text = "PAGE " + (page + 1) + " / " + pages;
        previousButton.interactable = page > 0;
        nextButton.interactable = page + 1 < pages;
        filterSummary.text = ownership.ToString().ToUpperInvariant() + (selectedTags.Count > 0 ? "  /  " + selectedTags.Count + " TAGS" : "");
        filterButton.targetGraphic.color = selectedTags.Count > 0 || ownership != Ownership.Owned || sort != SortOrder.Equipped ? UI.Yellow : UI.Blue;
    }

    private void AddItemCard(InventoryEntry item, Rect area)
    {
        var face = UI.Card(itemRoot, "Item " + item.itemId, area, UI.White, 0, true, 8);
        UI.Card(face.transform, "Rarity Tag", new Rect(15, 13, 165, 30), RarityColor(item.rarity), -1, false, 4);
        UI.Text(face.transform, "Rarity", item.rarity ?? "COMMON", new Rect(24, 13, 146, 30), 18);
        UI.Doodle(face.transform, "Cosmetic Preview", new Rect(23, 67, 73, 92), Icon(ItemCategory(item)),
            item.localEffect && item.unityAssetKey == "confetti" ? UI.Pink : UI.Ink, -4);
        UI.Text(face.transform, "Item Name", item.name, new Rect(116, 56, 240, 83), 29, UI.Ink, TextAlignmentOptions.MidlineLeft, true);
        UI.Text(face.transform, "Category Tag", ItemCategory(item).ToString().ToUpperInvariant(), new Rect(116, 141, 220, 25), 18, UI.Muted);
        UI.Text(face.transform, "Item State", item.isEquipped ? "EQUIPPED" : !item.owned ? "LOCKED" : !item.isActive ? "UNAVAILABLE" : "EQUIP",
            new Rect(178, 170, 173, 23), 18, item.isEquipped ? new Color(.20f, .40f, .23f) : UI.Muted, TextAlignmentOptions.Right);
        if (!item.owned || !item.isActive || item.isEquipped) return;
        face.raycastTarget = true;
        var button = face.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = face; button.interactable = !loading && !equipping;
        button.onClick.AddListener(() => EquipItem(item));
    }

    private void AddLocalEffects()
    {
        string selected = CosmeticSelection.Load().moveEffectId;
        string[] keys = { "classic", "ink_sparks", "confetti" };
        string[] names = { "Classic", "Ink sparks", "Confetti" };
        for (int i = 0; i < keys.Length; i++) items.Add(new InventoryEntry
        {
            itemId = "local-effect:" + keys[i], name = names[i], type = "MOVE_EFFECT", rarity = "COMMON",
            unityAssetKey = keys[i], owned = true, isActive = true, isEquipped = selected == keys[i],
            localEffect = true, tags = new[] { "BUILT-IN" }
        });
    }

    private async void RefreshFromServer()
    {
        string userId = PlayerAuthService.CurrentApiUser?.userId;
        int version = requestVersion;
        if (string.IsNullOrWhiteSpace(userId)) { loading = false; SetStatus("Sign in to load your skins."); RefreshView(); return; }
        loadedUserId = userId; loading = true; RefreshView(); SetStatus("Loading collection...");
        try
        {
            Task<List<ItemCatalogResponse>> catalogTask = inventory.GetItemsAsync();
            Task<List<InventoryItemResponse>> ownedTask = inventory.GetInventoryAsync();
            await Task.WhenAll(catalogTask, ownedTask);
            if (!this || version != requestVersion || userId != PlayerAuthService.CurrentApiUser?.userId) return;
            MergeItems(catalogTask.Result, ownedTask.Result);
            SyncEquippedVisuals();
            SetStatus("");
        }
        catch (Exception error)
        {
            if (this && version == requestVersion) SetStatus("Could not load inventory: " + PlayerNotificationText.FromException(error));
        }
        finally
        {
            if (this && version == requestVersion)
            {
                loading = false; RefreshView();
                if (filterOverlay.gameObject.activeSelf) RebuildTags();
                if (root.gameObject.activeInHierarchy && loadedUserId != PlayerAuthService.CurrentApiUser?.userId)
                { items.Clear(); AddLocalEffects(); requestVersion++; RefreshFromServer(); }
            }
        }
    }

    private void MergeItems(List<ItemCatalogResponse> catalog, List<InventoryItemResponse> owned)
    {
        items.Clear();
        var ownershipMap = new Dictionary<string, InventoryItemResponse>(StringComparer.OrdinalIgnoreCase);
        if (owned != null) foreach (InventoryItemResponse item in owned)
            if (item != null && !string.IsNullOrWhiteSpace(item.itemId)) ownershipMap[item.itemId] = item;
        if (catalog != null) foreach (ItemCatalogResponse item in catalog)
        {
            if (item == null || !item.isActive || string.IsNullOrWhiteSpace(item.itemId)) continue;
            ownershipMap.TryGetValue(item.itemId, out InventoryItemResponse playerItem);
            items.Add(new InventoryEntry { itemId = item.itemId, name = DisplayName(item.name, item.code, item.itemId),
                type = item.type, rarity = item.rarity, unityAssetKey = item.unityAssetKey, tags = item.tags,
                owned = playerItem != null, isEquipped = playerItem != null && playerItem.isEquipped, isActive = true });
            ownershipMap.Remove(item.itemId);
        }
        foreach (InventoryItemResponse item in ownershipMap.Values)
            items.Add(new InventoryEntry { itemId = item.itemId, name = DisplayName(item.name, item.code, item.itemId),
                type = item.type, rarity = item.rarity, unityAssetKey = item.unityAssetKey, tags = item.tags,
                owned = true, isEquipped = item.isEquipped, isActive = false });
        AddLocalEffects();
    }

    private async void EquipItem(InventoryEntry item)
    {
        if (equipping || loading || item == null || !item.owned || !item.isActive) return;
        if (item.localEffect)
        {
            var selection = CosmeticSelection.Load(); selection.moveEffectId = item.unityAssetKey; selection.Save();
            foreach (InventoryEntry candidate in items) if (candidate.localEffect) candidate.isEquipped = candidate == item;
            RefreshView(); SetStatus("Equipped " + item.name + "."); return;
        }
        string userId = PlayerAuthService.CurrentApiUser?.userId;
        if (string.IsNullOrWhiteSpace(userId)) return;
        int version = requestVersion;
        equipping = true; RefreshView(); SetStatus("Equipping " + item.name + "...");
        try
        {
            EquipItemResponse response = await inventory.EquipAsync(item.itemId);
            if (!this || userId != PlayerAuthService.CurrentApiUser?.userId) return;
            if (response == null || !response.success || response.equipped == null)
                throw new ApiException(response?.message ?? "The server did not confirm equipment.");
            PlayerAuthService.CurrentApiUser.equipped = response.equipped;
            if (version != requestVersion)
            {
                ApplyLocalVisual(item);
                requestVersion++;
                RefreshFromServer();
                return;
            }
            foreach (InventoryEntry candidate in items)
            {
                if (candidate.localEffect) continue;
                candidate.isEquipped = candidate.owned && string.Equals(candidate.itemId,
                    IsChessType(candidate.type) ? response.equipped.chessSkinId : IsBoardType(candidate.type) ? response.equipped.boardSkinId : null,
                    StringComparison.OrdinalIgnoreCase);
            }
            bool hasVisual = ApplyLocalVisual(item);
            SetStatus(hasVisual ? "Equipped " + item.name + "." : "Equipped on account. Local art unavailable.");
        }
        catch (Exception error) { if (this && version == requestVersion) SetStatus("Could not equip: " + PlayerNotificationText.FromException(error)); }
        finally { if (this) { equipping = false; RefreshView(); } }
    }

    private void SyncEquippedVisuals()
    {
        CosmeticSelection selection = CosmeticSelection.Load();
        selection.whiteSkinId = selection.blackSkinId = selection.boardId = CosmeticSelection.LowPolyId;
        foreach (InventoryEntry item in items)
        {
            if (!item.isEquipped || item.localEffect) continue;
            if (IsChessType(item.type)) selection.whiteSkinId = selection.blackSkinId = IsKnownPieceSkin(item.unityAssetKey) ? item.unityAssetKey : CosmeticSelection.LowPolyId;
            else if (IsBoardType(item.type)) selection.boardId = IsKnownBoardSkin(item.unityAssetKey) ? item.unityAssetKey : CosmeticSelection.LowPolyId;
        }
        selection.Save();
    }

    private bool ApplyLocalVisual(InventoryEntry item)
    {
        var selection = CosmeticSelection.Load();
        bool supported;
        if (IsChessType(item.type))
        {
            supported = IsKnownPieceSkin(item.unityAssetKey);
            selection.whiteSkinId = selection.blackSkinId = supported ? item.unityAssetKey : CosmeticSelection.LowPolyId;
        }
        else
        {
            supported = IsKnownBoardSkin(item.unityAssetKey);
            selection.boardId = supported ? item.unityAssetKey : CosmeticSelection.LowPolyId;
        }
        selection.Save(); return supported;
    }

    private static bool IsChessType(string type) => !string.IsNullOrWhiteSpace(type) && type.IndexOf("CHESS", StringComparison.OrdinalIgnoreCase) >= 0;
    private static bool IsBoardType(string type) => !string.IsNullOrWhiteSpace(type) && type.IndexOf("BOARD", StringComparison.OrdinalIgnoreCase) >= 0;
    private bool IsKnownBoardSkin(string key) => string.Equals(key, CosmeticSelection.LowPolyId, StringComparison.OrdinalIgnoreCase) ||
        (cosmetics && cosmetics.Catalog && cosmetics.Catalog.FindBoard(key) != null);
    private bool IsKnownPieceSkin(string key)
    {
        if (cosmetics && cosmetics.Catalog) return cosmetics.Catalog.FindPiece(key) != null;
        foreach (PieceSkinDefinition skin in PieceSkinCatalog.All) if (string.Equals(skin.Id, key, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    private static string DisplayName(string name, string code, string id) => !string.IsNullOrWhiteSpace(name) ? name : !string.IsNullOrWhiteSpace(code) ? code : id;
    private static SketchbookDoodle.Shape Icon(Category value) => value == Category.Chess ? SketchbookDoodle.Shape.Pawn : value == Category.Board ? SketchbookDoodle.Shape.Board : SketchbookDoodle.Shape.Star;
    private static int RarityRank(string rarity)
    {
        switch (rarity?.Trim().ToUpperInvariant()) { case "LEGENDARY": return 4; case "EPIC": return 3; case "RARE": return 2; default: return 1; }
    }
    private static Color RarityColor(string rarity)
    {
        switch (RarityRank(rarity)) { case 4: return UI.Yellow; case 3: return UI.Lavender; case 2: return UI.Blue; default: return UI.Green; }
    }
    private void SetStatus(string value) { if (statusLabel) statusLabel.text = value ?? ""; }
    private static void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject; child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
    }
    private void Update()
    {
        if (filterOverlay && filterOverlay.gameObject.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseFilters();
    }
    private void OnDisable()
    {
        if (filterOverlay) filterOverlay.gameObject.SetActive(false);
        if (contentInput) contentInput.interactable = contentInput.blocksRaycasts = true;
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
