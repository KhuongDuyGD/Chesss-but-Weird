using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI = SketchbookUI;

public enum ShopCategory { Featured, PieceSkins, Boards, Effects, Bundles, Collection, Summon }

public sealed partial class ShopMenuController : MonoBehaviour
{
    private sealed class Product
    {
        public string id, name, description, itemId, type, key, rarity;
        public Sprite art;
        public ShopOfferResponse offer;
        public bool owned, equipped, builtIn, isDefault;
        public ShopCategory Category => offer?.isBundle == true ? ShopCategory.Bundles :
            type == "MOVE_EFFECT" ? ShopCategory.Effects : type == CosmeticItemKinds.Board ? ShopCategory.Boards : ShopCategory.PieceSkins;
    }
    private readonly List<Product> products = new List<Product>();
    private readonly HashSet<string> owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private IShopGateway gateway;
    private ShopSnapshot snapshot;
    private UnityAction close, summon;
    private Product selected;
    private ShopCategory category;
    private ShopPurchaseRequest quote;
    private bool loading, busy, initialized, onlyEquipped;
    private int page, version, collectionKind;
    private string sessionUser;
    // Tests use an injected gateway and ephemeral pending state, never the player's wallet/save.
    private bool previewSession;
    private ShopPurchaseRequest previewPending;
    public ShopCategory CurrentCategory => category;
    public bool IsBusy => busy || loading;
    public int VisibleCount => VisibleProducts().Count;

    public void Initialize(UnityAction onClose, UnityAction onSummon, IShopGateway service = null, bool isolatedPreview = false)
    {
        if (initialized) return;
        initialized = true; gateway = service ?? new ShopService(); close = onClose; summon = onSummon; previewSession = isolatedPreview;
        backButton.onClick.AddListener(Back);
        refreshButton.onClick.AddListener(() => { if (!busy) _ = RefreshAsync(); });
        previousButton.onClick.AddListener(() => { page--; Render(); });
        nextButton.onClick.AddListener(() => { page++; Render(); });
        for (int i = 0; i < tabs.Length; i++) { var value = (ShopCategory)i; tabs[i].onClick.AddListener(() => ShowCategory(value)); }
        search.onValueChanged.AddListener(_ => { page = 0; Render(); });
        goldButton.onClick.AddListener(() => Confirm("GOLDS"));
        diamondButton.onClick.AddListener(() => Confirm("DIAMONDS"));
        equipButton.onClick.AddListener(() => { if (Pending != null) RecoverPending(); else _ = EquipAsync(); });
        cancelButton.onClick.AddListener(DismissConfirmation);
        confirmButton.onClick.AddListener(() => _ = PurchaseAsync());
        collectionFilter.onClick.AddListener(() => { collectionKind = (collectionKind + 1) % 4; page = 0; Render(); });
        equippedFilter.onClick.AddListener(() => { onlyEquipped = !onlyEquipped; page = 0; Render(); });
        heroButton.onClick.AddListener(() =>
        {
            var hero = products.FirstOrDefault(p => p.offer?.featured == true && !p.owned) ?? products.FirstOrDefault(p => p.offer?.featured == true);
            if (hero != null) { selected = hero; Render(); }
        });
    }

    public void Open(bool preserveCategory = false)
    {
        if (!initialized) return;
        if (!preserveCategory) { category = ShopCategory.Featured; page = collectionKind = 0; onlyEquipped = false; search.SetTextWithoutNotify(""); }
        DismissConfirmation();
        if (!busy) _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (busy || !initialized) return;
        int epoch = ++version;
        if (sessionUser != PlayerAuthService.UserId) { snapshot = null; selected = null; }
        sessionUser = PlayerAuthService.UserId;
        loading = true;
        if (snapshot == null) RebuildProducts();
        Render(); SetStatus("Loading your shop and collection...");
        try
        {
            var result = await gateway.LoadAsync();
            if (!Current(epoch)) return;
            if (result.user == null || (!previewSession && result.user.userId != sessionUser)) throw new InvalidOperationException("The account changed. Reopen the shop.");
            snapshot = result;
            if (!previewSession) PlayerAuthService.ApplyApiUser(result.user);
            RebuildProducts();
            SetStatus(Pending != null ? "A purchase is awaiting confirmation. Select RETRY PURCHASE to recover it." : result.catalogError ?? "Cosmetics only. Make your next match your own.");
        }
        catch (Exception error)
        {
            if (Current(epoch)) SetStatus("Couldn't refresh the shop. " + PlayerNotificationText.FromException(error));
        }
        finally { if (Current(epoch)) { loading = false; Render(); } }
    }

    private void RebuildProducts()
    {
        string selectedId = selected?.id; products.Clear(); owned.Clear();
        foreach (var item in snapshot?.owned ?? new List<InventoryItemResponse>()) if (item != null) owned.Add(item.itemId);
        foreach (var item in snapshot?.items ?? new List<ItemCatalogResponse>()) if (item != null && item.isDefault) owned.Add(item.itemId);
        var artCatalog = presentation.cosmetics;
        foreach (var offer in snapshot?.catalog?.offers ?? new List<ShopOfferResponse>())
        {
            if (!ValidOffer(offer, artCatalog)) continue;
            var item = offer.items[0];
            var product = FromItem(item.itemId, item.name, item.type, item.unityAssetKey, item.rarity);
            product.id = "offer:" + offer.offerId; product.name = offer.name; product.description = offer.description; product.offer = offer;
            product.owned = offer.items.All(i => owned.Contains(i.itemId));
            product.equipped = !offer.isBundle && IsEquipped(item.itemId, item.type, item.unityAssetKey);
            products.Add(product);
        }
        foreach (var item in snapshot?.items ?? new List<ItemCatalogResponse>())
        {
            if (item == null || !item.isActive || CosmeticItemKinds.KnownMissingCode(item.code) ||
                products.Any(p => p.offer?.isBundle == false && p.itemId == item.itemId)) continue;
            string key = item.isDefault ? CosmeticSelection.LowPolyId : item.unityAssetKey;
            if (!CosmeticThumbnailUI.HasArt(artCatalog, item.type, key)) continue;
            var product = FromItem(item.itemId, item.name, item.type, key, item.rarity);
            product.isDefault = item.isDefault; products.Add(product);
        }
        // Cached local art keeps browsing useful when the server is unavailable; it never grants ownership.
        if (snapshot == null)
        {
            foreach (var entry in artCatalog.pieceSkins.Where(e => e?.HasArt == true)) products.Add(FromItem(null, entry.displayName, CosmeticItemKinds.Set, entry.id, entry.tier));
            foreach (var entry in artCatalog.boards.Where(e => e?.HasArt == true)) products.Add(FromItem(null, entry.displayName, CosmeticItemKinds.Board, entry.id, entry.tier));
        }
        string[] effects = { "classic", "ink_sparks", "confetti" }, names = { "Classic", "Ink sparks", "Confetti" };
        string effect = previewSession ? "classic" : CosmeticSelection.Load().moveEffectId;
        for (int i = 0; i < effects.Length; i++) products.Add(new Product { id = "effect:" + effects[i], name = names[i], key = effects[i],
            type = "MOVE_EFFECT", rarity = "BUILT-IN", builtIn = true, owned = true, equipped = effect == effects[i], isDefault = i == 0,
            description = i == 0 ? "The original move effect. Quiet, clean, and always yours." :
                i == 1 ? "A small burst of ink marks each move. Already included in your collection." : "A playful sprinkle of confetti after every move. Already included in your collection." });
        selected = products.FirstOrDefault(p => p.id == selectedId);
    }
    private Product FromItem(string itemId, string name, string type, string key, string tier) => new Product
    {
        id = "item:" + type + ":" + (itemId ?? key), itemId = itemId, name = name, type = type, key = key, rarity = tier,
        art = CosmeticThumbnailUI.Find(presentation.cosmetics, type, key)?.thumbnail,
        owned = itemId != null && owned.Contains(itemId), equipped = IsEquipped(itemId, type, key),
        description = type == CosmeticItemKinds.Board ? "A fresh place to play. Changes the board's appearance in your matches." : "Dress up your next match. A cosmetic look with the same familiar moves."
    };
    private bool IsEquipped(string id, string type, string key)
    {
        if (id == null) return false;
        var equipped = snapshot?.user?.equipped;
        return snapshot?.owned?.Any(i => i != null && i.itemId == id && i.isEquipped) == true ||
            (type == CosmeticItemKinds.Set && equipped?.pieceMode != "CUSTOM" && equipped?.chessSkinId == id) ||
            (type == CosmeticItemKinds.Board && equipped?.boardSkinId == id);
    }
    private static bool ValidOffer(ShopOfferResponse offer, CosmeticCatalog catalog)
    {
        if (offer == null || string.IsNullOrWhiteSpace(offer.offerId) || offer.items == null || offer.items.Count == 0 || offer.items.Count > 12 ||
            offer.isBundle != (offer.items.Count > 1) || offer.discountPercent < 0 || offer.discountPercent > 90) return false;
        return offer.items.All(i => i != null && !string.IsNullOrWhiteSpace(i.itemId) && i.golds > 0 && i.diamonds > 0 &&
            i.golds <= 1000000000 && i.diamonds <= 1000000000 && CosmeticThumbnailUI.HasArt(catalog, i.type, i.unityAssetKey)) &&
            offer.items.Select(i => i.itemId).Distinct().Count() == offer.items.Count;
    }
    public void ShowCategory(ShopCategory value)
    {
        if (busy || confirmation.gameObject.activeSelf) return;
        if (value == ShopCategory.Summon) { summon?.Invoke(); return; }
        category = value; page = 0; selected = null; search.SetTextWithoutNotify(""); Render();
    }
    private List<Product> VisibleProducts()
    {
        string query = search ? search.text.Trim() : "";
        return products.Where(p => (category == ShopCategory.Featured ? p.offer?.featured == true || snapshot?.catalog == null && !p.builtIn :
            category == ShopCategory.Collection ? p.owned && p.Category != ShopCategory.Bundles && (!onlyEquipped || p.equipped) &&
                (collectionKind == 0 || collectionKind == 1 && p.Category == ShopCategory.PieceSkins || collectionKind == 2 && p.Category == ShopCategory.Boards || collectionKind == 3 && p.Category == ShopCategory.Effects) : p.Category == category) &&
            (query.Length == 0 || p.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
            .OrderByDescending(p => category == ShopCategory.Collection && p.equipped).ThenBy(p => p.name, StringComparer.OrdinalIgnoreCase).ToList();
    }
    private void Render()
    {
        if (!initialized) return;
        for (int i = 0; i < tabs.Length; i++) { tabs[i].targetGraphic.color = (int)category == i ? UI.Yellow : i == 6 ? UI.Lavender : UI.White; tabs[i].interactable = !busy; }
        SetButtonVisible(collectionFilter, category == ShopCategory.Collection); SetButtonVisible(equippedFilter, category == ShopCategory.Collection);
        header.gameObject.SetActive(category != ShopCategory.Collection);
        header.text = category == ShopCategory.Featured ? "Featured finds" : CategoryLabel(category);
        collectionFilter.GetComponentInChildren<TextMeshProUGUI>().text = new[] { "ALL OWNED", "PIECE SKINS", "BOARDS", "EFFECTS" }[collectionKind];
        equippedFilter.targetGraphic.color = onlyEquipped ? UI.Yellow : UI.White;
        collectionFilter.interactable = equippedFilter.interactable = !busy;
        var visible = VisibleProducts(); page = Mathf.Clamp(page, 0, Mathf.Max(0, (visible.Count - 1) / 6));
        if (selected == null || !products.Contains(selected)) selected = visible.FirstOrDefault();
        for (int i = 0; i < cards.Length; i++)
        {
            var product = visible.Skip(page * 6 + i).FirstOrDefault(); cards[i].gameObject.SetActive(product != null);
            if (product == null) continue;
            cards[i].Bind(product.name, product.Category == ShopCategory.Bundles ? "BUNDLE / " + product.offer.items.Count + " ITEMS" :
                CategoryLabel(product.Category).ToUpperInvariant() + " / " + product.rarity, PriceText(product),
                product.equipped ? "EQUIPPED" : product.owned ? "OWNED" : product.offer?.discountPercent > 0 ? "SAVE " + product.offer.discountPercent + "%" : "",
                product.art, product.builtIn ? product.key : null, product.equipped ? UI.Yellow : UI.Green, product == selected,
                () => { selected = product; Render(); });
            cards[i].Button.interactable = !busy;
        }
        previousButton.interactable = page > 0 && !busy; nextButton.interactable = (page + 1) * 6 < visible.Count && !busy;
        pageLabel.text = (page + 1) + " / " + Mathf.Max(1, (visible.Count + 5) / 6);
        count.text = visible.Count == 0 ? "No items match. Try another category or search." : visible.Count + " looks  /  Cosmetics only. No gameplay advantage.";
        refreshButton.interactable = !busy && !loading; backButton.interactable = !busy; search.interactable = !busy;
        goldBalance.text = snapshot?.user?.wallet?.golds.ToString("N0") ?? "--";
        diamondBalance.text = snapshot?.user?.wallet?.diamonds.ToString("N0") ?? "--";
        var hero = products.FirstOrDefault(p => p.offer?.featured == true && !p.owned) ?? products.FirstOrDefault(p => p.offer?.featured == true);
        heroTitle.text = hero?.name ?? "Your next favourite look"; heroArt.sprite = hero?.art; heroArt.enabled = heroArt.sprite;
        heroCopy.text = hero != null ? (hero.offer.isBundle ? "Complete the look. Only pay for what you still need.  >" : "A closer look is one click away.  >") : presentation.subtitle;
        heroButton.interactable = hero != null && !busy;
        RenderDetail();
    }
    private string PriceText(Product product) => product.owned ? product.builtIn ? "Included in your collection" : "In your collection" : product.offer != null ?
        ShopPurchasePolicy.Price(product.offer, owned, "GOLDS").ToString("N0") + " Gold  /  " + ShopPurchasePolicy.Price(product.offer, owned, "DIAMONDS").ToString("N0") + " Diamonds" : "Available through Summon";
    private void RenderDetail()
    {
        var p = selected;
        detailTitle.text = p?.name ?? "A little room for something new";
        detailType.text = p == null ? "YOUR COLLECTION" : p.Category == ShopCategory.Bundles ? "BUNDLE / " + p.offer.items.Count + " ITEMS" : CategoryLabel(p.Category).ToUpperInvariant() + " / " + p.rarity;
        detailArt.sprite = p?.art; detailArt.enabled = detailArt.sprite;
        detailEffect.gameObject.SetActive(p?.builtIn == true);
        detailEffect.Configure(SketchbookDoodle.Shape.Star, p?.key == "confetti" ? UI.Pink : UI.Ink);
        detailDescription.text = p?.description ?? "Select a category to find your next favourite look.";
        detailContents.text = p?.offer?.isBundle == true ? string.Join("\n", p.offer.items.Select(i => (owned.Contains(i.itemId) ? "Owned: " : "Includes: ") + i.name)) :
            p?.isDefault == true ? "The original look. Always included." : "";
        detailState.text = p == null ? "" : p.equipped ? "Equipped for your next match" : p.owned ? "Yours to keep" : p.offer?.isBundle == true ?
            (p.offer.items.Count - ShopPurchasePolicy.Missing(p.offer, owned).Count) + " / " + p.offer.items.Count + " already owned. Price adjusted below." : "Choose Gold or Diamonds";
        bool pending = Pending != null;
        bool sale = p?.offer != null && !p.owned && !pending;
        SetButtonVisible(goldButton, sale); SetButtonVisible(diamondButton, sale);
        SetButtonVisible(equipButton, !sale);
        if (sale)
        {
            long gold = ShopPurchasePolicy.Price(p.offer, owned, "GOLDS"), diamonds = ShopPurchasePolicy.Price(p.offer, owned, "DIAMONDS");
            goldPrice.text = gold.ToString("N0") + " Gold"; diamondPrice.text = diamonds.ToString("N0") + " Diamonds";
            goldButton.interactable = !busy && !loading && (snapshot?.user?.wallet?.golds ?? 0) >= gold;
            diamondButton.interactable = !busy && !loading && (snapshot?.user?.wallet?.diamonds ?? 0) >= diamonds;
            if (!goldButton.interactable && !diamondButton.interactable && !busy && !loading) detailState.text = "Not enough Gold or Diamonds yet";
        }
        equipLabel.text = pending ? "RETRY PURCHASE" : p == null ? "EXPLORE THE SHOP" : p.Category == ShopCategory.Bundles ? "BUNDLE OWNED" :
            !p.owned ? "OPEN SUMMON" : p.equipped ? p.isDefault ? "DEFAULT EQUIPPED" : "USE DEFAULT" : "EQUIP";
        equipButton.interactable = !busy && !loading && (pending || p != null &&
            (p.owned && p.Category != ShopCategory.Bundles && !(p.equipped && p.isDefault) || !p.owned));
    }
    private static void SetButtonVisible(Button button, bool visible)
    {
        button.gameObject.SetActive(visible);
        // SketchbookUI creates pencil shadows as siblings of their button faces.
        var shadow = button.transform.parent.Find(button.name + " Pencil Shadow");
        if (shadow) shadow.gameObject.SetActive(visible);
    }
    private void Confirm(string currency)
    {
        if (busy || loading || selected?.offer == null || selected.owned || Pending != null) return;
        quote = ShopPurchasePolicy.Create(selected.offer, owned, currency);
        confirmationTitle.text = "Make it yours?";
        confirmationText.text = selected.name + "\n\n" + quote.expectedPrice.ToString("N0") + (currency == "GOLDS" ? " Gold" : " Diamonds") +
            "\n" + quote.expectedItemIds.Count + " new item" + (quote.expectedItemIds.Count == 1 ? "" : "s") + " for your collection.";
        ShowConfirmation("CONFIRM PURCHASE");
    }
    private void RecoverPending()
    {
        if (busy || loading || Pending == null) return;
        quote = Pending; confirmationTitle.text = "Finish your purchase";
        confirmationText.text = "Check the purchase you already confirmed.\n\n" + quote.expectedPrice.ToString("N0") +
            (quote.currency == "GOLDS" ? " Gold" : " Diamonds") + "\nYou will only be charged once.";
        ShowConfirmation("RETRY PURCHASE");
    }
    private void ShowConfirmation(string action)
    {
        confirmLabel.text = action; controls.interactable = controls.blocksRaycasts = false;
        confirmation.gameObject.SetActive(true); cancelButton.interactable = confirmButton.interactable = true;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
    }
    private void DismissConfirmation()
    {
        if (busy) return;
        confirmation.gameObject.SetActive(false); controls.interactable = controls.blocksRaycasts = true; quote = null;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(backButton.gameObject);
    }
    public async Task PurchaseAsync()
    {
        if (busy || quote == null || !confirmation.gameObject.activeSelf || !Current(version)) return;
        busy = true; var request = quote; int epoch = version;
        confirmButton.interactable = cancelButton.interactable = false; confirmLabel.text = "CONFIRMING...";
        string message;
        try
        {
            Pending = request; // Durable before sending, so an interrupted response can be recovered.
            var receipt = await gateway.PurchaseAsync(request);
            if (!CurrentAccount()) return;
            if (!ShopPurchasePolicy.Matches(request, receipt)) throw new InvalidOperationException("The purchase response could not be confirmed.");
            Pending = null;
            message = "Purchased! Your new look is in your collection.";
        }
        catch (Exception error)
        {
            if (!CurrentAccount()) return;
            if (ShopPurchasePolicy.DefinitiveFailure(error)) { Pending = null; message = ShopPurchasePolicy.ErrorMessage(error); }
            else message = "Purchase not confirmed yet. Select RETRY PURCHASE to check it safely.";
        }
        finally { busy = false; }
        if (!Current(epoch)) return;
        DismissConfirmation();
        await RefreshAsync();
        SetStatus(message);
    }
    private async Task EquipAsync()
    {
        if (busy || loading || selected == null || !CurrentAccount()) return;
        var p = selected;
        if (!p.owned) { summon?.Invoke(); return; }
        if (p.Category == ShopCategory.Bundles) return;
        busy = true; int epoch = version; Render();
        try
        {
            string key = p.key;
            if (p.builtIn)
            {
                if (p.equipped) key = "classic";
                if (!previewSession) { var selection = CosmeticSelection.Load(); selection.moveEffectId = key; selection.Save(); }
                else foreach (var item in products.Where(i => i.builtIn)) item.equipped = item.key == key;
            }
            else
            {
                var target = p;
                if (p.equipped) target = products.FirstOrDefault(i => i.isDefault && (p.type == CosmeticItemKinds.Board ? i.type == CosmeticItemKinds.Board : i.type == CosmeticItemKinds.Set));
                if (target == null || string.IsNullOrWhiteSpace(target.itemId)) throw new InvalidOperationException("Refresh your collection to restore the default look.");
                var result = await gateway.EquipAsync(target.itemId);
                if (!CurrentAccount()) return;
                if (result?.success != true || result.equipped == null) throw new InvalidOperationException("The equipment change was not confirmed.");
                if (!previewSession)
                {
                    var selection = CosmeticSelection.Load();
                    if (target.type == CosmeticItemKinds.Board) selection.boardId = target.key;
                    else if (target.type == CosmeticItemKinds.Set) { selection.whiteSkinId = selection.blackSkinId = target.key; selection.customPieces = false; }
                    else if (CosmeticItemKinds.TryPiece(target.type, out var team, out var piece)) { selection.SetPiece(team, piece, target.key, target.itemId); selection.customPieces = true; }
                    selection.Save();
                    if (PlayerAuthService.CurrentApiUser != null) PlayerAuthService.CurrentApiUser.equipped = result.equipped;
                }
            }
            if (Current(epoch)) SetStatus("Your look is saved for your next match.");
        }
        catch (Exception error) { if (Current(epoch)) SetStatus("Couldn't equip this look. " + PlayerNotificationText.FromException(error)); }
        finally { busy = false; if (Current(epoch)) { if (!p.builtIn) await RefreshAsync(); else { if (!previewSession) RebuildProducts(); Render(); } } }
    }
    private ShopPurchaseRequest Pending
    {
        get => previewSession ? previewPending : CurrentAccount() ? PlayerProfileStore.Data.pendingShopPurchase : null;
        set { if (previewSession) previewPending = value; else if (CurrentAccount()) { PlayerProfileStore.Data.pendingShopPurchase = value; PlayerProfileStore.Save(); } }
    }
    private bool CurrentAccount() => previewSession || sessionUser == PlayerAuthService.UserId && !PlayerAuthService.IsGuestSession;
    private bool Current(int epoch) => this && epoch == version && CurrentAccount();
    private static string CategoryLabel(ShopCategory value) => value == ShopCategory.PieceSkins ? "Piece skins" : value.ToString();
    private void SetStatus(string text) { if (status) status.text = text; }
    private void Back() { if (!busy) { if (confirmation.gameObject.activeSelf) DismissConfirmation(); else close?.Invoke(); } }
    private void Update()
    {
        if (!initialized || busy || !controls.gameObject.activeInHierarchy) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !(search && search.isFocused)) Back();
    }
    private void OnDisable()
    {
        version++; loading = false;
        if (confirmation && !busy) { confirmation.gameObject.SetActive(false); controls.interactable = controls.blocksRaycasts = true; quote = null; }
    }
}
