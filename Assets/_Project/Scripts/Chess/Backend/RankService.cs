using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChessButWeird.Online
{
    // Config is public rank metadata only. Account rank always comes from /users/me.
    public sealed class RankService
    {
        private readonly ApiClient client;
        private static readonly Dictionary<string, RankConfig> configs = new Dictionary<string, RankConfig>();
        public RankService(ApiClient client = null) { this.client = client ?? ApiClient.Shared; }

        public async Task<RankConfig> ConfigAsync(string mode)
        {
            mode = PlayModeNames.IsChaos(mode) ? "Chaos" : "Classic";
            var config = await client.GetAsync<RankConfig>("/api/ranks/config?mode=" + Uri.EscapeDataString(mode), true);
            if (config == null || config.tiers == null || config.tiers.Count == 0 || config.mode != mode)
                throw new ApiException("Rank configuration is unavailable.");
            configs[mode] = config;
            return config;
        }

        public static RankConfig CachedConfig(string mode) => configs.TryGetValue(PlayModeNames.IsChaos(mode) ? "Chaos" : "Classic", out var value) ? value : null;
        public static RankTier Tier(string mode, string tierId)
            => CachedConfig(mode)?.tiers.Find(t => t.tierId == tierId);
    }
}
