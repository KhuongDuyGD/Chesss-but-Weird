using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class HandDrawnMenuView : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    private ChessTurnSelectionUI owner;
    private ChessGame chessGame;
    private HandDrawnMenuAssets assets;
    private Canvas canvas;
    private RectTransform mainScreen;
    private RectTransform modeScreen;
    private RectTransform sideScreen;

    public bool IsReady => assets != null && assets.HasRequiredSprites && canvas != null;

    public void Initialize(ChessTurnSelectionUI newOwner, ChessGame newChessGame, HandDrawnMenuAssets newAssets)
    {
        owner = newOwner;
        chessGame = newChessGame;
        assets = newAssets;

        if (!assets)
            return;

        EnsureEventSystem();
        BuildCanvas();
        BuildMainScreen();
        BuildModeScreen();
        BuildSideScreen();
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        SetVisible(true);
        SetScreen(mainScreen);
    }

    public void ShowModeSelection()
    {
        SetVisible(true);
        SetScreen(modeScreen);
    }

    public void ShowSideSelection()
    {
        SetVisible(true);
        SetScreen(sideScreen);
    }

    public void HideForPlaying()
    {
        SetVisible(false);
    }

    private void BuildCanvas()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform root = gameObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image background = CreateImage(root, "Paper Background", null, Vector2.zero, Vector2.zero);
        background.color = new Color(0.985f, 0.965f, 0.91f, 1f);
        Stretch(background.rectTransform);
    }

    private void BuildMainScreen()
    {
        mainScreen = CreateScreen("Main Menu");

        AddBattleDoodles(mainScreen, false);
        AddImage(mainScreen, "Mascot", assets.mascot, new Vector2(-830f, 410f), new Vector2(225f, 175f), 1.7f, 0.7f);
        AddImage(mainScreen, "Logo", assets.logo, new Vector2(0f, 300f), new Vector2(720f, 360f), 1.0f, 0.35f);
        AddInteractive(mainScreen, "Start", assets.startButton, new Vector2(0f, 40f), new Vector2(610f, 150f), () => chessGame.OpenTurnSelection(), true);
        AddInteractive(mainScreen, "Settings", assets.settingsButton, new Vector2(0f, -145f), new Vector2(610f, 138f), () => LogMenuClick("Settings"), true);
        AddInteractive(mainScreen, "Credits", assets.creditsButton, new Vector2(0f, -325f), new Vector2(610f, 150f), () => LogMenuClick("Credits"), true);

        AddImage(mainScreen, "Crown Doodle", assets.crown, new Vector2(-790f, -390f), new Vector2(210f, 160f), 0.8f, 0.35f);
        AddImage(mainScreen, "Hearts Left", assets.hearts, new Vector2(-475f, -305f), new Vector2(92f, 82f), 0.35f, 0.2f);
        AddImage(mainScreen, "Hearts Right", assets.hearts, new Vector2(475f, -305f), new Vector2(100f, 90f), 0.35f, 0.2f);
        AddImage(mainScreen, "Stars Left", assets.stars, new Vector2(-480f, -70f), new Vector2(86f, 58f), 0.3f, 0.18f);
        AddImage(mainScreen, "Stars Right", assets.stars, new Vector2(480f, -95f), new Vector2(86f, 58f), 0.3f, 0.18f);
        AddImage(mainScreen, "Early Access", assets.earlyAccess, new Vector2(700f, -405f), new Vector2(320f, 145f), 0f, 0f);
    }

    private void BuildModeScreen()
    {
        modeScreen = CreateScreen("Mode Select");
        BuildPlayHub(modeScreen);
    }

    private void BuildPlayHub(RectTransform screen)
    {
        AddBattleDoodles(screen, true);
        AddMenuConfetti(screen);

        AddImage(screen, "Mascot", assets.mascot, new Vector2(-820f, 420f), new Vector2(205f, 160f), 1.3f, 0.55f);
        AddImage(screen, "Slogan", assets.slogan, new Vector2(-350f, 425f), new Vector2(670f, 95f), 0.35f, 0.15f);
        AddImage(screen, "Logo", assets.logo, new Vector2(530f, 315f), new Vector2(520f, 270f), 0.8f, 0.25f);

        AddInteractive(screen, "Local", assets.localButton, new Vector2(-735f, 250f), new Vector2(380f, 116f), () => owner.ShowSideSelection(), true);
        AddInteractive(screen, "Online", assets.onlineButton, new Vector2(-735f, 75f), new Vector2(380f, 116f), () => LogMenuClick("Online"), true);
        AddInteractive(screen, "Aram", assets.aramButton, new Vector2(-735f, -100f), new Vector2(390f, 122f), () => LogMenuClick("ARAM"), true);
        AddInteractive(screen, "Shop", assets.shopButton, new Vector2(-735f, -280f), new Vector2(380f, 118f), () => LogMenuClick("Shop"), true);

        AddInteractive(screen, "Inventory", assets.inventoryIcon, new Vector2(800f, 125f), new Vector2(125f, 125f), () => LogMenuClick("Inventory"), false);
        AddInteractive(screen, "Gacha", assets.gachaIcon, new Vector2(800f, -55f), new Vector2(130f, 130f), () => LogMenuClick("Gacha"), false);
        AddInteractive(screen, "Player Profile", assets.playerProfileIcon, new Vector2(800f, -235f), new Vector2(132f, 132f), () => LogMenuClick("Player Profile"), false);
        AddInteractive(screen, "Settings Icon", assets.settingsIcon, new Vector2(500f, -395f), new Vector2(116f, 116f), () => LogMenuClick("Settings"), false);
        AddImage(screen, "Early Access", assets.earlyAccess, new Vector2(735f, -410f), new Vector2(310f, 138f), 0f, 0f);
    }

    private void BuildSideScreen()
    {
        sideScreen = CreateScreen("Choose Side");

        AddBattleDoodles(sideScreen, true);
        AddMenuConfetti(sideScreen);
        AddImage(sideScreen, "Choose Side Title", assets.chooseSideTitle, new Vector2(0f, 365f), new Vector2(880f, 150f), 0f, 0f);
        AddInteractive(sideScreen, "White Card", assets.whiteCard, new Vector2(-365f, -105f), new Vector2(560f, 745f), () => chessGame.BeginGame(PieceTeam.White), true);
        AddInteractive(sideScreen, "Black Card", assets.blackCard, new Vector2(365f, -105f), new Vector2(560f, 745f), () => chessGame.BeginGame(PieceTeam.Black), true);
        AddImage(sideScreen, "Settings Icon", assets.settingsIcon, new Vector2(-760f, -410f), new Vector2(120f, 120f), 0f, 0f);
        AddImage(sideScreen, "Early Access", assets.earlyAccess, new Vector2(735f, -420f), new Vector2(330f, 145f), 0f, 0f);
    }

    private RectTransform CreateScreen(string screenName)
    {
        GameObject screen = new GameObject(screenName, typeof(RectTransform));
        RectTransform rect = screen.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        Stretch(rect);
        return rect;
    }

    private Image AddImage(RectTransform parent, string imageName, Sprite sprite, Vector2 position, Vector2 size, float wigglePosition, float wiggleRotation)
    {
        Image image = CreateImage(parent, imageName, sprite, position, size);
        if (wigglePosition > 0.001f || wiggleRotation > 0.001f)
        {
            HandDrawnIdleWiggle wiggle = image.gameObject.AddComponent<HandDrawnIdleWiggle>();
            wiggle.Configure(wigglePosition, wiggleRotation, 0.01f, Random.Range(0.08f, 0.14f));
        }

        return image;
    }

    private void AddDoodle(RectTransform parent, Vector2 position, float size, float rotation, float alpha, bool animate)
    {
        if (!assets.punchDoodle)
            return;

        float positionWiggle = animate ? 1.4f : 0f;
        float rotationWiggle = animate ? 0.8f : 0f;
        Image image = AddImage(parent, "Punch Doodle", assets.punchDoodle, position, new Vector2(size * 1.55f, size), positionWiggle, rotationWiggle);
        image.color = new Color(1f, 1f, 1f, alpha);
        image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
    }

    private void AddBattleDoodles(RectTransform parent, bool dense)
    {
        AddDoodle(parent, new Vector2(-545f, 375f), 78f, -8f, 0.86f, true);
        AddDoodle(parent, new Vector2(-260f, 330f), 82f, 5f, 0.86f, false);
        AddDoodle(parent, new Vector2(35f, 345f), 84f, -4f, 0.84f, true);
        AddDoodle(parent, new Vector2(-500f, 145f), 88f, 4f, 0.88f, false);
        AddDoodle(parent, new Vector2(-230f, 95f), 92f, -5f, 0.88f, true);
        AddDoodle(parent, new Vector2(30f, 145f), 88f, 8f, 0.86f, false);
        AddDoodle(parent, new Vector2(330f, 85f), 86f, -6f, 0.82f, true);
        AddDoodle(parent, new Vector2(-445f, -75f), 90f, -4f, 0.86f, false);
        AddDoodle(parent, new Vector2(-140f, -150f), 92f, 5f, 0.88f, true);
        AddDoodle(parent, new Vector2(175f, -95f), 90f, -5f, 0.88f, false);
        AddDoodle(parent, new Vector2(450f, -170f), 86f, 6f, 0.84f, true);
        AddDoodle(parent, new Vector2(-285f, -320f), 82f, -6f, 0.82f, false);
        AddDoodle(parent, new Vector2(170f, -285f), 82f, 5f, 0.82f, false);

        if (!dense)
            return;

        AddDoodle(parent, new Vector2(-900f, -100f), 82f, 8f, 0.84f, false);
        AddDoodle(parent, new Vector2(-880f, -260f), 78f, -5f, 0.78f, true);
        AddDoodle(parent, new Vector2(600f, 25f), 82f, -5f, 0.78f, false);
        AddDoodle(parent, new Vector2(615f, -250f), 80f, 6f, 0.78f, false);
        AddDoodle(parent, new Vector2(895f, 55f), 78f, -8f, 0.78f, true);
    }

    private void AddMenuConfetti(RectTransform parent)
    {
        AddImage(parent, "Stars Logo", assets.stars, new Vector2(695f, 240f), new Vector2(76f, 50f), 0.35f, 0.18f);
        AddImage(parent, "Stars Mid Left", assets.stars, new Vector2(-300f, 170f), new Vector2(78f, 52f), 0.35f, 0.18f);
        AddImage(parent, "Stars Center", assets.stars, new Vector2(-20f, 120f), new Vector2(82f, 56f), 0.35f, 0.18f);
        AddImage(parent, "Stars Lower Right", assets.stars, new Vector2(300f, -390f), new Vector2(64f, 42f), 0.35f, 0.18f);
        AddImage(parent, "Hearts Button Left", assets.hearts, new Vector2(-910f, -110f), new Vector2(78f, 68f), 0.35f, 0.18f);
        AddImage(parent, "Hearts Button Right", assets.hearts, new Vector2(-535f, -110f), new Vector2(78f, 68f), 0.35f, 0.18f);
        AddImage(parent, "Hearts Logo", assets.hearts, new Vector2(650f, 200f), new Vector2(78f, 70f), 0.35f, 0.18f);
        AddImage(parent, "Crown Doodle", assets.crown, new Vector2(45f, -350f), new Vector2(100f, 78f), 0.3f, 0.15f);
    }

    private Button AddInteractive(RectTransform parent, string buttonName, Sprite sprite, Vector2 position, Vector2 size, UnityAction action, bool addIdleWiggle)
    {
        Image image = CreateImage(parent, buttonName, sprite, position, size);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        image.gameObject.AddComponent<HandDrawnPressable>();

        if (addIdleWiggle)
        {
            HandDrawnIdleWiggle wiggle = image.gameObject.AddComponent<HandDrawnIdleWiggle>();
            wiggle.Configure(1.1f, 0.45f, 0.006f, 0.12f);
        }

        return button;
    }

    private Image CreateImage(Transform parent, string imageName, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject imageObject = new GameObject(imageName, typeof(RectTransform), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private void SetScreen(RectTransform activeScreen)
    {
        if (!mainScreen || !modeScreen || !sideScreen || !activeScreen)
            return;

        mainScreen.gameObject.SetActive(activeScreen == mainScreen);
        modeScreen.gameObject.SetActive(activeScreen == modeScreen);
        sideScreen.gameObject.SetActive(activeScreen == sideScreen);
    }

    private void SetVisible(bool visible)
    {
        if (canvas)
            canvas.enabled = visible;
    }

    private void LogMenuClick(string label)
    {
        Debug.Log($"[HandDrawnMenu] {label} clicked.");
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
        EventSystem eventSystem = EventSystem.current;
        if (!eventSystem)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        StandaloneInputModule legacyInputModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacyInputModule)
            legacyInputModule.enabled = false;

        if (!eventSystem.GetComponent<InputSystemUIInputModule>())
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }
}
