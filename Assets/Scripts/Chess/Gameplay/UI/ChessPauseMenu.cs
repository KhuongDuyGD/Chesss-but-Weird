using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ChessPauseMenu : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    private ChessGame chessGame;
    private ChessLanController lanController;
    private GameObject canvasRoot;
    private GameObject overlay;
    private GameObject buttonGroup;
    private Button resumeButton;
    private Button restartButton;
    private Button quitButton;
    private Text statusText;
    private bool localPaused;
    private bool opponentPaused;
    private bool quitting;
    private int lastEscapeFrame = -1;

    public void Initialize(ChessGame game, ChessLanController networkController)
    {
        chessGame = game;
        lanController = networkController;

        PauseMenuAssetCatalog assets = PauseMenuAssetCatalog.Load();
        if (!assets || !assets.menuBlank || !assets.resumeButton || !assets.restartButton || !assets.settingsButton || !assets.quitButton)
        {
            Debug.LogWarning("[ChessPauseMenu] Pause menu textures are missing.");
            return;
        }

        BuildUi(assets);
        lanController.OpponentPauseChanged += HandleOpponentPauseChanged;
        overlay.SetActive(false);
    }

    private void Update()
    {
        if (!overlay)
            return;

        if (!chessGame || !chessGame.GameStarted)
        {
            if (localPaused || opponentPaused || overlay.activeSelf)
                ResetPauseState();
            return;
        }

        bool escapePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#if ENABLE_LEGACY_INPUT_MANAGER
        escapePressed |= Input.GetKeyDown(KeyCode.Escape);
#endif
        if (escapePressed)
            TryToggleFromEscape();
    }

    private void OnGUI()
    {
        Event currentEvent = Event.current;
        if (currentEvent != null && currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
            TryToggleFromEscape();
    }

    private void TryToggleFromEscape()
    {
        if (!overlay || !chessGame || !chessGame.GameStarted || quitting || lastEscapeFrame == Time.frameCount)
            return;

        lastEscapeFrame = Time.frameCount;
        if (localPaused)
            ResumeLocalPause();
        else
            PauseLocally();
    }

    private void OnDestroy()
    {
        if (lanController)
            lanController.OpponentPauseChanged -= HandleOpponentPauseChanged;

        if (localPaused && (lanController == null || !lanController.IsNetworkGameActive))
            Time.timeScale = 1f;

        if (canvasRoot)
            Destroy(canvasRoot);
    }

    private void BuildUi(PauseMenuAssetCatalog assets)
    {
        canvasRoot = new GameObject("Pause Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        // This must not be nested under HandDrawnMenuView's Canvas. That Canvas is
        // disabled during gameplay, which would make a nested pause menu invisible.
        canvasRoot.transform.SetParent(chessGame ? chessGame.transform : null, false);

        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 2500;

        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        overlay = CreateRectObject("Pause Overlay", canvasRoot.transform);
        Stretch((RectTransform)overlay.transform);

        Image dim = CreateImage("Dim Background", overlay.transform, null, new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform);

        Image panel = CreateImage("Settings Menu Blank", overlay.transform,
            CreateCroppedSprite(assets.menuBlank, 0.086f, 0.054f, 0.829f, 0.895f), Color.white);
        SetRect(panel.rectTransform, Vector2.zero, new Vector2(700f, 940f));

        statusText = CreateStatusText(panel.transform);

        buttonGroup = CreateRectObject("Buttons", panel.transform);
        Stretch((RectTransform)buttonGroup.transform);

        resumeButton = CreateButton(buttonGroup.transform, "Resume Game", assets.resumeButton,
            new Rect(0.074f, 0.137f, 0.857f, 0.675f), new Vector2(0f, 165f), ResumeLocalPause, true);
        restartButton = CreateButton(buttonGroup.transform, "Restart Game", assets.restartButton,
            new Rect(0.085f, 0.124f, 0.836f, 0.688f), new Vector2(0f, 15f), RestartLocalGame, true);

        Button settings = CreateButton(buttonGroup.transform, "Settings Locked", assets.settingsButton,
            new Rect(0.062f, 0.180f, 0.882f, 0.640f), new Vector2(0f, -135f), null, false);
        settings.image.color = new Color(0.72f, 0.72f, 0.72f, 0.72f);

        quitButton = CreateButton(buttonGroup.transform, "Quit Game", assets.quitButton,
            new Rect(0.035f, 0.165f, 0.935f, 0.675f), new Vector2(0f, -285f), QuitGame, true);

        EnsureEventSystem();
    }

    private void PauseLocally()
    {
        if (!chessGame || !chessGame.GameStarted)
            return;

        localPaused = true;
        chessGame.SetPauseLocked(true);
        if (lanController != null && lanController.IsNetworkGameActive)
            lanController.SendPauseState(true);
        else
            Time.timeScale = 0f;

        ShowCurrentState();
    }

    private void ResumeLocalPause()
    {
        if (!localPaused)
            return;

        bool isNetworkGame = lanController != null && lanController.IsNetworkGameActive;
        localPaused = false;
        if (isNetworkGame)
            lanController.SendPauseState(false);
        else
            Time.timeScale = 1f;

        chessGame?.SetPauseLocked(opponentPaused);
        ShowCurrentState();
    }

    private void RestartLocalGame()
    {
        if (!chessGame || (lanController != null && lanController.IsNetworkGameActive))
            return;

        Time.timeScale = 1f;
        localPaused = false;
        opponentPaused = false;
        overlay.SetActive(false);
        chessGame.RestartCurrentLocalGame();
    }

    private void QuitGame()
    {
        if (!chessGame || quitting)
            return;

        quitting = true;
        resumeButton.interactable = false;
        restartButton.interactable = false;
        quitButton.interactable = false;

        if (lanController != null && lanController.IsNetworkGameActive)
        {
            statusText.text = "Surrendering...";
            statusText.gameObject.SetActive(true);
            lanController.QuitActiveMatch();
            return;
        }

        Time.timeScale = 1f;
        ResetPauseState();
        chessGame.RestartToMainMenu();
    }

    private void HandleOpponentPauseChanged(bool paused, string opponentName)
    {
        opponentPaused = paused;
        chessGame?.SetPauseLocked(localPaused || opponentPaused);
        ShowCurrentState(string.IsNullOrWhiteSpace(opponentName) ? "Opponent" : opponentName);
    }

    private void ShowCurrentState(string opponentName = "Opponent")
    {
        if (!overlay)
            return;

        bool visible = localPaused || opponentPaused;
        overlay.SetActive(visible);
        if (!visible)
            return;

        bool networkGame = lanController != null && lanController.IsNetworkGameActive;
        resumeButton.interactable = localPaused && !quitting;
        restartButton.interactable = !networkGame && localPaused && !quitting;
        quitButton.interactable = !quitting;

        if (opponentPaused && localPaused)
            statusText.text = $"{opponentName} is also paused";
        else if (opponentPaused)
            statusText.text = $"{opponentName} paused the game";
        else if (networkGame)
            statusText.text = "Waiting for both players to resume";
        else
            statusText.text = string.Empty;

        statusText.gameObject.SetActive(!string.IsNullOrEmpty(statusText.text));
    }

    private void ResetPauseState()
    {
        bool wasOfflinePause = localPaused && (lanController == null || !lanController.IsNetworkGameActive);
        localPaused = false;
        opponentPaused = false;
        quitting = false;
        if (wasOfflinePause)
            Time.timeScale = 1f;

        chessGame?.SetPauseLocked(false);
        if (overlay)
            overlay.SetActive(false);
    }

    private static Button CreateButton(Transform parent, string name, Texture2D texture, Rect crop,
        Vector2 position, Action onClick, bool interactive)
    {
        Image image = CreateImage(name, parent,
            CreateCroppedSprite(texture, crop.x, crop.y, crop.width, crop.height), Color.white);
        SetRect(image.rectTransform, position, new Vector2(590f, 155f));

        Button button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.interactable = interactive;
        if (onClick != null)
            button.onClick.AddListener(() => onClick());

        if (interactive)
        {
            HandDrawnPressable pressable = image.gameObject.AddComponent<HandDrawnPressable>();
            pressable.Configure(1.055f, 0.96f, 1.2f, new Color(1f, 0.96f, 0.84f, 1f));
        }

        return button;
    }

    private static Text CreateStatusText(Transform parent)
    {
        GameObject textObject = new GameObject("Pause Status", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 30;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.12f, 0.32f, 0.75f, 1f);
        text.raycastTarget = false;
        SetRect(text.rectTransform, new Vector2(0f, 280f), new Vector2(590f, 54f));
        return text;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        return image;
    }

    private static GameObject CreateRectObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static Sprite CreateCroppedSprite(Texture2D texture, float x, float y, float width, float height)
    {
        Rect rect = new Rect(
            Mathf.Round(texture.width * x),
            Mathf.Round(texture.height * y),
            Mathf.Round(texture.width * width),
            Mathf.Round(texture.height * height));
        rect.width = Mathf.Clamp(rect.width, 1f, texture.width - rect.x);
        rect.height = Mathf.Clamp(rect.height, 1f, texture.height - rect.y);
        int sourceX = Mathf.RoundToInt(rect.x);
        int sourceY = Mathf.RoundToInt(rect.y);
        int sourceWidth = Mathf.RoundToInt(rect.width);
        int sourceHeight = Mathf.RoundToInt(rect.height);
        Color32[] pixels = texture.GetPixels32();
        Color32[] croppedPixels = new Color32[sourceWidth * sourceHeight];
        for (int row = 0; row < sourceHeight; row++)
        {
            int sourceOffset = (sourceY + row) * texture.width + sourceX;
            Array.Copy(pixels, sourceOffset, croppedPixels, row * sourceWidth, sourceWidth);
        }

        RemoveConnectedCheckerboard(croppedPixels, sourceWidth, sourceHeight);
        Texture2D croppedTexture = new Texture2D(sourceWidth, sourceHeight, TextureFormat.RGBA32, false)
        {
            name = texture.name + " UI Crop",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        croppedTexture.SetPixels32(croppedPixels);
        croppedTexture.Apply(false, true);
        return Sprite.Create(croppedTexture, new Rect(0f, 0f, sourceWidth, sourceHeight), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void RemoveConnectedCheckerboard(Color32[] pixels, int width, int height)
    {
        int[] stack = new int[pixels.Length];
        int stackCount = 0;

        for (int x = 0; x < width; x++)
        {
            TryQueueOutsidePixel(x, pixels, stack, ref stackCount);
            TryQueueOutsidePixel((height - 1) * width + x, pixels, stack, ref stackCount);
        }

        for (int y = 1; y < height - 1; y++)
        {
            TryQueueOutsidePixel(y * width, pixels, stack, ref stackCount);
            TryQueueOutsidePixel(y * width + width - 1, pixels, stack, ref stackCount);
        }

        while (stackCount > 0)
        {
            int index = stack[--stackCount];
            int x = index % width;
            if (x > 0)
                TryQueueOutsidePixel(index - 1, pixels, stack, ref stackCount);
            if (x < width - 1)
                TryQueueOutsidePixel(index + 1, pixels, stack, ref stackCount);
            if (index >= width)
                TryQueueOutsidePixel(index - width, pixels, stack, ref stackCount);
            if (index < pixels.Length - width)
                TryQueueOutsidePixel(index + width, pixels, stack, ref stackCount);
        }
    }

    private static void TryQueueOutsidePixel(int index, Color32[] pixels, int[] stack, ref int stackCount)
    {
        Color32 pixel = pixels[index];
        if (pixel.a == 0)
            return;

        byte min = Math.Min(pixel.r, Math.Min(pixel.g, pixel.b));
        byte max = Math.Max(pixel.r, Math.Max(pixel.g, pixel.b));
        if (min < 220 || max - min > 12)
            return;

        pixel.a = 0;
        pixels[index] = pixel;
        stack[stackCount++] = index;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
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
        if (EventSystem.current)
            return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        EventSystem.current = eventSystem.GetComponent<EventSystem>();
    }
}
