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
    private readonly List<SkinOptionButton> skinOptionButtons = new List<SkinOptionButton>();
    private string selectedWhiteSkinId = PieceSkinCatalog.DefaultSkinId;
    private string selectedBlackSkinId = PieceSkinCatalog.DefaultSkinId;
    private bool skinSelectionVsBot;
    private int whiteSkinPage;
    private int blackSkinPage;
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
        whiteSkinPage = blackSkinPage = 0;

        SetVisible(true);
        SetInputEnabled(true);
        TransitionToScreen(skinScreen, RebuildSkinScreen);
    }

    public void ShowMultiplayerModeSelection()
    {
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

        Image background = CreateImage(root, "Paper Background", null, Vector2.zero, Vector2.zero);
        background.color = new Color(0.985f, 0.965f, 0.91f, 1f);
        Stretch(background.rectTransform);
    }

    private void BuildMainScreen()
    {
        mainScreen = CreateScreen("Main Menu");

        AddBattleDoodles(mainScreen, false);
        AddImage(mainScreen, "Doodle Face", assets.doodleFaceDecoration, new Vector2(-835f, 425f), new Vector2(225f, 178f), 1.2f, 0.45f);
        AddImage(mainScreen, "Game Logo", assets.gameLogo, new Vector2(0f, 345f), new Vector2(720f, 398f), 0.6f, 0.2f);
        AddInteractive(mainScreen, "Start", assets.startButton, new Vector2(0f, 42f), new Vector2(660f, 158f), () => chessGame.OpenTurnSelection(), false, 1.035f);
        AddInteractive(mainScreen, "Settings", assets.settingsButton, new Vector2(0f, -142f), new Vector2(660f, 148f), ShowSettingsMenu, false, 1.035f);
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

        AddBattleDoodles(aramModeScreen, true);
        AddMenuConfetti(aramModeScreen);
        AddText(aramModeScreen, "ARAM Mode Title", "ARAM Mode", new Vector2(0f, 340f), new Vector2(760f, 125f), 76, TextAnchor.MiddleCenter, new Color(0.12f, 0.1f, 0.08f, 1f));
        AddTextButton(aramModeScreen, "ARAM Practice", "Practice", new Vector2(-430f, 35f), new Vector2(470f, 180f), owner.StartAramPracticeGame, new Color(0.98f, 0.78f, 0.3f, 1f));
        AddTextButton(aramModeScreen, "ARAM LAN", "LAN", new Vector2(120f, 35f), new Vector2(430f, 160f), owner.ShowAramLanSetup, new Color(0.56f, 0.82f, 0.94f, 1f));
        AddTextButton(aramModeScreen, "ARAM Multiplayer", "Multiplayer", new Vector2(600f, 35f), new Vector2(430f, 160f), owner.ShowAramOnlineSetup, new Color(0.74f, 0.66f, 0.96f, 1f));
        AddText(aramModeScreen, "ARAM Practice Caption", "Practice local", new Vector2(-430f, -95f), new Vector2(470f, 48f), 30, TextAnchor.MiddleCenter, new Color(0.14f, 0.11f, 0.08f, 1f));
        AddText(aramModeScreen, "ARAM LAN Caption", "Private LAN room", new Vector2(120f, -95f), new Vector2(430f, 48f), 28, TextAnchor.MiddleCenter, new Color(0.14f, 0.11f, 0.08f, 1f));
        AddText(aramModeScreen, "ARAM Online Caption", "Online matchmaking", new Vector2(600f, -95f), new Vector2(430f, 48f), 28, TextAnchor.MiddleCenter, new Color(0.14f, 0.11f, 0.08f, 1f));
        AddBackButton(aramModeScreen, new Vector2(-820f, -420f), () => ShowModeSelection());
    }

    private void BuildPlayHub(RectTransform screen)
    {
        AddBattleDoodles(screen, true);
        AddMenuConfetti(screen);

        AddImage(screen, "Doodle Face", assets.doodleFaceDecoration, new Vector2(-840f, 430f), new Vector2(205f, 160f), 0.8f, 0.3f);
        AddImage(screen, "Game Slogan", assets.gameSlogan, new Vector2(-240f, 438f), new Vector2(755f, 98f), 0.2f, 0.08f);
        AddImage(screen, "Game Logo", assets.gameLogo, new Vector2(515f, 320f), new Vector2(540f, 285f), 0.4f, 0.12f);

        AddInteractive(screen, "Local Gameplay", assets.localGameplayButton, new Vector2(-660f, 270f), new Vector2(475f, 132f), () => owner.ShowLocalModeSelection(), false, 1.035f);
        AddInteractive(screen, "Online Play", assets.onlinePlayButton, new Vector2(-660f, 95f), new Vector2(475f, 132f), () => owner.ShowMultiplayerModeSelection(), false, 1.035f);
        AddInteractive(screen, "ARAM Mode", assets.aramModeButton, new Vector2(-660f, -80f), new Vector2(475f, 132f), CreateAuthenticatedAction("ARAM", owner.ShowAramModeSelection), false, 1.035f);
        AddInteractive(screen, "Shop", assets.shopButton, new Vector2(-660f, -255f), new Vector2(475f, 132f), CreateAuthenticatedAction("Shop", () => LogMenuClick("Shop")), false, 1.035f);

        AddInteractive(screen, "Inventory", assets.inventoryButton, new Vector2(805f, 130f), new Vector2(132f, 132f), CreateAuthenticatedAction("Inventory", ShowInventoryMenu), false, 1.045f);
        AddInteractive(screen, "Gacha", assets.gachaButton, new Vector2(805f, -50f), new Vector2(132f, 132f), CreateAuthenticatedAction("Gacha", ShowGachaMenu), false, 1.045f);
        AddInteractive(screen, "Player Profile", assets.playerProfile, new Vector2(805f, -230f), new Vector2(136f, 136f), CreateAuthenticatedAction("Player Profile", ShowProfileMenu), false, 1.045f);
        AddInteractive(screen, "Settings Icon", assets.settingsIcon, new Vector2(500f, -398f), new Vector2(118f, 118f), ShowSettingsMenu, false, 1.045f);
        AddImage(screen, "Game Version", assets.gameVersion, new Vector2(735f, -415f), new Vector2(320f, 142f), 0f, 0f);
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

        AddBattleDoodles(sideScreen, true);
        AddMenuConfetti(sideScreen);
        AddImage(sideScreen, "Choose Side Title", assets.chooseYourSide, new Vector2(0f, 365f), new Vector2(880f, 150f), 0f, 0f);
        AddInteractive(sideScreen, "White Card", assets.whiteSideButton, new Vector2(-365f, -105f), new Vector2(560f, 745f), () => owner.ShowBotSkinSelection(PieceTeam.White), false, 1.035f);
        AddInteractive(sideScreen, "Black Card", assets.blackSideButton, new Vector2(365f, -105f), new Vector2(560f, 745f), () => owner.ShowBotSkinSelection(PieceTeam.Black), false, 1.035f);
        AddImage(sideScreen, "Game Version", assets.gameVersion, new Vector2(735f, -420f), new Vector2(330f, 145f), 0f, 0f);
        AddBackButton(sideScreen, new Vector2(-820f, -455f), () => ShowBotDifficultySelection(), new Vector2(300f, 125f));
    }

    private void BuildSkinScreen()
    {
        skinScreen = CreateScreen("Piece Skin Select");
    }

    private void RebuildSkinScreen()
    {
        ClearSkinScreen();
        skinOptionButtons.Clear();

        AddBattleDoodles(skinScreen, true);
        AddMenuConfetti(skinScreen);
        AddText(skinScreen, "Skin Title", "Choose Piece Skin", new Vector2(0f, 410f), new Vector2(980f, 110f), 66, TextAnchor.MiddleCenter, new Color(0.12f, 0.1f, 0.08f, 1f));

        if (skinSelectionVsBot)
        {
            AddSkinSection(skinSelectionPlayerTeam, new Vector2(0f, 90f), $"{skinSelectionPlayerTeam} Player");
            AddTextButton(skinScreen, "Start Bot Match", "Start", new Vector2(0f, -410f), new Vector2(330f, 115f), StartSelectedSkinMatch, new Color(0.98f, 0.83f, 0.32f, 1f));
            AddBackButton(skinScreen, new Vector2(-820f, -420f), () => ShowSideSelection());
        }
        else
        {
            AddSkinSection(PieceTeam.White, new Vector2(-470f, 65f), "White Player");
            AddSkinSection(PieceTeam.Black, new Vector2(470f, 65f), "Black Player");
            AddTextButton(skinScreen, "Start Local Match", "Start", new Vector2(0f, -430f), new Vector2(330f, 105f), StartSelectedSkinMatch, new Color(0.98f, 0.83f, 0.32f, 1f));
            AddBackButton(skinScreen, new Vector2(-820f, -430f), () => ShowLocalModeSelection());
        }

        var selection = CosmeticSelection.Load();
        var catalog = LoadingManager.For(chessGame).Catalog;
        AddTextButton(skinScreen, "Board selection", "Board: " + (catalog?.FindBoard(selection.boardId)?.displayName ?? "Classic"),
            new Vector2(-260, -260), new Vector2(490, 65), () => CycleCosmetic(false), new Color(.86f, .88f, .95f));
        AddTextButton(skinScreen, "Environment selection", "Environment: " + (catalog?.FindEnvironment(selection.environmentId)?.displayName ?? "None"),
            new Vector2(260, -260), new Vector2(490, 65), () => CycleCosmetic(true), new Color(.86f, .88f, .95f));
        RefreshSkinOptionStates();
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
            Destroy(skinScreen.GetChild(i).gameObject);
    }

    private void AddSkinSection(PieceTeam team, Vector2 center, string title)
    {
        AddText(skinScreen, $"{team} Skin Section Title", title, center + new Vector2(0f, 215f), new Vector2(520f, 65f), 42, TextAnchor.MiddleCenter, new Color(0.12f, 0.1f, 0.08f, 1f));

        IReadOnlyList<PieceSkinDefinition> skins = PieceSkinCatalog.All;
        float startX = center.x - 135f;
        float startY = center.y + 108f;
        int pages = Mathf.Max(1, (skins.Count + 3) / 4);
        int page = (team == PieceTeam.White ? whiteSkinPage : blackSkinPage) % pages;
        for (int slot = 0; slot < 4 && page * 4 + slot < skins.Count; slot++)
        {
            int i = page * 4 + slot;
            int column = slot % 2;
            int row = slot / 2;
            Vector2 position = new Vector2(startX + column * 270f, startY - row * 105f);
            AddSkinOption(team, skins[i], position);
        }
        if (pages > 1)
            AddTextButton(skinScreen, team + " More skins", $"More ({page + 1}/{pages})", center + new Vector2(0, -125), new Vector2(240, 58), () =>
            {
                if (team == PieceTeam.White) whiteSkinPage++; else blackSkinPage++;
                RebuildSkinScreen();
            }, new Color(.88f, .88f, .9f));
    }

    private void AddSkinOption(PieceTeam team, PieceSkinDefinition skin, Vector2 position)
    {
        GameObject optionObject = new GameObject($"{team} {skin.DisplayName}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup), typeof(Outline));
        RectTransform rect = optionObject.GetComponent<RectTransform>();
        rect.SetParent(skinScreen, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(245f, 82f);

        Image background = optionObject.GetComponent<Image>();
        background.raycastTarget = true;

        Button button = optionObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;
        button.onClick.AddListener(() => SelectSkinOption(team, skin));

        Outline outline = optionObject.GetComponent<Outline>();
        outline.effectDistance = new Vector2(5f, -5f);

        Text label = AddText(rect, "Label", skin.DisplayName, Vector2.zero, rect.sizeDelta, 28, TextAnchor.MiddleCenter, Color.black);
        label.fontStyle = FontStyle.Bold;

        skinOptionButtons.Add(new SkinOptionButton
        {
            Team = team,
            Skin = skin,
            Button = button,
            Background = background,
            Label = label,
            CanvasGroup = optionObject.GetComponent<CanvasGroup>(),
            Outline = outline
        });
    }

    private void SelectSkinOption(PieceTeam team, PieceSkinDefinition skin)
    {
        SetSelectedSkinId(team, skin.Id);
        RefreshSkinOptionStates();
    }

    private void RefreshSkinOptionStates()
    {
        for (int i = 0; i < skinOptionButtons.Count; i++)
        {
            SkinOptionButton option = skinOptionButtons[i];
            bool selected = string.Equals(GetSelectedSkinId(option.Team), option.Skin.Id, System.StringComparison.OrdinalIgnoreCase);
            Color backgroundColor = option.Skin.SwatchColor;

            option.Background.color = backgroundColor;
            option.Label.color = GetReadableTextColor(backgroundColor);
            option.CanvasGroup.alpha = 1f;
            option.Outline.effectColor = selected ? new Color(1f, 0.72f, 0.08f, 1f) : new Color(0f, 0f, 0f, 0f);
            option.Button.interactable = true;
        }
    }

    private string GetSelectedSkinId(PieceTeam team)
    {
        return team == PieceTeam.White ? selectedWhiteSkinId : selectedBlackSkinId;
    }

    private void SetSelectedSkinId(PieceTeam team, string skinId)
    {
        if (team == PieceTeam.White)
            selectedWhiteSkinId = PieceSkinCatalog.NormalizeId(skinId);
        else
            selectedBlackSkinId = PieceSkinCatalog.NormalizeId(skinId);
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

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

    private sealed class SkinOptionButton
    {
        public PieceTeam Team;
        public PieceSkinDefinition Skin;
        public Button Button;
        public Image Background;
        public Text Label;
        public CanvasGroup CanvasGroup;
        public Outline Outline;
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
        AddFullImage(mainScreen, "Gacha Menu Design", GetSprite("GachaMenuDesign.png"));
        AddResourceBars(mainScreen);
        AddButton(mainScreen, "Back Gacha", GetSprite("BackGacha.png"), D(230f, 76f), S(90f, 90f), BackToModeSelection, 1.04f);

        AddPityPanel(mainScreen);
        AddRewardButton(mainScreen, "Gold Reward", D(220f, 792f), S(122f, 122f), "Gold rewards: 100, 250, 500, 1,000, or jackpot 4,500 gold.");
        AddRewardButton(mainScreen, "Diamond Reward", D(360f, 792f), S(122f, 122f), "Diamond rewards: 10, 30, 80, 120, or jackpot 1,200 diamonds.");
        AddRewardButton(mainScreen, "Ticket Reward", D(501f, 792f), S(122f, 122f), "Ticket rewards: 1, 2, 5, or jackpot 10 summon tickets.");
        AddRewardButton(mainScreen, "Skin Reward", D(641f, 792f), S(122f, 122f), "5-star skin reward rate is reserved for a later skin pool. Skin rolls are disabled for now.");

        AddInvisibleButton(mainScreen, "Summon x1", D(966f, 707f), S(315f, 115f), () => StartSummon(1));
        AddInvisibleButton(mainScreen, "Summon x10", D(1322f, 708f), S(315f, 115f), () => StartSummon(10));
        AddInvisibleButton(mainScreen, "History", D(955f, 861f), S(260f, 72f), ShowHistory);

        statusLabel = AddText(mainScreen, "Gacha Status", D(1145f, 858f), S(760f, 48f), F(28f), TextAlignmentOptions.Center, new Color(0.16f, 0.13f, 0.1f, 1f));
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
        RectTransform gold = AddImage(parent, "Gold Counter", GetSprite("GoldGacha.png"), D(654f, 74f), S(335f, 92f)).rectTransform;
        RectTransform diamond = AddImage(parent, "Diamond Counter", GetSprite("DiamondGacha.png"), D(976f, 74f), S(335f, 92f)).rectTransform;
        RectTransform ticket = AddImage(parent, "Ticket Counter", GetSprite("SummonTicket.png"), D(1257f, 74f), S(260f, 92f)).rectTransform;
        goldLabels.Add(AddText(gold, "Gold Amount", L(42f, 0f), S(178f, 54f), F(39f), TextAlignmentOptions.Center, Color.black));
        diamondLabels.Add(AddText(diamond, "Diamond Amount", L(45f, 0f), S(178f, 54f), F(39f), TextAlignmentOptions.Center, Color.black));
        ticketLabels.Add(AddText(ticket, "Ticket Amount", L(42f, 0f), S(95f, 54f), F(39f), TextAlignmentOptions.Center, Color.black));
    }

    private void AddPityPanel(RectTransform parent)
    {
        RectTransform pity = AddImage(parent, "Pity Panel", GetSprite("PityGacha.png"), D(275f, 575f), S(360f, 205f)).rectTransform;
        pityLabel = AddText(pity, "Pity Text", L(74f, 54f), S(105f, 42f), F(35f), TextAlignmentOptions.Center, Color.black);
        Image fill = CreatePlainImage(pity, "Pity Fill", new Color(1f, 0.78f, 0.08f, 1f), L(-106f, 7f), S(1f, 26f));
        pityFill = fill.rectTransform;
        pityFill.pivot = new Vector2(0f, 0.5f);
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
            pityFill.sizeDelta = S(Mathf.Lerp(26f, 265f, saveData.pityPulls / (float)PityLimit), 26f);
    }

    private void SetStatus(string message)
    {
        if (statusLabel)
            statusLabel.text = message;
    }

    private Button AddRewardButton(RectTransform parent, string name, Vector2 position, Vector2 size, string tooltip)
    {
        Button button = AddInvisibleButton(parent, name, position, size, () => ShowTooltip(tooltip));
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
        LoadSpriteToCache("GachaMenuDesign.png", false);
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
        if (CoreArtworkCache.GetSprite(path) is Sprite prepared) return prepared;
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
        texture.Apply(false, true);
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
