using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ChessButWeird.Online;

public sealed class LoadingUI : MonoBehaviour
{
    private CanvasGroup group;
    private Image bar;
    private Text label;
    private Text percentage;
    private Button retry;
    private Button back;
    private float target;
    private float displayed;
    private RectTransform layout;
    private Text title;
    private MatchVersusView versus;
    private MatchLoadingContext matchup;
    private CosmeticCatalog catalog;
    private float shownAt;

    public static LoadingUI Create(Transform parent)
    {
        if (!UnityEngine.EventSystems.EventSystem.current)
        {
            var events = new GameObject("Loading EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            events.transform.SetParent(parent, false);
        }
        var root = new GameObject("Loading", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var ui = root.AddComponent<LoadingUI>();
        ui.group = root.GetComponent<CanvasGroup>();
        var paper = ui.Rect("Paper", root.transform, Vector2.zero, Vector2.zero);
        paper.anchorMin = Vector2.zero; paper.anchorMax = Vector2.one;
        paper.offsetMin = paper.offsetMax = Vector2.zero;
        paper.gameObject.AddComponent<AntialiasedMenuImage>().color = new Color(.985f, .97f, .93f);
        ui.layout = MenuDesignFrame.Create(root.transform, "Loading", new Vector2(1672, 941));
        ui.title = ui.AddText("Chess but Weird", new Vector2(0, 145), 48);
        ui.label = ui.AddText("Preparing...", new Vector2(0, 40), 26);
        var track = ui.Rect("Progress", ui.layout, new Vector2(0, -25), new Vector2(620, 14));
        track.gameObject.AddComponent<AntialiasedMenuImage>().color = new Color(.2f, .18f, .16f, .15f);
        var fill = ui.Rect("Fill", track, Vector2.zero, Vector2.zero);
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        ui.bar = fill.gameObject.AddComponent<AntialiasedMenuImage>();
        ui.bar.color = new Color(.2f, .35f, .65f);
        ui.percentage = ui.AddText("0%", new Vector2(0, -75), 22);
        ui.retry = ui.AddButton("Retry", new Vector2(-145, -155));
        ui.back = ui.AddButton("Back to menu", new Vector2(145, -155));
        ui.retry.gameObject.SetActive(false); ui.back.gameObject.SetActive(false);
        return ui;
    }

    public void ShowMatchup(MatchLoadingContext value)
    {
        if (value == null || versus != null) return;
        matchup = value; shownAt = Time.realtimeSinceStartup;
        title.gameObject.SetActive(false);
        versus = new MatchVersusView(layout, value);
        label.rectTransform.anchoredPosition = new Vector2(0, -320);
        label.rectTransform.sizeDelta = new Vector2(1330, 50);
        bar.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -367);
        percentage.rectTransform.anchoredPosition = new Vector2(430, -367);
        percentage.rectTransform.sizeDelta = new Vector2(150, 38);
        retry.GetComponent<RectTransform>().anchoredPosition = new Vector2(-145, -439);
        back.GetComponent<RectTransform>().anchoredPosition = new Vector2(145, -439);
        versus.Refresh(catalog, displayed);
    }

    public void SetCatalog(CosmeticCatalog value) { catalog = value; }

    private RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    private Text AddText(string text, Vector2 position, int size)
    {
        var t = Rect(text, layout, position, new Vector2(950, 70)).gameObject.AddComponent<Text>();
        t.font = ChessFontCatalog.RuntimeFont;
        t.fontSize = size; t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(.16f, .14f, .12f); t.text = text; t.raycastTarget = false;
        t.supportRichText = false;
        return t;
    }

    private Button AddButton(string text, Vector2 position)
    {
        var rect = Rect(text, layout, position, new Vector2(260, 60));
        rect.gameObject.AddComponent<AntialiasedMenuImage>().color = new Color(.95f, .82f, .4f);
        var b = rect.gameObject.AddComponent<Button>();
        var t = AddText(text, Vector2.zero, 22);
        t.transform.SetParent(rect, false); t.rectTransform.sizeDelta = rect.sizeDelta;
        return b;
    }

    public void Report(float progress, string message)
    {
        target = Mathf.Max(target, Mathf.Clamp01(progress));
        label.text = message;
    }

    private void Update()
    {
        displayed = Mathf.MoveTowards(displayed, target, Time.unscaledDeltaTime * 2.5f);
        bar.rectTransform.anchorMax = new Vector2(displayed, 1);
        percentage.text = Mathf.FloorToInt(displayed * 100) + "%";
        versus?.Refresh(catalog, displayed);
    }

    public void Error(string message, UnityEngine.Events.UnityAction onRetry, UnityEngine.Events.UnityAction onBack)
    {
        label.text = message;
        retry.gameObject.SetActive(true); back.gameObject.SetActive(true);
        retry.onClick.RemoveAllListeners(); back.onClick.RemoveAllListeners();
        retry.onClick.AddListener(() => { retry.interactable = back.interactable = false; onRetry(); });
        back.onClick.AddListener(() => { retry.interactable = back.interactable = false; onBack(); });
    }

    public IEnumerator Finish()
    {
        target = 1;
        while (displayed < 1) yield return null;
        // Let a fast first load show both players. Rejoining an already running
        // match adds no reading delay, and online Ready still waits for loading.
        while (matchup?.State.status == "AwaitingReady" && Time.realtimeSinceStartup - shownAt < 2f) yield return null;
        for (float t = 0; t < .18f; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1 - t / .18f;
            yield return null;
        }
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
