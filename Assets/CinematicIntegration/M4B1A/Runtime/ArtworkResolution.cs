#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.Linq;
using ChessButWeird.CinematicIntegration.M4A;

namespace ChessButWeird.CinematicIntegration.M4B1A
{
    public enum ArtworkCategoryStatus { Unresolved, Missing, Unknown, Ambiguous, Unsupported, Matched }
    public enum ArtworkEntryStatus { NotExamined, Missing, Ambiguous, IdentityConflict, InvalidDataReference, Matched }
    public enum ArtworkReferenceStatus { NotExamined, Missing, Invalid, Unsupported, Specified }
    public enum ArtworkStatus
    {
        MissingIdentity, MetadataConflict, MissingCategory, UnknownItemType, MissingCatalogKey,
        AmbiguousCategoryKey, UnsupportedArtwork, MissingCatalogEntry, AmbiguousCatalogEntry,
        CatalogIdentityConflict, MissingAssetReference, InvalidAssetReference, ReferenceSpecified, Cancelled
    }
    // No Succeeded/Loaded state exists in this milestone: acquisition is deliberately absent.
    public enum ArtworkAcquisitionState { NotRequested, Failed, Cancelled }

    public sealed class ArtworkDescriptor
    {
        public GachaRewardPresentationData Reward { get; }
        public GachaArtworkResolutionRequest Metadata { get; }
        public ArtworkStatus Status { get; }
        public ArtworkCategoryStatus CategoryStatus { get; }
        public ArtworkEntryStatus EntryStatus { get; }
        public ArtworkReferenceStatus ReferenceStatus { get; }
        public ArtworkCategory? Category { get; }
        public ArtworkCatalogEntry Entry { get; }
        public ArtworkEvidence CatalogEvidence { get; }
        public ArtworkEvidence BindingEvidence { get; }
        public bool HasMatchedCatalogEntry => EntryStatus == ArtworkEntryStatus.Matched;
        public bool HasSpecifiedReference => Status == ArtworkStatus.ReferenceSpecified;
        public bool IsArtworkAcquired => false;
        public bool IsProductionArtworkVerified => false;
        internal ArtworkDescriptor(GachaRewardPresentationData reward, ArtworkStatus status, ArtworkCategoryStatus categoryStatus,
            ArtworkEntryStatus entryStatus, ArtworkReferenceStatus referenceStatus, ArtworkCategory? category,
            ArtworkCatalogEntry entry, ArtworkCatalogSnapshot catalog, ArtworkTypePolicy policy)
        {
            Reward = reward; Metadata = GachaArtworkResolutionRequest.For(reward); Status = status;
            CategoryStatus = categoryStatus; EntryStatus = entryStatus; ReferenceStatus = referenceStatus;
            Category = category; Entry = entry; CatalogEvidence = catalog.Evidence; BindingEvidence = policy.Evidence;
        }
    }
    public sealed class ArtworkAcquisitionObservation
    {
        public ArtworkDescriptor Descriptor { get; }
        public ArtworkAcquisitionState State { get; }
        public string Reason { get; }
        public bool IsAcquired => false;
        private ArtworkAcquisitionObservation(ArtworkDescriptor descriptor, ArtworkAcquisitionState state, string reason)
        { Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor)); State = state; Reason = reason; }
        public static ArtworkAcquisitionObservation NotRequested(ArtworkDescriptor descriptor)
            => new ArtworkAcquisitionObservation(descriptor, ArtworkAcquisitionState.NotRequested, null);
        public static ArtworkAcquisitionObservation Failed(ArtworkDescriptor descriptor, string reason)
        {
            if (descriptor == null || !descriptor.HasSpecifiedReference) throw new ArgumentException("No reference was specified", nameof(descriptor));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Failure reason required", nameof(reason));
            return new ArtworkAcquisitionObservation(descriptor, ArtworkAcquisitionState.Failed, reason);
        }
        public static ArtworkAcquisitionObservation Cancelled(ArtworkDescriptor descriptor, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Cancellation reason required", nameof(reason));
            return new ArtworkAcquisitionObservation(descriptor, ArtworkAcquisitionState.Cancelled, reason);
        }
    }

    public static class ArtworkResolver
    {
        public static ArtworkDescriptor Resolve(GachaRewardPresentationData reward, ArtworkCatalogSnapshot catalog,
            ArtworkTypePolicy policy = null, bool cancelled = false)
        {
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            policy = policy ?? ArtworkTypePolicy.Unresolved;
            ArtworkCategoryStatus categoryState = ArtworkCategoryStatus.Unresolved;
            ArtworkEntryStatus entryState = ArtworkEntryStatus.NotExamined;
            ArtworkReferenceStatus referenceState = ArtworkReferenceStatus.NotExamined;
            ArtworkCategory? category = null; ArtworkCatalogEntry entry = null;
            ArtworkDescriptor Result(ArtworkStatus status) => new ArtworkDescriptor(reward, status, categoryState,
                entryState, referenceState, category, entry, catalog, policy);
            if (cancelled) return Result(ArtworkStatus.Cancelled);
            if ((reward.Issues & (RewardIssues.NullReward | RewardIssues.MissingIdentity)) != 0) return Result(ArtworkStatus.MissingIdentity);
            if ((reward.Issues & (RewardIssues.MetadataIdentityConflict | RewardIssues.MetadataFieldConflict)) != 0)
                return Result(ArtworkStatus.MetadataConflict);
            if (reward.ItemType.State != MetadataState.Resolved)
            { categoryState = ArtworkCategoryStatus.Missing; return Result(ArtworkStatus.MissingCategory); }
            var bindings = policy.Bindings.Where(b => string.Equals(b.ExactItemType, reward.ItemType.Value, StringComparison.Ordinal)).ToArray();
            if (bindings.Length > 1) { categoryState = ArtworkCategoryStatus.Ambiguous; return Result(ArtworkStatus.AmbiguousCategoryKey); }
            if (bindings.Length == 0 || policy.Evidence.Scope == ArtworkEvidenceScope.Unverified)
            {
                categoryState = ArtworkCategoryStatus.Unknown;
                bool ambiguous = reward.CatalogLookupKey.Value != null && catalog.Entries.Where(e => KeyEquals(e.Key, reward.CatalogLookupKey.Value)).Select(e => e.Category).Distinct().Count() > 1;
                return Result(ambiguous ? ArtworkStatus.AmbiguousCategoryKey : ArtworkStatus.UnknownItemType);
            }
            category = bindings[0].Category;
            if (category == ArtworkCategory.Unsupported || category == ArtworkCategory.Environment)
            { categoryState = ArtworkCategoryStatus.Unsupported; return Result(ArtworkStatus.UnsupportedArtwork); }
            categoryState = ArtworkCategoryStatus.Matched;
            if (reward.CatalogLookupKey.State != MetadataState.Resolved) return Result(ArtworkStatus.MissingCatalogKey);
            var matches = catalog.Entries.Where(e => e.Category == category && KeyEquals(e.Key, reward.CatalogLookupKey.Value)).ToArray();
            if (matches.Length == 0) { entryState = ArtworkEntryStatus.Missing; return Result(ArtworkStatus.MissingCatalogEntry); }
            if (matches.Length > 1) { entryState = ArtworkEntryStatus.Ambiguous; return Result(ArtworkStatus.AmbiguousCatalogEntry); }
            var candidate = matches[0];
            if (!string.Equals(candidate.Key, candidate.InternalId, StringComparison.OrdinalIgnoreCase))
            { entryState = ArtworkEntryStatus.IdentityConflict; return Result(ArtworkStatus.CatalogIdentityConflict); }
            string expected = category == ArtworkCategory.PieceSkin ? "PieceSkinData" : "BoardSkinData";
            if (candidate.Data == null || candidate.Data.IsMissing)
            { entryState = ArtworkEntryStatus.InvalidDataReference; referenceState = ArtworkReferenceStatus.Missing; return Result(ArtworkStatus.MissingAssetReference); }
            if (!candidate.Data.HasUsableSpecification || candidate.Data.Kind != ArtworkReferenceKind.AddressableGuid || candidate.Data.ObservedType != expected || catalog.Evidence.Scope == ArtworkEvidenceScope.Unverified)
            { entryState = ArtworkEntryStatus.InvalidDataReference; referenceState = ArtworkReferenceStatus.Invalid; return Result(ArtworkStatus.InvalidAssetReference); }
            entry = candidate; entryState = ArtworkEntryStatus.Matched;
            if (entry.Preview == null || entry.Preview.IsMissing)
            { referenceState = ArtworkReferenceStatus.Missing; return Result(ArtworkStatus.MissingAssetReference); }
            if (!entry.Preview.HasUsableSpecification)
            { referenceState = ArtworkReferenceStatus.Invalid; return Result(ArtworkStatus.InvalidAssetReference); }
            if (entry.Preview.ObservedType != "Sprite")
            { referenceState = ArtworkReferenceStatus.Unsupported; return Result(ArtworkStatus.UnsupportedArtwork); }
            referenceState = ArtworkReferenceStatus.Specified;
            return Result(ArtworkStatus.ReferenceSpecified);
        }
        private static bool KeyEquals(string entryKey, string lookup) => lookup != null &&
            string.Equals(entryKey, lookup.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

#endif
