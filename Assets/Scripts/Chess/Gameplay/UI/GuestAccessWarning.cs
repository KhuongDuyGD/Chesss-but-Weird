using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// A reusable modal: dismissing it never changes authentication or menu state.
public sealed class GuestAccessWarning : MonoBehaviour, ICancelHandler
{
    private static readonly Color Ink = new Color(0.16f, 0.12f, 0.09f);
    private Text messageLabel;
    private Button dismissButton;
    private GameObject previousSelection;

    public static void Show(Transform owner, string message)
    {
        var warning = owner.GetComponentInChildren<GuestAccessWarning>(true);
        if (!warning)
        {
            var root = new GameObject("Guest Access Warning", typeof(RectTransform));
            root.transform.SetParent(owner, false);
            warning = root.AddComponent<GuestAccessWarning>();
            warning.Build();
        }

        if (!warning.gameObject.activeSelf || !warning.previousSelection)
            warning.previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        warning.messageLabel.text = message;
        warning.gameObject.SetActive(true);
        warning.dismissButton.Select();
    }

    public void Dismiss()
    {
        gameObject.SetActive(false);
        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(previousSelection && previousSelection.activeInHierarchy ? previousSelection : null);
        previousSelection = null;
    }

    public void OnCancel(BaseEventData eventData)
    {
        Dismiss();
        eventData.Use();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Dismiss();
            return;
        }

        // Keep keyboard/controller focus inside the modal as well as blocking clicks.
        if (EventSystem.current && EventSystem.current.currentSelectedGameObject != dismissButton.gameObject)
            dismissButton.Select();
    }

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3000;
        ResponsiveUi.ConfigureCanvasScaler(gameObject.AddComponent<CanvasScaler>(), new Vector2(1920f, 1080f));
        gameObject.AddComponent<GraphicRaycaster>();

        if (!EventSystem.current)
        {
            var events = new GameObject("Guest Warning Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform.parent, false);
        }

        var backdrop = Box(transform, "Dimmed Background", Vector2.zero, Vector2.zero, new Color(0.08f, 0.06f, 0.04f, 0.66f));
        backdrop.rectTransform.anchorMin = Vector2.zero;
        backdrop.rectTransform.anchorMax = Vector2.one;
        backdrop.rectTransform.offsetMin = backdrop.rectTransform.offsetMax = Vector2.zero;
        backdrop.raycastTarget = true;

        Box(transform, "Paper Shadow", new Vector2(9f, -10f), new Vector2(790f, 520f), new Color(0f, 0f, 0f, 0.3f));
        var border = Box(transform, "Ink Border", Vector2.zero, new Vector2(790f, 520f), Ink);
        var paper = Box(border.transform, "Paper Card", Vector2.zero, new Vector2(780f, 510f), new Color(0.985f, 0.965f, 0.91f));
        paper.raycastTarget = true;

        // Draw a lock with UI primitives so it works without new sprite assets.
        Box(paper.transform, "Lock Shackle", new Vector2(0f, 182f), new Vector2(42f, 42f), Ink);
        Box(paper.transform, "Shackle Opening", new Vector2(0f, 179f), new Vector2(26f, 30f), paper.color);
        Box(paper.transform, "Lock Outline", new Vector2(0f, 149f), new Vector2(72f, 55f), Ink);
        Box(paper.transform, "Amber Lock", new Vector2(0f, 149f), new Vector2(62f, 45f), new Color(0.98f, 0.78f, 0.3f));
        Box(paper.transform, "Keyhole", new Vector2(0f, 148f), new Vector2(7f, 17f), Ink);

        Label(paper.transform, "Title", "Guest access limited", new Vector2(0f, 74f), new Vector2(700f, 60f), 44, FontStyle.Bold);
        messageLabel = Label(paper.transform, "Feature Message", "", new Vector2(0f, -9f), new Vector2(670f, 100f), 28);
        messageLabel.supportRichText = false;
        Label(paper.transform, "Guest Reassurance", "You're still playing as Guest. Local play is available.", new Vector2(0f, -93f), new Vector2(690f, 44f), 24);

        var buttonImage = Box(paper.transform, "Continue as Guest", new Vector2(0f, -178f), new Vector2(380f, 76f), new Color(0.98f, 0.83f, 0.32f));
        buttonImage.raycastTarget = true;
        var outline = buttonImage.gameObject.AddComponent<Outline>();
        outline.effectColor = Ink;
        outline.effectDistance = new Vector2(3f, -3f);
        dismissButton = buttonImage.gameObject.AddComponent<Button>();
        dismissButton.targetGraphic = buttonImage;
        dismissButton.navigation = new Navigation { mode = Navigation.Mode.None };
        dismissButton.onClick.AddListener(Dismiss);
        Label(buttonImage.transform, "Label", "Continue as Guest", Vector2.zero, new Vector2(350f, 64f), 30, FontStyle.Bold);
    }

    private static Image Box(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Ink;
        text.raycastTarget = false;
        return text;
    }
}
