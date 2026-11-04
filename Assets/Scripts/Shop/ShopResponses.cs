using System;
using System.Collections.Generic;

[Serializable] public sealed class ShopCatalogResponse { public List<ShopOfferResponse> offers = new List<ShopOfferResponse>(); }
[Serializable] public sealed class ShopOfferResponse
{
    public string offerId, name, description;
    public bool featured, isBundle;
    public int discountPercent;
    public List<ShopProductResponse> items = new List<ShopProductResponse>();
}
[Serializable] public sealed class ShopProductResponse
{
    public string itemId, name, type, unityAssetKey, rarity;
    public long golds, diamonds;
}
[Serializable] public sealed class ShopPurchaseRequest
{
    public string requestId, offerId, currency;
    public long expectedPrice;
    public List<string> expectedItemIds = new List<string>();
}
[Serializable] public sealed class ShopPurchaseResponse
{
    public string requestId, offerId, currency;
    public long totalCost;
    public List<string> grantedItemIds;
    public GachaWalletResponse wallet;
}
