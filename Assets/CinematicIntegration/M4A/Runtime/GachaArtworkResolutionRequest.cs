#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
namespace ChessButWeird.CinematicIntegration.M4A
{
    public enum ArtworkResolutionStatus { MissingIdentity, MetadataConflict, MissingItemType, MissingCatalogKey, RequiresVerifiedResolver }

    /// <summary>A lookup specification, NOT a loaded asset, Addressables address, or verified artwork.</summary>
    public sealed class GachaArtworkResolutionRequest
    {
        public string ItemId { get; }
        public string ItemCode { get; }
        public string RawItemType { get; }
        public string CatalogLookupKey { get; }
        public ArtworkResolutionStatus Status { get; }
        public bool IsArtworkVerified => false;
        public string FallbackSemantic => "TextOnlyUnresolvedArtwork";

        private GachaArtworkResolutionRequest(GachaRewardPresentationData reward)
        {
            ItemId = reward?.Source.ItemId; ItemCode = reward?.Source.ItemCode;
            RawItemType = reward?.ItemType.Value; CatalogLookupKey = reward?.CatalogLookupKey.Value;
            if (reward == null || (reward.Issues & (RewardIssues.NullReward | RewardIssues.MissingIdentity)) != 0)
                Status = ArtworkResolutionStatus.MissingIdentity;
            else if (reward.ItemType.State == MetadataState.Conflict || reward.CatalogLookupKey.State == MetadataState.Conflict ||
                (reward.Issues & RewardIssues.MetadataIdentityConflict) != 0)
                Status = ArtworkResolutionStatus.MetadataConflict;
            else if (reward.ItemType.State != MetadataState.Resolved) Status = ArtworkResolutionStatus.MissingItemType;
            else if (reward.CatalogLookupKey.State != MetadataState.Resolved) Status = ArtworkResolutionStatus.MissingCatalogKey;
            else Status = ArtworkResolutionStatus.RequiresVerifiedResolver;
        }

        public static GachaArtworkResolutionRequest For(GachaRewardPresentationData reward) => new GachaArtworkResolutionRequest(reward);
    }
}

#endif
