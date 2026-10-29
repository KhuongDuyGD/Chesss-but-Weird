#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ChessButWeird.CinematicIntegration.M4A
{
    public enum PresentationRarity { Unknown, Common, Rare, Epic, Legendary }
    public enum MetadataOrigin { BannerPool, ItemCatalog }
    public enum MetadataState { Unresolved, Resolved, Conflict }

    [Flags]
    public enum BatchIssues
    {
        None = 0, NullResponse = 1, MissingResults = 2, EmptyResults = 4,
        UnsupportedDeclaredCount = 8, CountMismatch = 16, NullRewardSlot = 32,
        MissingRewardIdentity = 64, MissingRequestId = 128, MissingBannerCode = 256
    }

    [Flags]
    public enum RewardIssues
    {
        None = 0, NullReward = 1, MissingIdentity = 2, UnknownRarity = 4,
        MetadataIdentityConflict = 8, MetadataFieldConflict = 16,
        DuplicateBonusWithoutDuplicate = 32, NegativeDuplicateBonus = 64
    }

    /// <summary>Exact server bonus scalars, never a calculated grant or formatted amount.</summary>
    public sealed class DuplicateBonusData
    {
        public string Currency { get; }
        public long Amount { get; }
        internal DuplicateBonusData(string currency, long amount) { Currency = currency; Amount = amount; }
    }

    /// <summary>Detached raw reward. No DTO, wallet, inventory, Unity object or inferred item metadata.</summary>
    public sealed class SourceRewardData
    {
        public bool IsPresent { get; }
        public string ItemId { get; }
        public string ItemCode { get; }
        public string ItemName { get; }
        public string RawRarity { get; }
        public bool IsDuplicate { get; }
        public DuplicateBonusData DuplicateBonus { get; }
        // Quantity, type and asset key do not exist in the current reward DTO.
        public long? Quantity => null;

        internal SourceRewardData(GachaRewardResponse source)
        {
            IsPresent = source != null;
            if (source == null) return;
            ItemId = source.itemId; ItemCode = source.itemCode; ItemName = source.itemName;
            RawRarity = source.rarity; IsDuplicate = source.isDuplicate;
            if (source.duplicateReward != null)
                DuplicateBonus = new DuplicateBonusData(source.duplicateReward.currency, source.duplicateReward.amount);
        }
    }

    public sealed class ResolvedMetadataText
    {
        public MetadataState State { get; }
        public string Value { get; }
        public IReadOnlyList<MetadataOrigin> Origins { get; }
        internal ResolvedMetadataText(MetadataState state, string value, IEnumerable<MetadataOrigin> origins)
        {
            State = state; Value = value;
            Origins = new ReadOnlyCollection<MetadataOrigin>(new List<MetadataOrigin>(origins));
        }
    }

    /// <summary>Candidate values and eligibility are evidence, never replacement reward identity.</summary>
    public sealed class RewardMetadataEvidence
    {
        public RewardMetadataRecord Record { get; }
        public bool IdentityAgrees { get; }
        public bool BannerContextAgrees { get; }
        public bool IsEligible => IdentityAgrees && BannerContextAgrees;
        internal RewardMetadataEvidence(RewardMetadataRecord record, bool identityAgrees, bool bannerContextAgrees)
        { Record = record; IdentityAgrees = identityAgrees; BannerContextAgrees = bannerContextAgrees; }
    }

    public sealed class GachaRewardPresentationData
    {
        public int OriginalSlotIndex { get; }
        public SourceRewardData Source { get; }
        public PresentationRarity CanonicalRarity { get; }
        public ResolvedMetadataText ItemType { get; }
        public ResolvedMetadataText CatalogLookupKey { get; }
        public IReadOnlyList<RewardMetadataEvidence> MetadataEvidence { get; }
        public RewardIssues Issues { get; }
        // Derived text only; Source.ItemName remains byte-for-byte unchanged as a .NET string.
        public string DisplayLabel { get; }
        public bool UsesFallbackLabel { get; }
        public int? LegacyVisualStars { get; }

        internal GachaRewardPresentationData(int slot, SourceRewardData source, PresentationRarity rarity,
            ResolvedMetadataText type, ResolvedMetadataText key, IEnumerable<RewardMetadataEvidence> evidence,
            RewardIssues issues, string label, bool usesFallback)
        {
            OriginalSlotIndex = slot; Source = source; CanonicalRarity = rarity;
            ItemType = type; CatalogLookupKey = key;
            MetadataEvidence = new ReadOnlyCollection<RewardMetadataEvidence>(new List<RewardMetadataEvidence>(evidence));
            Issues = issues; DisplayLabel = label; UsesFallbackLabel = usesFallback;
            switch (rarity)
            {
                case PresentationRarity.Common: LegacyVisualStars = 2; break;
                case PresentationRarity.Rare: LegacyVisualStars = 3; break;
                case PresentationRarity.Epic: LegacyVisualStars = 4; break;
                case PresentationRarity.Legendary: LegacyVisualStars = 5; break;
                default: LegacyVisualStars = null; break;
            }
        }
    }

    public sealed class GachaPresentationBatch
    {
        public string RequestId { get; }
        public string BannerCode { get; }
        public string CreatedAtRaw { get; }
        public string SourceCurrency { get; }
        public long SourceTotalCost { get; }
        public int DeclaredCount { get; }
        public int ActualCount => Rewards.Count;
        public IReadOnlyList<GachaRewardPresentationData> Rewards { get; }
        public BatchIssues Issues { get; }
        public bool IsStructurallyValid => (Issues & (BatchIssues.NullResponse | BatchIssues.MissingResults |
            BatchIssues.EmptyResults | BatchIssues.UnsupportedDeclaredCount | BatchIssues.CountMismatch |
            BatchIssues.NullRewardSlot | BatchIssues.MissingRewardIdentity)) == BatchIssues.None;

        internal GachaPresentationBatch(GachaRollResponse source, IEnumerable<GachaRewardPresentationData> rewards, BatchIssues issues)
        {
            if (source != null)
            {
                RequestId = source.requestId; BannerCode = source.bannerCode; CreatedAtRaw = source.createdAt;
                DeclaredCount = source.count; SourceCurrency = source.currency; SourceTotalCost = source.totalCost;
            }
            Rewards = new ReadOnlyCollection<GachaRewardPresentationData>(new List<GachaRewardPresentationData>(rewards));
            Issues = issues;
        }
    }
}

#endif
