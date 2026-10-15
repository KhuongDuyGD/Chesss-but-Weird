using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UI = SketchbookUI;

public static class CosmeticThumbnailUI
{
    public static bool IsChess(string type) => type?.IndexOf("CHESS", StringComparison.OrdinalIgnoreCase) >= 0;
    public static bool IsBoard(string type) => type?.IndexOf("BOARD", StringComparison.OrdinalIgnoreCase) >= 0;
    public static CosmeticCatalogEntry Find(CosmeticCatalog catalog, string type, string key) => !catalog ? null :
        IsChess(type) ? catalog.FindPiece(key) : IsBoard(type) ? catalog.FindBoard(key) : null;
    public static bool HasArt(CosmeticCatalog catalog, string type, string key) => Find(catalog, type, key)?.HasArt == true;
    public static int TierRank(string tier)
    {
        switch (tier?.Trim().ToUpperInvariant())
        { case "LEGENDARY": return 4; case "EPIC": return 3; case "RARE": return 2; default: return 1; }
    }
    public static Color TierColor(string tier)
    {
        switch (TierRank(tier))
        { case 4: return UI.Yellow; case 3: return UI.Lavender; case 2: return UI.Blue; default: return UI.Green; }
    }

    public static void Draw(Transform parent, Rect area, Sprite sprite, bool equipped, string effect = null)
    {
        var frame = UI.Node(parent, "Thumbnail", area);
        var border = frame.gameObject.AddComponent<Image>();
        border.color = equipped ? new Color(1f, .73f, .12f) : new Color(.85f, .83f, .79f);
        border.raycastTarget = false;
        var inner = UI.Node(frame, "Art Background", new Rect(5, 5, area.width - 10, area.height - 10));
        var background = inner.gameObject.AddComponent<Image>();
        background.color = UI.Paper; background.raycastTarget = false;
        if (sprite) UI.Image(inner, "Skin Art", sprite, new Rect(0, 0, area.width - 10, area.height - 10));
        else if (effect != null)
            UI.Doodle(inner, "Effect Preview", new Rect((area.width - 100) / 2, (area.height - 100) / 2, 90, 90),
                SketchbookDoodle.Shape.Star, effect == "confetti" ? UI.Pink : UI.Ink);
        if (!equipped) return;
        var badge = UI.Node(frame, "Equipped Badge", new Rect(5, area.height - 39, area.width - 10, 34));
        var fill = badge.gameObject.AddComponent<Image>(); fill.color = new Color(1f, .77f, .22f); fill.raycastTarget = false;
        UI.Text(badge, "Equipped", "Equipped", new Rect(4, 0, area.width - 18, 34), 21, UI.Ink, TextAlignmentOptions.Center);
    }
}
