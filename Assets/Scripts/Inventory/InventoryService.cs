using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class InventoryService
{
    private readonly ApiClient client;

    public InventoryService(ApiClient client = null) => this.client = client ?? ApiClient.Shared;

    public Task<List<ItemCatalogResponse>> GetItemsAsync() =>
        client.GetAsync<List<ItemCatalogResponse>>("/api/items");

    public Task<List<InventoryItemResponse>> GetInventoryAsync() =>
        client.GetAsync<List<InventoryItemResponse>>("/api/inventory", true);

    public Task<EquipItemResponse> EquipAsync(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("Item ID is required.", nameof(itemId));
        return client.PutAsync<EquipItemResponse>("/api/inventory/equip",
            new EquipItemRequest { itemId = itemId.Trim() }, true);
    }
}
