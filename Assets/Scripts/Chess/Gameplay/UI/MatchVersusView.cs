using System;
using System.Linq;
using ChessButWeird.Online;
using TMPro;
using UnityEngine;
using UI = SketchbookUI;

// Two portrait-style player banners above the actual content loading progress.
public sealed class MatchVersusView
{
    private readonly MatchLoadingContext context;
    private readonly PlayerBanner white, black;
    public MatchVersusView(Transform parent, MatchLoadingContext matchup)
    {
        context = matchup;
        UI.Text(parent, "Match Heading", context.Heading, new Rect(64, 24, 1544, 32), 24, UI.Muted, TextAlignmentOptions.Center);
        UI.Text(parent, "Versus Title", "Meet your opponent", new Rect(64, 66, 1544, 66), 48, UI.Ink, TextAlignmentOptions.Center);
        var settings = context.State.settings;
        UI.Text(parent, "Match Rules", $"{PlayModeNames.DisplayBackend(settings.mode).ToUpperInvariant()}   /   {settings.initialSeconds / 60} MIN + {settings.incrementSeconds} SEC   /   {settings.region}",
            new Rect(64, 139, 1544, 34), 23, UI.Muted, TextAlignmentOptions.Center);
        white = new PlayerBanner(parent, new Rect(208, 204, 520, 515), "WHITE", UI.Blue);
        black = new PlayerBanner(parent, new Rect(944, 204, 520, 515), "BLACK", UI.Lavender);
        UI.Doodle(parent, "Versus Star", new Rect(783, 361, 106, 106), SketchbookDoodle.Shape.Star, UI.Yellow);
        UI.Text(parent, "Versus", "VS", new Rect(758, 466, 156, 70), 54, UI.Ink, TextAlignmentOptions.Center);
        UI.Text(parent, "Loading Hint", "Make your next move count.", new Rect(64, 734, 1544, 31), 22, UI.Muted, TextAlignmentOptions.Center);
    }

    public void Refresh(CosmeticCatalog catalog, float localProgress)
    {
        white.Bind(context.Player("White"), context, catalog, localProgress);
        black.Bind(context.Player("Black"), context, catalog, localProgress);
    }

    private sealed class PlayerBanner
    {
        private readonly TextMeshProUGUI name, username, rankName, rating, state, order, chessName, boardName;
        private readonly RankBadgeGraphic badge;
        private readonly UnityEngine.UI.Image chessArt, boardArt;
        private readonly SketchbookDoodle chessFallback, boardFallback;
        public PlayerBanner(Transform parent, Rect area, string color, Color tint)
        {
            var root = UI.Card(parent, color + " Player Banner", area, UI.White).rectTransform;
            UI.Card(root, "Side Ribbon", new Rect(12, 12, 496, 51), tint, 0, false, 12);
            UI.Text(root, "Side", color + " SIDE", new Rect(25, 15, 470, 44), 25, UI.Ink, TextAlignmentOptions.Center);
            badge = UI.Node(root, "Rank Crest", new Rect(198, 78, 124, 130)).gameObject.AddComponent<RankBadgeGraphic>();
            order = UI.Text(badge.transform, "Rank Order", "?", new Rect(0, 0, 124, 130), 36, new Color(.10f, .12f, .16f), TextAlignmentOptions.Center);
            name = UI.Text(root, "Player Name", "", new Rect(24, 219, 472, 47), 36, UI.Ink, TextAlignmentOptions.Center);
            username = UI.Text(root, "Player Username", "", new Rect(24, 267, 472, 28), 20, UI.Muted, TextAlignmentOptions.Center);
            rankName = UI.Text(root, "Player Rank", "", new Rect(24, 301, 472, 35), 26, UI.Ink, TextAlignmentOptions.Center);
            rating = UI.Text(root, "Player Rating", "", new Rect(24, 339, 472, 28), 21, UI.Muted, TextAlignmentOptions.Center);
            UI.Card(root, "Loadout Divider", new Rect(28, 382, 464, 2), tint, 0, false, 0);
            chessArt = UI.Image(root, "Chess Art", null, new Rect(24, 399, 68, 68));
            boardArt = UI.Image(root, "Board Art", null, new Rect(276, 399, 68, 68));
            chessFallback = UI.Doodle(root, "Chess Placeholder", new Rect(34, 408, 48, 48), SketchbookDoodle.Shape.Pawn, UI.Ink);
            boardFallback = UI.Doodle(root, "Board Placeholder", new Rect(286, 408, 48, 48), SketchbookDoodle.Shape.Board, UI.Ink);
            UI.Text(root, "Chess Caption", "CHESS SET", new Rect(102, 400, 162, 22), 16, UI.Muted);
            UI.Text(root, "Board Caption", "ARENA", new Rect(354, 400, 142, 22), 16, UI.Muted);
            chessName = UI.Text(root, "Chess Set", "", new Rect(102, 426, 162, 36), 21);
            boardName = UI.Text(root, "Arena", "", new Rect(354, 426, 142, 36), 21);
            state = UI.Text(root, "Player Loading State", "", new Rect(24, 475, 472, 28), 20, UI.Muted, TextAlignmentOptions.Center);
        }

        public void Bind(Player player, MatchLoadingContext match, CosmeticCatalog catalog, float progress)
        {
            if (player == null) { name.text = "Waiting for player"; return; }
            bool local = player.userId == match.LocalUserId;
            var rank = match.Rank(player);
            var definition = RankService.Tier(match.State.settings.mode, rank?.tierId);
            var accent = RankCardView.Accent(definition, rank?.tierId);
            bool placed = rank != null && !rank.provisional && !string.IsNullOrEmpty(rank.tierId);
            badge.Configure(accent, placed);
            order.text = placed ? (definition?.order ?? RankCardView.TierOrder(rank.tierId)).ToString() : "?";
            name.text = string.IsNullOrWhiteSpace(player.displayName) ? player.username : player.displayName;
            username.text = (local ? "YOU   /   " : "") + "@" + player.username;
            rankName.text = rank == null ? "Rank unavailable" : rank.provisional ? "Unranked" : RankCardView.TierName(match.State.settings.mode, rank.tierId);
            // MatchPlayer.rating is the authoritative pre-match rating for both players.
            rating.text = $"{player.rating:N0} rating" + (rank?.provisional == true ? $"   /   Placement {rank.placementPlayed}/{rank.placementRequired}" : "");
            state.text = match.State.status == "Cancelled" ? "Match cancelled" : !player.connected ? "Reconnecting..." : player.ready ? "Ready" :
                local ? progress >= 1 ? "Content ready" : "Preparing your board..." : "Connected / preparing match";
            BindCosmetic(player, catalog, true, chessArt, chessFallback, chessName);
            BindCosmetic(player, catalog, false, boardArt, boardFallback, boardName);
        }

        private static void BindCosmetic(Player player, CosmeticCatalog catalog, bool chess, UnityEngine.UI.Image art, SketchbookDoodle fallback, TextMeshProUGUI label)
        {
            var selected = player.loadout?.FirstOrDefault(l => chess ? CosmeticThumbnailUI.IsChess(l.type) : CosmeticThumbnailUI.IsBoard(l.type));
            var entry = CosmeticThumbnailUI.Find(catalog, chess ? "CHESS" : "BOARD", selected?.unityAssetKey ?? CosmeticSelection.LowPolyId);
            art.sprite = entry?.thumbnail; art.enabled = art.sprite;
            fallback.enabled = !art.sprite;
            label.text = entry?.displayName ?? selected?.code ?? "Default";
        }
    }
}
