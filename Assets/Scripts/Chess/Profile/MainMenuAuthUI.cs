using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class MainMenuAuthUI : MonoBehaviour
{
    public event Action SubmitRequested;
    public event Action GuestRequested;

    public enum AuthMode
    {
        Login,
        SignUp
    }

    private const float ReferenceWidth = 1672f;
    private const float ReferenceHeight = 941f;

    private HandDrawnMenuAssets sharedMenuAssets;
    private TMP_Text statusLabel;
    private TMP_InputField loginUsernameField;
    private TMP_InputField loginPasswordField;
    private TMP_InputField signUpUsernameField;
    private TMP_InputField signUpPasswordField;
    private TMP_InputField signUpConfirmPasswordField;
    private TMP_FontAsset handwrittenFont;
    private Sprite confirmLoginSprite;
    private Sprite confirmSignUpSprite;
    private Sprite playAsGuestSprite;

    public GameObject LoginMenuPanel { get; private set; }
    public GameObject SignUpMenuPanel { get; private set; }
    public AuthMode CurrentMode { get; private set; } = AuthMode.Login;

    public string Username =>
        CurrentMode == AuthMode.Login
            ? loginUsernameField != null ? loginUsernameField.text : string.Empty
            : signUpUsernameField != null ? signUpUsernameField.text : string.Empty;

    public string Password =>
        CurrentMode == AuthMode.Login
            ? loginPasswordField != null ? loginPasswordField.text : string.Empty
            : signUpPasswordField != null ? signUpPasswordField.text : string.Empty;

    public string ConfirmPassword => signUpConfirmPasswordField != null ? signUpConfirmPasswordField.text : string.Empty;

    public void Initialize(HandDrawnMenuAssets menuAssets, string initialUsername, string initialStatus)
    {
        sharedMenuAssets = menuAssets;

        EnsureEventSystem();
        BuildCanvas();
        PopulateInitialValues(initialUsername, initialStatus);
        ShowLogin();
    }

    public void ShowLogin()
    {
        CurrentMode = AuthMode.Login;
        if (LoginMenuPanel != null)
            LoginMenuPanel.SetActive(true);
        if (SignUpMenuPanel != null)
            SignUpMenuPanel.SetActive(false);
        if (loginUsernameField != null)
            loginUsernameField.ActivateInputField();
    }

    public void ShowSignUp()
    {
        CurrentMode = AuthMode.SignUp;
        if (LoginMenuPanel != null)
            LoginMenuPanel.SetActive(false);
        if (SignUpMenuPanel != null)
            SignUpMenuPanel.SetActive(true);
        if (signUpUsernameField != null)
            signUpUsernameField.ActivateInputField();
    }

    public void SetInteractable(bool interactable)
    {
        SetFieldInteractable(loginUsernameField, interactable);
        SetFieldInteractable(loginPasswordField, interactable);
        SetFieldInteractable(signUpUsernameField, interactable);
        SetFieldInteractable(signUpPasswordField, interactable);
        SetFieldInteractable(signUpConfirmPasswordField, interactable);
        SetPanelButtonsInteractable(LoginMenuPanel, interactable);
        SetPanelButtonsInteractable(SignUpMenuPanel, interactable);
    }

    public void SetStatusMessage(string message)
    {
        if (statusLabel == null)
            return;

        statusLabel.text = message ?? string.Empty;
    }

    public void RequestSubmit()
    {
        SubmitRequested?.Invoke();
    }

    public void RequestGuestPlay()
    {
        GuestRequested?.Invoke();
    }

    public void ClearSensitiveFields()
    {
        if (loginPasswordField != null)
            loginPasswordField.text = string.Empty;
        if (signUpPasswordField != null)
            signUpPasswordField.text = string.Empty;
        if (signUpConfirmPasswordField != null)
            signUpConfirmPasswordField.text = string.Empty;
    }

    private void BuildCanvas()
    {
        handwrittenFont = CreateHandwrittenFont();
        LoadActionSprites();

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        ResponsiveUi.ConfigureCanvasScaler(scaler, new Vector2(ReferenceWidth, ReferenceHeight));

        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform root = gameObject.GetComponent<RectTransform>();
        Stretch(root);

        Image paper = CreateImage(root, "Paper Background", null, Vector2.zero, Vector2.zero);
        paper.color = new Color(0.985f, 0.965f, 0.91f, 1f);
        Stretch(paper.rectTransform);

        RectTransform authMenuGroup = CreateGroup(root, "AuthMenuGroup");
        authMenuGroup.gameObject.AddComponent<ResponsiveSafeArea>();
        BuildAuthPanels(authMenuGroup);
    }

    private void BuildAuthPanels(RectTransform parent)
    {
        LoginMenuPanel = CreatePanel(parent, "LoginMenuPanel", LoadSpriteFromProjectFile("Assets/Materials/Main_Menu/LoginMainMenu.png"), Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight));
        SignUpMenuPanel = CreatePanel(parent, "SignUpMenuPanel", LoadSpriteFromProjectFile("Assets/Materials/Main_Menu/SignUpMainMenu.png"), Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight));

        CreateTransparentButton(LoginMenuPanel.transform, "LoginButton", new Vector2(-326f, -58f), new Vector2(418f, 130f), ShowLogin);
        CreateTransparentButton(LoginMenuPanel.transform, "SignUpButton", new Vector2(-324f, -229f), new Vector2(424f, 130f), ShowSignUp);
        CreateSpriteButton(LoginMenuPanel.transform, "PlayAsGuestButton", playAsGuestSprite, new Vector2(-324f, -391f), new Vector2(424f, 130f), RequestGuestPlay);
        CreateTransparentButton(SignUpMenuPanel.transform, "LoginButton", new Vector2(-335f, -65f), new Vector2(428f, 132f), ShowLogin);
        CreateTransparentButton(SignUpMenuPanel.transform, "SignUpButton", new Vector2(-329f, -236f), new Vector2(435f, 136f), ShowSignUp);
        CreateSpriteButton(SignUpMenuPanel.transform, "PlayAsGuestButton", playAsGuestSprite, new Vector2(-329f, -395f), new Vector2(435f, 136f), RequestGuestPlay);
        CreateSpriteButton(LoginMenuPanel.transform, "ConfirmLoginButton", confirmLoginSprite, new Vector2(255f, -317f), new Vector2(520f, 92f), RequestSubmit);
        CreateSpriteButton(SignUpMenuPanel.transform, "ConfirmSignUpButton", confirmSignUpSprite, new Vector2(258f, -395f), new Vector2(540f, 92f), RequestSubmit);

        loginUsernameField = CreateInputField(
            LoginMenuPanel.transform as RectTransform,
            "LoginUsernameField",
            new Vector2(246.5f, -83.5f),
            new Vector2(478f, 72f),
            TMP_InputField.ContentType.Standard,
            false,
            0f,
            32f,
            23f);
        loginPasswordField = CreateInputField(
            LoginMenuPanel.transform as RectTransform,
            "LoginPasswordField",
            new Vector2(245.5f, -247.5f),
            new Vector2(478f, 72f),
            TMP_InputField.ContentType.Password,
            true,
            0f,
            32f,
            23f);

        signUpUsernameField = CreateInputField(
            SignUpMenuPanel.transform as RectTransform,
            "SignUpUsernameField",
            new Vector2(247f, -26.5f),
            new Vector2(576f, 62f),
            TMP_InputField.ContentType.Standard,
            false,
            0f);
        signUpPasswordField = CreateInputField(
            SignUpMenuPanel.transform as RectTransform,
            "SignUpPasswordField",
            new Vector2(245f, -166f),
            new Vector2(576f, 62f),
            TMP_InputField.ContentType.Password,
            true,
            0f);
        signUpConfirmPasswordField = CreateInputField(
            SignUpMenuPanel.transform as RectTransform,
            "SignUpConfirmPasswordField",
            new Vector2(245f, -302f),
            new Vector2(576f, 62f),
            TMP_InputField.ContentType.Password,
            true,
            0f);

        statusLabel = CreateStatusLabel(parent, new Vector2(296f, -442f), new Vector2(760f, 44f));
    }

    private void PopulateInitialValues(string initialUsername, string initialStatus)
    {
        if (loginUsernameField != null)
            loginUsernameField.text = initialUsername ?? string.Empty;
        if (signUpUsernameField != null)
            signUpUsernameField.text = initialUsername ?? string.Empty;
        SetStatusMessage(initialStatus);
    }

    private static RectTransform CreateGroup(Transform parent, string name)
    {
        GameObject group = new GameObject(name, typeof(RectTransform));
        RectTransform rect = group.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Stretch(rect);
        return rect;
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static Button CreateTransparentButton(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        Image image = CreateImage(parent, name, null, anchoredPosition, size);
        image.color = new Color(1f, 1f, 1f, 0.001f);
        image.raycastTarget = true;

        Button button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        if (onClick != null)
            button.onClick.AddListener(onClick);
        return button;
    }

    private static Button CreateSpriteButton(Transform parent, string name, Sprite sprite, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        Image image = CreateImage(parent, name, sprite, anchoredPosition, size);
        image.raycastTarget = true;

        Button button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        if (onClick != null)
            button.onClick.AddListener(onClick);
        return button;
    }

    private static GameObject CreatePanel(Transform parent, string name, Sprite sprite, Vector2 anchoredPosition, Vector2 size)
    {
        Image panel = CreateImage(parent, name, sprite, anchoredPosition, size);
        panel.raycastTarget = false;
        return panel.gameObject;
    }

    private static TMP_InputField CreateInputField(
        RectTransform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        TMP_InputField.ContentType contentType,
        bool password,
        float textOffsetY,
        float fontSize = 28f,
        float leftPadding = 18f)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image background = root.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.001f);
        background.raycastTarget = true;

        TMP_InputField inputField = root.GetComponent<TMP_InputField>();
        inputField.contentType = contentType;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.richText = false;
        inputField.caretWidth = 3;
        inputField.selectionColor = new Color(0.2f, 0.42f, 0.96f, 0.25f);
        if (password)
            inputField.asteriskChar = '*';

        GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.SetParent(root.transform, false);
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(leftPadding, 3f);
        textAreaRect.offsetMax = new Vector2(-18f, -3f);

        GameObject placeholder = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.SetParent(textArea.transform, false);
        ConfigureSingleLineTextRect(placeholderRect, textOffsetY);
        TextMeshProUGUI placeholderText = placeholder.GetComponent<TextMeshProUGUI>();
        MainMenuAuthUI owner = parent.GetComponentInParent<MainMenuAuthUI>();
        ApplyInputTextStyle(placeholderText, owner != null ? owner.handwrittenFont : null, fontSize);
        placeholderText.text = string.Empty;
        placeholderText.color = new Color(0f, 0f, 0f, 0.14f);

        GameObject text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.SetParent(textArea.transform, false);
        ConfigureSingleLineTextRect(textRect, textOffsetY);
        TextMeshProUGUI inputText = text.GetComponent<TextMeshProUGUI>();
        ApplyInputTextStyle(inputText, owner != null ? owner.handwrittenFont : null, fontSize);
        inputText.color = new Color(0.05f, 0.05f, 0.05f, 1f);
        inputText.extraPadding = true;
        inputText.raycastTarget = false;

        inputField.textViewport = textAreaRect;
        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;
        inputField.customCaretColor = true;
        inputField.caretColor = new Color(0.05f, 0.05f, 0.05f, 1f);
        inputField.shouldHideMobileInput = true;
        inputField.onFocusSelectAll = false;

        return inputField;
    }

    private static TMP_Text CreateStatusLabel(Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject labelObject = new GameObject("StatusLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        MainMenuAuthUI owner = parent.GetComponentInParent<MainMenuAuthUI>();
        ApplyInputTextStyle(label, owner != null ? owner.handwrittenFont : null);
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 15f;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.color = new Color(0.33f, 0.33f, 0.33f, 0.82f);
        label.text = string.Empty;
        return label;
    }

    private static void ApplyInputTextStyle(TMP_Text text, TMP_FontAsset font, float fontSize = 28f)
    {
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.characterSpacing = 0.5f;
        text.margin = Vector4.zero;
    }

    private static void ConfigureSingleLineTextRect(RectTransform rect, float textOffsetY)
    {
        // Stretch to the complete input viewport. The previous fixed 34 px
        // baseline was clipped by RectMask2D whenever font size or Y offset grew.
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = new Vector2(0f, textOffsetY);
    }

    private static void SetFieldInteractable(TMP_InputField field, bool interactable)
    {
        if (field == null)
            return;

        field.interactable = interactable;
    }

    private static void SetPanelButtonsInteractable(GameObject panel, bool interactable)
    {
        if (panel == null)
            return;

        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
            button.interactable = interactable;
    }

    private static Sprite LoadSpriteFromProjectFile(string projectRelativePath)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[MainMenuAuthUI] Missing sprite at {projectRelativePath}");
            return null;
        }

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        if (!texture.LoadImage(bytes))
        {
            UnityEngine.Object.Destroy(texture);
            Debug.LogWarning($"[MainMenuAuthUI] Failed to load sprite at {projectRelativePath}");
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(projectRelativePath);
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
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
            GameObject eventSystemObject = new GameObject("EventSystem");
            current = eventSystemObject.AddComponent<EventSystem>();
        }

        StandaloneInputModule standaloneInput = current.GetComponent<StandaloneInputModule>();
        if (standaloneInput != null)
            standaloneInput.enabled = false;

        if (current.GetComponent<InputSystemUIInputModule>() == null)
            current.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    private static TMP_FontAsset CreateHandwrittenFont()
    {
        // OS dynamic fonts such as Segoe Print do not expose font-face data to
        // TMP reliably and emit "Include Font Data" warnings. Use the imported
        // project default, which is available consistently in builds.
        return TMP_Settings.defaultFontAsset;
    }

    private void LoadActionSprites()
    {
        confirmLoginSprite = LoadSpriteFromProjectFile("Assets/Materials/Main_Menu/ConfirmLogin.png");
        confirmSignUpSprite = LoadSpriteFromProjectFile("Assets/Materials/Main_Menu/ConfirmSignUp.png");
        playAsGuestSprite = LoadSpriteFromProjectFile("Assets/Materials/Main_Menu/PlayAsGuest.png");
    }
}
