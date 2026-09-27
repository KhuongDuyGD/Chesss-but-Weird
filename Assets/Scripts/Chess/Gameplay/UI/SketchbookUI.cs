using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Shared drawing primitives for the account and gacha pages. Rects use top-left coordinates.
public static class SketchbookUI
{
    public static readonly Color Paper = new Color(0.975f, 0.96f, 0.925f);
    public static readonly Color White = new Color(1f, 0.993f, 0.973f);
    public static readonly Color Ink = new Color(0.16f, 0.15f, 0.18f);
    public static readonly Color Muted = new Color(0.39f, 0.37f, 0.40f);
    public static readonly Color Yellow = new Color(1f, 0.84f, 0.39f);
    public static readonly Color Blue = new Color(0.77f, 0.87f, 0.97f);
    public static readonly Color Pink = new Color(0.98f, 0.78f, 0.82f);
    public static readonly Color Green = new Color(0.80f, 0.89f, 0.73f);
    public static readonly Color Lavender = new Color(0.85f, 0.81f, 0.96f);

    public static RectTransform Node(Transform parent, string name, Rect area)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(area.x + area.width * .5f, -area.y - area.height * .5f);
        rect.sizeDelta = area.size;
        return rect;
    }

    public static HandDrawnRoundedGraphic Card(Transform parent, string name, Rect area, Color fill,
        float tilt = 0f, bool shadow = true, float radius = 22f)
    {
        if (shadow)
        {
            var back = Node(parent, name + " Pencil Shadow", new Rect(area.x + 5f, area.y + 7f, area.width, area.height));
            back.localRotation = Quaternion.Euler(0, 0, tilt);
            var shade = back.gameObject.AddComponent<HandDrawnRoundedGraphic>();
            shade.Configure(new Color(.26f, .23f, .24f, .13f), Color.clear, radius, 0f, 1.2f, 6);
            shade.raycastTarget = false;
        }
        var rect = Node(parent, name, area);
        rect.localRotation = Quaternion.Euler(0, 0, tilt);
        var graphic = rect.gameObject.AddComponent<HandDrawnRoundedGraphic>();
        graphic.Configure(fill, Ink, radius, 2.6f, 1.8f, name.Length);
        graphic.raycastTarget = false;
        return graphic;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string value, Rect area,
        float size = 28f, Color? color = null, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft,
        bool wrap = false)
    {
        var label = Node(parent, name, area).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = ChessFontCatalog.TmpFont;
        label.fontSize = size;
        label.fontSizeMax = size;
        label.fontSizeMin = Mathf.Max(16f, size * .72f);
        label.enableAutoSizing = true;
        label.color = color ?? Ink;
        label.alignment = alignment;
        label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.richText = false;
        label.raycastTarget = false;
        label.text = value ?? string.Empty;
        return label;
    }

    public static Button Button(Transform parent, string name, string label, Rect area, Color fill,
        UnityAction action, float size = 28f)
    {
        var graphic = Card(parent, name, area, fill, 0f, true, 15f);
        graphic.raycastTarget = true;
        var button = graphic.gameObject.AddComponent<Button>();
        button.targetGraphic = graphic;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.highlightedColor = new Color(.95f, .97f, 1f);
        colors.selectedColor = new Color(.91f, .95f, 1f);
        colors.pressedColor = new Color(.83f, .85f, .88f);
        colors.disabledColor = new Color(.68f, .68f, .68f, .65f);
        button.colors = colors;
        button.onClick.AddListener(action);
        Text(graphic.transform, "Label", label, new Rect(14, 5, area.width - 28, area.height - 10),
            size, Ink, TextAlignmentOptions.Center);
        return button;
    }

    public static Image Image(Transform parent, string name, Sprite sprite, Rect area)
    {
        var image = Node(parent, name, area).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    public static void Tape(Transform parent, float x, float y, float width = 125f, float tilt = -4f)
    {
        var tape = Card(parent, "Washi Tape", new Rect(x, y, width, 32),
            new Color(.92f, .81f, .57f, .72f), tilt, false, 2f);
        tape.Configure(tape.color, new Color(.55f, .45f, .28f, .16f), 2f, 1f, 1.5f, 23);
    }

    public static SketchbookDoodle Doodle(Transform parent, string name, Rect area,
        SketchbookDoodle.Shape shape, Color color, float tilt = 0f)
    {
        var rect = Node(parent, name, area);
        rect.localRotation = Quaternion.Euler(0, 0, tilt);
        var doodle = rect.gameObject.AddComponent<SketchbookDoodle>();
        doodle.Configure(shape, color);
        doodle.raycastTarget = false;
        return doodle;
    }

    public static void Background(RectTransform parent)
    {
        var background = parent.gameObject.GetComponent<Image>() ?? parent.gameObject.AddComponent<Image>();
        background.color = Paper;
        background.raycastTarget = true;
        Doodle(parent, "Notebook Lines", new Rect(0, 0, 1672, 941), SketchbookDoodle.Shape.Paper,
            new Color(.47f, .56f, .62f, .10f));
    }

    public static TextMeshProUGUI Wallet(Transform parent, string name, Rect area, Color fill,
        SketchbookDoodle.Shape icon, bool compact = false)
    {
        var card = Card(parent, name + " Sticker", area, fill, 0f, true, 18f).rectTransform;
        float iconSize = compact ? 38 : 55;
        Doodle(card, name + " Icon", new Rect(20, (area.height - iconSize) * .5f, iconSize, iconSize), icon, Ink);
        Text(card, name + " Caption", name.ToUpperInvariant(), new Rect(compact ? 72 : 95, 15, area.width - (compact ? 88 : 110), 26), 20, Muted);
        return Text(card, name + " Amount", "--", new Rect(compact ? 72 : 95, compact ? 39 : 48,
            area.width - (compact ? 88 : 110), compact ? 35 : 53), compact ? 30 : 40);
    }

    public static RectTransform ScrollArea(Transform parent, string name, Rect area, out ScrollRect scroll)
    {
        var viewport = Node(parent, name, area);
        var hit = viewport.gameObject.AddComponent<Image>();
        hit.color = new Color(1, 1, 1, .001f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Node(viewport, name + " Content", new Rect(0, 0, area.width, area.height));
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, area.height);
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        return content;
    }
}
