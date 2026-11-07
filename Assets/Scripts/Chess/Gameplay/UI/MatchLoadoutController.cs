using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChessButWeird.Online;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI = SketchbookUI;

/// <summary>Shared pre-match picker for local games, practice, rooms and matchmaking.</summary>
public sealed class MatchLoadoutController : MonoBehaviour
{
    private enum Slot { Chess, Board, Effect, Background }
    private sealed class Choice
    {
        public string id, name, type, tier, itemId, assetKey;
        public Sprite thumbnail;
        public bool owned;
    }
    private readonly List<Choice> choices = new List<Choice>();
    private RectTransform root, content, choiceRoot, overlay, summaryRoot;
    private CanvasGroup contentInput;
    private ScrollRect choiceScroll;
    private TextMeshProUGUI status, sideLabel, modeLabel;
    private Button whiteButton, blackButton, startButton, effectButton, backgroundButton, backButton, refreshButton;
    private bool lobbyLocked, queueSearching, queueBusy;
    private string actionLabel = "START MATCH";
    private UnityAction cancelSearch;
    private readonly Button[] categoryButtons = new Button[4];
    private Func<bool> canContinue;
    private TextMeshProUGUI networkNotice;
    private RankCardView rankCard;
    private bool rankedLobby;
    private string rankMode;
    private UserMeResponse shownRankUser;
    private RankConfig shownRankConfig;
    private Action refreshRank;
    private Slot slot;
    private PieceType? editingPiece;
    private Button pieceModeButton;
    private bool loading, canChooseBlack, accountReady, onlineMode, submitting, hasSavedSelection;
    private int requestVersion;
    private string sessionUser;
    private PieceTeam playerTeam;
    private CosmeticSelection selection;
    private Action<CosmeticSelection> startAction;
    private UnityAction backAction;
    private LoadingManager cosmetics;

    public void Initialize(RectTransform screen, LoadingManager loadingManager)
    { root = screen; cosmetics = loadingManager; Build(); }

    public void Open(bool againstBot, PieceTeam team, Action<CosmeticSelection> onStart, UnityAction onBack)
    { OpenPicker(!againstBot, false, team, onStart, onBack); }

    public void OpenOnline(Action<CosmeticSelection> onStart, UnityAction onBack, string actionLabel)
    {
        OpenPicker(false, true, PieceTeam.White, onStart, onBack);
        this.actionLabel = actionLabel;
        startButton.GetComponentInChildren<TextMeshProUGUI>().text = actionLabel;
    }

    public void ConfigureLobby(string mode, string action, string description = null)
    {
        modeLabel.text = "CHESS BUT WEIRD / " + mode;
        actionLabel = action;
        startButton.name = "Find Match";
        startButton.GetComponentInChildren<TextMeshProUGUI>().text = action;
        sideLabel.text = description ?? "Choose your loadout. Your equipment is saved before matchmaking.";
        if (onlineMode && !networkNotice)
        {
            networkNotice = UI.Text(content, "Network Notice", "", new Rect(64, 811, 1544, 32), 20, UI.Muted);
            status.rectTransform.sizeDelta = new Vector2(1544, 28);
            status.rectTransform.anchoredPosition = new Vector2(836, -853);
        }
    }

    public void SetMatchmakingState(bool locked, bool searching, string elapsed, bool busy, UnityAction cancel)
    {
        bool changed = lobbyLocked != locked;
        lobbyLocked = locked; queueSearching = searching; queueBusy = busy; cancelSearch = cancel;
        startButton.GetComponentInChildren<TextMeshProUGUI>().text = searching ? elapsed + "   X" : locked ? "PLEASE WAIT" : actionLabel;
        backButton.interactable = refreshButton.interactable = !locked && !submitting;
        effectButton.interactable = backgroundButton.interactable = !locked && !submitting;
        if (locked && overlay.gameObject.activeSelf) ClosePopup();
        if (changed) RenderSummary();
        RefreshStartButton();
    }

    public void SetOnlineContext(Func<bool> available, string message)
    {
        canContinue = available;
        if (networkNotice) networkNotice.text = message;
        RefreshStartButton();
        RefreshRankCard();
    }

    public void ConfigureRankedLobby(string mode, bool ranked, Action onRefresh = null)
    {
        rankedLobby = ranked; rankMode = mode; refreshRank = onRefresh;
        if (!ranked) return;
        // Keep the two cosmetic cards centered; rank occupies the unused header corner.
        var title = content.Find("Title") as RectTransform;
        var hint = content.Find("Hint") as RectTransform;
        if (title) { title.sizeDelta = new Vector2(900, 82); title.anchoredPosition = new Vector2(514, -103); }
        if (hint) { hint.sizeDelta = new Vector2(890, 45); hint.anchoredPosition = new Vector2(509, -175.5f); }
        rankCard = new RankCardView(content, "Current Ranked Standing", new Rect(1030, 62, 578, 140));
        RefreshRankCard(true);
    }

    private void RefreshRankCard(bool force = false)
    {
        if (!rankedLobby || rankCard == null) return;
        var user = PlayerAuthService.CurrentApiUser;
        var config = RankService.CachedConfig(rankMode);
        if (!force && ReferenceEquals(shownRankUser, user) && ReferenceEquals(shownRankConfig, config)) return;
        shownRankUser = user; shownRankConfig = config;
        rankCard.Bind(rankMode, user?.ranks?.For(rankMode));
    }

    private void OpenPicker(bool bothSides, bool online, PieceTeam team, Action<CosmeticSelection> onStart, UnityAction onBack)
    {
        requestVersion++; sessionUser = PlayerAuthService.CurrentApiUser?.userId;
        playerTeam = team; canChooseBlack = bothSides || online; onlineMode = online;
        lobbyLocked = queueSearching = queueBusy = false; cancelSearch = null; canContinue = null;
        actionLabel = "START MATCH";
        startAction = onStart; backAction = onBack;
        hasSavedSelection = !online && CosmeticSelection.HasSavedSelection;
        selection = CosmeticSelection.Load(); slot = Slot.Chess; editingPiece = null;
        ClosePopup(); RefreshSideButtons();
        startButton.GetComponentInChildren<TextMeshProUGUI>().text = "START MATCH";
        LoadOwnedChoices();
    }

    private void Build()
    {
        UI.Background(root);
        content = UI.Node(root, "Match Loadout Content", new Rect(0, 0, 1672, 941));
        content.gameObject.AddComponent<InventoryContentRootFitter>().Configure(1672, 941, .94f);
        contentInput = content.gameObject.AddComponent<CanvasGroup>();
        modeLabel = UI.Text(content, "Eyebrow", "CHESS BUT WEIRD  /  BEFORE THE MATCH", new Rect(64, 25, 1500, 32), 24, UI.Muted);
        UI.Text(content, "Title", "Choose your loadout", new Rect(64, 62, 1400, 82), 62);
        UI.Text(content, "Hint", "Pick a full set or customize each piece. Board skins are independent.", new Rect(64, 153, 1500, 45), 26, UI.Muted);
        whiteButton = UI.Button(content, "White Loadout", "WHITE SIDE", new Rect(64, 216, 225, 54), UI.Blue, () => ChangeSide(PieceTeam.White), 23);
        blackButton = UI.Button(content, "Black Loadout", "BLACK SIDE", new Rect(307, 216, 225, 54), UI.Pink, () => ChangeSide(PieceTeam.Black), 23);
        sideLabel = UI.Text(content, "Side", "", new Rect(64, 219, 920, 44), 24, UI.Muted);
        pieceModeButton = UI.Button(content, "Piece Mode", "", new Rect(1050, 216, 558, 54), UI.Yellow, () => {
            if (submitting || lobbyLocked || loading || onlineMode && PlayerAuthService.CurrentApiUser?.equipped?.loadoutVersion != 1) return;
            selection.customPieces = !selection.customPieces; editingPiece = null; RenderChoices();
        }, 23);
        summaryRoot = UI.Node(content, "Selected Cosmetics", new Rect(226, 300, 1220, 425));
        effectButton = UI.Button(content, "Move Effect", "", new Rect(546, 752, 580, 56), UI.White, () => OpenPopup(Slot.Effect), 23);
        backgroundButton = UI.Button(content, "Background", "", new Rect(64, 752, 450, 56), UI.Blue, () => OpenPopup(Slot.Background), 22);
        status = UI.Text(content, "Loadout Status", "", new Rect(64, 821, 1544, 40), 21, UI.Muted, TextAlignmentOptions.MidlineLeft, true);
        backButton = UI.Button(content, "Back", "<  BACK", new Rect(64, 875, 235, 62), UI.Pink, () => { if (!submitting && !lobbyLocked) backAction?.Invoke(); }, 25);
        refreshButton = UI.Button(content, "Refresh Collection", "REFRESH", new Rect(1150, 875, 190, 62), UI.White,
            () => { if (!loading && !submitting && !lobbyLocked) { LoadOwnedChoices(); refreshRank?.Invoke(); } }, 22);
        startButton = UI.Button(content, "Start Match", "START MATCH", new Rect(1360, 867, 248, 70), UI.Yellow, Submit, 25);

        overlay = UI.Node(root, "Skin Collection Popup", new Rect(0, 0, 1672, 941));
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        var dimmer = overlay.gameObject.AddComponent<Image>(); dimmer.color = new Color(.10f, .09f, .13f, .68f); dimmer.raycastTarget = true;
        var popup = UI.Node(overlay, "Collection Panel", new Rect(0, 0, 1370, 800));
        popup.gameObject.AddComponent<InventoryContentRootFitter>().Configure(1370, 800, .88f);
        UI.Card(popup, "Paper", new Rect(0, 0, 1370, 800), UI.Paper, 0, false);
        UI.Text(popup, "Title", "Your collection", new Rect(34, 20, 1050, 62), 40);
        UI.Button(popup, "Close Collection", "CLOSE", new Rect(1160, 25, 176, 54), UI.Pink, ClosePopup, 23);
        string[] categories = { "CHESS", "BOARD", "MOVE EFFECT", "BACKGROUND" };
        for (int i = 0; i < categories.Length; i++)
        {
            Slot category = (Slot)i;
            categoryButtons[i] = UI.Button(popup, "Category " + category, categories[i], new Rect(34 + i * 328, 102, 310, 56),
                UI.White, () => { editingPiece = null; slot = category; choiceScroll.verticalNormalizedPosition = 1; RenderChoices(); }, 24);
        }
        choiceRoot = UI.ScrollArea(popup, "Loadout Choices", new Rect(34, 186, 1302, 574), out choiceScroll);
        UI.Text(popup, "Scroll Hint", "Scroll to browse tiers", new Rect(34, 765, 1302, 26), 18, UI.Muted, TextAlignmentOptions.Center);
        overlay.gameObject.SetActive(false);
    }

    private void ChangeSide(PieceTeam team)
    {
        if (!canChooseBlack || submitting || lobbyLocked) return;
        playerTeam = team; RefreshSideButtons(); RenderSummary();
    }
    private void RefreshSideButtons()
    {
        whiteButton.gameObject.SetActive(canChooseBlack); blackButton.gameObject.SetActive(canChooseBlack);
        whiteButton.targetGraphic.color = playerTeam == PieceTeam.White ? UI.Yellow : UI.White;
        blackButton.targetGraphic.color = playerTeam == PieceTeam.Black ? UI.Yellow : UI.White;
        sideLabel.gameObject.SetActive(!canChooseBlack);
        sideLabel.text = onlineMode ? "Your equipment will be saved before joining a room or queue." :
            "Your side: " + playerTeam + "   ·   Bot uses its default pieces";
    }
    private void OpenPopup(Slot category)
    {
        if (submitting || lobbyLocked) return;
        slot = category; overlay.gameObject.SetActive(true);
        contentInput.interactable = contentInput.blocksRaycasts = false;
        choiceScroll.verticalNormalizedPosition = 1; RenderChoices();
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(categoryButtons[(int)category].gameObject);
    }
    private void ClosePopup()
    {
        if (overlay) overlay.gameObject.SetActive(false);
        if (contentInput) contentInput.interactable = contentInput.blocksRaycasts = true;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }
    private void RefreshStartButton()
    {
        if (startButton) startButton.interactable = queueSearching ? !queueBusy && !submitting :
            !lobbyLocked && accountReady && !loading && !submitting && canContinue?.Invoke() != false;
    }

    private async void LoadOwnedChoices()
    {
        int version = ++requestVersion; string requestedUser = PlayerAuthService.CurrentApiUser?.userId;
        // Keep the saved selection until ownership has been loaded and can be validated.
        loading = true; accountReady = false; choices.Clear(); AddBuiltInChoices();
        RefreshStartButton(); RenderChoices();
        if (PlayerAuthService.IsGuestSession || string.IsNullOrWhiteSpace(requestedUser))
        {
            loading = false; accountReady = PlayerAuthService.IsGuestSession && !onlineMode; NormalizeSelection();
            status.text = accountReady ? "Default loadout is ready. Sign in to use collected skins." : "Sign in to load your collection.";
            RefreshStartButton(); RenderChoices(); return;
        }
        status.text = "Loading your collection...";
        try
        {
            var service = new InventoryService();
            Task<List<ItemCatalogResponse>> catalogTask = service.GetItemsAsync();
            Task<List<InventoryItemResponse>> ownedTask = service.GetInventoryAsync();
            await Task.WhenAll(catalogTask, ownedTask);
            if (!IsCurrent(version, requestedUser)) return;
            var catalog = catalogTask.Result ?? new List<ItemCatalogResponse>();
            var ownedItems = ownedTask.Result ?? new List<InventoryItemResponse>();
            var ownedIds = new HashSet<string>(ownedItems.Where(item => item != null).Select(item => item.itemId), StringComparer.OrdinalIgnoreCase);
            foreach (var item in catalog)
            {
                if (item == null || !item.isActive || !IsLoadoutType(item.type) || !IsLocallyAvailable(item)) continue;
                string key = LocalKey(item);
                string choiceId = CosmeticItemKinds.TryPiece(item.type, out _, out _) ? item.itemId : key;
                AddChoice(choiceId, string.IsNullOrWhiteSpace(item.name) ? item.code : item.name, item.type, ownedIds.Contains(item.itemId), key);
                var choice = FindChoice(item.type, choiceId);
                if (choice == null) continue;
                choice.itemId = item.itemId;
                choice.tier = string.IsNullOrWhiteSpace(item.rarity) ? choice.tier : item.rarity.ToUpperInvariant();
            }
            if (!hasSavedSelection)
            {
                var equipped = PlayerAuthService.CurrentApiUser?.equipped;
                foreach (var item in catalog.Where(item => item != null && IsLoadoutType(item.type) && IsLocallyAvailable(item)))
                {
                    bool active = ownedItems.Any(owned => owned != null && owned.itemId == item.itemId && owned.isEquipped) ||
                        item.itemId == (IsChess(item.type) ? equipped?.chessSkinId : IsBoard(item.type) ? equipped?.boardSkinId : equipped?.backgroundSkinId);
                    if (!active) continue;
                    if (item.type == CosmeticItemKinds.Set) selection.whiteSkinId = selection.blackSkinId = LocalKey(item);
                    else if (IsBoard(item.type)) selection.boardId = LocalKey(item);
                    else if (item.type == CosmeticItemKinds.Background) selection.environmentId = LocalKey(item);
                }
            }
            if (!hasSavedSelection)
            {
                var equipped = PlayerAuthService.CurrentApiUser?.equipped;
                selection.customPieces = equipped?.pieceMode == "CUSTOM";
                selection.pieces.Clear();
                foreach (var binding in equipped?.pieces ?? new List<EquipPieceRequest>())
                {
                    var choice = choices.FirstOrDefault(c => c.itemId == binding.itemId && c.owned);
                    if (choice != null && CosmeticItemKinds.TrySlot(binding.slot, out var team, out var kind) && FitsPiece(choice, team, kind))
                        selection.SetPiece(team, kind, choice.assetKey, choice.itemId);
                }
            }
            accountReady = true; status.text = "Choose your cosmetics, then continue."; NormalizeSelection();
        }
        catch (Exception error)
        {
            if (!IsCurrent(version, requestedUser)) return;
            accountReady = !onlineMode;
            status.text = "Couldn't load your collection. " + (onlineMode ? "Press Refresh to retry. " : "Default items are ready. ") + PlayerNotificationText.FromException(error);
            NormalizeSelection();
        }
        finally
        { if (IsCurrent(version, requestedUser)) { loading = false; RefreshStartButton(); RenderChoices(); } }
    }

    private bool IsCurrent(int version, string user) => this && version == requestVersion && user == PlayerAuthService.CurrentApiUser?.userId;
    private static string LocalKey(ItemCatalogResponse item) => item.isDefault ? item.type == CosmeticItemKinds.Background ? "classic" : CosmeticSelection.LowPolyId : item.unityAssetKey;
    private void AddBuiltInChoices()
    {
        var catalog = cosmetics ? cosmetics.Catalog : null;
        if (catalog)
        {
            foreach (var entry in catalog.pieceSkins ?? new List<CosmeticCatalogEntry>())
                if (entry?.HasArt == true) AddChoice(entry.id, entry.displayName, "CHESS_PIECE_SKIN", entry.id == catalog.defaultPieceSkinId);
            foreach (var entry in catalog.boards ?? new List<CosmeticCatalogEntry>())
                if (entry?.HasArt == true) AddChoice(entry.id, entry.displayName, "BOARD_SKIN", entry.id == catalog.defaultBoardId);
            foreach (var entry in catalog.environments ?? new List<CosmeticCatalogEntry>())
                if (entry?.HasArt == true) AddChoice(entry.id, entry.displayName, CosmeticItemKinds.Background, entry.id == "classic");
        }
        AddChoice("classic", "Classic", "MOVE_EFFECT", true);
        if (!PlayerAuthService.IsGuestSession)
        { AddChoice("ink_sparks", "Ink sparks", "MOVE_EFFECT", true); AddChoice("confetti", "Confetti", "MOVE_EFFECT", true); }
    }
    private bool IsLocallyAvailable(ItemCatalogResponse item) => item != null && !CosmeticItemKinds.KnownMissingCode(item.code) &&
        CosmeticThumbnailUI.HasArt(cosmetics ? cosmetics.Catalog : null, item.type, LocalKey(item));
    private static bool IsLoadoutType(string type) => IsChess(type) || IsBoard(type) || type == CosmeticItemKinds.Background;
    private static bool IsChess(string type) => CosmeticThumbnailUI.IsChess(type);
    private static bool IsBoard(string type) => CosmeticThumbnailUI.IsBoard(type);
    private Choice FindChoice(string type, string id) => choices.FirstOrDefault(item =>
        string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase) &&
        item.type == type);
    private void AddChoice(string id, string name, string type, bool owned, string assetKey = null)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        var existing = FindChoice(type, id);
        if (existing != null) { existing.owned |= owned; if (!string.IsNullOrWhiteSpace(name)) existing.name = name; return; }
        var art = CosmeticThumbnailUI.Find(cosmetics ? cosmetics.Catalog : null, type, assetKey ?? id);
        if (IsLoadoutType(type) && art?.HasArt != true) return;
        choices.Add(new Choice { id = id, name = string.IsNullOrWhiteSpace(name) ? id : name, type = type, owned = owned,
            assetKey = assetKey ?? id, thumbnail = art?.thumbnail, tier = art?.tier ?? "COMMON" });
    }
    private static bool FitsPiece(Choice choice, PieceTeam team, PieceType kind) =>
        choice.type == CosmeticItemKinds.Set || choice.type == CosmeticItemKinds.ItemType(team, kind);
    private string SelectedId() => slot == Slot.Background ? selection.environmentId : slot == Slot.Board ? selection.boardId : slot == Slot.Effect ? selection.moveEffectId :
        playerTeam == PieceTeam.White ? selection.whiteSkinId : selection.blackSkinId;
    private void Select(Choice choice)
    {
        if (!choice.owned || loading || !accountReady || submitting || lobbyLocked) return;
        if (slot == Slot.Background) selection.environmentId = choice.id;
        else if (slot == Slot.Board) selection.boardId = choice.id;
        else if (slot == Slot.Effect) selection.moveEffectId = choice.id;
        else if (editingPiece.HasValue)
        {
            if (!FitsPiece(choice, playerTeam, editingPiece.Value)) return;
            selection.SetPiece(playerTeam, editingPiece.Value, choice.assetKey, choice.itemId);
        }
        else if (onlineMode) selection.whiteSkinId = selection.blackSkinId = choice.id;
        else if (playerTeam == PieceTeam.White) selection.whiteSkinId = choice.id;
        else selection.blackSkinId = choice.id;
        RenderChoices();
    }
    private void NormalizeSelection()
    {
        selection.whiteSkinId = AvailableOrDefault(selection.whiteSkinId, "CHESS_PIECE_SKIN", CosmeticSelection.LowPolyId);
        selection.blackSkinId = AvailableOrDefault(selection.blackSkinId, "CHESS_PIECE_SKIN", CosmeticSelection.LowPolyId);
        selection.pieces.RemoveAll(p => !CosmeticItemKinds.TrySlot(p.slot, out var team, out var kind) ||
            !choices.Any(c => c.owned && c.itemId == p.itemId && c.assetKey == p.skinId && FitsPiece(c, team, kind)));
        selection.boardId = AvailableOrDefault(selection.boardId, "BOARD_SKIN", CosmeticSelection.LowPolyId);
        selection.environmentId = AvailableOrDefault(selection.environmentId, CosmeticItemKinds.Background, "classic");
        if (!choices.Any(item => item.type == "MOVE_EFFECT" && item.owned && item.id == selection.moveEffectId)) selection.moveEffectId = "classic";
    }
    private string AvailableOrDefault(string id, string type, string fallback) => FindChoice(type, id)?.owned == true ? id : fallback;

    private void RenderSummary()
    {
        if (!summaryRoot || selection == null) return;
        ClearChildren(summaryRoot);
        SummaryCard(Slot.Board, "BOARD", selection.boardId, "BOARD_SKIN", 0);
        pieceModeButton.GetComponentInChildren<TextMeshProUGUI>().text = selection.customPieces ? "CUSTOM PIECES  /  SWITCH TO SET" : "FULL SET  /  CUSTOMIZE PIECES";
        bool supportsCustom = !onlineMode || PlayerAuthService.CurrentApiUser?.equipped?.loadoutVersion == 1;
        pieceModeButton.interactable = !loading && !submitting && !lobbyLocked && supportsCustom;
        if (!supportsCustom) pieceModeButton.GetComponentInChildren<TextMeshProUGUI>().text = "CUSTOM ONLINE REQUIRES UPDATED SERVER";
        if (!selection.customPieces)
            SummaryCard(Slot.Chess, "CHESS SET", playerTeam == PieceTeam.White ? selection.whiteSkinId : selection.blackSkinId, CosmeticItemKinds.Set, 640);
        else
        {
            int i = 0;
            foreach (PieceType kind in Enum.GetValues(typeof(PieceType)))
            {
                var piece = kind;
                var binding = selection.GetPiece(playerTeam, piece);
                var choice = binding == null ? FindChoice(CosmeticItemKinds.Set, playerTeam == PieceTeam.White ? selection.whiteSkinId : selection.blackSkinId) :
                    choices.FirstOrDefault(c => c.itemId == binding.itemId && FitsPiece(c, playerTeam, piece));
                var button = UI.Button(summaryRoot, playerTeam + " " + kind + " Skin", "", new Rect(640 + i % 2 * 294, i / 2 * 142, 286, 132), UI.White,
                    () => { editingPiece = piece; OpenPopup(Slot.Chess); });
                UI.Text(button.transform, "Slot", playerTeam + " " + kind, new Rect(10, 9, 266, 34), 24);
                UI.Text(button.transform, "Skin", choice?.name ?? "Base set", new Rect(10, 48, 266, 70), 23, UI.Muted, TextAlignmentOptions.Center, true);
                button.interactable = !loading && !submitting && !lobbyLocked; i++;
            }
        }
        var effect = FindChoice("MOVE_EFFECT", selection.moveEffectId);
        effectButton.GetComponentInChildren<TextMeshProUGUI>().text = "MOVE EFFECT  /  " + (effect?.name ?? "Classic") + "  >";
        backgroundButton.GetComponentInChildren<TextMeshProUGUI>().text = "BACKGROUND  /  " + (FindChoice(CosmeticItemKinds.Background, selection.environmentId)?.name ?? "Classic") + "  >";
    }
    private void SummaryCard(Slot category, string title, string id, string type, float x)
    {
        Choice choice = FindChoice(type, id);
        var button = UI.Button(summaryRoot, title + " Slot", "", new Rect(x, 0, 580, 425), UI.White, () => OpenPopup(category));
        UI.Text(button.transform, "Slot Label", title, new Rect(22, 12, 536, 43), 29);
        CosmeticThumbnailUI.Draw(button.transform, new Rect(22, 66, 536, 278), choice?.thumbnail, true);
        UI.Text(button.transform, "Selected Name", choice?.name ?? "Default", new Rect(22, 357, 536, 47), 27, UI.Ink, TextAlignmentOptions.Center);
        button.interactable = !submitting && !lobbyLocked;
    }
    private void RenderChoices()
    {
        RenderSummary();
        if (!choiceRoot || selection == null) return;
        for (int i = 0; i < categoryButtons.Length; i++) categoryButtons[i].targetGraphic.color = (int)slot == i ? UI.Yellow : UI.White;
        ClearChildren(choiceRoot);
        var visible = choices.Where(item => slot == Slot.Background ? item.type == CosmeticItemKinds.Background : slot == Slot.Effect ? item.type == "MOVE_EFFECT" : slot == Slot.Board ? IsBoard(item.type) :
            editingPiece.HasValue ? FitsPiece(item, playerTeam, editingPiece.Value) : item.type == CosmeticItemKinds.Set);
        if (PlayerAuthService.IsGuestSession) visible = visible.Where(item => item.owned);
        float y = 4;
        foreach (var group in visible.GroupBy(item => item.tier).OrderByDescending(group => CosmeticThumbnailUI.TierRank(group.Key)))
        {
            UI.Card(choiceRoot, group.Key + " Tier", new Rect(4, y, 1288, 42), CosmeticThumbnailUI.TierColor(group.Key), 0, false, 6);
            UI.Text(choiceRoot, group.Key + " Heading", group.Key + "  /  " + group.Count(), new Rect(20, y, 1256, 42), 22);
            y += 58; int i = 0;
            foreach (Choice choice in group.OrderByDescending(item => item.owned).ThenBy(item => item.name, StringComparer.OrdinalIgnoreCase))
            {
                bool active = choice.owned && (slot == Slot.Chess && editingPiece.HasValue ?
                    selection.GetPiece(playerTeam, editingPiece.Value)?.itemId == choice.itemId : string.Equals(choice.id, SelectedId(), StringComparison.OrdinalIgnoreCase));
                var button = UI.Button(choiceRoot, "Choice " + choice.type + " " + choice.id, "",
                    new Rect(6 + (i % 3) * 432, y + (i / 3) * 274, 414, 254), UI.White, () => Select(choice));
                CosmeticThumbnailUI.Draw(button.transform, new Rect(12, 12, 390, 168), choice.thumbnail, active, choice.type == "MOVE_EFFECT" ? choice.id : null);
                UI.Text(button.transform, "Name", choice.name, new Rect(13, 185, 388, 34), 23, UI.Ink, TextAlignmentOptions.Center);
                UI.Text(button.transform, "Ownership", choice.owned ? active ? "Selected for this match" : "Owned" : "Locked",
                    new Rect(13, 221, 388, 24), 18, UI.Muted, TextAlignmentOptions.Center);
                button.interactable = choice.owned && accountReady && !loading && !submitting && !lobbyLocked; i++;
            }
            y += Mathf.CeilToInt(group.Count() / 3f) * 274 + 15;
        }
        choiceRoot.sizeDelta = new Vector2(0, Mathf.Max(574, y));
    }

    private async void Submit()
    {
        if (queueSearching) { if (!queueBusy && !submitting) cancelSearch?.Invoke(); return; }
        if (loading || !accountReady || submitting || lobbyLocked || canContinue?.Invoke() == false) return;
        int version = requestVersion; string user = sessionUser;
        submitting = true; ClosePopup(); RefreshStartButton(); RenderSummary();
        try
        {
            NormalizeSelection();
            if (onlineMode)
            {
                status.text = "Saving equipment...";
                var set = FindChoice(CosmeticItemKinds.Set, selection.whiteSkinId);
                var board = FindChoice(CosmeticItemKinds.Board, selection.boardId);
                if (string.IsNullOrWhiteSpace(set?.itemId) || string.IsNullOrWhiteSpace(board?.itemId))
                    throw new InvalidOperationException("Equipment unavailable. Refresh your collection.");
                if (selection.customPieces)
                {
                    var response = await new InventoryService().EquipLoadoutAsync(new EquipLoadoutRequest {
                        pieceMode = "CUSTOM", chessSkinId = set.itemId, boardSkinId = board.itemId,
                        pieces = selection.pieces.Select(p => new EquipPieceRequest { slot = p.slot, itemId = p.itemId }).ToList()
                    });
                    if (!IsCurrent(version, user)) return;
                    if (response?.success != true || response.equipped == null) throw new InvalidOperationException(response?.message ?? "Unable to save loadout.");
                    PlayerAuthService.CurrentApiUser.equipped = response.equipped;
                }
                else foreach (var choice in new[] { set, board })
                {
                    // Keep the existing endpoint compatible with backend versions that predate custom slots.
                    var response = await new InventoryService().EquipAsync(choice.itemId);
                    if (!IsCurrent(version, user)) return;
                    if (response?.success != true || response.equipped == null) throw new InvalidOperationException(response?.message ?? "Unable to save equipment.");
                    PlayerAuthService.CurrentApiUser.equipped = response.equipped;
                }
                selection.blackSkinId = selection.whiteSkinId;
            }
            // Background is account equipment in both local and online modes;
            // an empty item ID explicitly restores the locked, non-sale default.
            if (!PlayerAuthService.IsGuestSession && (selection.environmentId != "classic" || !string.IsNullOrEmpty(PlayerAuthService.CurrentApiUser?.equipped?.backgroundSkinId)))
            {
                var background = FindChoice(CosmeticItemKinds.Background, selection.environmentId);
                if (selection.environmentId != "classic" && string.IsNullOrWhiteSpace(background?.itemId)) throw new InvalidOperationException("Background unavailable. Refresh your collection.");
                var response = await new InventoryService().EquipBackgroundAsync(background?.itemId);
                if (!IsCurrent(version, user)) return;
                if (response?.success != true || response.equipped == null) throw new InvalidOperationException(response?.message ?? "Unable to save background.");
                PlayerAuthService.CurrentApiUser.equipped = response.equipped;
            }
            if (!IsCurrent(version, user)) return;
            selection.Save(); startAction?.Invoke(selection.Copy());
        }
        catch (Exception error) { if (IsCurrent(version, user)) status.text = PlayerNotificationText.FromException(error); }
        finally { if (this) { submitting = false; RefreshStartButton(); RenderSummary(); } }
    }
    private static void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject; child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
    }
    private void Update()
    {
        if (overlay && overlay.gameObject.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ClosePopup();
    }
    private void OnDisable() { requestVersion++; loading = submitting = false; ClosePopup(); }
}

