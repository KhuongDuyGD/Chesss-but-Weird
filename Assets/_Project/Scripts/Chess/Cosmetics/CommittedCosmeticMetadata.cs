using System;

// Presentation-only: old/malformed receipts remain readable and receive no invented artwork metadata.
public static class CommittedCosmeticMetadata
{
    public static GachaRewardMetadataResponse ForSlot(GachaRollResponse receipt, int index)
    {
        var snapshot = receipt?.presentationSnapshot;
        if (snapshot == null || snapshot.schemaVersion != 1 || snapshot.metadataSource != "TRANSACTION_ITEM_READ" ||
            receipt.results == null || snapshot.rewards == null || snapshot.rewards.Count != receipt.results.Count ||
            index < 0 || index >= receipt.results.Count) return null;
        var seen = new bool[receipt.results.Count];
        GachaRewardMetadataResponse match = null;
        foreach (var row in snapshot.rewards)
        {
            if (row == null || row.slotIndex < 0 || row.slotIndex >= seen.Length || seen[row.slotIndex]) return null;
            var reward = receipt.results[row.slotIndex];
            if (reward == null || string.IsNullOrWhiteSpace(row.itemId) || row.itemId != reward.itemId || row.itemCode != reward.itemCode) return null;
            seen[row.slotIndex] = true;
            if (row.slotIndex == index) match = row;
        }
        return match;
    }
}
