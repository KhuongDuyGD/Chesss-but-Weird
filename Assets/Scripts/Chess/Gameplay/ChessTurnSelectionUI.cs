using System;
using UnityEngine;

public class ChessTurnSelectionUI : MonoBehaviour
{
    private enum ScreenState
    {
        MainMenu,
        TransitionToTurnSelection,
        TurnSelection,
        Playing,
        Promotion,
        GameOver,
        Spectating
    }

    private const float TransitionDuration = 0.45f;
    private const float UiReferenceWidth = 1920f;
    private const float UiReferenceHeight = 1080f;

    private ChessGame chessGame;
    private PieceTeam currentTurn;
    private PieceTeam winningTeam;
    private PieceTeam playerTeam;
    private PieceTeam checkedTeam;
    private PieceTeam promotionTeam;
    private bool resultIsDraw;
    private string drawReason;
    private Action<PieceType> promotionCallback;
    private ScreenState state = ScreenState.MainMenu;
    private float transitionStartTime;
    private bool showCheckWarning;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle turnLabelStyle;
    private GUIStyle checkWarningStyle;
    private GUIStyle resultStyle;
    private HandDrawnMenuView handDrawnMenu;
    private ChessLanController lanController;
    private ChessPauseMenu pauseMenu;
    private AnalysisBoardView analysisBoard;
    private ResultMenuView resultMenu;
    private StockfishBotController botController;
    private StockfishDifficulty selectedBotDifficulty = StockfishDifficulty.Medium;
    private string whitePlayerName;
    private string blackPlayerName;
    private float uiWidth = UiReferenceWidth;
    private float uiHeight = UiReferenceHeight;

    public static ChessTurnSelectionUI Create(ChessGame chessGame)
    {
        GameObject root = new GameObject("Chess Turn Selection UI", typeof(RectTransform));
        ChessTurnSelectionUI ui = root.AddComponent<ChessTurnSelectionUI>();
        ui.chessGame = chessGame;
        ui.TryCreateHandDrawnMenu();
        ui.TryCreateLanController();
        ui.TryCreatePauseMenu();
        ui.TryCreateAnalysisBoard();
        ui.TryCreateResultMenu();
        ui.TryCreateBotController();
        GameMusicManager.PlayMainMenuPrimaryMusic();
        return ui;
    }

    public void SetTurn(PieceTeam newCurrentTurn)
    {
        currentTurn = newCurrentTurn;
        promotionCallback = null;
        resultIsDraw = false;
        drawReason = string.Empty;
        showCheckWarning = false;
        state = ScreenState.Playing;
        handDrawnMenu?.HideForPlaying();
        resultMenu?.Hide();
        pauseMenu?.SetResultSpectating(false);
        analysisBoard?.SetVisible(true);
        analysisBoard?.SetInteractionEnabled(true);
    }

    public void ShowCheckWarning(PieceTeam newCheckedTeam)
    {
        checkedTeam = newCheckedTeam;
        showCheckWarning = true;
    }

    public void HideCheckWarning()
    {
        showCheckWarning = false;
    }

    public void ShowPromotionChoice(PieceTeam newPromotionTeam, Action<PieceType> onPromotionSelected)
    {
        promotionTeam = newPromotionTeam;
        promotionCallback = onPromotionSelected;
        showCheckWarning = false;
        state = ScreenState.Promotion;
    }

    public void ShowMainMenu()
    {
        GameMusicManager.PlayMainMenuPrimaryMusic();
        promotionCallback = null;
        resultIsDraw = false;
        drawReason = string.Empty;
        showCheckWarning = false;
        state = ScreenState.MainMenu;
        lanController?.HideLanSetup();
        handDrawnMenu?.ShowMainMenu();
        resultMenu?.Hide();
        pauseMenu?.SetResultSpectating(false);
        analysisBoard?.SetVisible(false);
        analysisBoard?.SetInteractionEnabled(true);
        ClearMatchPlayers();
    }

    public void ShowTurnSelection()
    {
        GameMusicManager.PlayMainMenuHubMusic();
        showCheckWarning = false;
        lanController?.HideLanSetup();
        if (handDrawnMenu && handDrawnMenu.IsReady)
        {
            state = ScreenState.TransitionToTurnSelection;
            handDrawnMenu.ShowModeSelection();
            return;
        }

        state = ScreenState.TransitionToTurnSelection;
        analysisBoard?.SetVisible(false);
        transitionStartTime = Time.realtimeSinceStartup;
    }

    public void ShowSideSelection()
    {
        GameMusicManager.PlayMainMenuHubMusic();
        showCheckWarning = false;
        lanController?.HideLanSetup();
        state = ScreenState.TurnSelection;
        analysisBoard?.SetVisible(false);
        handDrawnMenu?.ShowSideSelection();
    }

    public void ShowBotDifficultySelection()
    {
        GameMusicManager.PlayMainMenuHubMusic();
        showCheckWarning = false;
        lanController?.HideLanSetup();
        state = ScreenState.TurnSelection;
        analysisBoard?.SetVisible(false);
        handDrawnMenu?.ShowBotDifficultySelection();
    }

    public void ShowLocalModeSelection()
    {
        showCheckWarning = false;
        lanController?.HideLanSetup();
        state = ScreenState.TurnSelection;
        analysisBoard?.SetVisible(false);
        handDrawnMenu?.ShowLocalModeSelection();
    }

    public void ShowAramModeSelection()
    {
        showCheckWarning = false;
        lanController?.HideLanSetup();
        state = ScreenState.TurnSelection;
        analysisBoard?.SetVisible(false);
        handDrawnMenu?.ShowAramModeSelection();
    }

    public void SelectBotDifficulty(StockfishDifficulty difficulty)
    {
        selectedBotDifficulty = difficulty;
        ShowSideSelection();
    }

    public void ShowBotSkinSelection(PieceTeam selectedPlayerTeam)
    {
        showCheckWarning = false;
        lanController?.HideLanSetup();
        state = ScreenState.TurnSelection;
        analysisBoard?.SetVisible(false);
        handDrawnMenu?.ShowPieceSkinSelection(true, selectedPlayerTeam);
    }

    public void ShowTwoPlayerSkinSelection()
    {
        showCheckWarning = false;
        lanController?.HideLanSetup();
        state = ScreenState.TurnSelection;
        analysisBoard?.SetVisible(false);
        handDrawnMenu?.ShowPieceSkinSelection(false, PieceTeam.White);
    }

    public void StartBotGame(PieceTeam playerTeam)
    {
        chessGame.ConfigurePieceSkinsForBot(playerTeam, PieceSkinCatalog.DefaultSkinId);
        StartBotGameWithConfiguredSkins(playerTeam);
    }

    public void StartBotGameWithSkin(PieceTeam playerTeam, string playerSkinId)
    {
        chessGame.ConfigurePieceSkinsForBot(playerTeam, playerSkinId);
        StartBotGameWithConfiguredSkins(playerTeam);
    }

    public void StartLocalTwoPlayerGameWithSkins(string whiteSkinId, string blackSkinId)
    {
        chessGame.ConfigurePieceSkinsForLocalPlayers(whiteSkinId, blackSkinId);
        chessGame.BeginGame(PieceTeam.White);
    }

    public void StartAramPracticeGame()
    {
        showCheckWarning = false;
        lanController?.HideLanSetup();
        analysisBoard?.SetVisible(true);
        analysisBoard?.SetInteractionEnabled(true);
        chessGame.ConfigurePieceSkinsForLocalPlayers(PieceSkinCatalog.DefaultSkinId, PieceSkinCatalog.DefaultSkinId);
        chessGame.BeginAramGame();
    }

    public void ShowAramLanSetup()
    {
        GameMusicManager.PlayMainMenuHubMusic();
        showCheckWarning = false;
        state = ScreenState.TurnSelection;
        handDrawnMenu?.HideForPlaying();
        analysisBoard?.SetVisible(false);
        lanController?.ShowAramLanSetup();
    }

    public void ShowAramOnlineSetup()
    {
        if (!PlayerAuthService.CanUseOnlineFeatures)
        {
            RequestAuthentication("ARAM online play requires a valid backend login. Please log in or sign up.");
            return;
        }

        GameMusicManager.PlayMainMenuHubMusic();
        showCheckWarning = false;
        state = ScreenState.TurnSelection;
        handDrawnMenu?.HideForPlaying();
        analysisBoard?.SetVisible(false);
        lanController?.ShowAramMultiplayerSetup();
    }

    private void StartBotGameWithConfiguredSkins(PieceTeam playerTeam)
    {
        if (botController)
            botController.StartBotGame(playerTeam, selectedBotDifficulty);
        else
            chessGame.BeginGame(playerTeam);
    }

    public void ShowMultiplayerModeSelection()
    {
        GameMusicManager.PlayMainMenuHubMusic();
        if (!PlayerAuthService.CanUseOnlineFeatures)
        {
            RequestAuthentication("Multiplayer requires a valid backend login. Please log in or sign up.");
            return;
        }

        showCheckWarning = false;
        lanController?.HideLanSetup();
        state = ScreenState.TurnSelection;
        analysisBoard?.SetVisible(false);
        handDrawnMenu?.ShowMultiplayerModeSelection();
    }

    public void ShowLanSetup()
    {
        GameMusicManager.PlayMainMenuHubMusic();
        showCheckWarning = false;
        state = ScreenState.TurnSelection;
        handDrawnMenu?.HideForPlaying();
        analysisBoard?.SetVisible(false);
        lanController?.ShowLanSetup();
    }

    public void ShowOnlineSetup()
    {
        if (!PlayerAuthService.CanUseOnlineFeatures)
        {
            RequestAuthentication("Online play requires a valid backend login. Please log in or sign up.");
            return;
        }

        GameMusicManager.PlayMainMenuHubMusic();
        showCheckWarning = false;
        state = ScreenState.TurnSelection;
        handDrawnMenu?.HideForPlaying();
        analysisBoard?.SetVisible(false);
        lanController?.ShowMultiplayerSetup();
    }

    public void RequestAuthentication(string message)
    {
        LogoutToAuthentication();
        Debug.Log($"[ChessTurnSelectionUI] Authentication requested. {message}");
    }

    public void LogoutToAuthentication()
    {
        lanController?.ResetForAuthenticationChange();
        PlayerAuthService.Logout();
        lanController?.HideLanSetup();
        analysisBoard?.SetVisible(false);
        handDrawnMenu?.HideForPlaying();
        AuthController.Create(chessGame, HandleAuthenticationCompleted);
        Debug.Log("[ChessTurnSelectionUI] Session cleared. Returning to login/sign-up.");
    }

    private void HandleAuthenticationCompleted()
    {
        // Rebuild the multiplayer state after the account identity changes so a
        // previous Guest socket/room can never leak into the new login session.
        lanController?.ResetForAuthenticationChange(PlayerAuthService.CanUseOnlineFeatures);
        ShowMainMenu();
    }

    public void ShowGameOver(PieceTeam winner, PieceTeam selectedPlayerTeam)
    {
        promotionCallback = null;
        showCheckWarning = false;
        resultIsDraw = false;
        drawReason = string.Empty;
        winningTeam = winner;
        playerTeam = selectedPlayerTeam;
        lanController?.HideLanSetup();
        state = ScreenState.GameOver;
        handDrawnMenu?.HideForPlaying();
        analysisBoard?.SetVisible(true);
        analysisBoard?.SetInteractionEnabled(true);
        pauseMenu?.SetResultSpectating(false);
        resultMenu?.Show(winningTeam == playerTeam ? ResultMenuView.ResultKind.Win : ResultMenuView.ResultKind.Lose);
    }

    public void ShowDraw(PieceTeam selectedPlayerTeam, string reason)
    {
        promotionCallback = null;
        showCheckWarning = false;
        resultIsDraw = true;
        drawReason = string.IsNullOrWhiteSpace(reason) ? "Draw" : reason;
        playerTeam = selectedPlayerTeam;
        lanController?.HideLanSetup();
        state = ScreenState.GameOver;
        handDrawnMenu?.HideForPlaying();
        analysisBoard?.SetVisible(true);
        analysisBoard?.SetInteractionEnabled(true);
        pauseMenu?.SetResultSpectating(false);
        ResultMenuView.ResultKind kind = drawReason.IndexOf("stalemate", StringComparison.OrdinalIgnoreCase) >= 0
            ? ResultMenuView.ResultKind.Stalemate
            : ResultMenuView.ResultKind.Draw;
        resultMenu?.Show(kind);
    }

    public void SpectateFinishedGame()
    {
        state = ScreenState.Spectating;
        resultMenu?.Hide();
        analysisBoard?.SetVisible(true);
        analysisBoard?.SetInteractionEnabled(false);
        pauseMenu?.SetResultSpectating(true);
    }

    public void StartNewGameFromResult()
    {
        GameMusicManager.PlayInGameMusic(chessGame && chessGame.IsBotGame, selectedBotDifficulty);
        resultMenu?.Hide();
        pauseMenu?.SetResultSpectating(false);
        if (!chessGame.RestartCurrentLocalGame())
            chessGame.RestartToMainMenu();
    }

    public void ReturnToMainMenuFromResult()
    {
        resultMenu?.Hide();
        pauseMenu?.SetResultSpectating(false);
        chessGame.RestartToMainMenu();
    }

    public void SetMatchPlayers(string whiteName, string blackName)
    {
        whitePlayerName = string.IsNullOrWhiteSpace(whiteName) ? "White" : whiteName.Trim();
        blackPlayerName = string.IsNullOrWhiteSpace(blackName) ? "Black" : blackName.Trim();
    }

    public void ClearMatchPlayers()
    {
        whitePlayerName = null;
        blackPlayerName = null;
    }

    public void SetLatestMoveText(string moveText, bool replaceLatestHistory = false)
    {
        chessGame?.SetExternalLastMoveSummary(moveText, replaceLatestHistory);
    }

    private void OnGUI()
    {
        EnsureStyles();

        Matrix4x4 previousMatrix = GUI.matrix;
        float scale = ResponsiveUi.GetFitScale(UiReferenceWidth, UiReferenceHeight);
        uiWidth = Screen.width / scale;
        uiHeight = Screen.height / scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        try
        {
            DrawResponsiveGui();
        }
        finally
        {
            GUI.matrix = previousMatrix;
        }
    }

    private void DrawResponsiveGui()
    {

        if (resultMenu && resultMenu.IsReady && (state == ScreenState.GameOver || state == ScreenState.Spectating))
            return;

        if (handDrawnMenu && handDrawnMenu.IsReady &&
            (state == ScreenState.MainMenu || state == ScreenState.TransitionToTurnSelection || state == ScreenState.TurnSelection))
            return;

        if (state == ScreenState.Playing)
        {
            if (showCheckWarning)
                GUI.Label(new Rect(28f, 96f, 760f, 78f), $"{checkedTeam} king is in CHECK", checkWarningStyle);
        }
        else if (state == ScreenState.GameOver)
        {
            GUI.Label(new Rect(28f, 24f, 680f, 72f), $"You played: {playerTeam}", turnLabelStyle);
        }

        switch (state)
        {
            case ScreenState.MainMenu:
                DrawMainMenu();
                break;
            case ScreenState.TransitionToTurnSelection:
                DrawTransition();
                break;
            case ScreenState.TurnSelection:
                DrawTurnSelection();
                break;
            case ScreenState.Promotion:
                DrawPromotion();
                break;
            case ScreenState.GameOver:
                DrawGameOver();
                break;
        }
    }

    private void DrawMainMenu()
    {
        DrawDimBackground(0.78f);

        float panelWidth = Mathf.Clamp(uiWidth * 0.60f, 840f, 1240f);
        float panelHeight = Mathf.Clamp(uiHeight * 0.48f, 500f, 660f);
        Rect panelRect = GetCenteredRect(panelWidth, panelHeight);

        GUI.Box(panelRect, string.Empty);
        GUI.Label(new Rect(panelRect.x, panelRect.y + 58f, panelRect.width, 96f), "Chess but Weird", titleStyle);
        GUI.Label(new Rect(panelRect.x + 72f, panelRect.y + 164f, panelRect.width - 144f, 126f),
            "Classic chess foundation with room for weird variants.", subtitleStyle);

        float buttonWidth = Mathf.Clamp(panelWidth * 0.34f, 280f, 380f);
        float buttonHeight = Mathf.Clamp(panelHeight * 0.20f, 86f, 110f);
        Rect playButton = new Rect(
            panelRect.x + (panelRect.width - buttonWidth) * 0.5f,
            panelRect.y + panelRect.height * 0.68f,
            buttonWidth,
            buttonHeight);

        if (GUI.Button(playButton, "Play", buttonStyle))
            chessGame.OpenTurnSelection();
    }

    private void DrawTransition()
    {
        float elapsed = Time.realtimeSinceStartup - transitionStartTime;
        float progress = Mathf.Clamp01(elapsed / TransitionDuration);
        DrawDimBackground(0.78f * (1f - progress));

        if (progress >= 1f)
            state = ScreenState.TurnSelection;
    }

    private void DrawTurnSelection()
    {
        DrawDimBackground(0.35f);

        float panelWidth = Mathf.Clamp(uiWidth * 0.48f, 760f, 1040f);
        float panelHeight = Mathf.Clamp(uiHeight * 0.34f, 380f, 520f);
        Rect panelRect = GetCenteredRect(panelWidth, panelHeight);

        GUI.Box(panelRect, string.Empty);
        GUI.Label(new Rect(panelRect.x, panelRect.y + 58f, panelRect.width, 78f), "Choose first turn", titleStyle);

        float buttonWidth = Mathf.Clamp(panelWidth * 0.32f, 250f, 340f);
        float buttonHeight = Mathf.Clamp(panelHeight * 0.24f, 92f, 120f);
        float buttonGap = Mathf.Clamp(panelWidth * 0.08f, 64f, 96f);
        float buttonY = panelRect.y + panelHeight * 0.58f;
        float firstButtonX = panelRect.x + (panelWidth - buttonWidth * 2f - buttonGap) * 0.5f;
        Rect whiteButton = new Rect(firstButtonX, buttonY, buttonWidth, buttonHeight);
        Rect blackButton = new Rect(firstButtonX + buttonWidth + buttonGap, buttonY, buttonWidth, buttonHeight);

        if (GUI.Button(whiteButton, "White", buttonStyle))
            StartBotGame(PieceTeam.White);

        if (GUI.Button(blackButton, "Black", buttonStyle))
            StartBotGame(PieceTeam.Black);
    }

    private void DrawPromotion()
    {
        DrawDimBackground(0.48f);

        float panelWidth = Mathf.Clamp(uiWidth * 0.56f, 820f, 1160f);
        float panelHeight = Mathf.Clamp(uiHeight * 0.34f, 380f, 500f);
        Rect panelRect = GetCenteredRect(panelWidth, panelHeight);

        GUI.Box(panelRect, string.Empty);
        GUI.Label(new Rect(panelRect.x, panelRect.y + 48f, panelRect.width, 76f), $"{promotionTeam} pawn promotion", titleStyle);

        float buttonWidth = Mathf.Clamp(panelWidth * 0.19f, 150f, 210f);
        float buttonHeight = Mathf.Clamp(panelHeight * 0.22f, 82f, 108f);
        float gap = Mathf.Clamp(panelWidth * 0.035f, 26f, 42f);
        float totalWidth = buttonWidth * 4f + gap * 3f;
        float buttonX = panelRect.x + (panelRect.width - totalWidth) * 0.5f;
        float buttonY = panelRect.y + panelRect.height * 0.56f;

        DrawPromotionButton(new Rect(buttonX, buttonY, buttonWidth, buttonHeight), "Queen", PieceType.Queen);
        DrawPromotionButton(new Rect(buttonX + (buttonWidth + gap), buttonY, buttonWidth, buttonHeight), "Rook", PieceType.Rook);
        DrawPromotionButton(new Rect(buttonX + (buttonWidth + gap) * 2f, buttonY, buttonWidth, buttonHeight), "Bishop", PieceType.Bishop);
        DrawPromotionButton(new Rect(buttonX + (buttonWidth + gap) * 3f, buttonY, buttonWidth, buttonHeight), "Knight", PieceType.Knight);
    }

    private void DrawPromotionButton(Rect rect, string label, PieceType pieceType)
    {
        if (!GUI.Button(rect, label, buttonStyle))
            return;

        Action<PieceType> callback = promotionCallback;
        promotionCallback = null;
        callback?.Invoke(pieceType);
    }

    private void DrawGameOver()
    {
        DrawDimBackground(0.62f);

        float panelWidth = Mathf.Clamp(uiWidth * 0.48f, 720f, 980f);
        float panelHeight = Mathf.Clamp(uiHeight * 0.34f, 380f, 500f);
        Rect panelRect = GetCenteredRect(panelWidth, panelHeight);

        GUI.Box(panelRect, string.Empty);
        string title = resultIsDraw ? "Match Drawn!" : "Game Over";
        string result = resultIsDraw ? "Draw" : winningTeam == playerTeam ? "You Win" : "You Lose";
        string detail = resultIsDraw ? $"Reason: {drawReason}" : $"{winningTeam} wins";

        GUI.Label(new Rect(panelRect.x, panelRect.y + 56f, panelRect.width, 80f), title, titleStyle);
        GUI.Label(new Rect(panelRect.x, panelRect.y + 132f, panelRect.width, 70f), result, resultStyle);
        GUI.Label(new Rect(panelRect.x, panelRect.y + 200f, panelRect.width, 58f), detail, turnLabelStyle);

        float buttonWidth = Mathf.Clamp(panelWidth * 0.34f, 260f, 360f);
        float buttonHeight = Mathf.Clamp(panelHeight * 0.20f, 82f, 104f);
        Rect restartButton = new Rect(
            panelRect.x + (panelRect.width - buttonWidth) * 0.5f,
            panelRect.y + panelRect.height * 0.70f,
            buttonWidth,
            buttonHeight);

        if (GUI.Button(restartButton, "Restart", buttonStyle))
            chessGame.RestartToMainMenu();
    }

    private Rect GetCenteredRect(float width, float height)
    {
        return new Rect(
            (uiWidth - width) * 0.5f,
            (uiHeight - height) * 0.5f,
            width,
            height);
    }

    private void DrawDimBackground(float alpha)
    {
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, uiWidth, uiHeight), Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
            return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 72,
            fontStyle = FontStyle.Bold
        };

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 54,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 46,
            fontStyle = FontStyle.Bold
        };

        turnLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 48,
            fontStyle = FontStyle.Bold
        };

        checkWarningStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 50,
            fontStyle = FontStyle.Bold
        };
        checkWarningStyle.normal.textColor = new Color(1f, 0.28f, 0.18f, 1f);

        resultStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 60,
            fontStyle = FontStyle.Bold
        };

    }

    private void TryCreateHandDrawnMenu()
    {
        HandDrawnMenuAssets menuAssets = FindAnyObjectByType<HandDrawnMenuAssets>();
        if (!menuAssets)
        {
            menuAssets = gameObject.AddComponent<HandDrawnMenuAssets>();
        }

        if (!menuAssets.HasRequiredSprites)
            menuAssets.LoadFromResources();

        if (!menuAssets.HasRequiredSprites)
        {
            Debug.LogWarning("[HandDrawnMenu] Missing required sprites. Falling back to IMGUI menu.");
            return;
        }

        handDrawnMenu = gameObject.AddComponent<HandDrawnMenuView>();
        handDrawnMenu.Initialize(this, chessGame, menuAssets);
    }

    private void TryCreateLanController()
    {
        lanController = gameObject.AddComponent<ChessLanController>();
        lanController.Initialize(chessGame, this);
    }

    private void TryCreatePauseMenu()
    {
        pauseMenu = gameObject.AddComponent<ChessPauseMenu>();
        pauseMenu.Initialize(chessGame, lanController);
    }

    private void TryCreateAnalysisBoard()
    {
        analysisBoard = GetComponent<AnalysisBoardView>();
        if (!analysisBoard)
            analysisBoard = gameObject.AddComponent<AnalysisBoardView>();
        analysisBoard.Initialize(chessGame);
    }

    private void TryCreateResultMenu()
    {
        resultMenu = gameObject.AddComponent<ResultMenuView>();
        resultMenu.Initialize(this, pauseMenu, chessGame);
    }

    private void TryCreateBotController()
    {
        botController = GetComponent<StockfishBotController>();
        if (!botController)
            botController = gameObject.AddComponent<StockfishBotController>();
        botController.Initialize(chessGame);
    }
}
