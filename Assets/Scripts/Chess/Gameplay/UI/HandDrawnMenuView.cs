using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class HandDrawnMenuView : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float ScreenTransitionDuration = 0.42f;
    private const float ScreenTransitionOffset = 54f;
    private const float OverlayTransitionDuration = 0.22f;

    private ChessTurnSelectionUI owner;
    private ChessGame chessGame;
    private HandDrawnMenuAssets assets;
    private Canvas canvas;
    private GraphicRaycaster raycaster;
    private RectTransform mainScreen;
    private RectTransform modeScreen;
    private RectTransform localModeScreen;
    private RectTransform aramModeScreen;
    private RectTransform multiplayerModeScreen;
    private RectTransform gachaScreen;
    private RectTransform inventoryOverlay;
    private RectTransform profileOverlay;
    private RectTransform settingsOverlay;
    private RectTransform botDifficultyScreen;
    private RectTransform sideScreen;
    private RectTransform skinScreen;
    private RectTransform transitionVeil;
    private CanvasGroup transitionVeilGroup;
    private RectTransform currentScreen;
    private Button modeLogoutButton;
    private GachaMenuController gachaController;
    private InventoryMenuController inventoryController;
    private PlayerProfileMenuController profileController;
    private SettingsMenuController settingsController;
    private Coroutine screenTransitionCoroutine;
    private Coroutine inventoryOverlayTransitionCoroutine;
    private Coroutine profileOverlayTransitionCoroutine;
    private Coroutine settingsOverlayTransitionCoroutine;
    private readonly List<Sprite> runtimeSprites = new List<Sprite>();
    private string selectedWhiteSkinId = PieceSkinCatalog.DefaultSkinId;
    private string selectedBlackSkinId = PieceSkinCatalog.DefaultSkinId;
    private bool skinSelectionVsBot;
    private PieceTeam skinSelectionPlayerTeam = PieceTeam.White;
    private static Font uiFont;

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
        BuildLocalModeScreen();
        BuildAramModeScreen();
        BuildMultiplayerModeScreen();
        BuildGachaScreen();
        BuildBotDifficultyScreen();
        BuildSideScreen();
        BuildSkinScreen();
        BuildSettingsOverlay();
        BuildTransitionVeil();
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        GameMusicManager.PlayMainMenuPrimaryMusic();
        SetVisible(true);
        SetInputEnabled(true);
        TransitionToScreen(mainScreen, null);
    }

    public void ShowModeSelection()
    {
        GameMusicManager.PlayMainMenuHubMusic();
        SetVisible(true);
        SetInputEnabled(true);
        TransitionToScreen(modeScreen, () =>
        {
            HideInventoryOverlayImmediate();
            HideProfileOverlayImmediate();
        });
    }

    public void ShowSideSelection()
    {
        GameMusicManager.PlayMainMenuHubMusic();
        SetVisible(true);
        SetInputEnabled(true);
        TransitionToScreen(sideScreen, null);
    }

    public void ShowBotDifficultySelection()
    {
        GameMusicManager.PlayMainMenuHubMusic();
        SetVisible(true);
        SetInputEnabled(true);
        TransitionToScreen(botDifficultyScreen, null);
    }

    public void ShowLocalModeSelection()
    {
        GameMusicManager.PlayMainMenuHubMusic();
        SetVisible(true);
        SetInputEnabled(true);
        TransitionToScreen(localModeScreen, null);
    }

    public void ShowAramModeSelection()
    {
        if (!owner.RequireAccountAccess("ARAM mode")) return;
        GameMusicManager.PlayMainMenuHubMusic();
        SetVisible(true);
        SetInputEnabled(true);
        TransitionToScreen(aramModeScreen, null);
    }

    public void ShowPieceSkinSelection(bool vsBot, PieceTeam playerTeam)
    {
        skinSelectionVsBot = vsBot;
        skinSelectionPlayerTeam = playerTeam;
        var saved = CosmeticSelection.Load();
        selectedWhiteSkinId = saved.whiteSkinId;
        selectedBlackSkinId = saved.blackSkinId;

        SetVisible(true);
        SetInputEnabled(true);
        TransitionToScreen(skinScreen, RebuildSkinScreen);
    }

    public void ShowMultiplayerModeSelection()
    {
        if (!owner.RequireAccountAccess("Multiplayer")) return;
        GameMusicManager.PlayMainMenuHubMusic();
        if (!assets || !assets.HasMultiplayerModeSprites)
        {
            ShowSideSelection();
            return;
        }

        SetVisible(true);
        SetInputEnabled(true);
        TransitionToScreen(multiplayerModeScreen, null);
    }

    public void ShowGachaMenu()
    {
        if (!owner.RequireAccountAccess("Gacha")) return;
        GameMusicManager.PlayGachaMusic();
        SetVisible(true);
        SetInputEnabled(true);
        TransitionToScreen(gachaScreen, () => gachaController?.OpenMainScreen());
    }

    public void HideForPlaying()
    {
        StopMenuTransitions();
        HideInventoryOverlayImmediate();
        HideProfileOverlayImmediate();
        HideSettingsOverlayImmediate();
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

        Image background = CreateImage(root, "Paper Background", assets.paperBackground, Vector2.zero, Vector2.zero);
        background.color = assets.paperBackground ? Color.white : new Color(0.985f, 0.965f, 0.91f, 1f);
        background.type = Image.Type.Tiled;
        background.preserveAspect = false;
        Stretch(background.rectTransform);
    }

    private void BuildMainScreen()
    {
        mainScreen = CreateScreen("Main Menu");
        // Positions measured in the 1672 x 941 reference artwork.
        AddReferenceDoodles(mainScreen, false);
        AddReferenceAccents(mainScreen, false);
        AddImage(mainScreen, "Doodle Face", assets.doodleFaceDecoration, M(148f, 108f), MS(265f, 185f), .4f, .15f);
        AddImage(mainScreen, "Game Logo", assets.gameLogo, M(830f, 206f), MS(742f, 367f), .3f, .1f).preserveAspect = false;
        AddInteractive(mainScreen, "Start", assets.startButton, M(835f, 470f), MS(590f, 141f), () => chessGame.OpenTurnSelection(), false, 1.025f);
        AddInteractive(mainScreen, "Settings", assets.settingsButton, M(830f, 625f), MS(580f, 128f), ShowSettingsMenu, false, 1.025f);
        AddInteractive(mainScreen, "Credits", assets.creditsButton, M(825f, 779f), MS(580f, 142f), () => LogMenuClick("Credits"), false, 1.025f);
        AddImage(mainScreen, "Crown Doodle", assets.backgroundDecoration4, M(133f, 850f), MS(180f, 146f), .2f, .1f);
        AddImage(mainScreen, "Game Version", assets.gameVersion, M(1410f, 849f), MS(440f, 185f), 0f, 0f);
        AddLogoutButton(mainScreen);
    }

    private static Vector2 M(float x, float y) => new Vector2((x / 1672f - .5f) * ReferenceWidth, (.5f - y / 941f) * ReferenceHeight);
    private static Vector2 MS(float width, float height) => new Vector2(width / 1672f * ReferenceWidth, height / 941f * ReferenceHeight);

    private void AddReferenceDoodles(RectTransform screen, bool hub)
    {
        Vector2[] points = hub
            ? new[] { new Vector2(564, 240), new Vector2(838, 240), new Vector2(667, 424), new Vector2(992, 385), new Vector2(1197, 471), new Vector2(605, 573), new Vector2(854, 540), new Vector2(758, 703), new Vector2(1013, 674), new Vector2(1304, 674), new Vector2(627, 850), new Vector2(929, 844) }
            : new[] { new Vector2(492, 84), new Vector2(339, 200), new Vector2(130, 329), new Vector2(351, 386), new Vector2(106, 500), new Vector2(307, 554), new Vector2(119, 689), new Vector2(354, 728), new Vector2(370, 873), new Vector2(644, 900), new Vector2(984, 911), new Vector2(1319, 78), new Vector2(1358, 239), new Vector2(1590, 239), new Vector2(1256, 363), new Vector2(1515, 401), new Vector2(1350, 528), new Vector2(1572, 551), new Vector2(1264, 677), new Vector2(1516, 702) };
        foreach (Vector2 point in points)
            AddImage(screen, "Battle Doodle", assets.backgroundDecoration1, M(point.x, point.y), MS(hub ? 145f : 150f, hub ? 98f : 102f), 0f, 0f);
    }

    private Sprite ArtworkFragment(Sprite source, Rect pixels, float sourceSize = 1254f)
    {
        Texture2D texture = source.texture;
        Rect crop = new Rect(pixels.x / sourceSize * texture.width, (sourceSize - pixels.yMax) / sourceSize * texture.height, pixels.width / sourceSize * texture.width, pixels.height / sourceSize * texture.height);
        Sprite sprite = Sprite.Create(texture, crop, new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        runtimeSprites.Add(sprite);
        return sprite;
    }

    private void AddReferenceAccents(RectTransform screen, bool hub)
    {
        Sprite heart = ArtworkFragment(assets.backgroundDecoration2, new Rect(419, 188, 545, 519));
        Sprite smallHeart = ArtworkFragment(assets.backgroundDecoration2, new Rect(203, 705, 278, 293));
        Sprite star = ArtworkFragment(assets.backgroundDecoration3, new Rect(238, 430, 319, 332));
        Vector2[] hearts = hub ? new[] { new Vector2(1631, 231), new Vector2(1354, 421) } : new[] { new Vector2(1584, 75) };
        Vector2[] smallHearts = hub ? new[] { new Vector2(31, 597), new Vector2(495, 629), new Vector2(495, 873) } : new[] { new Vector2(499, 737), new Vector2(1155, 764) };
        Vector2[] stars = hub ? new[] { new Vector2(1010, 138), new Vector2(704, 303), new Vector2(1573, 278), new Vector2(1150, 581), new Vector2(50, 734) } : new[] { new Vector2(88, 218), new Vector2(1504, 137), new Vector2(490, 571), new Vector2(1162, 590) };
        foreach (Vector2 p in hearts) AddImage(screen, "Heart", heart, M(p.x, p.y), MS(hub ? 44 : 70, hub ? 46 : 70), 0, 0);
        foreach (Vector2 p in smallHearts) AddImage(screen, "Small Heart", smallHeart, M(p.x, p.y), MS(hub ? 36 : 42, hub ? 39 : 44), 0, 0);
        foreach (Vector2 p in stars) AddImage(screen, "Star", star, M(p.x, p.y), MS(hub ? 39 : 40, hub ? 39 : 40), 0, 0);
    }

    private void AddSelectionDecorations(RectTransform screen)
    {
        // Leave title, cards, captions and navigation unobstructed.
        foreach (Vector2 p in new[] { new Vector2(-800, 300), new Vector2(800, 300), new Vector2(-800, -200), new Vector2(800, -200), new Vector2(-510, -385), new Vector2(510, -385) })
            AddDoodle(screen, p, 65f, 0f, .8f, false);
    }

    private void BuildModeScreen()
    {
        modeScreen = CreateScreen("Mode Select");
        BuildPlayHub(modeScreen);
        BuildInventoryOverlay();
        BuildProfileOverlay();
    }

    private void BuildLocalModeScreen()
    {
        localModeScreen = CreateScreen("Local Mode");

        AddBattleDoodles(localModeScreen, true);
        AddMenuConfetti(localModeScreen);
        AddText(localModeScreen, "Local Mode Title", "Local Mode", new Vector2(0f, 340f), new Vector2(720f, 130f), 76, TextAnchor.MiddleCenter, new Color(0.12f, 0.1f, 0.08f, 1f));
        AddTextButton(localModeScreen, "Vs Bot", "Vs Bot", new Vector2(-315f, 10f), new Vector2(470f, 180f), () => owner.ShowBotDifficultySelection(), new Color(0.96f, 0.86f, 0.48f, 1f));
        AddTextButton(localModeScreen, "Two Players", "2 Players", new Vector2(315f, 10f), new Vector2(470f, 180f), () => owner.ShowTwoPlayerSkinSelection(), new Color(0.6f, 0.84f, 0.94f, 1f));
        AddBackButton(localModeScreen, new Vector2(-820f, -420f), () => ShowModeSelection());
    }

    private void BuildAramModeScreen()
    {
        aramModeScreen = CreateScreen("ARAM Mode");

        AddSelectionDecorations(aramModeScreen);
        AddText(aramModeScreen, "ARAM Mode Title", "ARAM Mode", new Vector2(0f, 340f), new Vector2(760f, 125f), 76, TextAnchor.MiddleCenter, new Color(0.12f, 0.1f, 0.08f, 1f));
        AddTextButton(aramModeScreen, "ARAM Practice", "Practice", new Vector2(-540f, 35f), new Vector2(470f, 180f), owner.StartAramPracticeGame, new Color(0.98f, 0.78f, 0.3f, 1f));
        AddTextButton(aramModeScreen, "ARAM LAN", "LAN", new Vector2(0f, 35f), new Vector2(470f, 180f), owner.ShowAramLanSetup, new Color(0.56f, 0.82f, 0.94f, 1f));
        AddTextButton(aramModeScreen, "ARAM Multiplayer", "Multiplayer", new Vector2(540f, 35f), new Vector2(470f, 180f), owner.ShowAramOnlineSetup, new Color(0.74f, 0.66f, 0.96f, 1f));
        AddText(aramModeScreen, "ARAM Practice Caption", "Practice local", new Vector2(-540f, -105f), new Vector2(470f, 48f), 30, TextAnchor.MiddleCenter, new Color(0.14f, 0.11f, 0.08f, 1f));
        AddText(aramModeScreen, "ARAM LAN Caption", "Private LAN room", new Vector2(0f, -105f), new Vector2(430f, 48f), 28, TextAnchor.MiddleCenter, new Color(0.14f, 0.11f, 0.08f, 1f));
        AddText(aramModeScreen, "ARAM Online Caption", "Online matchmaking", new Vector2(540f, -105f), new Vector2(430f, 48f), 28, TextAnchor.MiddleCenter, new Color(0.14f, 0.11f, 0.08f, 1f));
        AddBackButton(aramModeScreen, new Vector2(-820f, -420f), () => ShowModeSelection());
    }

    private void BuildPlayHub(RectTransform screen)
    {
        AddReferenceDoodles(screen, true);
        AddReferenceAccents(screen, true);
        AddImage(screen, "Doodle Face", assets.doodleFaceDecoration, M(141f, 115f), MS(258f, 182f), .3f, .1f);
        AddImage(screen, "Game Slogan", assets.gameSlogan, M(625f, 110f), MS(635f, 82f), 0f, 0f);
        AddImage(screen, "Game Logo", assets.gameLogo, M(1310f, 179f), MS(594f, 315f), .2f, .1f);
        AddInteractive(screen, "Local Gameplay", assets.localGameplayButton, M(257f, 315f), MS(410f, 143f), () => owner.ShowLocalModeSelection(), false, 1.025f);
        AddInteractive(screen, "Online Play", assets.onlinePlayButton, M(261f, 483f), MS(422f, 146f), () => owner.ShowMultiplayerModeSelection(), false, 1.025f);
        AddInteractive(screen, "ARAM Mode", assets.aramModeButton, M(258f, 640f), MS(418f, 131f), CreateAuthenticatedAction("ARAM", owner.ShowAramModeSelection), false, 1.025f);
        AddInteractive(screen, "Shop", assets.shopButton, M(257f, 804f), MS(418f, 155f), CreateAuthenticatedAction("Shop", () => LogMenuClick("Shop")), false, 1.025f);
        AddInteractive(screen, "Inventory", assets.inventoryButton, M(1544f, 420f), MS(136f, 140f), CreateAuthenticatedAction("Inventory", ShowInventoryMenu), false, 1.045f);
        AddInteractive(screen, "Gacha", assets.gachaButton, M(1544f, 560f), MS(150f, 125f), CreateAuthenticatedAction("Gacha", ShowGachaMenu), false, 1.045f);
        AddInteractive(screen, "Player Profile", assets.playerProfile, M(1544f, 700f), MS(150f, 142f), CreateAuthenticatedAction("Player Profile", ShowProfileMenu), false, 1.045f);
        AddInteractive(screen, "Settings Icon", assets.settingsIcon, M(1285f, 859f), MS(122f, 124f), ShowSettingsMenu, false, 1.045f);
        AddImage(screen, "Game Version", assets.gameVersion, M(1494f, 870f), MS(284f, 122f), 0f, 0f);
        modeLogoutButton = AddLogoutButton(screen);
    }

    private void BuildInventoryOverlay()
    {
        GameObject overlay = new GameObject("Inventory Overlay", typeof(RectTransform));
        inventoryOverlay = overlay.GetComponent<RectTransform>();
        inventoryOverlay.SetParent(modeScreen, false);
        Stretch(inventoryOverlay);
        inventoryOverlay.SetAsLastSibling();
        CanvasGroup group = overlay.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = false;

        inventoryController = overlay.AddComponent<InventoryMenuController>();
        inventoryController.Initialize(inventoryOverlay, CloseInventoryMenu);
        overlay.SetActive(false);
    }

    private void BuildProfileOverlay()
    {
        GameObject overlay = new GameObject("Player Profile Overlay", typeof(RectTransform));
        profileOverlay = overlay.GetComponent<RectTransform>();
        profileOverlay.SetParent(modeScreen, false);
        Stretch(profileOverlay);
        profileOverlay.SetAsLastSibling();
        CanvasGroup group = overlay.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = false;

        profileController = overlay.AddComponent<PlayerProfileMenuController>();
        profileController.Initialize(profileOverlay, CloseProfileMenu);
        overlay.SetActive(false);
    }

    private void BuildSettingsOverlay()
    {
        GameObject overlay = new GameObject("Settings Overlay", typeof(RectTransform));
        settingsOverlay = overlay.GetComponent<RectTransform>();
        settingsOverlay.SetParent(transform, false);
        Stretch(settingsOverlay);
        settingsOverlay.SetAsLastSibling();
        CanvasGroup group = overlay.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = false;

        settingsController = overlay.AddComponent<SettingsMenuController>();
        settingsController.Initialize(settingsOverlay, CloseSettingsMenu);
        overlay.SetActive(false);
    }

    private void ShowInventoryMenu()
    {
        if (!owner.RequireAccountAccess("Inventory")) return;
        if (!inventoryOverlay)
            return;

        SetVisible(true);
        SetInputEnabled(true);
        TransitionToScreen(modeScreen, OpenInventoryOverlay);
    }

    private void CloseInventoryMenu()
    {
        if (inventoryOverlay)
            inventoryOverlayTransitionCoroutine = StartOverlayTransition(inventoryOverlay, inventoryOverlayTransitionCoroutine, false);

        if (!IsProfileOpen)
            SetModeScreenBackgroundInteractable(true);
    }

    private void ShowProfileMenu()
    {
        if (!owner.RequireAccountAccess("Player Profile")) return;
        if (!profileOverlay)
            return;

        SetVisible(true);
        SetInputEnabled(true);
        TransitionToScreen(modeScreen, OpenProfileOverlay);
    }

    private void CloseProfileMenu()
    {
        if (profileOverlay)
            profileOverlayTransitionCoroutine = StartOverlayTransition(profileOverlay, profileOverlayTransitionCoroutine, false);

        SetModeLogoutVisible(true);

        if (!IsInventoryOpen)
            SetModeScreenBackgroundInteractable(true);
    }

    private void ShowSettingsMenu()
    {
        if (!settingsOverlay)
            return;

        SetVisible(true);
        SetInputEnabled(true);
        HideInventoryOverlayImmediate();
        HideProfileOverlayImmediate();
        settingsOverlay.SetAsLastSibling();
        settingsController?.Open();
        settingsOverlayTransitionCoroutine = StartOverlayTransition(settingsOverlay, settingsOverlayTransitionCoroutine, true);
    }

    private void CloseSettingsMenu()
    {
        if (settingsOverlay)
            settingsOverlayTransitionCoroutine = StartOverlayTransition(settingsOverlay, settingsOverlayTransitionCoroutine, false);
    }

    private void OpenInventoryOverlay()
    {
        if (!inventoryOverlay)
            return;

        HideProfileOverlayImmediate();
        SetModeScreenBackgroundInteractable(false);
        inventoryOverlay.SetAsLastSibling();
        inventoryController?.Open();
        inventoryOverlayTransitionCoroutine = StartOverlayTransition(inventoryOverlay, inventoryOverlayTransitionCoroutine, true);
    }

    private void OpenProfileOverlay()
    {
        if (!profileOverlay)
            return;

        HideInventoryOverlayImmediate();
        SetModeScreenBackgroundInteractable(false);
        profileOverlay.SetAsLastSibling();
        profileController?.Open();
        SetModeLogoutVisible(false);
        profileOverlayTransitionCoroutine = StartOverlayTransition(profileOverlay, profileOverlayTransitionCoroutine, true);
    }

    private void HideInventoryOverlayImmediate()
    {
        if (inventoryOverlayTransitionCoroutine != null)
        {
            StopCoroutine(inventoryOverlayTransitionCoroutine);
            inventoryOverlayTransitionCoroutine = null;
        }

        SetOverlayState(inventoryOverlay, false, 1f, 0.96f);
        if (!IsProfileOpen)
            SetModeScreenBackgroundInteractable(true);
    }

    private void HideProfileOverlayImmediate()
    {
        if (profileOverlayTransitionCoroutine != null)
        {
            StopCoroutine(profileOverlayTransitionCoroutine);
            profileOverlayTransitionCoroutine = null;
        }

        SetOverlayState(profileOverlay, false, 1f, 0.96f);
        SetModeLogoutVisible(true);
        if (!IsInventoryOpen)
            SetModeScreenBackgroundInteractable(true);
    }

    private void HideSettingsOverlayImmediate()
    {
        if (settingsOverlayTransitionCoroutine != null)
        {
            StopCoroutine(settingsOverlayTransitionCoroutine);
            settingsOverlayTransitionCoroutine = null;
        }

        SetOverlayState(settingsOverlay, false, 1f, 0.96f);
    }

    private bool IsInventoryOpen => inventoryOverlay && inventoryOverlay.gameObject.activeSelf;
    private bool IsProfileOpen => profileOverlay && profileOverlay.gameObject.activeSelf;

    private void SetModeScreenBackgroundInteractable(bool interactable)
    {
        // Inventory/Profile overlays already include a full-screen raycast blocker.
        // Do not flip Selectable.interactable here: Unity applies disabled tint,
        // which makes the hand-drawn buttons look permanently washed out during
        // fade transitions.
    }

    private void SetModeLogoutVisible(bool visible)
    {
        if (modeLogoutButton)
            modeLogoutButton.gameObject.SetActive(visible);
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

        AddSelectionDecorations(sideScreen);
        AddImage(sideScreen, "Choose Side Title", assets.chooseYourSide, new Vector2(0f, 365f), new Vector2(880f, 150f), 0f, 0f);
        AddInteractive(sideScreen, "White Card", assets.whiteSideButton, new Vector2(-330f, -30f), new Vector2(470f, 590f), () => owner.ShowBotSkinSelection(PieceTeam.White), false, 1.035f);
        AddInteractive(sideScreen, "Black Card", assets.blackSideButton, new Vector2(330f, -30f), new Vector2(477f, 590f), () => owner.ShowBotSkinSelection(PieceTeam.Black), false, 1.035f);
        AddImage(sideScreen, "Game Version", assets.gameVersion, new Vector2(735f, -420f), new Vector2(330f, 145f), 0f, 0f);
        AddBackButton(sideScreen, new Vector2(-720f, -435f), () => ShowBotDifficultySelection(), new Vector2(270f, 100f));
    }

    private void BuildSkinScreen()
    {
        skinScreen = CreateScreen("Piece Skin Select");
    }

    private void RebuildSkinScreen()
    {
        ClearSkinScreen();

        AddBattleDoodles(skinScreen, true);
        AddMenuConfetti(skinScreen);
        AddText(skinScreen, "Skin Title", "Match Appearance", new Vector2(0f, 410f), new Vector2(980f, 110f), 66, TextAnchor.MiddleCenter, new Color(0.12f, 0.1f, 0.08f, 1f));

        if (skinSelectionVsBot)
        {
            AddTextButton(skinScreen, "Start Bot Match", "Start", new Vector2(0f, -410f), new Vector2(330f, 115f), StartSelectedSkinMatch, new Color(0.98f, 0.83f, 0.32f, 1f));
            AddBackButton(skinScreen, new Vector2(-820f, -420f), () => ShowSideSelection());
        }
        else
        {
            AddTextButton(skinScreen, "Start Local Match", "Start", new Vector2(0f, -430f), new Vector2(330f, 105f), StartSelectedSkinMatch, new Color(0.98f, 0.83f, 0.32f, 1f));
            AddBackButton(skinScreen, new Vector2(-820f, -430f), () => ShowLocalModeSelection());
        }

        var selection = CosmeticSelection.Load();
        var catalog = LoadingManager.For(chessGame).Catalog;
        AddTextButton(skinScreen, "Board selection", "Board: " + (catalog?.FindBoard(selection.boardId)?.displayName ?? "Classic"),
            new Vector2(0f, 100f), new Vector2(660f, 100f), () => CycleCosmetic(false), new Color(.86f, .88f, .95f));
        AddTextButton(skinScreen, "Environment selection", "Environment: " + (catalog?.FindEnvironment(selection.environmentId)?.displayName ?? "None"),
            new Vector2(0f, -60f), new Vector2(660f, 100f), () => CycleCosmetic(true), new Color(.86f, .88f, .95f));
    }

    private void CycleCosmetic(bool environment)
    {
        var catalog = LoadingManager.For(chessGame).Catalog;
        if (!catalog) return;
        var items = environment ? catalog.environments : catalog.boards;
        if (items == null || items.Count == 0) return;
        var selection = CosmeticSelection.Load();
        string current = environment ? selection.environmentId : selection.boardId;
        int index = items.FindIndex(e => e != null && e.id == current) + 1;
        string next = index >= items.Count ? (environment ? "" : items[0].id) : items[index].id;
        if (environment) selection.environmentId = next; else selection.boardId = next;
        selection.Save();
        RebuildSkinScreen();
    }

    private void ClearSkinScreen()
    {
        if (!skinScreen)
            return;

        for (int i = skinScreen.childCount - 1; i >= 0; i--)
        {
            skinScreen.GetChild(i).gameObject.SetActive(false);
            Destroy(skinScreen.GetChild(i).gameObject);
        }
    }

    private string GetSelectedSkinId(PieceTeam team)
    {
        return team == PieceTeam.White ? selectedWhiteSkinId : selectedBlackSkinId;
    }

    private void StartSelectedSkinMatch()
    {
        if (skinSelectionVsBot)
            owner.StartBotGameWithSkin(skinSelectionPlayerTeam, GetSelectedSkinId(skinSelectionPlayerTeam));
        else
            owner.StartLocalTwoPlayerGameWithSkins(selectedWhiteSkinId, selectedBlackSkinId);
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
        rect.SetParent(MenuDesignFrame.Create(transform, screenName, new Vector2(ReferenceWidth, ReferenceHeight)), false);
        Stretch(rect);
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

    private Button AddTextButton(RectTransform parent, string buttonName, string label, Vector2 position, Vector2 size, UnityAction action, Color backgroundColor)
    {
        GameObject buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = backgroundColor;
        image.raycastTarget = true;
        if (parent == aramModeScreen && assets.optionFrame)
        {
            image.sprite = assets.optionFrame;
            image.type = Image.Type.Sliced;
            buttonObject.GetComponent<Outline>().enabled = false;
        }

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.1f, 0.08f, 0.05f, 0.75f);
        outline.effectDistance = new Vector2(4f, -4f);

        Text text = AddText(rect, "Label", label, Vector2.zero, size, 42, TextAnchor.MiddleCenter, GetReadableTextColor(backgroundColor));
        text.fontStyle = FontStyle.Bold;

        HandDrawnPressable pressable = buttonObject.AddComponent<HandDrawnPressable>();
        pressable.Configure(1.035f, 0.95f, 1.1f, Color.Lerp(backgroundColor, Color.white, 0.26f));
        return button;
    }

    private void SetLockedButton(Button button)
    {
        if (!button)
            return;

        button.interactable = false;
        Image image = button.GetComponent<Image>();
        if (image)
            image.color = new Color(0.48f, 0.48f, 0.5f, 0.82f);

        HandDrawnPressable pressable = button.GetComponent<HandDrawnPressable>();
        if (pressable)
            pressable.enabled = false;
    }

    private Text AddText(Transform parent, string textName, string textValue, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(textName, typeof(RectTransform), typeof(Text));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.text = textValue;
        text.font = GetUiFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(14, Mathf.RoundToInt(fontSize * 0.55f));
        text.resizeTextMaxSize = fontSize;
        return text;
    }

    private static Font GetUiFont()
    {
        if (uiFont)
            return uiFont;

        uiFont = ChessFontCatalog.RuntimeFont;
        if (!uiFont)
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        return uiFont;
    }

    private static Color GetReadableTextColor(Color backgroundColor)
    {
        float brightness = backgroundColor.r * 0.299f + backgroundColor.g * 0.587f + backgroundColor.b * 0.114f;
        return brightness > 0.58f ? new Color(0.12f, 0.1f, 0.08f, 1f) : Color.white;
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
            MS(175f, 49f),
            () => owner.LogoutToAuthentication(),
            false,
            1.025f,
            new Color(1f, 0.92f, 0.92f, 1f),
            0.965f,
            0.5f);

        RectTransform rect = button.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = parent == mainScreen ? M(836f, 897f) : M(778f, 813f);
        return button;
    }

    private Button AddBackButton(RectTransform parent, Vector2 position, UnityAction action, Vector2? size = null)
    {
        if (!assets || !assets.backButton)
            return null;

        Vector2 buttonSize = size ?? new Vector2(250f, 110f);
        return AddInteractive(
            parent,
            "Back Button",
            assets.backButton,
            position,
            buttonSize,
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

    private void BuildTransitionVeil()
    {
        GameObject veilObject = new GameObject("Menu Transition Veil", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        transitionVeil = veilObject.GetComponent<RectTransform>();
        transitionVeil.SetParent(transform, false);
        Stretch(transitionVeil);
        transitionVeil.SetAsLastSibling();

        Image veilImage = veilObject.GetComponent<Image>();
        veilImage.color = new Color(0.985f, 0.965f, 0.91f, 0.94f);
        veilImage.raycastTarget = true;

        transitionVeilGroup = veilObject.GetComponent<CanvasGroup>();
        transitionVeilGroup.alpha = 0f;
        transitionVeilGroup.interactable = true;
        transitionVeilGroup.blocksRaycasts = false;

        AddImage(transitionVeil, "Transition Stars Left", assets.backgroundDecoration3, new Vector2(-135f, 18f), new Vector2(78f, 52f), 0.2f, 0.08f);
        AddImage(transitionVeil, "Transition Stars Right", assets.backgroundDecoration3, new Vector2(155f, -20f), new Vector2(78f, 52f), 0.2f, 0.08f);

        transitionVeil.gameObject.SetActive(false);
    }

    private void TransitionToScreen(RectTransform activeScreen, Action prepareAction)
    {
        if (!mainScreen || !modeScreen || !localModeScreen || !aramModeScreen || !multiplayerModeScreen || !gachaScreen || !botDifficultyScreen || !sideScreen || !skinScreen || !activeScreen)
            return;

        if (activeScreen != modeScreen)
        {
            HideInventoryOverlayImmediate();
            HideProfileOverlayImmediate();
        }

        if (!canvas || !canvas.enabled || currentScreen == null || !currentScreen.gameObject.activeSelf)
        {
            prepareAction?.Invoke();
            SetScreenImmediate(activeScreen);
            SetInputEnabled(true);
            return;
        }

        if (currentScreen == activeScreen)
        {
            prepareAction?.Invoke();
            SetScreenImmediate(activeScreen);
            SetInputEnabled(true);
            return;
        }

        if (screenTransitionCoroutine != null)
        {
            StopCoroutine(screenTransitionCoroutine);
            screenTransitionCoroutine = null;
            HideTransitionVeil();
        }
        screenTransitionCoroutine = StartCoroutine(AnimateScreenTransition(activeScreen, prepareAction));
    }

    private IEnumerator AnimateScreenTransition(RectTransform activeScreen, Action prepareAction)
    {
        SetInputEnabled(false);

        RectTransform previousScreen = currentScreen;
        CanvasGroup previousGroup = GetOrAddCanvasGroup(previousScreen);
        CanvasGroup activeGroup = GetOrAddCanvasGroup(activeScreen);
        bool forward = GetScreenOrder(activeScreen) >= GetScreenOrder(previousScreen);
        float direction = forward ? 1f : -1f;
        float halfDuration = ScreenTransitionDuration * 0.5f;

        previousScreen.gameObject.SetActive(true);
        activeScreen.gameObject.SetActive(true);
        ResetInactiveTransitionScreens(previousScreen, activeScreen);
        previousScreen.SetAsLastSibling();
        activeScreen.SetAsLastSibling();
        ShowTransitionVeil();

        previousGroup.alpha = 1f;
        previousGroup.interactable = true;
        previousGroup.blocksRaycasts = false;
        activeGroup.alpha = 0f;
        activeGroup.interactable = true;
        activeGroup.blocksRaycasts = false;
        previousScreen.anchoredPosition = Vector2.zero;
        activeScreen.anchoredPosition = new Vector2(direction * ScreenTransitionOffset, 0f);

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            float eased = EaseOutCubic(elapsed / halfDuration);
            previousGroup.alpha = Mathf.Lerp(1f, 0.22f, eased);
            previousScreen.anchoredPosition = new Vector2(Mathf.Lerp(0f, -direction * ScreenTransitionOffset * 0.45f, eased), 0f);
            SetTransitionVeil(eased);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        prepareAction?.Invoke();

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            float eased = EaseOutCubic(elapsed / halfDuration);
            previousGroup.alpha = Mathf.Lerp(0.22f, 0f, eased);
            activeGroup.alpha = Mathf.Lerp(0f, 1f, eased);
            previousScreen.anchoredPosition = new Vector2(Mathf.Lerp(-direction * ScreenTransitionOffset * 0.45f, -direction * ScreenTransitionOffset, eased), 0f);
            activeScreen.anchoredPosition = new Vector2(Mathf.Lerp(direction * ScreenTransitionOffset, 0f, eased), 0f);
            SetTransitionVeil(1f - eased);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        SetScreenImmediate(activeScreen);
        HideTransitionVeil();
        SetInputEnabled(true);
        screenTransitionCoroutine = null;
    }

    private void SetScreenImmediate(RectTransform activeScreen)
    {
        if (!mainScreen || !modeScreen || !localModeScreen || !aramModeScreen || !multiplayerModeScreen || !gachaScreen || !botDifficultyScreen || !sideScreen || !skinScreen || !activeScreen)
            return;

        RectTransform[] screens = GetMenuScreens();
        for (int i = 0; i < screens.Length; i++)
        {
            RectTransform screen = screens[i];
            bool isActive = screen == activeScreen;
            CanvasGroup group = GetOrAddCanvasGroup(screen);
            screen.gameObject.SetActive(isActive);
            screen.anchoredPosition = Vector2.zero;
            group.alpha = isActive ? 1f : 0f;
            group.interactable = true;
            group.blocksRaycasts = isActive;
        }

        activeScreen.SetAsLastSibling();
        currentScreen = activeScreen;
        if (transitionVeil)
            transitionVeil.SetAsLastSibling();
    }

    private void SetAllScreensActive(bool active)
    {
        if (mainScreen)
            mainScreen.gameObject.SetActive(active);
        if (modeScreen)
            modeScreen.gameObject.SetActive(active);
        if (localModeScreen)
            localModeScreen.gameObject.SetActive(active);
        if (aramModeScreen)
            aramModeScreen.gameObject.SetActive(active);
        if (multiplayerModeScreen)
            multiplayerModeScreen.gameObject.SetActive(active);
        if (gachaScreen)
            gachaScreen.gameObject.SetActive(active);
        if (botDifficultyScreen)
            botDifficultyScreen.gameObject.SetActive(active);
        if (sideScreen)
            sideScreen.gameObject.SetActive(active);
        if (skinScreen)
            skinScreen.gameObject.SetActive(active);
        if (!active)
            currentScreen = null;
    }

    private RectTransform[] GetMenuScreens()
    {
        return new[] { mainScreen, modeScreen, localModeScreen, aramModeScreen, multiplayerModeScreen, gachaScreen, botDifficultyScreen, sideScreen, skinScreen };
    }

    private void ResetInactiveTransitionScreens(RectTransform previousScreen, RectTransform activeScreen)
    {
        RectTransform[] screens = GetMenuScreens();
        for (int i = 0; i < screens.Length; i++)
        {
            RectTransform screen = screens[i];
            if (!screen || screen == previousScreen || screen == activeScreen)
                continue;

            CanvasGroup group = GetOrAddCanvasGroup(screen);
            screen.anchoredPosition = Vector2.zero;
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = false;
            screen.gameObject.SetActive(false);
        }
    }

    private int GetScreenOrder(RectTransform screen)
    {
        RectTransform[] screens = GetMenuScreens();
        for (int i = 0; i < screens.Length; i++)
            if (screens[i] == screen)
                return i;
        return 0;
    }

    private CanvasGroup GetOrAddCanvasGroup(RectTransform rect)
    {
        CanvasGroup group = rect.GetComponent<CanvasGroup>();
        if (!group)
            group = rect.gameObject.AddComponent<CanvasGroup>();
        return group;
    }

    private Coroutine StartOverlayTransition(RectTransform overlay, Coroutine existingCoroutine, bool visible)
    {
        if (!overlay)
            return null;

        if (existingCoroutine != null)
            StopCoroutine(existingCoroutine);

        return StartCoroutine(AnimateOverlayTransition(overlay, visible));
    }

    private IEnumerator AnimateOverlayTransition(RectTransform overlay, bool visible)
    {
        CanvasGroup group = GetOrAddCanvasGroup(overlay);
        float startScale = overlay.localScale.x;
        float endScale = visible ? 1f : 0.96f;
        group.alpha = 1f;

        if (visible)
        {
            overlay.gameObject.SetActive(true);
            overlay.SetAsLastSibling();
        }

        group.interactable = true;
        group.blocksRaycasts = visible;

        float elapsed = 0f;
        while (elapsed < OverlayTransitionDuration)
        {
            float eased = EaseOutCubic(elapsed / OverlayTransitionDuration);
            overlay.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, eased);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        SetOverlayState(overlay, visible, 1f, endScale);

        if (overlay == inventoryOverlay)
            inventoryOverlayTransitionCoroutine = null;
        else if (overlay == profileOverlay)
            profileOverlayTransitionCoroutine = null;
        else if (overlay == settingsOverlay)
            settingsOverlayTransitionCoroutine = null;
    }

    private void SetOverlayState(RectTransform overlay, bool visible, float alpha, float scale)
    {
        if (!overlay)
            return;

        CanvasGroup group = GetOrAddCanvasGroup(overlay);
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = visible;
        overlay.localScale = Vector3.one * scale;
        overlay.gameObject.SetActive(visible);
    }

    private void ShowTransitionVeil()
    {
        if (!transitionVeil || !transitionVeilGroup)
            return;

        transitionVeil.gameObject.SetActive(true);
        transitionVeil.SetAsLastSibling();
        transitionVeilGroup.interactable = true;
        transitionVeilGroup.blocksRaycasts = true;
        SetTransitionVeil(0f);
    }

    private void SetTransitionVeil(float alpha)
    {
        if (!transitionVeilGroup)
            return;

        transitionVeilGroup.alpha = Mathf.Clamp01(alpha);

    }

    private void HideTransitionVeil()
    {
        if (!transitionVeil || !transitionVeilGroup)
            return;

        transitionVeilGroup.alpha = 0f;
        transitionVeilGroup.interactable = true;
        transitionVeilGroup.blocksRaycasts = false;
        transitionVeil.gameObject.SetActive(false);
    }

    private void StopMenuTransitions()
    {
        if (screenTransitionCoroutine != null)
        {
            StopCoroutine(screenTransitionCoroutine);
            screenTransitionCoroutine = null;
        }

        if (inventoryOverlayTransitionCoroutine != null)
        {
            StopCoroutine(inventoryOverlayTransitionCoroutine);
            inventoryOverlayTransitionCoroutine = null;
        }

        if (profileOverlayTransitionCoroutine != null)
        {
            StopCoroutine(profileOverlayTransitionCoroutine);
            profileOverlayTransitionCoroutine = null;
        }

        if (settingsOverlayTransitionCoroutine != null)
        {
            StopCoroutine(settingsOverlayTransitionCoroutine);
            settingsOverlayTransitionCoroutine = null;
        }


        HideTransitionVeil();
    }

    private static float EaseOutCubic(float value)
    {
        value = Mathf.Clamp01(value);
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
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
            if (!owner.RequireAccountAccess(featureLabel)) return;

            action?.Invoke();
        };
    }

    private void SelectMultiplayerMode(string modeLabel)
    {
        if (!owner.RequireAccountAccess(modeLabel)) return;

        if (string.Equals(modeLabel, "LAN"))
        {
            owner.ShowLanSetup();
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
    private RectTransform contentRoot;
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
    private GachaSummonRevealController summonReveal;
    private UnityAction backToModeSelection;
    private GachaSaveData saveData;
    private List<GachaReward> pendingResults = new List<GachaReward>();
    private bool summonInProgress;

    public void Initialize(RectTransform newRoot, UnityAction newBackToModeSelection)
    {
        root = newRoot;
        backToModeSelection = newBackToModeSelection;
        LoadSprites();
        LoadSave();
        BuildContentRoot();
        BuildMainScreen();
        BuildResultScreen();
        BuildSummonReveal();
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
        AddText(mainScreen, "Gacha Subtitle", D(1080f, 257f), new Vector2(225f, 48f), 40f, TextAlignmentOptions.Center, Color.black).text = "But Weird";
        AddImage(mainScreen, "Standard Banner", GetSprite("StandardGacha.png"), D(274f, 362f), new Vector2(320f, 113f));
        Image description = AddImage(mainScreen, "Banner Description", GetSprite("GachaTitle.png"), D(991f, 459f), new Vector2(1030f, 337f));
        description.preserveAspect = false;
        AddResourceBars(mainScreen);
        AddButton(mainScreen, "Back Gacha", GetSprite("BackGacha.png"), D(230f, 76f), new Vector2(83f, 80f), BackToModeSelection, 1.04f);
        AddPityPanel(mainScreen);
        AddRewardButton(mainScreen, "Gold Reward", D(220f, 792f), new Vector2(115f, 126f), "Gold rewards: 100, 250, 500, 1,000, or jackpot 4,500 gold.");
        AddRewardButton(mainScreen, "Diamond Reward", D(356f, 792f), new Vector2(118f, 126f), "Diamond rewards: 10, 30, 80, 120, or jackpot 1,200 diamonds.");
        AddRewardButton(mainScreen, "Ticket Reward", D(495f, 791f), new Vector2(120f, 126f), "Ticket rewards: 1, 2, 5, or jackpot 10 summon tickets.");
        AddRewardButton(mainScreen, "Skin Reward", D(640f, 791f), new Vector2(128f, 126f), "5-star skin reward rate is reserved for a later skin pool. Skin rolls are disabled for now.");
        AddButton(mainScreen, "Summon x1", GetSprite("SummonX1.png"), D(967f, 728f), new Vector2(274f, 124f), () => StartSummon(1), 1.025f);
        AddButton(mainScreen, "Summon x10", GetSprite("SummonX10.png"), D(1322f, 732f), new Vector2(322f, 128f), () => StartSummon(10), 1.025f);
        AddButton(mainScreen, "History", GetSprite("HistoryButton.png"), D(948f, 874f), new Vector2(226f, 70f), ShowHistory, 1.025f);
        statusLabel = AddText(mainScreen, "Gacha Status", D(1338f, 890f), new Vector2(505f, 62f), 25f, TextAlignmentOptions.Center, new Color(.16f, .13f, .1f, 1f));
        BuildTooltip(mainScreen);
        BuildHistory(mainScreen);
    }

    private void BuildContentRoot()
    {
        contentRoot = CreateChild(root, "Gacha Content Root", Vector2.zero, new Vector2(DesignWidth, DesignHeight));
        contentRoot.gameObject.AddComponent<GachaContentRootFitter>().Configure(DesignWidth, DesignHeight);
    }

    private void BuildResultScreen()
    {
        resultScreen = CreateLayer("Gacha Result");
        AddFullImage(resultScreen, "Gacha Result Blank", GetSprite("GachaResultBlank.png"));
        AddResourceBars(resultScreen);
        resultSummaryLabel = AddText(resultScreen, "Result Summary", D(836f, 392f), S(880f, 55f), F(34f), TextAlignmentOptions.Center, new Color(0.13f, 0.1f, 0.08f, 1f));
        resultGrid = CreateChild(resultScreen, "Result Grid", D(836f, 570f), S(1120f, 330f));
        AddButton(resultScreen, "Result Summon x1", GetSprite("SummonX1.png"), D(463f, 848f), S(330f, 116f), () => StartSummon(1), 1.035f);
        AddButton(resultScreen, "Result Summon x10", GetSprite("SummonX10.png"), D(858f, 848f), S(330f, 116f), () => StartSummon(10), 1.035f);
        AddButton(resultScreen, "Result Back", GetSprite("BackButton.png"), D(1226f, 848f), S(255f, 90f), ShowMainScreen, 1.035f);
    }

    private void AddResourceBars(RectTransform parent)
    {
        RectTransform gold = AddImage(parent, "Gold Counter", GetSprite("GoldGacha.png"), D(657f, 74f), new Vector2(297f, 87f)).rectTransform;
        RectTransform diamond = AddImage(parent, "Diamond Counter", GetSprite("DiamondGacha.png"), D(975f, 74f), new Vector2(274f, 85f)).rectTransform;
        RectTransform ticket = AddImage(parent, "Ticket Counter", GetSprite("SummonTicket.png"), D(1247f, 74f), new Vector2(212f, 85f)).rectTransform;
        goldLabels.Add(AddText(gold, "Gold Amount", new Vector2(12f, 0f), new Vector2(152f, 52f), 39f, TextAlignmentOptions.Center, Color.black));
        diamondLabels.Add(AddText(diamond, "Diamond Amount", new Vector2(14f, 0f), new Vector2(125f, 52f), 39f, TextAlignmentOptions.Center, Color.black));
        ticketLabels.Add(AddText(ticket, "Ticket Amount", new Vector2(39f, 0f), new Vector2(90f, 52f), 39f, TextAlignmentOptions.Center, Color.black));
    }

    private void AddPityPanel(RectTransform parent)
    {
        RectTransform pity = AddImage(parent, "Pity Panel", GetSprite("PityGacha.png"), D(275f, 571f), new Vector2(319f, 183f)).rectTransform;
        pityLabel = AddText(pity, "Pity Text", new Vector2(-10f, 54f), new Vector2(58f, 40f), 31f, TextAlignmentOptions.Center, Color.black);
        Image fill = CreatePlainImage(pity, "Pity Fill", new Color(1f, .78f, .08f, 1f), new Vector2(-130f, 12f), new Vector2(0f, 20f));
        pityFill = fill.rectTransform;
        pityFill.pivot = new Vector2(0f, .5f);
    }

    private void BuildTooltip(RectTransform parent)
    {
        tooltipPanel = CreateChild(parent, "Reward Tooltip", D(835f, 884f), S(760f, 76f));
        Image backing = tooltipPanel.gameObject.AddComponent<Image>();
        backing.color = new Color(1f, 0.98f, 0.9f, 0.97f);
        backing.raycastTarget = false;
        Outline outline = tooltipPanel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        outline.effectDistance = new Vector2(2f, -2f);
        tooltipLabel = AddText(tooltipPanel, "Tooltip Text", Vector2.zero, S(720f, 60f), F(24f), TextAlignmentOptions.Center, Color.black);
        tooltipPanel.gameObject.SetActive(false);
    }

    private void BuildHistory(RectTransform parent)
    {
        historyPanel = CreateChild(parent, "History Panel", D(836f, 535f), S(780f, 610f));
        Image panel = historyPanel.gameObject.AddComponent<Image>();
        panel.color = new Color(1f, 0.98f, 0.91f, 0.98f);
        Outline outline = historyPanel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.78f);
        outline.effectDistance = new Vector2(3f, -3f);
        AddText(historyPanel, "History Title", L(0f, 252f), S(650f, 50f), F(34f), TextAlignmentOptions.Center, Color.black).text = "Summon History";
        AddButton(historyPanel, "Close History", GetSprite("BackButton.png"), L(0f, -252f), S(215f, 76f), HideHistory, 1.025f);
        historyPanel.gameObject.SetActive(false);
    }

    private void StartSummon(int count)
    {
        if (summonInProgress)
        {
            if (summonReveal && summonReveal.IsPlaying)
                summonReveal.RequestSkip();
            return;
        }

        HideHistory();
        HideTooltip();
        Debug.Log($"[GachaMenu] Summon requested. count={count}, gold={saveData.gold}, diamonds={saveData.diamonds}, tickets={saveData.tickets}, pity={saveData.pityPulls}/{PityLimit}");
        if (!TrySpendForSummon(count, out string paymentMessage))
        {
            Debug.LogWarning($"[GachaMenu] Summon rejected. count={count}, gold={saveData.gold}, diamonds={saveData.diamonds}, tickets={saveData.tickets}");
            SetStatus("Not enough summon tickets, gold, or diamonds.");
            return;
        }

        pendingResults = RollRewards(count);
        Debug.Log($"[GachaMenu] Summon rolled. count={count}, payment=\"{paymentMessage}\", rewards={DescribeRewardsForLog(pendingResults)}");
        ApplyRewards(pendingResults);
        saveData.pityPulls = Mathf.Clamp(saveData.pityPulls + count, 0, PityLimit);
        AddHistory(count, paymentMessage, pendingResults);
        Save();
        RefreshAll();
        summonInProgress = true;
        PlayPendingReveal();
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

    private string DescribeRewardsForLog(List<GachaReward> rewards)
    {
        if (rewards == null || rewards.Count == 0)
            return "none";

        List<string> parts = new List<string>(rewards.Count);
        for (int i = 0; i < rewards.Count; i++)
        {
            GachaReward reward = rewards[i];
            parts.Add($"{reward.rarity}* {reward.type} x{reward.amount} top={reward.isTopReward}");
        }
        return string.Join("; ", parts);
    }

    private void ShowPendingResults()
    {
        summonInProgress = false;
        PopulateResultGrid();
        resultSummaryLabel.text = SummarizeRewards(pendingResults);
        resultScreen.gameObject.SetActive(true);
        mainScreen.gameObject.SetActive(false);
        SetStatus(string.Empty);
    }

    private void BuildSummonReveal()
    {
        summonReveal = contentRoot.gameObject.AddComponent<GachaSummonRevealController>();
        summonReveal.Initialize(contentRoot);
    }

    private void PlayPendingReveal()
    {
        mainScreen.gameObject.SetActive(false);
        resultScreen.gameObject.SetActive(false);
        if (summonReveal)
            summonReveal.PlaySequence(pendingResults, reward => GetRewardSprite(reward.type), ShowPendingResults);
        else
            ShowPendingResults();
    }

    private GachaReward GetFeaturedReward()
    {
        if (pendingResults == null || pendingResults.Count == 0)
            return new GachaReward(GachaRewardType.Ticket, 1, 3, false);

        GachaReward featured = pendingResults[0];
        for (int i = 1; i < pendingResults.Count; i++)
        {
            GachaReward candidate = pendingResults[i];
            if (candidate.rarity > featured.rarity || (candidate.rarity == featured.rarity && candidate.isTopReward && !featured.isTopReward))
                featured = candidate;
        }
        return featured;
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
            AddText(historyPanel, "History Row Empty", L(0f, 120f), S(650f, 44f), F(26f), TextAlignmentOptions.Center, Color.black).text = "No summons yet.";
        }
        else
        {
            int visible = Mathf.Min(saveData.history.Count, 8);
            for (int i = 0; i < visible; i++)
            {
                GachaHistoryEntry entry = saveData.history[i];
                TextMeshProUGUI row = AddText(historyPanel, $"History Row {i}", L(0f, 180f - i * 48f), S(680f, 42f), F(21f), TextAlignmentOptions.Left, Color.black);
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
            pityFill.sizeDelta = new Vector2(Mathf.Lerp(0f, 259f, saveData.pityPulls / (float)PityLimit), 20f);
    }

    private void SetStatus(string message)
    {
        if (statusLabel)
            statusLabel.text = message;
    }

    private Button AddRewardButton(RectTransform parent, string name, Vector2 position, Vector2 size, string tooltip)
    {
        string spriteName = name == "Gold Reward" ? "GoldPR.png" : name == "Diamond Reward" ? "DiamondPR.png" : name == "Ticket Reward" ? "SummonTicketPR.png" : "Skin5StarPR.png";
        Button button = AddButton(parent, name, GetSprite(spriteName), position, size, () => ShowTooltip(tooltip), 1.03f);
        GachaRewardHoverTarget hover = button.gameObject.AddComponent<GachaRewardHoverTarget>();
        hover.Initialize(() => ShowTooltip(tooltip), HideTooltip);
        return button;
    }

    private Button AddInvisibleButton(RectTransform parent, string name, Vector2 position, Vector2 size, UnityAction action)
    {
        Image image = CreatePlainImage(parent, name, new Color(1f, 1f, 1f, 0f), position, size);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        button.onClick.AddListener(action);
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
        Image image = AddImage(parent, name, sprite, Vector2.zero, new Vector2(DesignWidth, DesignHeight));
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
        text.font = ChessFontCatalog.TmpFont != null ? ChessFontCatalog.TmpFont : TMP_Settings.defaultFontAsset;
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
        RectTransform layer = CreateChild(contentRoot ? contentRoot : root, name, Vector2.zero, new Vector2(DesignWidth, DesignHeight));
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
        return new Vector2(x - DesignWidth * 0.5f, DesignHeight * 0.5f - y);
    }

    private Vector2 S(float width, float height)
    {
        return new Vector2(width * DesignWidth / ReferenceWidth, height * DesignHeight / ReferenceHeight);
    }

    private Vector2 L(float x, float y)
    {
        return new Vector2(x * DesignWidth / ReferenceWidth, y * DesignHeight / ReferenceHeight);
    }

    private float F(float fontSize)
    {
        return fontSize * DesignHeight / ReferenceHeight;
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
        // Design PNGs are references only; compose the runtime menu on the Blank.
        LoadSpriteToCache("GachaMenuDesignBlank.png", false);
        LoadSpriteToCache("GachaResultBlank.png", false);
        LoadSpriteToCache("GachaTitle.png", true);
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
        if (CoreArtworkCache.GetSprite(path, trimTransparent) is Sprite prepared) return prepared;
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
        Rect rect = trimTransparent ? MenuArtworkBounds.GetRect(AssetFolder + "/" + fileName, texture) : new Rect(0f, 0f, texture.width, texture.height);
        Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
        texture.Apply(false, true);
        runtimeSprites.Add(sprite);
        return sprite;
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
        SyncCurrencyFromProfile();
    }

    private void Save()
    {
        SyncCurrencyToProfile();
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saveData));
        PlayerPrefs.Save();
    }

    private void SyncCurrencyFromProfile()
    {
        PlayerProfileSaveData profile = PlayerProfileStore.Data;
        saveData.gold = profile.gold;
        saveData.diamonds = profile.diamonds;
        saveData.tickets = profile.tickets;
    }

    private void SyncCurrencyToProfile()
    {
        if (saveData == null)
            return;

        PlayerProfileStore.SetCurrency(saveData.gold, saveData.diamonds, saveData.tickets);
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
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), AssetFolder, fileName));
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

public sealed class GachaContentRootFitter : MonoBehaviour
{
    private RectTransform rectTransform;
    private float referenceWidth = 1920f;
    private float referenceHeight = 1080f;

    public void Configure(float width, float height)
    {
        referenceWidth = Mathf.Max(1f, width);
        referenceHeight = Mathf.Max(1f, height);
        Apply();
    }

    private void Awake()
    {
        rectTransform = transform as RectTransform;
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        Apply();
    }

    private void Apply()
    {
        if (!rectTransform)
            rectTransform = transform as RectTransform;
        if (!rectTransform)
            return;

        RectTransform parent = rectTransform.parent as RectTransform;
        if (!parent)
            return;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(referenceWidth, referenceHeight);

        float scale = Mathf.Min(parent.rect.width / referenceWidth, parent.rect.height / referenceHeight);
        if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale))
            scale = 1f;
        rectTransform.localScale = new Vector3(scale, scale, 1f);
    }
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
