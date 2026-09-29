using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class MainMenuAuthUI : MonoBehaviour
{
    public event Action SubmitRequested;
    public event Action GuestRequested;

    public enum AuthMode { Login, SignUp }

    private const float ReferenceWidth = 1672f;
    private const float ReferenceHeight = 941f;
    private static readonly Color Paper = new Color(0.985f, 0.965f, 0.91f);
    private static readonly Color Ink = new Color(0.10f, 0.09f, 0.08f);
    private static readonly Color Blue = new Color(0.67f, 0.79f, 0.98f);
    private static readonly Color Green = new Color(0.75f, 0.88f, 0.69f);
    private static readonly Color Yellow = new Color(0.98f, 0.85f, 0.44f);

    private TMP_FontAsset font;
    private TMP_InputField loginEmailField;
    private TMP_InputField loginPasswordField;
    private TMP_InputField signUpUsernameField;
    private TMP_InputField signUpEmailField;
    private TMP_InputField signUpPasswordField;
    private TMP_InputField signUpConfirmPasswordField;

    public GameObject LoginMenuPanel { get; private set; }
    public GameObject SignUpMenuPanel { get; private set; }
    public AuthMode CurrentMode { get; private set; } = AuthMode.Login;
    public string Username => signUpUsernameField != null ? signUpUsernameField.text : string.Empty;
    public string Email => CurrentMode == AuthMode.Login
        ? loginEmailField != null ? loginEmailField.text : string.Empty
        : signUpEmailField != null ? signUpEmailField.text : string.Empty;
    public string Password => CurrentMode == AuthMode.Login
        ? loginPasswordField != null ? loginPasswordField.text : string.Empty
        : signUpPasswordField != null ? signUpPasswordField.text : string.Empty;
    public string ConfirmPassword => signUpConfirmPasswordField != null ? signUpConfirmPasswordField.text : string.Empty;

    // Keep the existing entry point used by AuthController and the layout audit.
    public void Initialize(HandDrawnMenuAssets unusedAssets, string initialEmail, string initialStatus)
    {
        font = ChessFontCatalog.TmpFont != null ? ChessFontCatalog.TmpFont : TMP_Settings.defaultFontAsset;
        EnsureEventSystem();
        BuildCanvas();
        if (loginEmailField != null) loginEmailField.text = initialEmail ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(initialStatus)) SetStatusMessage(initialStatus);
        ShowLogin();
    }

    public void ShowLogin()
    {
        CurrentMode = AuthMode.Login;
        if (LoginMenuPanel) LoginMenuPanel.SetActive(true);
        if (SignUpMenuPanel) SignUpMenuPanel.SetActive(false);
        if (loginEmailField) loginEmailField.ActivateInputField();
    }

    public void ShowSignUp()
    {
        CurrentMode = AuthMode.SignUp;
        if (LoginMenuPanel) LoginMenuPanel.SetActive(false);
        if (SignUpMenuPanel) SignUpMenuPanel.SetActive(true);
        if (signUpUsernameField) signUpUsernameField.ActivateInputField();
    }

    public void SetInteractable(bool interactable)
    {
        foreach (TMP_InputField field in GetComponentsInChildren<TMP_InputField>(true))
            field.interactable = interactable;
        foreach (Button button in GetComponentsInChildren<Button>(true))
            button.interactable = interactable;
    }

    public void SetStatusMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            AuthNotificationView.Show("Account", message, AuthNotificationView.ResultKind.Info);
    }

    public void RequestSubmit() => SubmitRequested?.Invoke();
    public void RequestGuestPlay() => GuestRequested?.Invoke();

    public void ClearSensitiveFields()
    {
        if (loginPasswordField) loginPasswordField.text = string.Empty;
        if (signUpPasswordField) signUpPasswordField.text = string.Empty;
        if (signUpConfirmPasswordField) signUpConfirmPasswordField.text = string.Empty;
    }

    private void BuildCanvas()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        ResponsiveUi.ConfigureCanvasScaler(scaler, new Vector2(ReferenceWidth, ReferenceHeight));
        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform root = gameObject.GetComponent<RectTransform>();
        Stretch(root);
        Image background = CreateImage(root, "Paper Background", new Rect(0, 0, ReferenceWidth, ReferenceHeight), Paper);
        Stretch(background.rectTransform);

        RectTransform safeArea = new GameObject("Auth Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
        safeArea.SetParent(root, false);
        Stretch(safeArea);
        safeArea.gameObject.AddComponent<ResponsiveSafeArea>();

        RectTransform layout = new GameObject("Auth Layout", typeof(RectTransform)).GetComponent<RectTransform>();
        layout.SetParent(safeArea, false);
        layout.gameObject.AddComponent<InventoryContentRootFitter>().Configure(ReferenceWidth, ReferenceHeight, 1f);
        BuildPanels(layout);
    }

    private void BuildPanels(RectTransform parent)
    {
        LoginMenuPanel = BuildPanel(parent, "LoginMenuPanel", false).gameObject;
        SignUpMenuPanel = BuildPanel(parent, "SignUpMenuPanel", true).gameObject;
    }

    private RectTransform BuildPanel(RectTransform parent, string name, bool signUp)
    {
        RectTransform panel = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(parent, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
        panel.anchoredPosition = Vector2.zero;

        CreateText(panel, "Game Title", "Chess But Weird", new Rect(110, 72, 600, 115), 78, Ink);
        CreateText(panel, "Account Caption", "YOUR CHESS ADVENTURE STARTS HERE",
            new Rect(128, 190, 510, 50), 27, Ink);
        CreateImage(panel, "Title Accent", new Rect(126, 244, 475, 7), Blue);

        CreateButton(panel, "LoginButton", "LOG IN", new Rect(130, 318, 430, 105),
            signUp ? Paper : Yellow, ShowLogin);
        CreateButton(panel, "SignUpButton", "SIGN UP", new Rect(130, 448, 430, 105),
            signUp ? Green : Paper, ShowSignUp);
        CreateButton(panel, "PlayAsGuestButton", "PLAY AS GUEST", new Rect(130, 578, 430, 105),
            Paper, RequestGuestPlay);

        RectTransform card = CreateRoundedGraphic(panel, "Account Form", new Rect(650, 130, 880, 700),
            new Color(1f, 0.995f, 0.975f), 28f, 3f, 1.4f, 11).rectTransform;
        CreateText(card, "Form Heading", signUp ? "Create your account" : "Welcome back",
            new Rect(58, 35, 750, 70), 48, Ink);

        if (signUp)
        {
            signUpUsernameField = CreateField(card, "SignUpUsernameField", "Username",
                "Choose a username", new Rect(65, 130, 740, 75), false);
            signUpEmailField = CreateField(card, "SignUpEmailField", "Email",
                "you@example.com", new Rect(65, 238, 740, 75), false);
            signUpEmailField.contentType = TMP_InputField.ContentType.EmailAddress;
            signUpPasswordField = CreateField(card, "SignUpPasswordField", "Password",
                "Password", new Rect(65, 346, 740, 75), true);
            signUpConfirmPasswordField = CreateField(card, "SignUpConfirmPasswordField",
                "Confirm password", "Repeat password", new Rect(65, 454, 740, 75), true);
            CreateButton(card, "ConfirmSignUpButton", "CREATE ACCOUNT",
                new Rect(65, 580, 740, 75), Green, RequestSubmit);
        }
        else
        {
            loginEmailField = CreateField(card, "LoginEmailField", "Email",
                "you@example.com", new Rect(65, 175, 740, 90), false);
            loginEmailField.contentType = TMP_InputField.ContentType.EmailAddress;
            loginPasswordField = CreateField(card, "LoginPasswordField", "Password",
                "Password", new Rect(65, 325, 740, 90), true);
            CreateButton(card, "ConfirmLoginButton", "LOG IN",
                new Rect(65, 520, 740, 90), Yellow, RequestSubmit);
        }
        return panel;
    }

    private TMP_InputField CreateField(RectTransform parent, string name, string label,
        string hint, Rect area, bool password)
    {
        CreateText(parent, name + " Label", label,
            new Rect(area.x, area.y, area.width, 38), 27, Ink);

        GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(HandDrawnRoundedGraphic), typeof(TMP_InputField));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Place(rect, new Rect(area.x, area.y + 42, area.width, area.height - 32),
            parent.rect.width, parent.rect.height);
        HandDrawnRoundedGraphic fieldGraphic = root.GetComponent<HandDrawnRoundedGraphic>();
        fieldGraphic.Configure(new Color(1f, 1f, 1f, 0.96f), Ink, 14f, 2f, 0.9f, name.GetHashCode());
        fieldGraphic.raycastTarget = true;

        TMP_InputField field = root.GetComponent<TMP_InputField>();
        field.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.richText = false;
        field.caretWidth = 3;
        field.asteriskChar = '*';

        RectTransform viewport = new GameObject("Text Viewport", typeof(RectTransform), typeof(RectMask2D))
            .GetComponent<RectTransform>();
        viewport.SetParent(rect, false);
        Stretch(viewport);
        viewport.offsetMin = new Vector2(18, 2);
        viewport.offsetMax = new Vector2(-18, -2);

        TextMeshProUGUI placeholder = CreateText(viewport, "Placeholder", hint,
            new Rect(0, 0, area.width - 36, area.height - 32), 25,
            new Color(0.38f, 0.38f, 0.38f));
        TextMeshProUGUI value = CreateText(viewport, "Text", string.Empty,
            new Rect(0, 0, area.width - 36, area.height - 32), 27, Ink);
        Stretch(placeholder.rectTransform);
        Stretch(value.rectTransform);
        placeholder.raycastTarget = false;
        value.raycastTarget = false;
        field.textViewport = viewport;
        field.placeholder = placeholder;
        field.textComponent = value;
        field.fontAsset = font;
        field.pointSize = 27;
        field.customCaretColor = true;
        field.caretColor = Ink;
        field.shouldHideMobileInput = true;
        return field;
    }

    private Button CreateButton(RectTransform parent, string name, string caption,
        Rect area, Color color, UnityEngine.Events.UnityAction action)
    {
        HandDrawnRoundedGraphic graphic = CreateRoundedGraphic(parent, name, area, color,
            22f, 3f, 1.6f, name.GetHashCode());
        graphic.raycastTarget = true;
        Button button = graphic.gameObject.AddComponent<Button>();
        button.targetGraphic = graphic;
        CreateText(graphic.rectTransform, "Label", caption,
            new Rect(0, 0, area.width, area.height), 33, Ink).alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(action);
        return button;
    }

    private TextMeshProUGUI CreateText(RectTransform parent, string name, string caption,
        Rect area, float size, Color color)
    {
        GameObject node = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = node.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Place(rect, area, parent.rect.width, parent.rect.height);
        TextMeshProUGUI text = node.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.text = caption;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateImage(RectTransform parent, string name, Rect area, Color color)
    {
        GameObject node = new GameObject(name, typeof(RectTransform), typeof(AntialiasedMenuImage));
        RectTransform rect = node.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Place(rect, area, parent.rect.width, parent.rect.height);
        Image image = node.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Place(RectTransform rect, Rect area, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(area.center.x - width * 0.5f,
            height * 0.5f - area.center.y);
        rect.sizeDelta = area.size;
    }

    private static HandDrawnRoundedGraphic CreateRoundedGraphic(RectTransform parent, string name,
        Rect area, Color fill, float radius, float strokeWidth, float wobble, int seed)
    {
        GameObject node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(HandDrawnRoundedGraphic));
        RectTransform rect = node.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Place(rect, area, parent.rect.width, parent.rect.height);
        HandDrawnRoundedGraphic graphic = node.GetComponent<HandDrawnRoundedGraphic>();
        graphic.Configure(fill, Ink, radius, strokeWidth, wobble, seed);
        graphic.raycastTarget = false;
        return graphic;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        EventSystem current = EventSystem.current;
        if (current == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            current = eventSystem.AddComponent<EventSystem>();
        }

        StandaloneInputModule oldInput = current.GetComponent<StandaloneInputModule>();
        if (oldInput != null) oldInput.enabled = false;
        if (current.GetComponent<InputSystemUIInputModule>() == null)
            current.gameObject.AddComponent<InputSystemUIInputModule>();
    }
}
