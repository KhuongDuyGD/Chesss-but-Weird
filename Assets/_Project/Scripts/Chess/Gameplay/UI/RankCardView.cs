using ChessButWeird.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UI = SketchbookUI;

// A compact, shared server-driven rank card for profile and matchmaking lobby.
public sealed class RankCardView
{
    private readonly RankBadgeGraphic badge;
    private readonly TextMeshProUGUI title, tier, detail, order;
    private readonly RectTransform progress;
    private readonly float progressWidth;

    public RankCardView(Transform parent, string name, Rect area)
    {
        var root = UI.Card(parent, name, area, UI.White, 0, false, 14).rectTransform;
        float badgeSize = Mathf.Min(100, area.height - 24);
        float x = badgeSize + 28, width = area.width - x - 18;
        badge = UI.Node(root, "Rank Badge", new Rect(12, (area.height - badgeSize) / 2, badgeSize, badgeSize)).gameObject.AddComponent<RankBadgeGraphic>();
        order = UI.Text(badge.transform, "Tier Order", "?", new Rect(0, 0, badgeSize, badgeSize), badgeSize * .27f, new Color(.10f, .12f, .16f), TextAlignmentOptions.Center);
        title = UI.Text(root, "Rank Mode", "", new Rect(x, 10, width, 24), 18, UI.Muted);
        tier = UI.Text(root, "Rank Tier", "", new Rect(x, 35, width, 36), 28);
        detail = UI.Text(root, "Rank Progress", "", new Rect(x, 74, width, 29), 19, UI.Muted);
        progressWidth = width;
        var track = UI.Node(root, "Rank Progress Track", new Rect(x, area.height - 16, width, 5));
        track.gameObject.AddComponent<Image>().color = new Color(.2f, .2f, .25f, .12f);
        progress = UI.Node(track, "Rank Progress Fill", new Rect(0, 0, 0, 5));
        progress.pivot = new Vector2(0, .5f);
        progress.anchoredPosition = new Vector2(0, -2.5f);
        progress.gameObject.AddComponent<Image>().color = UI.Ink;
    }

    public void Bind(string mode, RankOverview rank, bool loading = false)
    {
        title.text = PlayModeNames.DisplayBackend(mode).ToUpperInvariant() + " / RANKED";
        var definition = RankService.Tier(mode, rank?.tierId);
        Color accent = Accent(definition, rank?.tierId);
        bool placed = rank != null && !rank.provisional && !string.IsNullOrEmpty(rank.tierId);
        badge.Configure(accent, placed);
        order.text = placed ? (definition?.order ?? TierOrder(rank.tierId)).ToString() : "?";
        tier.text = rank == null ? loading ? "Loading rank..." : "Rank unavailable" : rank.provisional ? "Unranked" : TierName(mode, rank.tierId);
        detail.text = rank == null ? "Refresh to load your rank" : rank.provisional
            ? $"Placement {rank.placementPlayed}/{rank.placementRequired}  /  {rank.rating:N0} rating"
            : rank.nextTierId == null ? $"{rank.rating:N0} rating  /  Highest tier"
            : $"{rank.rating:N0} rating  /  {rank.pointsToNextTier:N0} to {rank.nextTierId}";
        float ratio = rank == null ? 0 : rank.provisional ? (float)rank.placementPlayed / Mathf.Max(1, rank.placementRequired) :
            rank.nextTierMinRating.HasValue && rank.tierMinRating.HasValue
                ? (float)(rank.rating - rank.tierMinRating.Value) / Mathf.Max(1, rank.nextTierMinRating.Value - rank.tierMinRating.Value) : 1;
        progress.sizeDelta = new Vector2(progressWidth * Mathf.Clamp01(ratio), 5);
        progress.GetComponent<Image>().color = accent;
    }

    public static string TierName(string mode, string id)
    {
        if (string.IsNullOrEmpty(id)) return "Unranked";
        var definition = RankService.Tier(mode, id);
        return definition == null || string.IsNullOrWhiteSpace(definition.displayName) ? id : id + " / " + definition.displayName;
    }
    public static int TierOrder(string id)
    {
        string[] ids = { "Pawn", "Knight", "Bishop", "Rook", "Queen", "King", "Grandmaster", "Legend", "Celestial" };
        return System.Array.IndexOf(ids, id) + 1;
    }
    public static Color Accent(RankTier definition, string id)
    {
        if (definition != null && ColorUtility.TryParseHtmlString(definition.colorHex, out var serverColor)) return serverColor;
        // Visual fallback only; no rating thresholds or placement inference on the client.
        string[] colors = { "#747D8C", "#B97847", "#BCC9D5", "#E6B541", "#44B9B0", "#4B9AE8", "#9461D9", "#D95065", "#F4D68A" };
        int order = TierOrder(id);
        ColorUtility.TryParseHtmlString(order > 0 ? colors[order - 1] : "#747D8C", out var result);
        return result;
    }
}
