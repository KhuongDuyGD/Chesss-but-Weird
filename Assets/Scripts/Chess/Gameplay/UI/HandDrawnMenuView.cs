using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.Video;

public class HandDrawnMenuView : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    private ChessTurnSelectionUI owner;
    private ChessGame chessGame;
    private HandDrawnMenuAssets assets;
    private Canvas canvas;
    private GraphicRaycaster raycaster;
    private RectTransform mainScreen;
    private RectTransform modeScreen;
    private RectTransform multiplayerModeScreen;
    private RectTransform gachaScreen;
    private RectTransform botDifficultyScreen;
    private RectTransform sideScreen;
    private GachaMenuController gachaController;
    private readonly List<Sprite> runtimeSprites = new List<Sprite>();

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
        BuildMultiplayerModeScreen();
        BuildGachaScreen();
        BuildBotDifficultyScreen();
        BuildSideScreen();
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        SetVisible(true);
        SetInputEnabled(true);
        SetScreen(mainScreen);
    }

    public void ShowModeSelection()
    {
        SetVisible(true);
        SetInputEnabled(true);
        SetScreen(modeScreen);
    }

    public void ShowSideSelection()
    {
        SetVisible(true);
        SetInputEnabled(true);
        SetScreen(sideScreen);
    }

    public void ShowBotDifficultySelection()
    {
        SetVisible(true);
        SetInputEnabled(true);
        SetScreen(botDifficultyScreen);
    }

    public void ShowMultiplayerModeSelection()
    {
        if (!assets || !assets.HasMultiplayerModeSprites)
        {
            ShowSideSelection();
            return;
        }

        SetVisible(true);
        SetInputEnabled(true);
        SetScreen(multiplayerModeScreen);
    }

    public void ShowGachaMenu()
    {
        SetVisible(true);
        SetInputEnabled(true);
        gachaController?.OpenMainScreen();
        SetScreen(gachaScreen);
    }

    public void HideForPlaying()
    {
        SetVisible(false);
        SetInputEnabled(false);
        SetAllScreensActive(false);
    }

    public void SetInputEnabled(bool enabled)
    {
        if (raycaster)
            raycaster.enabled = enabled;
    }

    private void BuildCanvas()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        ResponsiveUi.ConfigureCanvasScaler(scaler, new Vector2(ReferenceWidth, ReferenceHeight));

        raycaster = gameObject.AddComponent<GraphicRaycaster>();

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
        AddImage(mainScreen, "Doodle Face", assets.doodleFaceDecoration, new Vector2(-835f, 425f), new Vector2(225f, 178f), 1.2f, 0.45f);
        AddImage(mainScreen, "Game Logo", assets.gameLogo, new Vector2(0f, 295f), new Vector2(760f, 420f), 0.6f, 0.2f);
        AddInteractive(mainScreen, "Start", assets.startButton, new Vector2(0f, 42f), new Vector2(660f, 158f), () => chessGame.OpenTurnSelection(), false, 1.035f);
        AddInteractive(mainScreen, "Settings", assets.settingsButton, new Vector2(0f, -142f), new Vector2(660f, 148f), () => LogMenuClick("Settings"), false, 1.035f);
        AddInteractive(mainScreen, "Credits", assets.creditsButton, new Vector2(0f, -326f), new Vector2(660f, 158f), () => LogMenuClick("Credits"), false, 1.035f);

        AddImage(mainScreen, "Crown Doodle", assets.backgroundDecoration4, new Vector2(-735f, -330f), new Vector2(205f, 158f), 0.45f, 0.2f);
        AddImage(mainScreen, "Hearts Left", assets.backgroundDecoration2, new Vector2(-475f, -305f), new Vector2(92f, 82f), 0.25f, 0.1f);
        AddImage(mainScreen, "Hearts Right", assets.backgroundDecoration2, new Vector2(475f, -305f), new Vector2(100f, 90f), 0.25f, 0.1f);
        AddImage(mainScreen, "Stars Left", assets.backgroundDecoration3, new Vector2(-480f, -70f), new Vector2(86f, 58f), 0.2f, 0.08f);
        AddImage(mainScreen, "Stars Right", assets.backgroundDecoration3, new Vector2(480f, -95f), new Vector2(86f, 58f), 0.2f, 0.08f);
        AddImage(mainScreen, "Game Version", assets.gameVersion, new Vector2(735f, -410f), new Vector2(335f, 150f), 0f, 0f);
        AddLogoutButton(mainScreen);
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

        AddImage(screen, "Doodle Face", assets.doodleFaceDecoration, new Vector2(-840f, 430f), new Vector2(205f, 160f), 0.8f, 0.3f);
        AddImage(screen, "Game Slogan", assets.gameSlogan, new Vector2(-240f, 438f), new Vector2(755f, 98f), 0.2f, 0.08f);
        AddImage(screen, "Game Logo", assets.gameLogo, new Vector2(515f, 320f), new Vector2(540f, 285f), 0.4f, 0.12f);

        AddInteractive(screen, "Local Gameplay", assets.localGameplayButton, new Vector2(-660f, 270f), new Vector2(475f, 132f), () => owner.ShowBotDifficultySelection(), false, 1.035f);
        AddInteractive(screen, "Online Play", assets.onlinePlayButton, new Vector2(-660f, 95f), new Vector2(475f, 132f), () => owner.ShowMultiplayerModeSelection(), false, 1.035f);
        AddInteractive(screen, "ARAM Mode", assets.aramModeButton, new Vector2(-660f, -80f), new Vector2(475f, 132f), CreateAuthenticatedAction("ARAM", () => LogMenuClick("ARAM")), false, 1.035f);
        AddInteractive(screen, "Shop", assets.shopButton, new Vector2(-660f, -255f), new Vector2(475f, 132f), CreateAuthenticatedAction("Shop", () => LogMenuClick("Shop")), false, 1.035f);

        AddInteractive(screen, "Inventory", assets.inventoryButton, new Vector2(805f, 130f), new Vector2(132f, 132f), CreateAuthenticatedAction("Inventory", () => LogMenuClick("Inventory")), false, 1.045f);
        AddInteractive(screen, "Gacha", assets.gachaButton, new Vector2(805f, -50f), new Vector2(132f, 132f), CreateAuthenticatedAction("Gacha", ShowGachaMenu), false, 1.045f);
        AddInteractive(screen, "Player Profile", assets.playerProfile, new Vector2(805f, -230f), new Vector2(136f, 136f), CreateAuthenticatedAction("Player Profile", () => LogMenuClick("Player Profile")), false, 1.045f);
        AddInteractive(screen, "Settings Icon", assets.settingsIcon, new Vector2(500f, -398f), new Vector2(118f, 118f), CreateAuthenticatedAction("Settings", () => LogMenuClick("Settings")), false, 1.045f);
        AddImage(screen, "Game Version", assets.gameVersion, new Vector2(735f, -415f), new Vector2(320f, 142f), 0f, 0f);
        AddLogoutButton(screen);
    }

    private void BuildMultiplayerModeScreen()
    {
        multiplayerModeScreen = CreateScreen("Multiplayer Mode");

        if (!assets.HasMultiplayerModeSprites)
            return;

        AddImage(multiplayerModeScreen, "Mode Title", assets.chooseMultiplayerMode, new Vector2(0f, 380f), new Vector2(1700f, 220f), 0f, 0f);
        AddInteractive(
            multiplayerModeScreen,
            "LAN Card",
            assets.lanButton,
            new Vector2(-365f, -105f),
            new Vector2(590f, 785f),
            () => SelectMultiplayerMode("LAN"),
            true,
            1.05f,
            new Color(0.83f, 0.95f, 1f, 1f),
            0.965f,
            1.25f);
        AddInteractive(
            multiplayerModeScreen,
            "Online Card",
            assets.multiplayerButton,
            new Vector2(365f, -105f),
            new Vector2(590f, 785f),
            () => SelectMultiplayerMode("Multiplayer Online"),
            true,
            1.05f,
            new Color(1f, 0.96f, 0.72f, 1f),
            0.965f,
            1.25f);

        AddImage(multiplayerModeScreen, "Stars Left", assets.backgroundDecoration3, new Vector2(-615f, 305f), new Vector2(92f, 62f), 0.2f, 0.08f);
        AddImage(multiplayerModeScreen, "Stars Right", assets.backgroundDecoration3, new Vector2(625f, 292f), new Vector2(92f, 62f), 0.2f, 0.08f);
        AddImage(multiplayerModeScreen, "Hearts Left", assets.backgroundDecoration2, new Vector2(-765f, -338f), new Vector2(92f, 80f), 0.2f, 0.08f);
        AddImage(multiplayerModeScreen, "Hearts Right", assets.backgroundDecoration2, new Vector2(770f, -338f), new Vector2(92f, 80f), 0.2f, 0.08f);
        AddBackButton(multiplayerModeScreen, new Vector2(-820f, -420f), () => ShowModeSelection());
    }

    private void BuildGachaScreen()
    {
        gachaScreen = CreateScreen("Gacha Menu");
        gachaController = gachaScreen.gameObject.AddComponent<GachaMenuController>();
        gachaController.Initialize(gachaScreen, ShowModeSelection);
    }

    private void BuildSideScreen()
    {
        sideScreen = CreateScreen("Choose Side");

        AddBattleDoodles(sideScreen, true);
        AddMenuConfetti(sideScreen);
        AddImage(sideScreen, "Choose Side Title", assets.chooseYourSide, new Vector2(0f, 365f), new Vector2(880f, 150f), 0f, 0f);
        AddInteractive(sideScreen, "White Card", assets.whiteSideButton, new Vector2(-365f, -105f), new Vector2(560f, 745f), () => owner.StartBotGame(PieceTeam.White), false, 1.035f);
        AddInteractive(sideScreen, "Black Card", assets.blackSideButton, new Vector2(365f, -105f), new Vector2(560f, 745f), () => owner.StartBotGame(PieceTeam.Black), false, 1.035f);
        AddImage(sideScreen, "Settings Icon", assets.settingsIcon, new Vector2(-760f, -410f), new Vector2(120f, 120f), 0f, 0f);
        AddImage(sideScreen, "Game Version", assets.gameVersion, new Vector2(735f, -420f), new Vector2(330f, 145f), 0f, 0f);
        AddBackButton(sideScreen, new Vector2(-600f, -420f), () => ShowBotDifficultySelection());
    }

    private void BuildBotDifficultyScreen()
    {
        botDifficultyScreen = CreateScreen("Bot Difficulty");
        BotDifficultyAssetCatalog catalog = BotDifficultyAssetCatalog.Load();
        if (!catalog || !catalog.HasRequiredTextures)
        {
            Debug.LogWarning("[BotDifficultyUI] Missing Resources/Chess/BotDifficultyAssets.");
            AddBackButton(botDifficultyScreen, new Vector2(-820f, -455f), () => ShowModeSelection());
            return;
        }

        Sprite backgroundSprite = CreateRuntimeSprite(
            catalog.background,
            new Rect(0f, 0f, catalog.background.width, catalog.background.height));
        Image background = CreateImage(
            botDifficultyScreen,
            "Difficulty Background",
            backgroundSprite,
            Vector2.zero,
            new Vector2(ReferenceWidth, ReferenceHeight));
        background.preserveAspect = false;

        Texture2D[] textures =
        {
            catalog.beginnerButton,
            catalog.easyButton,
            catalog.mediumButton,
            catalog.hardButton,
            catalog.expertButton
        };
        Rect[] sourceCrops =
        {
            new Rect(124f, 101f, 881f, 1223f),
            new Rect(130f, 88f, 864f, 1206f),
            new Rect(226f, 204f, 668f, 987f),
            new Rect(204f, 150f, 717f, 1060f),
            new Rect(205f, 178f, 708f, 956f)
        };
        Vector2[] positions =
        {
            new Vector2(-737f, -109f),
            new Vector2(-358f, -109f),
            new Vector2(20f, -109f),
            new Vector2(373f, -109f),
            new Vector2(746f, -109f)
        };
        Vector2[] sizes =
        {
            new Vector2(357f, 471f),
            new Vector2(340f, 471f),
            new Vector2(349f, 471f),
            new Vector2(336f, 471f),
            new Vector2(362f, 471f)
        };
        StockfishDifficulty[] levels =
        {
            StockfishDifficulty.Beginner,
            StockfishDifficulty.Easy,
            StockfishDifficulty.Medium,
            StockfishDifficulty.Hard,
            StockfishDifficulty.Expert
        };

        for (int i = 0; i < levels.Length; i++)
        {
            StockfishDifficulty level = levels[i];
            Sprite buttonSprite = CreateRuntimeSprite(textures[i], sourceCrops[i]);
            Button button = AddInteractive(
                botDifficultyScreen,
                $"{level} Difficulty",
                buttonSprite,
                positions[i],
                sizes[i],
                () => owner.SelectBotDifficulty(level),
                false,
                1.035f,
                Color.white,
                0.965f,
                0.8f);
            if (button.targetGraphic is Image buttonImage)
                buttonImage.preserveAspect = false;
        }

        AddBackButton(botDifficultyScreen, new Vector2(-820f, -455f), () => ShowModeSelection());
    }

    private Sprite CreateRuntimeSprite(Texture2D texture, Rect topLeftCrop)
    {
        if (!texture)
            return null;

        Rect unityCrop = new Rect(
            topLeftCrop.x,
            texture.height - topLeftCrop.y - topLeftCrop.height,
            topLeftCrop.width,
            topLeftCrop.height);
        Sprite sprite = Sprite.Create(
            texture,
            unityCrop,
            new Vector2(0.5f, 0.5f),
            100f,
            0u,
            SpriteMeshType.FullRect);
        runtimeSprites.Add(sprite);
        return sprite;
    }

    private RectTransform CreateScreen(string screenName)
    {
        GameObject screen = new GameObject(screenName, typeof(RectTransform));
        RectTransform rect = screen.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        Stretch(rect);
        screen.AddComponent<ResponsiveSafeArea>();
        return rect;
    }

    private Image AddImage(RectTransform parent, string imageName, Sprite sprite, Vector2 position, Vector2 size, float wigglePosition, float wiggleRotation)
    {
        Image image = CreateImage(parent, imageName, sprite, position, size);
        if (wigglePosition > 0.001f || wiggleRotation > 0.001f)
        {
            HandDrawnIdleWiggle wiggle = image.gameObject.AddComponent<HandDrawnIdleWiggle>();
            wiggle.Configure(wigglePosition, wiggleRotation, 0.01f, UnityEngine.Random.Range(0.08f, 0.14f));
        }

        return image;
    }

    private void AddDoodle(RectTransform parent, Vector2 position, float size, float rotation, float alpha, bool animate)
    {
        if (!assets.backgroundDecoration1)
            return;

        float positionWiggle = animate ? 1.4f : 0f;
        float rotationWiggle = animate ? 0.8f : 0f;
        Image image = AddImage(parent, "Battle Doodle", assets.backgroundDecoration1, position, new Vector2(size * 1.55f, size), positionWiggle, rotationWiggle);
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
        AddImage(parent, "Stars Logo", assets.backgroundDecoration3, new Vector2(695f, 240f), new Vector2(76f, 50f), 0.2f, 0.08f);
        AddImage(parent, "Stars Mid Left", assets.backgroundDecoration3, new Vector2(-300f, 170f), new Vector2(78f, 52f), 0.2f, 0.08f);
        AddImage(parent, "Stars Center", assets.backgroundDecoration3, new Vector2(-20f, 120f), new Vector2(82f, 56f), 0.2f, 0.08f);
        AddImage(parent, "Stars Lower Right", assets.backgroundDecoration3, new Vector2(300f, -390f), new Vector2(64f, 42f), 0.2f, 0.08f);
        AddImage(parent, "Hearts Button Left", assets.backgroundDecoration2, new Vector2(-910f, -110f), new Vector2(78f, 68f), 0.2f, 0.08f);
        AddImage(parent, "Hearts Button Right", assets.backgroundDecoration2, new Vector2(-535f, -110f), new Vector2(78f, 68f), 0.2f, 0.08f);
        AddImage(parent, "Hearts Logo", assets.backgroundDecoration2, new Vector2(650f, 200f), new Vector2(78f, 70f), 0.2f, 0.08f);
        AddImage(parent, "Crown Doodle", assets.backgroundDecoration4, new Vector2(45f, -350f), new Vector2(100f, 78f), 0.2f, 0.08f);
    }

    private Button AddInteractive(
        RectTransform parent,
        string buttonName,
        Sprite sprite,
        Vector2 position,
        Vector2 size,
        UnityAction action,
        bool useHandDrawnTilt,
        float hoverScale = 1.07f,
        Color? hoverTint = null,
        float pressedScale = 0.94f,
        float rotationAmount = 2.2f)
    {
        Image image = CreateImage(parent, buttonName, sprite, position, size);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        HandDrawnPressable pressable = image.gameObject.AddComponent<HandDrawnPressable>();
        pressable.Configure(
            hoverScale,
            pressedScale,
            useHandDrawnTilt ? rotationAmount : rotationAmount * 0.35f,
            hoverTint ?? new Color(1f, 0.96f, 0.72f, 1f));

        return button;
    }

    private Button AddLogoutButton(RectTransform parent)
    {
        if (!assets || !assets.logoutButton)
            return null;

        Button button = AddInteractive(
            parent,
            "Logout",
            assets.logoutButton,
            Vector2.zero,
            new Vector2(285f, 95f),
            () => owner.LogoutToAuthentication(),
            false,
            1.025f,
            new Color(1f, 0.92f, 0.92f, 1f),
            0.965f,
            0.5f);

        RectTransform rect = button.transform as RectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(34f, 28f);
        return button;
    }

    private Button AddBackButton(RectTransform parent, Vector2 position, UnityAction action)
    {
        if (!assets || !assets.backButton)
            return null;

        return AddInteractive(
            parent,
            "Back Button",
            assets.backButton,
            position,
            new Vector2(250f, 110f),
            action,
            true,
            1.04f,
            new Color(1f, 0.95f, 0.78f, 1f),
            0.95f,
            1.4f);
    }

    private Button AddGachaBackButton(RectTransform parent, Vector2 position, UnityAction action)
    {
        if (!assets || !assets.gachaBackButton)
            return AddBackButton(parent, position, action);

        return AddInteractive(
            parent,
            "Gacha Back Button",
            assets.gachaBackButton,
            position,
            new Vector2(240f, 110f),
            action,
            true,
            1.04f,
            new Color(1f, 0.95f, 0.78f, 1f),
            0.95f,
            1.4f);
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
        if (!mainScreen || !modeScreen || !multiplayerModeScreen || !gachaScreen || !botDifficultyScreen || !sideScreen || !activeScreen)
            return;

        mainScreen.gameObject.SetActive(activeScreen == mainScreen);
        modeScreen.gameObject.SetActive(activeScreen == modeScreen);
        multiplayerModeScreen.gameObject.SetActive(activeScreen == multiplayerModeScreen);
        gachaScreen.gameObject.SetActive(activeScreen == gachaScreen);
        botDifficultyScreen.gameObject.SetActive(activeScreen == botDifficultyScreen);
        sideScreen.gameObject.SetActive(activeScreen == sideScreen);
    }

    private void SetAllScreensActive(bool active)
    {
        if (mainScreen)
            mainScreen.gameObject.SetActive(active);
        if (modeScreen)
            modeScreen.gameObject.SetActive(active);
        if (multiplayerModeScreen)
            multiplayerModeScreen.gameObject.SetActive(active);
        if (gachaScreen)
            gachaScreen.gameObject.SetActive(active);
        if (botDifficultyScreen)
            botDifficultyScreen.gameObject.SetActive(active);
        if (sideScreen)
            sideScreen.gameObject.SetActive(active);
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

    private UnityAction CreateAuthenticatedAction(string featureLabel, UnityAction action)
    {
        return () =>
        {
            if (!PlayerAuthService.CanUseOnlineFeatures)
            {
                owner.RequestAuthentication($"{featureLabel} requires a backend account. Please log in or sign up.");
                return;
            }

            action?.Invoke();
        };
    }

    private void SelectMultiplayerMode(string modeLabel)
    {
        if (!PlayerAuthService.CanUseOnlineFeatures)
        {
            owner.RequestAuthentication($"{modeLabel} requires a valid login session.");
            return;
        }

        if (string.Equals(modeLabel, "LAN"))
        {
            owner.ShowOnlineSetup();
            return;
        }

        owner.ShowOnlineSetup();
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

    private void OnDestroy()
    {
        for (int i = 0; i < runtimeSprites.Count; i++)
            if (runtimeSprites[i])
                Destroy(runtimeSprites[i]);
        runtimeSprites.Clear();
    }
}

public sealed class GachaMenuController : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float DesignWidth = 1672f;
    private const float DesignHeight = 941f;
    private const int PityLimit = 90;
    private const string AssetFolder = "Assets/Materials/Gacha_menu";

    private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private readonly List<Sprite> runtimeSprites = new List<Sprite>();
    private readonly List<TextMeshProUGUI> goldLabels = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> diamondLabels = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> ticketLabels = new List<TextMeshProUGUI>();

    private RectTransform root;
    private RectTransform mainScreen;
    private RectTransform resultScreen;
    private RectTransform resultGrid;
    private RectTransform historyPanel;
    private RectTransform tooltipPanel;
    private RectTransform pityFill;
    private TextMeshProUGUI pityLabel;
    private TextMeshProUGUI statusLabel;
    private TextMeshProUGUI resultSummaryLabel;
    private TextMeshProUGUI tooltipLabel;
    private RawImage videoImage;
    private VideoPlayer videoPlayer;
    private RenderTexture videoTexture;
    private UnityAction backToModeSelection;
    private GachaSaveData saveData;
    private List<GachaReward> pendingResults = new List<GachaReward>();
    private bool usingLoopVideo;

    public void Initialize(RectTransform newRoot, UnityAction newBackToModeSelection)
    {
        root = newRoot;
        backToModeSelection = newBackToModeSelection;
        LoadSprites();
        LoadSave();
        BuildMainScreen();
        BuildResultScreen();
        BuildVideoOverlay();
        RefreshAll();
        ShowMainScreen();
    }

    public void OpenMainScreen()
    {
        if (mainScreen && resultScreen)
            ShowMainScreen();
    }

    private void OnEnable()
    {
        if (saveData != null)
            RefreshAll();
    }

    private void BuildMainScreen()
    {
        mainScreen = CreateLayer("Gacha Main");
        AddFullImage(mainScreen, "Gacha Menu Blank", GetSprite("GachaMenuDesignBlank.png"));
        AddFullImage(mainScreen, "Gacha Title", GetSprite("GachaTitle.png"));
        AddResourceBars(mainScreen);
        AddButton(mainScreen, "Back Gacha", GetSprite("BackGacha.png"), D(230f, 76f), new Vector2(90f, 90f), BackToModeSelection, 1.04f);

        AddImage(mainScreen, "Standard Gacha", GetSprite("StandardGacha.png"), D(274f, 362f), new Vector2(365f, 126f));
        AddPityPanel(mainScreen);
        AddRewardButton(mainScreen, "Gold Reward", GetSprite("GoldPR.png"), D(220f, 792f), new Vector2(122f, 122f), "Gold rewards: 100, 250, 500, 1,000, or jackpot 4,500 gold.");
        AddRewardButton(mainScreen, "Diamond Reward", GetSprite("DiamondPR.png"), D(360f, 792f), new Vector2(122f, 122f), "Diamond rewards: 10, 30, 80, 120, or jackpot 1,200 diamonds.");
        AddRewardButton(mainScreen, "Ticket Reward", GetSprite("SummonTicketPR.png"), D(501f, 792f), new Vector2(122f, 122f), "Ticket rewards: 1, 2, 5, or jackpot 10 summon tickets.");
        AddRewardButton(mainScreen, "Skin Reward", GetSprite("Skin5StarPR.png"), D(641f, 792f), new Vector2(122f, 122f), "5-star skin reward rate is reserved for a later skin pool. Skin rolls are disabled for now.");

        AddButton(mainScreen, "Summon x1", GetSprite("SummonX1.png"), D(966f, 707f), new Vector2(315f, 115f), () => StartSummon(1), 1.035f);
        AddButton(mainScreen, "Summon x10", GetSprite("SummonX10.png"), D(1322f, 708f), new Vector2(315f, 115f), () => StartSummon(10), 1.035f);
        AddButton(mainScreen, "History", GetSprite("HistoryButton.png"), D(955f, 861f), new Vector2(260f, 72f), ShowHistory, 1.03f);

        statusLabel = AddText(mainScreen, "Gacha Status", D(1145f, 858f), new Vector2(760f, 48f), 28f, TextAlignmentOptions.Center, new Color(0.16f, 0.13f, 0.1f, 1f));
        BuildTooltip(mainScreen);
        BuildHistory(mainScreen);
    }

    private void BuildResultScreen()
    {
        resultScreen = CreateLayer("Gacha Result");
        AddFullImage(resultScreen, "Gacha Result Blank", GetSprite("GachaResultBlank.png"));
        AddResourceBars(resultScreen);
        resultSummaryLabel = AddText(resultScreen, "Result Summary", D(836f, 392f), new Vector2(880f, 55f), 34f, TextAlignmentOptions.Center, new Color(0.13f, 0.1f, 0.08f, 1f));
        resultGrid = CreateChild(resultScreen, "Result Grid", D(836f, 570f), new Vector2(1120f, 330f));
        AddButton(resultScreen, "Result Summon x1", GetSprite("SummonX1.png"), D(463f, 848f), new Vector2(330f, 116f), () => StartSummon(1), 1.035f);
        AddButton(resultScreen, "Result Summon x10", GetSprite("SummonX10.png"), D(858f, 848f), new Vector2(330f, 116f), () => StartSummon(10), 1.035f);
        AddButton(resultScreen, "Result Back", GetSprite("BackButton.png"), D(1226f, 848f), new Vector2(255f, 90f), ShowMainScreen, 1.035f);
    }

    private void BuildVideoOverlay()
    {
        RectTransform overlay = CreateLayer("Gacha Video Overlay");
        overlay.SetAsLastSibling();
        videoImage = overlay.gameObject.AddComponent<RawImage>();
        videoImage.color = Color.black;
        videoImage.raycastTarget = true;

        Button skipButton = overlay.gameObject.AddComponent<Button>();
        skipButton.transition = Selectable.Transition.None;
        skipButton.onClick.AddListener(ShowPendingResults);

        videoTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
        videoTexture.name = "Gacha Animation RenderTexture";
        videoImage.texture = videoTexture;

        videoPlayer = overlay.gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.loopPointReached += HandleVideoLoopPointReached;
        AddText(overlay, "Skip Hint", new Vector2(0f, -470f), new Vector2(900f, 42f), 24f, TextAlignmentOptions.Center, new Color(1f, 0.96f, 0.82f, 0.88f)).text = "Click anywhere to skip";
        overlay.gameObject.SetActive(false);
    }

    private void AddResourceBars(RectTransform parent)
    {
        RectTransform gold = AddImage(parent, "Gold Counter", GetSprite("GoldGacha.png"), D(654f, 74f), new Vector2(335f, 92f)).rectTransform;
        RectTransform diamond = AddImage(parent, "Diamond Counter", GetSprite("DiamondGacha.png"), D(976f, 74f), new Vector2(335f, 92f)).rectTransform;
        RectTransform ticket = AddImage(parent, "Ticket Counter", GetSprite("SummonTicket.png"), D(1257f, 74f), new Vector2(260f, 92f)).rectTransform;
        goldLabels.Add(AddText(gold, "Gold Amount", new Vector2(42f, 0f), new Vector2(178f, 54f), 39f, TextAlignmentOptions.Center, Color.black));
        diamondLabels.Add(AddText(diamond, "Diamond Amount", new Vector2(45f, 0f), new Vector2(178f, 54f), 39f, TextAlignmentOptions.Center, Color.black));
        ticketLabels.Add(AddText(ticket, "Ticket Amount", new Vector2(42f, 0f), new Vector2(95f, 54f), 39f, TextAlignmentOptions.Center, Color.black));
    }

    private void AddPityPanel(RectTransform parent)
    {
        RectTransform pity = AddImage(parent, "Pity Panel", GetSprite("PityGacha.png"), D(275f, 575f), new Vector2(360f, 205f)).rectTransform;
        pityLabel = AddText(pity, "Pity Text", new Vector2(74f, 54f), new Vector2(105f, 42f), 35f, TextAlignmentOptions.Center, Color.black);
        Image fill = CreatePlainImage(pity, "Pity Fill", new Color(1f, 0.78f, 0.08f, 1f), new Vector2(-106f, 7f), new Vector2(1f, 26f));
        pityFill = fill.rectTransform;
        pityFill.pivot = new Vector2(0f, 0.5f);
    }

    private void BuildTooltip(RectTransform parent)
    {
        tooltipPanel = CreateChild(parent, "Reward Tooltip", D(835f, 884f), new Vector2(760f, 76f));
        Image backing = tooltipPanel.gameObject.AddComponent<Image>();
        backing.color = new Color(1f, 0.98f, 0.9f, 0.97f);
        backing.raycastTarget = false;
        Outline outline = tooltipPanel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        outline.effectDistance = new Vector2(2f, -2f);
        tooltipLabel = AddText(tooltipPanel, "Tooltip Text", Vector2.zero, new Vector2(720f, 60f), 24f, TextAlignmentOptions.Center, Color.black);
        tooltipPanel.gameObject.SetActive(false);
    }

    private void BuildHistory(RectTransform parent)
    {
        historyPanel = CreateChild(parent, "History Panel", D(836f, 535f), new Vector2(780f, 610f));
        Image panel = historyPanel.gameObject.AddComponent<Image>();
        panel.color = new Color(1f, 0.98f, 0.91f, 0.98f);
        Outline outline = historyPanel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.78f);
        outline.effectDistance = new Vector2(3f, -3f);
        AddText(historyPanel, "History Title", new Vector2(0f, 252f), new Vector2(650f, 50f), 34f, TextAlignmentOptions.Center, Color.black).text = "Summon History";
        AddButton(historyPanel, "Close History", GetSprite("BackButton.png"), new Vector2(0f, -252f), new Vector2(215f, 76f), HideHistory, 1.025f);
        historyPanel.gameObject.SetActive(false);
    }

    private void StartSummon(int count)
    {
        HideHistory();
        HideTooltip();
        if (!TrySpendForSummon(count, out string paymentMessage))
        {
            SetStatus("Not enough summon tickets, gold, or diamonds.");
            return;
        }

        pendingResults = RollRewards(count);
        ApplyRewards(pendingResults);
        saveData.pityPulls = Mathf.Clamp(saveData.pityPulls + count, 0, PityLimit);
        AddHistory(count, paymentMessage, pendingResults);
        Save();
        RefreshAll();
        PlayAnimation(HasTopReward(pendingResults));
    }

    private bool TrySpendForSummon(int count, out string paymentMessage)
    {
        if (saveData.tickets >= count)
        {
            saveData.tickets -= count;
            paymentMessage = count == 1 ? "Used 1 ticket" : "Used 10 tickets";
            return true;
        }

        if (count == 1)
        {
            if (saveData.diamonds >= 120)
            {
                saveData.diamonds -= 120;
                paymentMessage = "Spent 120 diamonds";
                return true;
            }
            if (saveData.gold >= 500)
            {
                saveData.gold -= 500;
                paymentMessage = "Spent 500 gold";
                return true;
            }
            paymentMessage = string.Empty;
            return false;
        }

        if (saveData.diamonds >= 1000)
        {
            saveData.diamonds -= 1000;
            paymentMessage = "Spent 1,000 diamonds";
            return true;
        }
        if (saveData.gold >= 4500)
        {
            saveData.gold -= 4500;
            paymentMessage = "Spent 4,500 gold";
            return true;
        }

        paymentMessage = string.Empty;
        return false;
    }

    private List<GachaReward> RollRewards(int count)
    {
        List<GachaReward> rewards = new List<GachaReward>(count);
        bool hasFourStarOrBetter = false;
        for (int i = 0; i < count; i++)
        {
            GachaReward reward = RollSingleReward(false);
            rewards.Add(reward);
            hasFourStarOrBetter |= reward.rarity >= 4;
        }
        if (count >= 10 && !hasFourStarOrBetter)
            rewards[rewards.Count - 1] = RollSingleReward(true);
        return rewards;
    }

    private GachaReward RollSingleReward(bool rareOnly)
    {
        RewardDrop[] table = rareOnly ? RareRewardTable : RewardTable;
        float total = 0f;
        for (int i = 0; i < table.Length; i++)
            total += table[i].weight;

        float roll = UnityEngine.Random.Range(0f, total);
        for (int i = 0; i < table.Length; i++)
        {
            roll -= table[i].weight;
            if (roll <= 0f)
                return table[i].CreateReward();
        }
        return table[table.Length - 1].CreateReward();
    }

    private void ApplyRewards(List<GachaReward> rewards)
    {
        for (int i = 0; i < rewards.Count; i++)
        {
            GachaReward reward = rewards[i];
            if (reward.type == GachaRewardType.Gold)
                saveData.gold += reward.amount;
            else if (reward.type == GachaRewardType.Diamond)
                saveData.diamonds += reward.amount;
            else if (reward.type == GachaRewardType.Ticket)
                saveData.tickets += reward.amount;
        }
    }

    private void AddHistory(int count, string paymentMessage, List<GachaReward> rewards)
    {
        if (saveData.history == null)
            saveData.history = new List<GachaHistoryEntry>();
        saveData.history.Insert(0, new GachaHistoryEntry
        {
            timestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
            pullCount = count,
            payment = paymentMessage,
            rewards = SummarizeRewards(rewards)
        });
        while (saveData.history.Count > 50)
            saveData.history.RemoveAt(saveData.history.Count - 1);
    }

    private string SummarizeRewards(List<GachaReward> rewards)
    {
        int gold = 0;
        int diamonds = 0;
        int tickets = 0;
        for (int i = 0; i < rewards.Count; i++)
        {
            if (rewards[i].type == GachaRewardType.Gold)
                gold += rewards[i].amount;
            else if (rewards[i].type == GachaRewardType.Diamond)
                diamonds += rewards[i].amount;
            else if (rewards[i].type == GachaRewardType.Ticket)
                tickets += rewards[i].amount;
        }

        List<string> parts = new List<string>();
        if (gold > 0)
            parts.Add($"+{gold:N0} Gold");
        if (diamonds > 0)
            parts.Add($"+{diamonds:N0} Diamonds");
        if (tickets > 0)
            parts.Add($"+{tickets:N0} Tickets");
        return parts.Count == 0 ? "No reward" : string.Join(", ", parts);
    }

    private bool HasTopReward(List<GachaReward> rewards)
    {
        for (int i = 0; i < rewards.Count; i++)
            if (rewards[i].isTopReward)
                return true;
        return false;
    }

    private void PlayAnimation(bool goldAnimation)
    {
        string intro = goldAnimation ? "Gacha animation gold.mp4" : "Gacha animation.mp4";
        string loop = goldAnimation ? "Gacha animation gold gif.mp4" : "Gacha animation gif.mp4";
        string introPath = FullAssetPath(intro);
        if (!File.Exists(introPath))
        {
            ShowPendingResults();
            return;
        }

        usingLoopVideo = false;
        videoPlayer.gameObject.SetActive(true);
        videoPlayer.Stop();
        videoPlayer.isLooping = false;
        videoPlayer.url = PathToUrl(introPath);
        videoPlayer.clip = null;
        videoPlayer.Play();

        if (!File.Exists(FullAssetPath(loop)))
            usingLoopVideo = true;
    }

    private void HandleVideoLoopPointReached(VideoPlayer source)
    {
        if (usingLoopVideo)
            return;

        string loopName = source.url.IndexOf("gold", StringComparison.OrdinalIgnoreCase) >= 0
            ? "Gacha animation gold gif.mp4"
            : "Gacha animation gif.mp4";
        string loopPath = FullAssetPath(loopName);
        if (!File.Exists(loopPath))
            return;

        usingLoopVideo = true;
        source.Stop();
        source.url = PathToUrl(loopPath);
        source.isLooping = true;
        source.Play();
    }

    private void ShowPendingResults()
    {
        if (videoPlayer)
        {
            videoPlayer.Stop();
            videoPlayer.gameObject.SetActive(false);
        }

        PopulateResultGrid();
        resultSummaryLabel.text = SummarizeRewards(pendingResults);
        resultScreen.gameObject.SetActive(true);
        mainScreen.gameObject.SetActive(false);
        SetStatus(string.Empty);
    }

    private void PopulateResultGrid()
    {
        for (int i = resultGrid.childCount - 1; i >= 0; i--)
            Destroy(resultGrid.GetChild(i).gameObject);

        int count = Mathf.Max(1, pendingResults.Count);
        int columns = count <= 1 ? 1 : 5;
        int rows = Mathf.CeilToInt(count / (float)columns);
        Vector2 cellSize = count <= 1 ? new Vector2(190f, 190f) : new Vector2(154f, 154f);
        float gapX = count <= 1 ? 0f : 46f;
        float gapY = 28f;
        float totalWidth = columns * cellSize.x + (columns - 1) * gapX;
        float totalHeight = rows * cellSize.y + (rows - 1) * gapY;

        for (int i = 0; i < pendingResults.Count; i++)
        {
            int col = i % columns;
            int row = i / columns;
            float x = -totalWidth * 0.5f + cellSize.x * 0.5f + col * (cellSize.x + gapX);
            float y = totalHeight * 0.5f - cellSize.y * 0.5f - row * (cellSize.y + gapY);
            AddRewardCard(resultGrid, pendingResults[i], new Vector2(x, y), cellSize);
        }
    }

    private void AddRewardCard(RectTransform parent, GachaReward reward, Vector2 position, Vector2 size)
    {
        RectTransform card = CreateChild(parent, reward.displayName, position, size);
        Image background = card.gameObject.AddComponent<Image>();
        background.color = reward.isTopReward ? new Color(1f, 0.9f, 0.38f, 0.92f) : new Color(1f, 0.98f, 0.9f, 0.92f);
        Outline outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.78f);
        outline.effectDistance = new Vector2(3f, -3f);
        AddImage(card, "Reward Icon", GetRewardSprite(reward.type), new Vector2(0f, 24f), size * 0.58f);
        AddText(card, "Reward Amount", new Vector2(0f, -56f), new Vector2(size.x - 18f, 38f), 27f, TextAlignmentOptions.Center, Color.black).text = reward.AmountText;
    }

    private void ShowMainScreen()
    {
        HideHistory();
        HideTooltip();
        if (videoPlayer)
            videoPlayer.gameObject.SetActive(false);
        mainScreen.gameObject.SetActive(true);
        resultScreen.gameObject.SetActive(false);
        RefreshAll();
    }

    private void BackToModeSelection()
    {
        HideHistory();
        HideTooltip();
        backToModeSelection?.Invoke();
    }

    private void ShowHistory()
    {
        if (!historyPanel)
            return;

        for (int i = historyPanel.childCount - 1; i >= 0; i--)
        {
            Transform child = historyPanel.GetChild(i);
            if (child.name.StartsWith("History Row", StringComparison.Ordinal))
                Destroy(child.gameObject);
        }

        if (saveData.history == null || saveData.history.Count == 0)
        {
            AddText(historyPanel, "History Row Empty", new Vector2(0f, 120f), new Vector2(650f, 44f), 26f, TextAlignmentOptions.Center, Color.black).text = "No summons yet.";
        }
        else
        {
            int visible = Mathf.Min(saveData.history.Count, 8);
            for (int i = 0; i < visible; i++)
            {
                GachaHistoryEntry entry = saveData.history[i];
                TextMeshProUGUI row = AddText(historyPanel, $"History Row {i}", new Vector2(0f, 180f - i * 48f), new Vector2(680f, 42f), 21f, TextAlignmentOptions.Left, Color.black);
                row.text = $"{entry.timestampUtc}  x{entry.pullCount}  {entry.payment}  ->  {entry.rewards}";
            }
        }
        historyPanel.gameObject.SetActive(true);
    }

    private void HideHistory()
    {
        if (historyPanel)
            historyPanel.gameObject.SetActive(false);
    }

    private void ShowTooltip(string text)
    {
        if (!tooltipPanel)
            return;
        tooltipLabel.text = text;
        tooltipPanel.gameObject.SetActive(true);
    }

    private void HideTooltip()
    {
        if (tooltipPanel)
            tooltipPanel.gameObject.SetActive(false);
    }

    private void RefreshAll()
    {
        for (int i = 0; i < goldLabels.Count; i++)
            goldLabels[i].text = saveData.gold.ToString("N0");
        for (int i = 0; i < diamondLabels.Count; i++)
            diamondLabels[i].text = saveData.diamonds.ToString("N0");
        for (int i = 0; i < ticketLabels.Count; i++)
            ticketLabels[i].text = saveData.tickets.ToString("N0");

        int remaining = Mathf.Max(0, PityLimit - saveData.pityPulls);
        if (pityLabel)
            pityLabel.text = remaining.ToString();
        if (pityFill)
            pityFill.sizeDelta = new Vector2(Mathf.Lerp(26f, 265f, saveData.pityPulls / (float)PityLimit), 26f);
    }

    private void SetStatus(string message)
    {
        if (statusLabel)
            statusLabel.text = message;
    }

    private Button AddRewardButton(RectTransform parent, string name, Sprite sprite, Vector2 position, Vector2 size, string tooltip)
    {
        Button button = AddButton(parent, name, sprite, position, size, () => ShowTooltip(tooltip), 1.035f);
        GachaRewardHoverTarget hover = button.gameObject.AddComponent<GachaRewardHoverTarget>();
        hover.Initialize(() => ShowTooltip(tooltip), HideTooltip);
        return button;
    }

    private Button AddButton(RectTransform parent, string name, Sprite sprite, Vector2 position, Vector2 size, UnityAction action, float hoverScale)
    {
        Image image = AddImage(parent, name, sprite, position, size);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        HandDrawnPressable pressable = image.gameObject.AddComponent<HandDrawnPressable>();
        pressable.Configure(hoverScale, 0.95f, 0.8f, new Color(1f, 0.96f, 0.72f, 1f));
        return button;
    }

    private Image AddImage(RectTransform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
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

    private Image AddFullImage(RectTransform parent, string name, Sprite sprite)
    {
        Image image = AddImage(parent, name, sprite, Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight));
        image.preserveAspect = false;
        return image;
    }

    private Image CreatePlainImage(RectTransform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        Image image = AddImage(parent, name, null, position, size);
        image.color = color;
        image.preserveAspect = false;
        return image;
    }

    private TextMeshProUGUI AddText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(14f, fontSize * 0.58f);
        text.fontSizeMax = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.text = string.Empty;
        return text;
    }

    private RectTransform CreateLayer(string name)
    {
        RectTransform layer = CreateChild(root, name, Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight));
        Stretch(layer);
        return layer;
    }

    private RectTransform CreateChild(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private Vector2 D(float x, float y)
    {
        float scaleX = ReferenceWidth / DesignWidth;
        float scaleY = ReferenceHeight / DesignHeight;
        return new Vector2((x - DesignWidth * 0.5f) * scaleX, (DesignHeight * 0.5f - y) * scaleY);
    }

    private Sprite GetRewardSprite(GachaRewardType type)
    {
        if (type == GachaRewardType.Gold)
            return GetSprite("GoldPR.png");
        if (type == GachaRewardType.Diamond)
            return GetSprite("DiamondPR.png");
        if (type == GachaRewardType.Ticket)
            return GetSprite("SummonTicketPR.png");
        return GetSprite("Skin5StarPR.png");
    }

    private Sprite GetSprite(string fileName)
    {
        if (sprites.TryGetValue(fileName, out Sprite sprite))
            return sprite;
        sprite = LoadSprite(fileName, true);
        sprites[fileName] = sprite;
        return sprite;
    }

    private void LoadSprites()
    {
        LoadSpriteToCache("GachaMenuDesignBlank.png", false);
        LoadSpriteToCache("GachaResultBlank.png", false);
        LoadSpriteToCache("GachaTitle.png", false);
        LoadSpriteToCache("BackGacha.png", true);
        LoadSpriteToCache("BackButton.png", true);
        LoadSpriteToCache("GoldGacha.png", true);
        LoadSpriteToCache("DiamondGacha.png", true);
        LoadSpriteToCache("SummonTicket.png", true);
        LoadSpriteToCache("StandardGacha.png", true);
        LoadSpriteToCache("PityGacha.png", true);
        LoadSpriteToCache("GoldPR.png", true);
        LoadSpriteToCache("DiamondPR.png", true);
        LoadSpriteToCache("SummonTicketPR.png", true);
        LoadSpriteToCache("Skin5StarPR.png", true);
        LoadSpriteToCache("SummonX1.png", true);
        LoadSpriteToCache("SummonX10.png", true);
        LoadSpriteToCache("HistoryButton.png", true);
    }

    private void LoadSpriteToCache(string fileName, bool trimTransparent)
    {
        sprites[fileName] = LoadSprite(fileName, trimTransparent);
    }

    private Sprite LoadSprite(string fileName, bool trimTransparent)
    {
        string path = FullAssetPath(fileName);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[GachaMenu] Missing asset: {fileName}");
            return null;
        }

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            Destroy(texture);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(fileName);
        texture.filterMode = FilterMode.Bilinear;
        Rect rect = trimTransparent ? FindOpaqueBounds(texture) : new Rect(0f, 0f, texture.width, texture.height);
        Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
        runtimeSprites.Add(sprite);
        return sprite;
    }

    private Rect FindOpaqueBounds(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        int minX = texture.width;
        int minY = texture.height;
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < texture.height; y++)
        {
            int row = y * texture.width;
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[row + x].a <= 8)
                    continue;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
            return new Rect(0f, 0f, texture.width, texture.height);

        int padding = 3;
        minX = Mathf.Max(0, minX - padding);
        minY = Mathf.Max(0, minY - padding);
        maxX = Mathf.Min(texture.width - 1, maxX + padding);
        maxY = Mathf.Min(texture.height - 1, maxY + padding);
        return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private void LoadSave()
    {
        string json = PlayerPrefs.GetString(SaveKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                saveData = JsonUtility.FromJson<GachaSaveData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[GachaMenu] Failed to load save data: {exception.Message}");
            }
        }

        if (saveData == null)
            saveData = GachaSaveData.CreateDefault();
        if (saveData.history == null)
            saveData.history = new List<GachaHistoryEntry>();
    }

    private void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saveData));
        PlayerPrefs.Save();
    }

    private string SaveKey
    {
        get
        {
            string user = string.IsNullOrWhiteSpace(PlayerAuthService.Username) ? PlayerAuthService.CurrentDisplayName : PlayerAuthService.Username;
            return $"chess_but_weird_gacha_{user}";
        }
    }

    private static string FullAssetPath(string fileName)
    {
        return Path.Combine(Directory.GetCurrentDirectory(), AssetFolder, fileName);
    }

    private static string PathToUrl(string path)
    {
        return new Uri(path).AbsoluteUri;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (videoPlayer)
            videoPlayer.loopPointReached -= HandleVideoLoopPointReached;
        if (videoTexture)
        {
            videoTexture.Release();
            Destroy(videoTexture);
        }
        for (int i = 0; i < runtimeSprites.Count; i++)
        {
            if (!runtimeSprites[i])
                continue;
            Texture2D texture = runtimeSprites[i].texture;
            Destroy(runtimeSprites[i]);
            if (texture)
                Destroy(texture);
        }
    }

    private static readonly RewardDrop[] RewardTable =
    {
        new RewardDrop(GachaRewardType.Gold, 100, 3, 20f, false),
        new RewardDrop(GachaRewardType.Gold, 250, 3, 17f, false),
        new RewardDrop(GachaRewardType.Gold, 500, 3, 12f, false),
        new RewardDrop(GachaRewardType.Gold, 1000, 4, 7f, false),
        new RewardDrop(GachaRewardType.Gold, 4500, 5, 1.5f, true),
        new RewardDrop(GachaRewardType.Diamond, 10, 3, 18f, false),
        new RewardDrop(GachaRewardType.Diamond, 30, 3, 10f, false),
        new RewardDrop(GachaRewardType.Diamond, 80, 3, 5f, false),
        new RewardDrop(GachaRewardType.Diamond, 120, 4, 2f, false),
        new RewardDrop(GachaRewardType.Diamond, 1200, 5, 0.5f, true),
        new RewardDrop(GachaRewardType.Ticket, 1, 3, 5f, false),
        new RewardDrop(GachaRewardType.Ticket, 2, 3, 1.5f, false),
        new RewardDrop(GachaRewardType.Ticket, 5, 4, 0.4f, false),
        new RewardDrop(GachaRewardType.Ticket, 10, 5, 0.1f, true)
    };

    private static readonly RewardDrop[] RareRewardTable =
    {
        new RewardDrop(GachaRewardType.Gold, 1000, 4, 54f, false),
        new RewardDrop(GachaRewardType.Gold, 4500, 5, 8f, true),
        new RewardDrop(GachaRewardType.Diamond, 120, 4, 28f, false),
        new RewardDrop(GachaRewardType.Diamond, 1200, 5, 5f, true),
        new RewardDrop(GachaRewardType.Ticket, 5, 4, 4f, false),
        new RewardDrop(GachaRewardType.Ticket, 10, 5, 1f, true)
    };
}

[Serializable]
public sealed class GachaSaveData
{
    public int gold;
    public int diamonds;
    public int tickets;
    public int pityPulls;
    public List<GachaHistoryEntry> history = new List<GachaHistoryEntry>();

    public static GachaSaveData CreateDefault()
    {
        return new GachaSaveData
        {
            gold = 12340,
            diamonds = 1320,
            tickets = 17,
            pityPulls = 0,
            history = new List<GachaHistoryEntry>()
        };
    }
}

[Serializable]
public sealed class GachaHistoryEntry
{
    public string timestampUtc;
    public int pullCount;
    public string payment;
    public string rewards;
}

public enum GachaRewardType
{
    Gold,
    Diamond,
    Ticket,
    Skin
}

public readonly struct GachaReward
{
    public readonly GachaRewardType type;
    public readonly int amount;
    public readonly int rarity;
    public readonly bool isTopReward;

    public string displayName => type.ToString();
    public string AmountText => type == GachaRewardType.Skin ? "5* Skin" : $"+{amount:N0}";

    public GachaReward(GachaRewardType type, int amount, int rarity, bool isTopReward)
    {
        this.type = type;
        this.amount = amount;
        this.rarity = rarity;
        this.isTopReward = isTopReward;
    }
}

public readonly struct RewardDrop
{
    public readonly GachaRewardType type;
    public readonly int amount;
    public readonly int rarity;
    public readonly float weight;
    public readonly bool isTopReward;

    public RewardDrop(GachaRewardType type, int amount, int rarity, float weight, bool isTopReward)
    {
        this.type = type;
        this.amount = amount;
        this.rarity = rarity;
        this.weight = weight;
        this.isTopReward = isTopReward;
    }

    public GachaReward CreateReward()
    {
        return new GachaReward(type, amount, rarity, isTopReward);
    }
}

public sealed class GachaRewardHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Action enter;
    private Action exit;

    public void Initialize(Action onEnter, Action onExit)
    {
        enter = onEnter;
        exit = onExit;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        enter?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        exit?.Invoke();
    }
}
