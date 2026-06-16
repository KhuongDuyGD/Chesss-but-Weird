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
        GameOver
    }

    private const float TransitionDuration = 0.45f;

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

    public static ChessTurnSelectionUI Create(ChessGame chessGame)
    {
        GameObject root = new GameObject("Chess Turn Selection UI", typeof(RectTransform));
        ChessTurnSelectionUI ui = root.AddComponent<ChessTurnSelectionUI>();
        ui.chessGame = chessGame;
        ui.TryCreateHandDrawnMenu();
        ui.TryCreateLanController();
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
        promotionCallback = null;
        resultIsDraw = false;
        drawReason = string.Empty;
        showCheckWarning = false;
        state = ScreenState.MainMenu;
        lanController?.HideLanSetup();
        handDrawnMenu?.ShowMainMenu();
    }

    public void ShowTurnSelection()
    {
        showCheckWarning = false;
        lanController?.HideLanSetup();
        if (handDrawnMenu && handDrawnMenu.IsReady)
        {
            state = ScreenState.TransitionToTurnSelection;
            handDrawnMenu.ShowModeSelection();
            return;
        }

        state = ScreenState.TransitionToTurnSelection;
        transitionStartTime = Time.realtimeSinceStartup;
    }

    public void ShowSideSelection()
    {
        showCheckWarning = false;
        lanController?.HideLanSetup();
        state = ScreenState.TurnSelection;
        handDrawnMenu?.ShowSideSelection();
    }

    public void ShowMultiplayerModeSelection()
    {
        showCheckWarning = false;
        lanController?.HideLanSetup();
        state = ScreenState.TurnSelection;
        handDrawnMenu?.ShowMultiplayerModeSelection();
    }

    public void ShowLanSetup()
    {
        showCheckWarning = false;
        state = ScreenState.TurnSelection;
        handDrawnMenu?.ShowMultiplayerModeSelection();
        handDrawnMenu?.SetInputEnabled(false);
        lanController?.ShowLanSetup();
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
    }

    private void OnGUI()
    {
        EnsureStyles();

        if (handDrawnMenu && handDrawnMenu.IsReady &&
            (state == ScreenState.MainMenu || state == ScreenState.TransitionToTurnSelection || state == ScreenState.TurnSelection))
            return;

        if (state == ScreenState.Playing)
        {
            GUI.Label(new Rect(28f, 24f, 460f, 72f), $"Turn: {currentTurn}", turnLabelStyle);
            if (showCheckWarning)
                GUI.Label(new Rect(28f, 96f, 760f, 78f), $"{checkedTeam} king is in CHECK", checkWarningStyle);
        }
        else if (state == ScreenState.GameOver)
            GUI.Label(new Rect(28f, 24f, 680f, 72f), $"You played: {playerTeam}", turnLabelStyle);

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

        float panelWidth = Mathf.Clamp(Screen.width * 0.60f, 840f, 1240f);
        float panelHeight = Mathf.Clamp(Screen.height * 0.48f, 500f, 660f);
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

        float panelWidth = Mathf.Clamp(Screen.width * 0.48f, 760f, 1040f);
        float panelHeight = Mathf.Clamp(Screen.height * 0.34f, 380f, 520f);
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
            chessGame.BeginGame(PieceTeam.White);

        if (GUI.Button(blackButton, "Black", buttonStyle))
            chessGame.BeginGame(PieceTeam.Black);
    }

    private void DrawPromotion()
    {
        DrawDimBackground(0.48f);

        float panelWidth = Mathf.Clamp(Screen.width * 0.56f, 820f, 1160f);
        float panelHeight = Mathf.Clamp(Screen.height * 0.34f, 380f, 500f);
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

        float panelWidth = Mathf.Clamp(Screen.width * 0.48f, 720f, 980f);
        float panelHeight = Mathf.Clamp(Screen.height * 0.34f, 380f, 500f);
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
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);
    }

    private void DrawDimBackground(float alpha)
    {
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
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
}
