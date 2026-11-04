using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public sealed class ShopSnapshot
{
    public ShopCatalogResponse catalog;
    public List<ItemCatalogResponse> items = new List<ItemCatalogResponse>();
    public List<InventoryItemResponse> owned = new List<InventoryItemResponse>();
    public UserMeResponse user;
    public string catalogError;
}

public interface IShopGateway
{
    Task<ShopSnapshot> LoadAsync();
    Task<ShopPurchaseResponse> PurchaseAsync(ShopPurchaseRequest request);
    Task<EquipItemResponse> EquipAsync(string itemId);
}

public sealed class ShopService : IShopGateway
{
    private readonly ApiClient client;
    private readonly InventoryService inventory;
    public ShopService(ApiClient client = null)
    { this.client = client ?? ApiClient.Shared; inventory = new InventoryService(this.client); }

    public async Task<ShopSnapshot> LoadAsync()
    {
        var snapshot = new ShopSnapshot();
        var shopTask = LoadCatalog(snapshot);
        var itemsTask = inventory.GetItemsAsync();
        var ownedTask = inventory.GetInventoryAsync();
        var userTask = new UserService(client).GetMeAsync();
        await Task.WhenAll(shopTask, itemsTask, ownedTask, userTask);
        snapshot.items = itemsTask.Result ?? new List<ItemCatalogResponse>();
        snapshot.owned = ownedTask.Result ?? new List<InventoryItemResponse>();
        snapshot.user = userTask.Result;
        return snapshot;
    }
    private async Task LoadCatalog(ShopSnapshot snapshot)
    {
        try { snapshot.catalog = await client.GetAsync<ShopCatalogResponse>("/api/shop/catalog"); }
        catch (Exception error) { snapshot.catalogError = error is ApiException api && api.StatusCode == 404
            ? "Shop purchases are not available on this server yet. Your collection is still available."
            : "Couldn't load shop prices. Select Refresh to try again."; }
    }
    public Task<ShopPurchaseResponse> PurchaseAsync(ShopPurchaseRequest request)
    {
        if (request == null || !Guid.TryParse(request.requestId, out var id) || id == Guid.Empty || request.expectedPrice <= 0 ||
            (request.currency != "GOLDS" && request.currency != "DIAMONDS") || request.expectedItemIds == null || request.expectedItemIds.Count == 0)
            throw new ArgumentException("Invalid shop purchase.");
        return client.PostAsync<ShopPurchaseResponse>("/api/shop/purchase", request, true);
    }
    public Task<EquipItemResponse> EquipAsync(string itemId) => inventory.EquipAsync(itemId);
}

public static class ShopPurchasePolicy
{
    public static List<ShopProductResponse> Missing(ShopOfferResponse offer, ISet<string> owned) =>
        offer?.items?.Where(i => i != null && !owned.Contains(i.itemId)).ToList() ?? new List<ShopProductResponse>();

    public static long Price(ShopOfferResponse offer, ISet<string> owned, string currency)
    {
        if (offer == null || offer.discountPercent < 0 || offer.discountPercent > 90 ||
            currency != "GOLDS" && currency != "DIAMONDS") throw new ArgumentException("Invalid shop quote.");
        long total = 0;
        foreach (var item in Missing(offer, owned))
        {
            long amount = currency == "GOLDS" ? item.golds : item.diamonds;
            if (amount <= 0 || amount > 1000000000) throw new ArgumentException("Invalid shop price.");
            total = checked(total + amount);
        }
        return checked((total * (100 - offer.discountPercent) + 99) / 100);
    }

    public static ShopPurchaseRequest Create(ShopOfferResponse offer, ISet<string> owned, string currency) => new ShopPurchaseRequest
    {
        requestId = Guid.NewGuid().ToString("D"), offerId = offer.offerId, currency = currency,
        expectedPrice = Price(offer, owned, currency), expectedItemIds = Missing(offer, owned).Select(i => i.itemId).ToList()
    };

    public static bool Matches(ShopPurchaseRequest request, ShopPurchaseResponse receipt) => request != null && receipt != null &&
        request.requestId == receipt.requestId && request.offerId == receipt.offerId && request.currency == receipt.currency &&
        request.expectedPrice == receipt.totalCost && receipt.grantedItemIds != null &&
        receipt.grantedItemIds.Count == request.expectedItemIds.Count &&
        new HashSet<string>(request.expectedItemIds, StringComparer.OrdinalIgnoreCase).SetEquals(receipt.grantedItemIds);

    public static bool DefinitiveFailure(Exception error) => error is ApiException api &&
        (api.StatusCode == 400 || api.StatusCode == 403 || api.StatusCode == 404 || api.StatusCode == 409);
    public static string ErrorMessage(Exception error)
    {
        if (error is ApiException api)
        {
            switch (api.ErrorCode)
            {
                case "INSUFFICIENT_CURRENCY": return "You don't have enough for this purchase. Your balance has been refreshed.";
                case "ALREADY_OWNED": return "You already own everything in this offer.";
                case "QUOTE_CHANGED": return "The price or bundle contents changed. Review the updated offer before buying.";
                case "OFFER_UNAVAILABLE": return "This offer is no longer available.";
                case "PURCHASE_CONFLICT": return "Your collection changed. Refresh and review the offer again.";
                case "REQUEST_CONFLICT": return "This request does not match the saved purchase. Refresh the shop.";
            }
        }
        return PlayerNotificationText.FromException(error);
    }
}
