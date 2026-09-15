using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lives independently of the auth form so success remains visible during loading.
public sealed class AuthNotificationView : MonoBehaviour
{
    public enum ResultKind { Info, Success, Error }

    private const float VisibleSeconds = 10f;
    private const float EnterSeconds = 0.22f;
    private const float ExitSeconds = 0.35f;
    private const float Width = 740f;
    private static AuthNotificationView instance;
    private static readonly Color Ink = new Color(0.16f, 0.18f, 0.15f);
    private RectTransform card;
    private CanvasGroup group;
    private Image paper;
    private Image accentStrip;
    private Image icon;
    private RectTransform iconMarks;
    private RectTransform countdown;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI messageLabel;
    private ScrollRect messageScroll;
    private Scrollbar messageScrollbar;
    private Coroutine lifetime;
    private bool dismissing;

    public static void Show(string title, string message, ResultKind result)
    {
        if (!instance)
        {
            var go = new GameObject("Account Notification", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            instance = go.AddComponent<AuthNotificationView>();
            DontDestroyOnLoad(go);
            instance.Build();
        }
        instance.Present(title, message, result);
    }

    public void Dismiss()
    {
        if (dismissing) return;
        dismissing = true;
        if (lifetime != null) StopCoroutine(lifetime);
        lifetime = StartCoroutine(FadeOut());
    }

    private void Present(string title, string message, ResultKind result)
    {
        if (lifetime != null) StopCoroutine(lifetime);
        dismissing = false;
        group.interactable = group.blocksRaycasts = true;
        titleLabel.text = title ?? string.Empty;
        messageLabel.text = message ?? string.Empty;

        Color accent = result == ResultKind.Success ? new Color(0.28f, 0.48f, 0.24f)
            : result == ResultKind.Error ? new Color(0.70f, 0.27f, 0.23f) : new Color(0.27f, 0.46f, 0.60f);
        paper.color = result == ResultKind.Success ? new Color(0.94f, 0.98f, 0.88f)
            : result == ResultKind.Error ? new Color(1f, 0.93f, 0.89f) : new Color(0.92f, 0.96f, 0.98f);
        accentStrip.color = icon.color = accent;
        titleLabel.color = accent;
        countdown.GetComponent<Image>().color = accent;
        DrawIcon(result);

        // Ordinary messages stay compact; lengthy server reasons can be scrolled
        // instead of being truncated or expanding over the login fields.
        float textHeight = Mathf.Max(38f, messageLabel.GetPreferredValues(messageLabel.text, 580f, Mathf.Infinity).y + 6f);
        float viewportHeight = Mathf.Min(112f, textHeight);
        card.sizeDelta = new Vector2(Width, 78f + viewportHeight);
        messageScroll.GetComponent<RectTransform>().sizeDelta = new Vector2(580f, viewportHeight);
        messageLabel.rectTransform.sizeDelta = new Vector2(580f, textHeight);
        messageScrollbar.GetComponent<RectTransform>().sizeDelta = new Vector2(5f, viewportHeight);
        messageScrollbar.gameObject.SetActive(textHeight > viewportHeight);
        messageScroll.StopMovement();
        messageScroll.verticalNormalizedPosition = 1f;
        messageLabel.rectTransform.anchoredPosition = Vector2.zero;
        countdown.anchorMax = Vector2.one;
        lifetime = StartCoroutine(ShowForTenSeconds());
    }

    private IEnumerator ShowForTenSeconds()
    {
        float startAlpha = group.alpha;
        Vector2 startPosition = card.anchoredPosition;
        for (float elapsed = 0f; elapsed < EnterSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / EnterSeconds), 3f);
            group.alpha = Mathf.Lerp(startAlpha, 1f, t);
            card.anchoredPosition = Vector2.Lerp(startPosition, new Vector2(0f, -24f), t);
            yield return null;
        }
        group.alpha = 1f;
        card.anchoredPosition = new Vector2(0f, -24f);
        for (float elapsed = 0f; elapsed < VisibleSeconds; elapsed += Time.unscaledDeltaTime)
        {
            countdown.anchorMax = new Vector2(1f - elapsed / VisibleSeconds, 1f);
            yield return null;
        }
        dismissing = true;
        yield return FadeOut();
    }

    private IEnumerator FadeOut()
    {
        group.interactable = group.blocksRaycasts = false;
        float startAlpha = group.alpha;
        Vector2 startPosition = card.anchoredPosition;
        for (float elapsed = 0f; elapsed < ExitSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / ExitSeconds);
            group.alpha = Mathf.Lerp(startAlpha, 0f, t);
            card.anchoredPosition = startPosition + new Vector2(0f, 16f * t);
            yield return null;
        }
        group.alpha = 0f;
        if (instance == this) instance = null;
        Destroy(gameObject);
    }

    private void Build()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32761; // Above the loading screen, but only this card catches input.
        ResponsiveUi.ConfigureCanvasScaler(GetComponent<CanvasScaler>(), new Vector2(1672f, 941f));
        var safe = Rect(transform, "Notification Safe Area", Vector2.zero, Vector2.zero);
        safe.anchorMin = Vector2.zero;
        safe.anchorMax = Vector2.one;
        safe.offsetMin = safe.offsetMax = Vector2.zero;
        safe.gameObject.AddComponent<ResponsiveSafeArea>();
        card = Rect(safe, "Paper Banner", new Vector2(0f, -8f), new Vector2(Width, 130f));
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
        card.pivot = new Vector2(0.5f, 1f);
        group = card.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        paper = card.gameObject.AddComponent<Image>();
        paper.raycastTarget = true;
        var shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.12f, 0.12f, 0.08f, 0.22f);
        shadow.effectDistance = new Vector2(4f, -5f);
        var outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = Ink;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        accentStrip = Box(card, "Result Accent", Vector2.zero, new Vector2(6f, 0f), Color.white);
        accentStrip.rectTransform.anchorMin = Vector2.zero;
        accentStrip.rectTransform.anchorMax = new Vector2(0f, 1f);
        accentStrip.rectTransform.offsetMin = Vector2.zero;
        accentStrip.rectTransform.offsetMax = new Vector2(6f, 0f);

        icon = Box(card, "Result Badge", new Vector2(22f, -23f), new Vector2(38f, 38f), Color.white);
        iconMarks = Rect(icon.transform, "Result Symbol", Vector2.zero, new Vector2(38f, 38f));
        titleLabel = Label(card, "Result Title", new Vector2(78f, -15f), new Vector2(580f, 38f), 30f);
        titleLabel.fontStyle = FontStyles.Bold;

        var body = Rect(card, "Reason Viewport", new Vector2(78f, -58f), new Vector2(580f, 52f));
        body.gameObject.AddComponent<RectMask2D>();
        var bodyHit = body.gameObject.AddComponent<Image>();
        bodyHit.color = Color.clear;
        bodyHit.raycastTarget = true;
        messageScroll = body.gameObject.AddComponent<ScrollRect>();
        messageScroll.viewport = body;
        messageScroll.horizontal = false;
        messageScroll.movementType = ScrollRect.MovementType.Clamped;
        messageScroll.scrollSensitivity = 24f;
        messageLabel = Label(body, "Result Reason", Vector2.zero, new Vector2(580f, 52f), 24f);
        messageScroll.content = messageLabel.rectTransform;
        var scrollTrack = Box(card, "Reason Scrollbar", new Vector2(668f, -58f), new Vector2(5f, 52f), new Color(0f, 0f, 0f, 0.12f));
        var scrollHandle = Box(scrollTrack.transform, "Scroll Handle", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.4f));
        scrollHandle.rectTransform.anchorMin = Vector2.zero;
        scrollHandle.rectTransform.anchorMax = Vector2.one;
        scrollHandle.rectTransform.offsetMin = scrollHandle.rectTransform.offsetMax = Vector2.zero;
        scrollHandle.raycastTarget = true;
        messageScrollbar = scrollTrack.gameObject.AddComponent<Scrollbar>();
        messageScrollbar.handleRect = scrollHandle.rectTransform;
        messageScrollbar.targetGraphic = scrollHandle;
        messageScrollbar.direction = Scrollbar.Direction.BottomToTop;
        messageScroll.verticalScrollbar = messageScrollbar;

        Image close = Box(card, "Dismiss Notification", new Vector2(681f, -13f), new Vector2(44f, 44f), new Color(1f, 1f, 1f, 0.45f));
        close.raycastTarget = true;
        for (int i = 0; i < 2; i++)
        {
            Image line = Box(close.transform, "Close Stroke", new Vector2(22f, -22f), new Vector2(18f, 2.5f), Ink);
            line.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
        }
        var button = close.gameObject.AddComponent<Button>();
        button.targetGraphic = close;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(Dismiss);
        var track = Rect(card, "Dismiss Countdown", Vector2.zero, new Vector2(-4f, 3f));
        track.anchorMin = Vector2.zero;
        track.anchorMax = new Vector2(1f, 0f);
        track.pivot = new Vector2(0.5f, 0f);
        track.anchoredPosition = new Vector2(0f, 1f);
        countdown = Box(track, "Time Remaining", Vector2.zero, Vector2.zero, Color.white).rectTransform;
        countdown.anchorMin = Vector2.zero;
        countdown.anchorMax = Vector2.one;
        countdown.offsetMin = countdown.offsetMax = Vector2.zero;
    }

    private void DrawIcon(ResultKind result)
    {
        foreach (Transform child in iconMarks)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        if (result == ResultKind.Success)
        {
            Stroke(new Vector2(12f, -22f), new Vector2(12f, 3f), -42f);
            Stroke(new Vector2(23f, -19f), new Vector2(22f, 3f), 48f);
        }
        else
        {
            Stroke(new Vector2(19f, result == ResultKind.Error ? -15f : -23f), new Vector2(3f, 14f), 0f);
            Stroke(new Vector2(19f, result == ResultKind.Error ? -28f : -10f), new Vector2(3f, 3f), 0f);
        }
    }

    private void Stroke(Vector2 position, Vector2 size, float angle)
    {
        var line = Box(iconMarks, "Ink Stroke", position, size, new Color(1f, 0.99f, 0.94f));
        line.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image Box(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var image = Rect(parent, name, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, Vector2 position, Vector2 size, float fontSize)
    {
        var label = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = ChessFontCatalog.TmpFont ? ChessFontCatalog.TmpFont : TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.color = Ink;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.richText = false;
        label.raycastTarget = false;
        return label;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
