using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class GachaService
{
    private readonly ApiClient client;

    public GachaService(ApiClient client = null) => this.client = client ?? ApiClient.Shared;

    public Task<List<GachaBannerSummaryResponse>> GetBannersAsync() =>
        client.GetAsync<List<GachaBannerSummaryResponse>>("/api/gacha/banners");

    public Task<GachaBannerDetailResponse> GetBannerAsync(string bannerCode) =>
        client.GetAsync<GachaBannerDetailResponse>(BannerPath(bannerCode));

    public Task<GachaPityResponse> GetPityAsync(string bannerCode) =>
        client.GetAsync<GachaPityResponse>(BannerPath(bannerCode) + "/pity", true);

    public Task<GachaHistoryResponse> GetHistoryAsync(int page = 1, int pageSize = 20)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize < 1 || pageSize > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        return client.GetAsync<GachaHistoryResponse>(
            "/api/gacha/history?page=" + page + "&pageSize=" + pageSize, true);
    }

    public Task<GachaRollResponse> RollAsync(string bannerCode, int count, Guid requestId)
    {
        if (count != 1 && count != 10) throw new ArgumentOutOfRangeException(nameof(count));
        if (requestId == Guid.Empty) throw new ArgumentException("A unique request ID is required.", nameof(requestId));
        return client.PostAsync<GachaRollResponse>("/api/gacha/roll", new GachaRollRequest
        {
            bannerCode = bannerCode,
            count = count,
            requestId = requestId.ToString("D")
        }, true);
    }

    private static string BannerPath(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Banner code is required.", nameof(code));
        return "/api/gacha/banners/" + Uri.EscapeDataString(code.Trim());
    }
}
