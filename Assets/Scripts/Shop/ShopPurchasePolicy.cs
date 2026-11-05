using System;
using System.Collections.Generic;
using System.Linq;

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
