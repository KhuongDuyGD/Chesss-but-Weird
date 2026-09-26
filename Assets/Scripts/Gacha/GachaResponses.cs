using System;
using System.Collections.Generic;

[Serializable]
public sealed class GachaRollRequest
{
    public string bannerCode;
    public int count;
    public string requestId;
}

[Serializable]
public class GachaBannerSummaryResponse
{
    public string code;
    public string name;
    public string description;
    public string pityGroup;
    public string startsAt;
    public string endsAt;
    public List<GachaCostResponse> costs;
    public GachaRatesResponse rarityRates;
    public GachaPityLimitResponse pityLimit;
}

[Serializable]
public sealed class GachaBannerDetailResponse : GachaBannerSummaryResponse
{
    public List<GachaPoolItemResponse> poolItems;
    public Dictionary<string, GachaRewardCurrencyResponse> duplicateRewards;
}

[Serializable]
public sealed class GachaCostResponse
{
    public int rollCount;
    public string currency;
    public long amount;
}

[Serializable]
public sealed class GachaRewardCurrencyResponse
{
    public string currency;
    public long amount;
}

[Serializable]
public sealed class GachaRatesResponse
{
    public decimal common;
    public decimal rare;
    public decimal epic;
    public decimal legendary;
}

[Serializable]
public sealed class GachaPityLimitResponse
{
    public int epic;
    public int legendary;
}

[Serializable]
public sealed class GachaPoolItemResponse
{
    public string itemId;
    public string code;
    public string name;
    public string type;
    public string rarity;
    public string unityAssetKey;
    public int weight;
}

[Serializable]
public sealed class GachaPityResponse
{
    public GachaPityTierResponse epic;
    public GachaPityTierResponse legendary;
}

[Serializable]
public sealed class GachaPityTierResponse
{
    public int current;
    public int limit;
}

[Serializable]
public sealed class GachaRollResponse
{
    public string requestId;
    public string bannerCode;
    public int count;
    public string currency;
    public long totalCost;
    public GachaWalletResponse wallet;
    public List<GachaRewardResponse> results;
    public GachaPityResponse pity;
    public string createdAt;
}

[Serializable]
public sealed class GachaWalletResponse
{
    public long golds;
    public long diamonds;
    public long tickets;
}

[Serializable]
public sealed class GachaRewardResponse
{
    public string itemId;
    public string itemCode;
    public string itemName;
    public string rarity;
    public bool isDuplicate;
    public GachaRewardCurrencyResponse duplicateReward;
}

[Serializable]
public sealed class GachaHistoryResponse
{
    public int page;
    public int pageSize;
    public long total;
    public List<GachaRollResponse> items;
}
