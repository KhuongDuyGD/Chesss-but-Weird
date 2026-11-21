#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
namespace ChessButWeird.CinematicIntegration.M4A
{
    /// <summary>Inert future-routing configuration. No production consumer or persisted preference.</summary>
    public sealed class GachaCinematicFeatureConfig
    {
        public bool EnableRoyalAscensionCinematic { get; }
        public GachaCinematicFeatureConfig(bool enableRoyalAscensionCinematic = false)
        { EnableRoyalAscensionCinematic = enableRoyalAscensionCinematic; }
    }
}

#endif
