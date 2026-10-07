using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UI = SketchbookUI;

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
    private RectTransform inventoryScreen;
    private RectTransform profileOverlay;
    private RectTransform settingsOverlay;
    private RectTransform botDifficultyScreen;
    private RectTransform sideScreen;
    private RectTransform skinScreen;
    private RectTransform transitionVeil;
    private CanvasGroup transitionVeilGroup;
    private RectTransform currentScreen;
    private Button modeLogoutButton;
    private ModeSelectionPresentation modePresentation;
    private GachaMenuController gachaController;
    private InventoryMenuController inventoryController;
    private PlayerProfileMenuController profileController;
    private SettingsMenuController settingsController;
    private Coroutine screenTransitionCoroutine;
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
        BuildInventoryScreen();
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
        if (!vsBot && !owner.RequireAccountAccess("Two-player mode")) return;
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

        var background = new GameObject("Main Menu Paper Surface", typeof(RectTransform), typeof(MainMenuPaperGraphic));
        background.transform.SetParent(root, false);
        Stretch((RectTransform)background.transform);
    }

    private void BuildMainScreen()
    {
        mainScreen = CreateScreen("Main Menu");
        mainScreen.gameObject.AddComponent<MainMenuPresentation>().Build(
            assets.mainMenuLogoOriginal,
            () => chessGame.OpenTurnSelection(), ShowSettingsMenu, () => owner.LogoutToAuthentication());
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
        modePresentation = screen.gameObject.AddComponent<ModeSelectionPresentation>();
        modePresentation.Build(assets.mainMenuLogoOriginal,
            new ModeSelectionPresentation.Actions
            {
                Local = () => owner.ShowLocalModeSelection(),
                Online = () => owner.ShowMultiplayerModeSelection(),
                Aram = CreateAuthenticatedAction("ARAM", owner.ShowAramModeSelection),
                Shop = CreateAuthenticatedAction("Shop", () => AuthNotificationView.Show("Shop coming soon",
                    "The shop is not open yet. Visit Inventory or Gacha to explore your collection.", AuthNotificationView.ResultKind.Info)),
                Inventory = CreateAuthenticatedAction("Inventory", ShowInventoryMenu),
                Gacha = CreateAuthenticatedAction("Gacha", ShowGachaMenu),
                Profile = CreateAuthenticatedAction("Player Profile", ShowProfileMenu),
                Settings = ShowSettingsMenu,
                Back = () => owner.ShowMainMenu(),
                Logout = () => owner.LogoutToAuthentication()
            });
        modeLogoutButton = modePresentation.LogoutButton;
    }

    private void BuildInventoryScreen()
    {
        inventoryScreen = CreateScreen("Inventory");
        inventoryController = inventoryScreen.gameObject.AddComponent<InventoryMenuController>();
        inventoryController.Initialize(inventoryScreen, ShowModeSelection);
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
        if (!inventoryScreen)
            return;

        SetVisible(true);
        SetInputEnabled(true);
        TransitionToScreen(inventoryScreen, () => inventoryController?.Open());
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

        SetModeScreenBackgroundInteractable(true);
    }

    private void ShowSettingsMenu()
    {
        if (!owner.RequireAccountAccess("Settings")) return;
        if (!settingsOverlay)
            return;

        SetVisible(true);
        SetInputEnabled(true);
        HideProfileOverlayImmediate();
        SetModeScreenBackgroundInteractable(false);
        settingsOverlay.SetAsLastSibling();
        settingsController?.Open();
        settingsOverlayTransitionCoroutine = StartOverlayTransition(settingsOverlay, settingsOverlayTransitionCoroutine, true);
    }

    private void CloseSettingsMenu()
    {
        if (settingsOverlay)
            settingsOverlayTransitionCoroutine = StartOverlayTransition(settingsOverlay, settingsOverlayTransitionCoroutine, false);
    }

    private void OpenProfileOverlay()
    {
        if (!profileOverlay)
            return;

        SetModeScreenBackgroundInteractable(false);
        profileOverlay.SetAsLastSibling();
        profileController?.Open();
        SetModeLogoutVisible(false);
        profileOverlayTransitionCoroutine = StartOverlayTransition(profileOverlay, profileOverlayTransitionCoroutine, true);
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

    private bool IsProfileOpen => profileOverlay && profileOverlay.gameObject.activeSelf;

    private void SetModeScreenBackgroundInteractable(bool interactable)
    {
        modePresentation?.SetInputEnabled(interactable && !IsProfileOpen &&
            !(settingsOverlay && settingsOverlay.gameObject.activeSelf));
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

        AddText(skinScreen, "Default Board", "Board: Tazji's Low Poly Arena",
            new Vector2(0f, 100f), new Vector2(760f, 80f), 40, TextAnchor.MiddleCenter,
            new Color(0.12f, 0.1f, 0.08f, 1f));
        AddText(skinScreen, "Default Chess Set", "Chess set: Tazji's Low Poly",
            new Vector2(0f, -60f), new Vector2(760f, 80f), 40, TextAnchor.MiddleCenter,
            new Color(0.12f, 0.1f, 0.08f, 1f));
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
        BotRosterView.Build(botDifficultyScreen, owner.SelectBotMatch, () => ShowModeSelection());
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
        GameObject buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(AntialiasedMenuImage), typeof(Button), typeof(Outline));
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

    private Image CreateImage(Transform parent, string imageName, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject imageObject = new GameObject(imageName, typeof(RectTransform), typeof(AntialiasedMenuImage));
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
        GameObject veilObject = new GameObject("Menu Transition Veil", typeof(RectTransform), typeof(CanvasGroup), typeof(AntialiasedMenuImage));
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
        if (!mainScreen || !modeScreen || !inventoryScreen || !localModeScreen || !aramModeScreen || !multiplayerModeScreen || !gachaScreen || !botDifficultyScreen || !sideScreen || !skinScreen || !activeScreen)
            return;

        if (activeScreen != modeScreen)
        {
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
        previousGroup.interactable = false;
        previousGroup.blocksRaycasts = false;
        activeGroup.alpha = 0f;
        activeGroup.interactable = false;
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
        if (!mainScreen || !modeScreen || !inventoryScreen || !localModeScreen || !aramModeScreen || !multiplayerModeScreen || !gachaScreen || !botDifficultyScreen || !sideScreen || !skinScreen || !activeScreen)
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
        if (inventoryScreen)
            inventoryScreen.gameObject.SetActive(active);
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
        return new[] { mainScreen, modeScreen, inventoryScreen, localModeScreen, aramModeScreen, multiplayerModeScreen, gachaScreen, botDifficultyScreen, sideScreen, skinScreen };
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

        if (overlay == profileOverlay)
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
        SetModeScreenBackgroundInteractable(true);
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
