using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AnalysisBoardView : MonoBehaviour
{
    private const float SourceWidth = 1122f;
    private const float SourceHeight = 1402f;
    private const float PanelWidth = 576f;
    private static AnalysisBoardView activeInstance;

    private ChessGame chessGame;
    private GameObject canvasObject;
    private Canvas canvas;
    private GraphicRaycaster raycaster;
    private RectTransform panel;
    private RectTransform whiteCapturedContainer;
    private RectTransform blackCapturedContainer;
    private Text whiteScoreText;
    private Text blackScoreText;
    private Text timeText;
    private Text moveListText;
    private ScrollRect moveListScroll;
    private Text toggleLabel;
    private GameObject whiteTurnIndicator;
    private GameObject blackTurnIndicator;
    private Material transparentWhiteMaterial;
    private float nextRefreshAt;
    private int lastMoveCount = -1;
    private int lastWhiteCaptureCount = -1;
    private int lastBlackCaptureCount = -1;
    private static Font runtimeFont;
    private Camera gameplayCamera;
    private Camera backgroundCamera;
    private Rect originalCameraRect;
    private bool cameraRectCaptured;
    private Vector2 originalLensShift;
    private float originalFieldOfView;
    private bool isVisible;
    private bool isExpanded = true;
    private PieceTeam lastDisplayedTurn;
    private bool hasDisplayedTurn;
    private bool ownsCanvas;
    private Vector2Int lastScreenSize = new Vector2Int(-1, -1);
    private bool panelPreferenceSet;

    public void Initialize(ChessGame game)
    {
        if (activeInstance && activeInstance != this)
            activeInstance.ReleaseOwnership();
        activeInstance = this;
        ownsCanvas = true;
        chessGame = game;
        if (canvasObject)
            return;
        Build();
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        if (!ownsCanvas || activeInstance != this || !canvasObject)
            return;
        isVisible = visible;
        if (visible && !panelPreferenceSet)
            isExpanded = IsWideEnoughForSidePanel();
        canvasObject.SetActive(visible);
        ApplyExpansionState();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        if (raycaster)
            raycaster.enabled = enabled;
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;
        if (gameplayCamera && cameraRectCaptured)
            RestoreCameraPresentation();
        if (transparentWhiteMaterial)
            Destroy(transparentWhiteMaterial);
        if (canvasObject)
            Destroy(canvasObject);
        if (backgroundCamera)
            Destroy(backgroundCamera.gameObject);
    }

    private void ReleaseOwnership()
    {
        ownsCanvas = false;
        if (gameplayCamera && cameraRectCaptured)
            RestoreCameraPresentation();
        if (canvasObject)
        {
            canvasObject.SetActive(false);
            Destroy(canvasObject);
            canvasObject = null;
        }
        enabled = false;
    }

    private void Update()
    {
        if (!canvasObject || !canvasObject.activeInHierarchy || chessGame == null)
            return;

        if (lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
        {
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (!panelPreferenceSet)
                isExpanded = IsWideEnoughForSidePanel();
            ApplyExpansionState();
        }

        if (Time.unscaledTime < nextRefreshAt)
            return;

        nextRefreshAt = Time.unscaledTime + 0.1f;
        RefreshDynamicContent();
    }

    private void Build()
    {
        AnalysisBoardAssetCatalog assets = AnalysisBoardAssetCatalog.Load();
        if (!assets || !assets.board || !assets.moveList)
        {
            Debug.LogWarning("[AnalysisBoard] Missing Resources/GameplayUI/AnalysisBoardAssets. Run Tools/Chess/Rebuild Analysis Board Assets.");
            return;
        }

        EnsureEventSystem();
        RemoveOrphanedCanvases();
        canvasObject = new GameObject("Analysis Board Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        // Chess Game has no Canvas, so this remains an independent root Canvas while its
        // lifetime is tied to the gameplay scene instead of becoming an orphan object.
        canvasObject.transform.SetParent(chessGame.transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        raycaster = canvasObject.GetComponent<GraphicRaycaster>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 18;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        ResponsiveUi.ConfigureCanvasScaler(scaler, new Vector2(ResponsiveUi.ReferenceWidth, ResponsiveUi.ReferenceHeight));

        // Fixed virtual width keeps the panel readable without letting it become
        // oversized on ultrawide displays; it still resolves to 30% at 16:9.
        panel = CreateRect(canvasObject.transform, "Analysis Board", new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(PanelWidth, 0f), Vector2.zero);
        RawImage boardImage = panel.gameObject.AddComponent<RawImage>();
        boardImage.texture = assets.board;
        boardImage.raycastTarget = false;

        CreatePanelToggle();

        Text whiteIndicator = CreateText("White Turn Indicator", 280f, 342f, 110f, 55f, 42, TextAnchor.MiddleCenter);
        whiteIndicator.text = "▼";
        whiteIndicator.color = new Color(0.10f, 0.48f, 1f, 1f);
        whiteTurnIndicator = whiteIndicator.gameObject;
        Text blackIndicator = CreateText("Black Turn Indicator", 842f, 342f, 110f, 55f, 42, TextAnchor.MiddleCenter);
        blackIndicator.text = "▼";
        blackIndicator.color = new Color(1f, 0.23f, 0.62f, 1f);
        blackTurnIndicator = blackIndicator.gameObject;

        whiteCapturedContainer = CreateCapturedContainer("White Captured", 280f, 503f, 330f, 110f);
        blackCapturedContainer = CreateCapturedContainer("Black Captured", 842f, 503f, 330f, 110f);
        whiteScoreText = CreateText("White Score", 280f, 678f, 250f, 70f, 42, TextAnchor.MiddleCenter);
        blackScoreText = CreateText("Black Score", 842f, 678f, 250f, 70f, 42, TextAnchor.MiddleCenter);
        timeText = CreateText("Elapsed Time", 690f, 790f, 360f, 70f, 40, TextAnchor.MiddleLeft);

        RawImage moveList = CreateRawImage("Move List Background", assets.moveList, 561f, 1080f, 880f, 350f, false);
        moveList.uvRect = new Rect(0.04f, 0.235f, 0.92f, 0.51f);
        CreateScrollableMoveList();

        if (assets.transparentWhiteShader)
            transparentWhiteMaterial = new Material(assets.transparentWhiteShader) { name = "Analysis Button Transparency (Runtime)" };

        CreateLockedButton("Replay (Locked)", assets.replayButton, 278f, 1313f, 220f, 155f);
        CreateLockedButton("Report (Locked)", assets.reportButton, 560f, 1313f, 220f, 155f);
        CreateLockedButton("Share (Locked)", assets.shareButton, 844f, 1313f, 220f, 155f);

        RefreshDynamicContent();
    }

    private void RefreshDynamicContent()
    {
        IReadOnlyList<PieceType> whiteCaptured = chessGame.GetCapturedPieces(PieceTeam.White);
        IReadOnlyList<PieceType> blackCaptured = chessGame.GetCapturedPieces(PieceTeam.Black);
        timeText.text = FormatTime(chessGame.MatchElapsedSeconds);

        if (whiteCaptured.Count != lastWhiteCaptureCount || blackCaptured.Count != lastBlackCaptureCount)
        {
            lastWhiteCaptureCount = whiteCaptured.Count;
            lastBlackCaptureCount = blackCaptured.Count;
            RefreshCapturedContainer(whiteCapturedContainer, whiteCaptured, PieceTeam.Black);
            RefreshCapturedContainer(blackCapturedContainer, blackCaptured, PieceTeam.White);

            int balance = chessGame.GetCapturedMaterialScore(PieceTeam.White) - chessGame.GetCapturedMaterialScore(PieceTeam.Black);
            whiteScoreText.text = FormatSigned(balance);
            blackScoreText.text = FormatSigned(-balance);
        }

        if (chessGame.MoveHistory.Count != lastMoveCount)
        {
            lastMoveCount = chessGame.MoveHistory.Count;
            moveListText.text = BuildMoveRows(chessGame.MoveHistory, chessGame.MoveHistoryFirstTurn);
            Canvas.ForceUpdateCanvases();
            moveListScroll.verticalNormalizedPosition = 0f;
        }

        if (!hasDisplayedTurn || lastDisplayedTurn != chessGame.CurrentTurn)
        {
            hasDisplayedTurn = true;
            lastDisplayedTurn = chessGame.CurrentTurn;
            whiteTurnIndicator.SetActive(lastDisplayedTurn == PieceTeam.White);
            blackTurnIndicator.SetActive(lastDisplayedTurn == PieceTeam.Black);
        }
    }

    private void CreatePanelToggle()
    {
        RectTransform toggleRect = CreateRect(canvasObject.transform, "Analysis Board Toggle", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(58f, 116f), new Vector2(-PanelWidth - 10f, 0f));
        Image background = toggleRect.gameObject.AddComponent<Image>();
        background.color = new Color(0.97f, 0.97f, 0.95f, 0.98f);
        Outline outline = toggleRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.04f, 0.04f, 0.05f, 0.95f);
        outline.effectDistance = new Vector2(3f, -3f);

        Button button = toggleRect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.72f, 0.88f, 1f, 1f);
        colors.pressedColor = new Color(0.52f, 0.76f, 1f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        button.onClick.AddListener(ToggleExpanded);

        RectTransform labelRect = CreateRect(toggleRect, "Arrow", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        toggleLabel = labelRect.gameObject.AddComponent<Text>();
        toggleLabel.font = GetRuntimeFont();
        toggleLabel.fontSize = 48;
        toggleLabel.resizeTextForBestFit = true;
        toggleLabel.resizeTextMinSize = 24;
        toggleLabel.resizeTextMaxSize = 48;
        toggleLabel.alignment = TextAnchor.MiddleCenter;
        toggleLabel.color = new Color(0.04f, 0.04f, 0.05f, 1f);
        toggleLabel.raycastTarget = false;
    }

    private void CreateScrollableMoveList()
    {
        RectTransform scrollRect = CreateSourceRect("Move List Scroll", 630f, 1105f, 590f, 190f);
        moveListScroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        moveListScroll.horizontal = false;
        moveListScroll.vertical = true;
        moveListScroll.movementType = ScrollRect.MovementType.Clamped;
        moveListScroll.scrollSensitivity = 28f;

        RectTransform viewport = CreateRect(scrollRect, "Viewport", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-18f, 0f), new Vector2(-9f, 0f));
        Image viewportGraphic = viewport.gameObject.AddComponent<Image>();
        viewportGraphic.color = new Color(1f, 1f, 1f, 0.001f);
        viewport.gameObject.AddComponent<RectMask2D>();
        moveListScroll.viewport = viewport;

        RectTransform content = CreateRect(viewport, "Content", new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, 1f), Vector2.zero);
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        moveListText = content.gameObject.AddComponent<Text>();
        moveListText.font = GetRuntimeFont();
        moveListText.fontSize = 24;
        moveListText.resizeTextForBestFit = false;
        moveListText.alignment = TextAnchor.UpperLeft;
        moveListText.color = new Color(0.055f, 0.055f, 0.065f, 1f);
        moveListText.horizontalOverflow = HorizontalWrapMode.Wrap;
        moveListText.verticalOverflow = VerticalWrapMode.Overflow;
        moveListText.lineSpacing = 1.06f;
        moveListText.raycastTarget = false;
        moveListScroll.content = content;

        RectTransform scrollbarRect = CreateRect(scrollRect, "Scrollbar", new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(13f, 0f), Vector2.zero);
        Image scrollbarBackground = scrollbarRect.gameObject.AddComponent<Image>();
        scrollbarBackground.color = new Color(0.08f, 0.08f, 0.09f, 0.14f);
        Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        RectTransform slidingArea = CreateRect(scrollbarRect, "Sliding Area", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-4f, -4f), Vector2.zero);
        RectTransform handle = CreateRect(slidingArea, "Handle", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.10f, 0.48f, 1f, 0.72f);
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        moveListScroll.verticalScrollbar = scrollbar;
        moveListScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        moveListScroll.verticalScrollbarSpacing = 3f;
    }

    private void ToggleExpanded()
    {
        panelPreferenceSet = true;
        isExpanded = !isExpanded;
        ApplyExpansionState();
    }

    private static bool IsWideEnoughForSidePanel()
    {
        return Screen.height <= 0 || Screen.width / (float)Screen.height >= 1.25f;
    }

    private void ApplyExpansionState()
    {
        if (!canvasObject || !panel)
            return;

        panel.gameObject.SetActive(isExpanded);
        RectTransform toggleRect = canvasObject.transform.Find("Analysis Board Toggle") as RectTransform;
        if (toggleRect)
            toggleRect.anchoredPosition = new Vector2(isExpanded ? -PanelWidth - 10f : -8f, 0f);
        if (toggleLabel)
            toggleLabel.text = isExpanded ? "›" : "‹";

        if (!gameplayCamera)
            gameplayCamera = Camera.main;
        if (!gameplayCamera)
            return;
        if (!cameraRectCaptured)
        {
            originalCameraRect = gameplayCamera.rect;
            originalLensShift = gameplayCamera.lensShift;
            originalFieldOfView = gameplayCamera.fieldOfView;
            cameraRectCaptured = true;
        }

        if (isVisible && isExpanded)
        {
            EnsureBackgroundCamera();
            backgroundCamera.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            gameplayCamera.rect = new Rect(0f, 0f, GetGameplayViewportWidth(), 1f);
            gameplayCamera.lensShift = originalLensShift;
            // Changing the viewport width must not change the perceived board
            // distance. Keep the gameplay camera's original vertical FOV.
            gameplayCamera.fieldOfView = originalFieldOfView;
        }
        else
        {
            RestoreCameraPresentation();
        }
    }

    private float GetGameplayViewportWidth()
    {
        if (!canvas || Screen.width <= 0)
            return 0.70f;

        float reservedScreenWidth = panel.rect.width * canvas.scaleFactor;
        float reservedFraction = Mathf.Clamp(reservedScreenWidth / Screen.width, 0.18f, 0.38f);
        return 1f - reservedFraction;
    }

    private void RestoreCameraPresentation()
    {
        gameplayCamera.rect = originalCameraRect;
        gameplayCamera.lensShift = originalLensShift;
        gameplayCamera.fieldOfView = originalFieldOfView;
        if (backgroundCamera)
            backgroundCamera.gameObject.SetActive(false);
    }

    private void EnsureBackgroundCamera()
    {
        if (backgroundCamera)
            return;

        GameObject cameraObject = new GameObject("Analysis Board Sky Background Camera", typeof(Camera));
        cameraObject.transform.SetParent(gameplayCamera.transform, false);
        backgroundCamera = cameraObject.GetComponent<Camera>();
        backgroundCamera.clearFlags = gameplayCamera.clearFlags;
        backgroundCamera.backgroundColor = gameplayCamera.backgroundColor;
        backgroundCamera.cullingMask = 0;
        backgroundCamera.depth = gameplayCamera.depth - 1f;
        backgroundCamera.rect = originalCameraRect;
        backgroundCamera.fieldOfView = originalFieldOfView;
        backgroundCamera.nearClipPlane = gameplayCamera.nearClipPlane;
        backgroundCamera.farClipPlane = gameplayCamera.farClipPlane;
        backgroundCamera.allowHDR = gameplayCamera.allowHDR;
        backgroundCamera.allowMSAA = gameplayCamera.allowMSAA;
    }

    private RectTransform CreateCapturedContainer(string name, float sourceX, float sourceY, float sourceWidth, float sourceHeight)
    {
        RectTransform container = CreateSourceRect(name, sourceX, sourceY, sourceWidth, sourceHeight);
        HorizontalLayoutGroup layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 3f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return container;
    }

    private Text CreateText(string name, float sourceX, float sourceY, float sourceWidth, float sourceHeight, int fontSize, TextAnchor alignment)
    {
        RectTransform rect = CreateSourceRect(name, sourceX, sourceY, sourceWidth, sourceHeight);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = GetRuntimeFont();
        text.fontSize = fontSize;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.055f, 0.055f, 0.065f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private RawImage CreateRawImage(string name, Texture texture, float sourceX, float sourceY, float sourceWidth, float sourceHeight, bool raycastTarget)
    {
        RectTransform rect = CreateSourceRect(name, sourceX, sourceY, sourceWidth, sourceHeight);
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private void CreateLockedButton(string name, Texture texture, float sourceX, float sourceY, float sourceWidth, float sourceHeight)
    {
        RawImage image = CreateRawImage(name, texture, sourceX, sourceY, sourceWidth, sourceHeight, true);
        image.uvRect = new Rect(0.10f, 0.10f, 0.80f, 0.80f);
        if (transparentWhiteMaterial)
            image.material = transparentWhiteMaterial;

        GameObject tooltip = CreateTooltip(image.rectTransform, "Coming soon");
        AnalysisBoardLockedButton hover = image.gameObject.AddComponent<AnalysisBoardLockedButton>();
        hover.Initialize(tooltip);
    }

    private GameObject CreateTooltip(RectTransform parent, string label)
    {
        RectTransform tooltip = CreateRect(parent, "Locked Tooltip", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(130f, 32f), new Vector2(0f, 8f));
        Image background = tooltip.gameObject.AddComponent<Image>();
        background.color = new Color(0.08f, 0.08f, 0.09f, 0.92f);
        background.raycastTarget = false;

        RectTransform textRect = CreateRect(tooltip, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Text text = textRect.gameObject.AddComponent<Text>();
        text.font = GetRuntimeFont();
        text.text = label;
        text.fontSize = 17;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        tooltip.gameObject.SetActive(false);
        return tooltip.gameObject;
    }

    private RectTransform CreateSourceRect(string name, float x, float y, float width, float height)
    {
        float left = Mathf.Clamp01((x - width * 0.5f) / SourceWidth);
        float right = Mathf.Clamp01((x + width * 0.5f) / SourceWidth);
        float bottom = Mathf.Clamp01(1f - (y + height * 0.5f) / SourceHeight);
        float top = Mathf.Clamp01(1f - (y - height * 0.5f) / SourceHeight);
        return CreateRect(panel, name, new Vector2(left, bottom), new Vector2(right, top), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static Font GetRuntimeFont()
    {
        if (!runtimeFont)
            runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe Print", "Comic Sans MS", "Arial" }, 32);
        if (!runtimeFont)
            runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return runtimeFont;
    }

    private static void RefreshCapturedContainer(RectTransform container, IReadOnlyList<PieceType> pieces, PieceTeam capturedTeam)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            GameObject oldGroup = container.GetChild(i).gameObject;
            oldGroup.SetActive(false);
            Destroy(oldGroup);
        }

        PieceType[] displayOrder =
        {
            PieceType.Pawn,
            PieceType.Knight,
            PieceType.Bishop,
            PieceType.Rook,
            PieceType.Queen
        };

        int visibleGroups = 0;
        for (int typeIndex = 0; typeIndex < displayOrder.Length; typeIndex++)
        {
            PieceType type = displayOrder[typeIndex];
            int count = 0;
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i] == type)
                    count++;

            if (count == 0)
                continue;
            visibleGroups++;
            CreateCapturedGroup(container, GetPieceGlyph(type, capturedTeam), count);
        }

        if (visibleGroups == 0)
            CreateCapturedGroup(container, '—', 1);
    }

    private static void CreateCapturedGroup(RectTransform parent, char glyph, int count)
    {
        RectTransform group = CreateRect(parent, $"Captured {glyph}", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        VerticalLayoutGroup layout = group.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = -3f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Text icon = CreateLayoutText(group, glyph.ToString(), 26, 31f);
        Text multiplier = CreateLayoutText(group, count > 1 ? $"x{count}" : " ", 15, 19f);
        icon.fontStyle = FontStyle.Normal;
        multiplier.fontStyle = FontStyle.Bold;
    }

    private static Text CreateLayoutText(RectTransform parent, string value, int fontSize, float preferredHeight)
    {
        RectTransform rect = CreateRect(parent, "Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = preferredHeight;
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = GetRuntimeFont();
        text.text = value;
        text.fontSize = fontSize;
        text.resizeTextForBestFit = false;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.055f, 0.055f, 0.065f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private static char GetPieceGlyph(PieceType type, PieceTeam team)
    {
        const string white = "♔♕♖♗♘♙";
        const string black = "♚♛♜♝♞♟";
        int index = type == PieceType.King ? 0 : type == PieceType.Queen ? 1 : type == PieceType.Rook ? 2 : type == PieceType.Bishop ? 3 : type == PieceType.Knight ? 4 : 5;
        return (team == PieceTeam.White ? white : black)[index];
    }

    private static string FormatSigned(int value)
    {
        return value > 0 ? $"+{value}" : value.ToString();
    }

    private static string FormatTime(float totalSeconds)
    {
        int seconds = Mathf.Max(0, Mathf.FloorToInt(totalSeconds));
        int hours = seconds / 3600;
        int minutes = (seconds % 3600) / 60;
        return hours > 0 ? $"{hours:00}:{minutes:00}:{seconds % 60:00}" : $"{minutes:00}:{seconds % 60:00}";
    }

    private static string BuildMoveRows(IReadOnlyList<string> moves, PieceTeam firstTurn)
    {
        if (moves.Count == 0)
            return "No moves yet";

        bool blackStarted = firstTurn == PieceTeam.Black;
        int totalRows = blackStarted ? (moves.Count + 2) / 2 : (moves.Count + 1) / 2;
        StringBuilder builder = new StringBuilder(180);
        for (int row = 0; row < totalRows; row++)
        {
            int whiteIndex = blackStarted ? row * 2 - 1 : row * 2;
            int blackIndex = blackStarted ? row * 2 : row * 2 + 1;
            string whiteMove = whiteIndex >= 0 && whiteIndex < moves.Count ? TrimMove(moves[whiteIndex]) : string.Empty;
            string blackMove = blackIndex >= 0 && blackIndex < moves.Count ? TrimMove(moves[blackIndex]) : string.Empty;
            builder.Append(row + 1).Append(".  ").Append(whiteMove.PadRight(14)).Append(blackMove);
            if (row < totalRows - 1)
                builder.AppendLine();
        }
        return builder.ToString();
    }

    private void RemoveOrphanedCanvases()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas candidate = canvases[i];
            if (!candidate || candidate.gameObject.name != "Analysis Board Canvas")
                continue;
            candidate.gameObject.SetActive(false);
            Destroy(candidate.gameObject);
        }
    }

    private static string TrimMove(string move)
    {
        if (string.IsNullOrWhiteSpace(move))
            return string.Empty;
        string trimmed = move.Trim();
        return trimmed.Length <= 13 ? trimmed : trimmed.Substring(0, 12) + "…";
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (!eventSystem)
            eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

        StandaloneInputModule legacyInput = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacyInput)
            legacyInput.enabled = false;
        if (!eventSystem.GetComponent<InputSystemUIInputModule>())
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }
}
