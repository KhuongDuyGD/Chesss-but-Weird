#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ChessButWeird.CinematicIntegration.M4A
{
    public sealed class RewardMetadataRecord
    {
        public MetadataOrigin Origin { get; }
        public string BannerCode { get; }
        public string ItemId { get; }
        public string ItemCode { get; }
        public string ItemName { get; }
        public string RawRarity { get; }
        public string RawItemType { get; }
        public string UnityAssetKey { get; }

        internal RewardMetadataRecord(MetadataOrigin origin, string bannerCode, string id, string code,
            string name, string rarity, string type, string key)
        {
            Origin = origin; BannerCode = bannerCode; ItemId = id; ItemCode = code;
            ItemName = name; RawRarity = rarity; RawItemType = type; UnityAssetKey = key;
        }
    }

    /// <summary>Caller-supplied, already-received metadata, copied once. Never fetches or loads anything.</summary>
    public sealed class GachaRewardMetadataSnapshot
    {
        public IReadOnlyList<RewardMetadataRecord> Records { get; }
        public int NullSourceRecords { get; }

        private GachaRewardMetadataSnapshot(List<RewardMetadataRecord> records, int nullRecords)
        {
            Records = new ReadOnlyCollection<RewardMetadataRecord>(new List<RewardMetadataRecord>(records));
            NullSourceRecords = nullRecords;
        }

        public static GachaRewardMetadataSnapshot FromAlreadyReceived(GachaBannerDetailResponse banner = null,
            IEnumerable<ItemCatalogResponse> catalog = null)
        {
            var records = new List<RewardMetadataRecord>();
            int nullRecords = 0;
            if (banner?.poolItems != null)
                foreach (var item in banner.poolItems)
                {
                    if (item == null) { nullRecords++; continue; }
                    records.Add(new RewardMetadataRecord(MetadataOrigin.BannerPool, banner.code, item.itemId,
                        item.code, item.name, item.rarity, item.type, item.unityAssetKey));
                }
            if (catalog != null)
                foreach (var item in catalog)
                {
                    if (item == null) { nullRecords++; continue; }
                    records.Add(new RewardMetadataRecord(MetadataOrigin.ItemCatalog, null, item.itemId,
                        item.code, item.name, item.rarity, item.type, item.unityAssetKey));
                }
            return new GachaRewardMetadataSnapshot(records, nullRecords);
        }
    }
}

#endif
