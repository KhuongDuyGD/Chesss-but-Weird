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
    Task<EquipItemResponse> EquipBackgroundAsync(string itemId);
}

public sealed class ShopService : IShopGateway
{
    private readonly ApiClient client;
    private readonly InventoryService inventory;
    private long loadedSessionVersion = -1;
    private string loadedUser;
    public ShopService(ApiClient client = null)
    { this.client = client ?? ApiClient.Shared; inventory = new InventoryService(this.client); }

    public async Task<ShopSnapshot> LoadAsync()
    {
        long sessionVersion = AuthStorage.SessionVersion;
        string userId = PlayerAuthService.UserId;
        var snapshot = new ShopSnapshot();
        var shopTask = LoadCatalog(snapshot);
        var itemsTask = inventory.GetItemsAsync();
        var ownedTask = inventory.GetInventoryAsync();
        var userTask = new UserService(client).GetMeAsync();
        await Task.WhenAll(shopTask, itemsTask, ownedTask, userTask);
        snapshot.items = itemsTask.Result ?? new List<ItemCatalogResponse>();
        snapshot.owned = ownedTask.Result ?? new List<InventoryItemResponse>();
        snapshot.user = userTask.Result;
        if (sessionVersion != AuthStorage.SessionVersion || snapshot.user?.userId != userId || PlayerAuthService.UserId != userId)
            throw new OperationCanceledException("The account changed while loading the shop.");
        loadedSessionVersion = sessionVersion; loadedUser = userId;
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
        if (!ShopPurchaseState.IsValid(request))
            throw new ArgumentException("Invalid shop purchase.");
        if (loadedUser != PlayerAuthService.UserId || string.IsNullOrEmpty(loadedUser))
            throw new OperationCanceledException("Reopen the shop for the current account.");
        return client.PostForSessionAsync<ShopPurchaseResponse>("/api/shop/purchase", request, loadedSessionVersion);
    }
    public Task<EquipItemResponse> EquipAsync(string itemId) => inventory.EquipAsync(itemId, loadedSessionVersion);
    public Task<EquipItemResponse> EquipBackgroundAsync(string itemId) => inventory.EquipBackgroundAsync(itemId, loadedSessionVersion);
}
