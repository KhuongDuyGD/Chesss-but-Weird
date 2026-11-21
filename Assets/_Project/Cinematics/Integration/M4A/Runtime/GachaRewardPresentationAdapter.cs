#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.Collections.Generic;

namespace ChessButWeird.CinematicIntegration.M4A
{
    /// <summary>Pure deterministic projection of a received response. No transaction, asset loading or routing.</summary>
    public static class GachaRewardPresentationAdapter
    {
        public static GachaPresentationBatch Map(GachaRollResponse response, GachaRewardMetadataSnapshot metadata = null)
        {
            var rewards = new List<GachaRewardPresentationData>();
            if (response == null)
                return new GachaPresentationBatch(null, rewards, BatchIssues.NullResponse);

            var issues = BatchIssues.None;
            if (string.IsNullOrWhiteSpace(response.requestId)) issues |= BatchIssues.MissingRequestId;
            if (string.IsNullOrWhiteSpace(response.bannerCode)) issues |= BatchIssues.MissingBannerCode;
            if (response.count != 1 && response.count != 10) issues |= BatchIssues.UnsupportedDeclaredCount;
            if (response.results == null) issues |= BatchIssues.MissingResults;
            else
            {
                if (response.results.Count == 0) issues |= BatchIssues.EmptyResults;
                if (response.results.Count != response.count) issues |= BatchIssues.CountMismatch;
                for (int slot = 0; slot < response.results.Count; slot++)
                {
                    var reward = MapReward(response.results[slot], slot, response.bannerCode, metadata);
                    rewards.Add(reward);
                    if (!reward.Source.IsPresent) issues |= BatchIssues.NullRewardSlot;
                    if ((reward.Issues & RewardIssues.MissingIdentity) != 0) issues |= BatchIssues.MissingRewardIdentity;
                }
            }
            return new GachaPresentationBatch(response, rewards, issues);
        }

        public static PresentationRarity ParseRarity(string raw)
        {
            switch (raw?.Trim().ToUpperInvariant())
            {
                case "COMMON": return PresentationRarity.Common;
                case "RARE": return PresentationRarity.Rare;
                case "EPIC": return PresentationRarity.Epic;
                case "LEGENDARY": return PresentationRarity.Legendary;
                default: return PresentationRarity.Unknown;
            }
        }

        private static GachaRewardPresentationData MapReward(GachaRewardResponse input, int slot,
            string bannerCode, GachaRewardMetadataSnapshot metadata)
        {
            var source = new SourceRewardData(input);
            var issues = RewardIssues.None;
            if (!source.IsPresent) issues |= RewardIssues.NullReward;
            if (string.IsNullOrWhiteSpace(source.ItemId) && string.IsNullOrWhiteSpace(source.ItemCode))
                issues |= RewardIssues.MissingIdentity;
            var rarity = ParseRarity(source.RawRarity);
            if (rarity == PresentationRarity.Unknown) issues |= RewardIssues.UnknownRarity;
            if (source.DuplicateBonus != null)
            {
                if (!source.IsDuplicate) issues |= RewardIssues.DuplicateBonusWithoutDuplicate;
                if (source.DuplicateBonus.Amount < 0) issues |= RewardIssues.NegativeDuplicateBonus;
            }

            var evidence = new List<RewardMetadataEvidence>();
            if (metadata != null && !string.IsNullOrWhiteSpace(source.ItemId))
                foreach (var record in metadata.Records)
                {
                    if (!string.Equals(source.ItemId, record.ItemId, StringComparison.Ordinal)) continue;
                    bool agrees = string.IsNullOrWhiteSpace(source.ItemCode) || string.IsNullOrWhiteSpace(record.ItemCode) ||
                        string.Equals(source.ItemCode, record.ItemCode, StringComparison.Ordinal);
                    bool context = record.Origin != MetadataOrigin.BannerPool ||
                        (!string.IsNullOrWhiteSpace(bannerCode) && string.Equals(bannerCode, record.BannerCode, StringComparison.Ordinal));
                    evidence.Add(new RewardMetadataEvidence(record, agrees, context));
                    // An out-of-context pool record is diagnostic evidence, not a veto on catalog metadata.
                    if (context && !agrees) issues |= RewardIssues.MetadataIdentityConflict;
                }
            var type = Resolve(evidence, record => record.RawItemType);
            var key = Resolve(evidence, record => record.UnityAssetKey);
            var name = Resolve(evidence, record => record.ItemName);
            if (type.State == MetadataState.Conflict || key.State == MetadataState.Conflict || name.State == MetadataState.Conflict)
                issues |= RewardIssues.MetadataFieldConflict;

            string label = source.ItemName;
            bool fallback = string.IsNullOrWhiteSpace(label);
            if (fallback)
                label = name.State == MetadataState.Resolved ? name.Value :
                    !string.IsNullOrWhiteSpace(source.ItemCode) ? source.ItemCode :
                    !string.IsNullOrWhiteSpace(source.ItemId) ? source.ItemId : "Unresolved reward";
            return new GachaRewardPresentationData(slot, source, rarity, type, key, evidence, issues, label, fallback);
        }

        private static ResolvedMetadataText Resolve(IEnumerable<RewardMetadataEvidence> evidence,
            Func<RewardMetadataRecord, string> select)
        {
            string value = null;
            bool conflict = false;
            var origins = new List<MetadataOrigin>();
            foreach (var candidate in evidence)
            {
                if (!candidate.IsEligible) continue;
                string text = select(candidate.Record);
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (value == null) value = text;
                else if (!string.Equals(value, text, StringComparison.Ordinal)) conflict = true;
                if (!origins.Contains(candidate.Record.Origin)) origins.Add(candidate.Record.Origin);
            }
            origins.Sort();
            return new ResolvedMetadataText(conflict ? MetadataState.Conflict : value == null ?
                MetadataState.Unresolved : MetadataState.Resolved, conflict ? null : value, origins);
        }
    }
}

#endif
