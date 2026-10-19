using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace ChessButWeird.Online
{
    [Serializable, Preserve] public sealed class RankConfig
    {
        public string mode, version, ratingRounding;
        public int placementRequired;
        public List<RankTier> tiers = new List<RankTier>();
    }

    [Serializable, Preserve] public sealed class RankTier
    {
        public string tierId, displayName, colorHex, badgeKey;
        public int order, minRating;
    }

    [Serializable, Preserve] public sealed class RankOverview
    {
        public string mode, tierId, nextTierId, peakTierId, rankConfigVersion;
        public int rating, placementPlayed, placementRequired, ratedGames, peakRating;
        public bool provisional;
        public int? tierMinRating, nextTierMinRating, pointsToNextTier;
        public long? leaderboardPosition;
    }

    [Serializable, Preserve] public sealed class PlayerRanks
    {
        public RankOverview classic, chaos;
        public RankOverview For(string mode) => PlayModeNames.IsChaos(mode) ? chaos : classic;
    }
}
