using System;
using System.Linq;

// Persistence checks do not infer a pending payment from ownership or a non-null JSON object.
public static class ShopPurchaseState
{
    public static bool IsValid(ShopPurchaseRequest request) => request != null &&
        Guid.TryParse(request.requestId, out var id) && id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(request.offerId) && request.offerId.Length <= 80 && request.expectedPrice > 0 &&
        (request.currency == "GOLDS" || request.currency == "DIAMONDS") &&
        request.expectedItemIds != null && request.expectedItemIds.Count >= 1 && request.expectedItemIds.Count <= 12 &&
        request.expectedItemIds.All(IsItemId) &&
        request.expectedItemIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() == request.expectedItemIds.Count;

    private static bool IsItemId(string value) => value != null && value.Length == 24 && value.All(Uri.IsHexDigit);

    // Older saves have no explicit owner: migrate only from the matching per-account profile.
    public static bool BelongsToAccount(ShopPurchaseRequest request, string ownerId, string profileId, string accountId) =>
        IsValid(request) && !string.IsNullOrWhiteSpace(accountId) &&
        string.Equals(profileId, accountId, StringComparison.Ordinal) &&
        (string.IsNullOrEmpty(ownerId) || string.Equals(ownerId, accountId, StringComparison.Ordinal));

    public static bool IsRetryFor(ShopPurchaseRequest pending, string offerId, bool includedOrLocked) =>
        !includedOrLocked && IsValid(pending) && string.Equals(pending.offerId, offerId, StringComparison.Ordinal);

    public static bool CanAfford(ShopPurchaseRequest request, long gold, long diamonds) => IsValid(request) &&
        (request.currency == "GOLDS" ? gold : diamonds) >= request.expectedPrice;
}
