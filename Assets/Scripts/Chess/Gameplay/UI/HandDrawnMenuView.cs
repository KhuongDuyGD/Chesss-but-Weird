using System.Collections.Generic;
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
    private GraphicRaycaster raycaster;
    private RectTransform mainScreen;
    private RectTransform modeScreen;
    private RectTransform localModeScreen;
    private RectTransform multiplayerModeScreen;
    private RectTransform gachaScreen;
    private RectTransform botDifficultyScreen;
    private RectTransform sideScreen;
    private RectTransform skinScreen;
    private Text skinMessageText;
    private readonly List<Sprite> runtimeSprites = new List<Sprite>();
    private readonly List<SkinOptionButton> skinOptionButtons = new List<SkinOptionButton>();
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
        BuildMultiplayerModeScreen();
        BuildGachaScreen();
        BuildBotDifficultyScreen();
        BuildSideScreen();
        BuildSkinScreen();
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

    public void ShowLocalModeSelection()
    {
        SetVisible(true);
        SetInputEnabled(true);
        SetScreen(localModeScreen);
    }

    public void ShowPieceSkinSelection(bool vsBot, PieceTeam playerTeam)
    {
        skinSelectionVsBot = vsBot;
        skinSelectionPlayerTeam = playerTeam;
        selectedWhiteSkinId = chessGame ? chessGame.GetPieceSkinId(PieceTeam.White) : PieceSkinCatalog.DefaultSkinId;
        selectedBlackSkinId = chessGame ? chessGame.GetPieceSkinId(PieceTeam.Black) : PieceSkinCatalog.DefaultSkinId;

        if (vsBot)
        {
            selectedWhiteSkinId = PieceSkinCatalog.DefaultSkinId;
            selectedBlackSkinId = PieceSkinCatalog.DefaultSkinId;
            SetSelectedSkinId(playerTeam, PieceSkinCatalog.DefaultSkinId);
        }

        RebuildSkinScreen();
        SetVisible(true);
        SetInputEnabled(true);
        SetScreen(skinScreen);
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

    private void BuildPlayHub(RectTransform screen)
    {
        AddBattleDoodles(screen, true);
        AddMenuConfetti(screen);

        AddImage(screen, "Doodle Face", assets.doodleFaceDecoration, new Vector2(-840f, 430f), new Vector2(205f, 160f), 0.8f, 0.3f);
        AddImage(screen, "Game Slogan", assets.gameSlogan, new Vector2(-240f, 438f), new Vector2(755f, 98f), 0.2f, 0.08f);
        AddImage(screen, "Game Logo", assets.gameLogo, new Vector2(515f, 320f), new Vector2(540f, 285f), 0.4f, 0.12f);

        AddInteractive(screen, "Local Gameplay", assets.localGameplayButton, new Vector2(-660f, 270f), new Vector2(475f, 132f), () => owner.ShowLocalModeSelection(), false, 1.035f);
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

        if (!assets || !assets.gachaBackground)
            return;

        Image background = CreateImage(
            gachaScreen,
            "Gacha Background",
            assets.gachaBackground,
            Vector2.zero,
            new Vector2(ReferenceWidth, ReferenceHeight));
        background.preserveAspect = false;

        AddImage(gachaScreen, "Gacha Title 1", assets.gachaTitle1, new Vector2(0f, 415f), new Vector2(840f, 154f), 0f, 0f);
        AddImage(gachaScreen, "Gacha Title 2", assets.gachaTitle2, new Vector2(0f, 295f), new Vector2(1008f, 231f), 0f, 0f);
        AddImage(gachaScreen, "Coins Amount", assets.gachaCoinsAmount, new Vector2(690f, 418f), new Vector2(360f, 150f), 0f, 0f);

        AddImage(gachaScreen, "Standard Banner", assets.gachaStandardBanner, new Vector2(-625f, 130f), new Vector2(377f, 162f), 0f, 0f);
        AddImage(gachaScreen, "Details", assets.gachaDetails, new Vector2(-250f, -360f), new Vector2(315f, 120f), 0f, 0f);

        AddImage(gachaScreen, "Gacha Footer", assets.gachaTitle3, new Vector2(0f, -435f), new Vector2(700f, 140f), 0f, 0f);
        AddGachaBackButton(gachaScreen, new Vector2(-760f, -430f), () => ShowModeSelection());
    }

    private void BuildSideScreen()
    {
        sideScreen = CreateScreen("Choose Side");

        AddBattleDoodles(sideScreen, true);
        AddMenuConfetti(sideScreen);
        AddImage(sideScreen, "Choose Side Title", assets.chooseYourSide, new Vector2(0f, 365f), new Vector2(880f, 150f), 0f, 0f);
        AddInteractive(sideScreen, "White Card", assets.whiteSideButton, new Vector2(-365f, -105f), new Vector2(560f, 745f), () => owner.ShowBotSkinSelection(PieceTeam.White), false, 1.035f);
        AddInteractive(sideScreen, "Black Card", assets.blackSideButton, new Vector2(365f, -105f), new Vector2(560f, 745f), () => owner.ShowBotSkinSelection(PieceTeam.Black), false, 1.035f);
        AddImage(sideScreen, "Settings Icon", assets.settingsIcon, new Vector2(-760f, -410f), new Vector2(120f, 120f), 0f, 0f);
        AddImage(sideScreen, "Game Version", assets.gameVersion, new Vector2(735f, -420f), new Vector2(330f, 145f), 0f, 0f);
        AddBackButton(sideScreen, new Vector2(-600f, -420f), () => ShowBotDifficultySelection());
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

        skinMessageText = AddText(
            skinScreen,
            "Skin Message",
            string.Empty,
            new Vector2(0f, -325f),
            new Vector2(1250f, 58f),
            34,
            TextAnchor.MiddleCenter,
            new Color(0.78f, 0.12f, 0.08f, 1f));

        RefreshSkinOptionStates();
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
        for (int i = 0; i < skins.Count; i++)
        {
            int column = i % 2;
            int row = i / 2;
            Vector2 position = new Vector2(startX + column * 270f, startY - row * 105f);
            AddSkinOption(team, skins[i], position);
        }
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
        if (IsSkinLockedForTeam(team, skin.Id))
        {
            if (skinMessageText)
                skinMessageText.text = "Skin n\u00e0y \u0111\u00e3 \u0111\u01b0\u1ee3c ng\u01b0\u1eddi ch\u01a1i c\u00f2n l\u1ea1i ch\u1ecdn.";
            return;
        }

        SetSelectedSkinId(team, skin.Id);
        if (skinMessageText)
            skinMessageText.text = string.Empty;
        RefreshSkinOptionStates();
    }

    private void RefreshSkinOptionStates()
    {
        for (int i = 0; i < skinOptionButtons.Count; i++)
        {
            SkinOptionButton option = skinOptionButtons[i];
            bool selected = string.Equals(GetSelectedSkinId(option.Team), option.Skin.Id, System.StringComparison.OrdinalIgnoreCase);
            bool locked = IsSkinLockedForTeam(option.Team, option.Skin.Id);
            Color backgroundColor = option.Skin.SwatchColor;

            option.Background.color = locked ? new Color(0.42f, 0.42f, 0.42f, 0.8f) : backgroundColor;
            option.Label.color = locked ? new Color(0.8f, 0.8f, 0.8f, 1f) : GetReadableTextColor(backgroundColor);
            option.CanvasGroup.alpha = locked ? 0.48f : 1f;
            option.Outline.effectColor = selected ? new Color(1f, 0.72f, 0.08f, 1f) : new Color(0f, 0f, 0f, 0f);
            option.Button.interactable = true;
        }
    }

    private bool IsSkinLockedForTeam(PieceTeam team, string skinId)
    {
        if (PieceSkinCatalog.IsDefault(skinId))
            return false;

        PieceTeam otherTeam = team == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        return string.Equals(GetSelectedSkinId(otherTeam), skinId, System.StringComparison.OrdinalIgnoreCase);
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
            wiggle.Configure(wigglePosition, wiggleRotation, 0.01f, Random.Range(0.08f, 0.14f));
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
        if (!mainScreen || !modeScreen || !localModeScreen || !multiplayerModeScreen || !gachaScreen || !botDifficultyScreen || !sideScreen || !skinScreen || !activeScreen)
            return;

        mainScreen.gameObject.SetActive(activeScreen == mainScreen);
        modeScreen.gameObject.SetActive(activeScreen == modeScreen);
        localModeScreen.gameObject.SetActive(activeScreen == localModeScreen);
        multiplayerModeScreen.gameObject.SetActive(activeScreen == multiplayerModeScreen);
        gachaScreen.gameObject.SetActive(activeScreen == gachaScreen);
        botDifficultyScreen.gameObject.SetActive(activeScreen == botDifficultyScreen);
        sideScreen.gameObject.SetActive(activeScreen == sideScreen);
        skinScreen.gameObject.SetActive(activeScreen == skinScreen);
    }

    private void SetAllScreensActive(bool active)
    {
        if (mainScreen)
            mainScreen.gameObject.SetActive(active);
        if (modeScreen)
            modeScreen.gameObject.SetActive(active);
        if (localModeScreen)
            localModeScreen.gameObject.SetActive(active);
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
