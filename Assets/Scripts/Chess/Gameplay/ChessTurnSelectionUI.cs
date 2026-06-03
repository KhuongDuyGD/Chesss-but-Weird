using UnityEngine;

public class ChessTurnSelectionUI : MonoBehaviour
{
    private enum ScreenState
    {
        MainMenu,
        TransitionToTurnSelection,
        TurnSelection,
        Playing,
        GameOver
    }

    private const float TransitionDuration = 0.45f;

    private ChessGame chessGame;
    private PieceTeam currentTurn;
    private PieceTeam winningTeam;
    private PieceTeam checkedTeam;
    private ScreenState state = ScreenState.MainMenu;
    private float transitionStartTime;
    private bool showCheckWarning;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle turnLabelStyle;
    private GUIStyle checkWarningStyle;
    private GUIStyle resultStyle;

    public static ChessTurnSelectionUI Create(ChessGame chessGame)
    {
        GameObject root = new GameObject("Chess Turn Selection UI");
        ChessTurnSelectionUI ui = root.AddComponent<ChessTurnSelectionUI>();
        ui.chessGame = chessGame;
        return ui;
    }

    public void SetTurn(PieceTeam newCurrentTurn)
    {
        currentTurn = newCurrentTurn;
        showCheckWarning = false;
        state = ScreenState.Playing;
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

    public void ShowMainMenu()
    {
        showCheckWarning = false;
        state = ScreenState.MainMenu;
    }

    public void ShowTurnSelection()
    {
        showCheckWarning = false;
        state = ScreenState.TransitionToTurnSelection;
        transitionStartTime = Time.realtimeSinceStartup;
    }

    public void ShowGameOver(PieceTeam winner)
    {
        showCheckWarning = false;
        winningTeam = winner;
        state = ScreenState.GameOver;
    }

    private void OnGUI()
    {
        EnsureStyles();

        if (state == ScreenState.Playing)
        {
            GUI.Label(new Rect(24f, 22f, 360f, 54f), $"Turn: {currentTurn}", turnLabelStyle);
            if (showCheckWarning)
                GUI.Label(new Rect(24f, 78f, 520f, 62f), $"{checkedTeam} king is in CHECK", checkWarningStyle);
        }
        else if (state == ScreenState.GameOver)
            GUI.Label(new Rect(24f, 22f, 480f, 54f), $"Winner: {winningTeam}", turnLabelStyle);

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
            case ScreenState.GameOver:
                DrawGameOver();
                break;
        }
    }

    private void DrawMainMenu()
    {
        DrawDimBackground(0.78f);

        float panelWidth = Mathf.Clamp(Screen.width * 0.54f, 760f, 1120f);
        float panelHeight = Mathf.Clamp(Screen.height * 0.42f, 420f, 560f);
        Rect panelRect = GetCenteredRect(panelWidth, panelHeight);

        GUI.Box(panelRect, string.Empty);
        GUI.Label(new Rect(panelRect.x, panelRect.y + 58f, panelRect.width, 80f), "Chess but Weird", titleStyle);
        GUI.Label(new Rect(panelRect.x + 70f, panelRect.y + 142f, panelRect.width - 140f, 72f),
            "Classic chess foundation with room for weird variants.", subtitleStyle);

        float buttonWidth = Mathf.Clamp(panelWidth * 0.34f, 280f, 380f);
        float buttonHeight = Mathf.Clamp(panelHeight * 0.20f, 86f, 110f);
        Rect playButton = new Rect(
            panelRect.x + (panelRect.width - buttonWidth) * 0.5f,
            panelRect.y + panelRect.height * 0.64f,
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

    private void DrawGameOver()
    {
        DrawDimBackground(0.62f);

        float panelWidth = Mathf.Clamp(Screen.width * 0.48f, 720f, 980f);
        float panelHeight = Mathf.Clamp(Screen.height * 0.34f, 380f, 500f);
        Rect panelRect = GetCenteredRect(panelWidth, panelHeight);

        GUI.Box(panelRect, string.Empty);
        GUI.Label(new Rect(panelRect.x, panelRect.y + 56f, panelRect.width, 80f), "Game Over", titleStyle);
        GUI.Label(new Rect(panelRect.x, panelRect.y + 142f, panelRect.width, 70f), $"{winningTeam} wins", resultStyle);

        float buttonWidth = Mathf.Clamp(panelWidth * 0.34f, 260f, 360f);
        float buttonHeight = Mathf.Clamp(panelHeight * 0.20f, 82f, 104f);
        Rect restartButton = new Rect(
            panelRect.x + (panelRect.width - buttonWidth) * 0.5f,
            panelRect.y + panelRect.height * 0.64f,
            buttonWidth,
            buttonHeight);

        if (GUI.Button(restartButton, "Main Menu", buttonStyle))
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
            fontSize = 46,
            fontStyle = FontStyle.Bold
        };

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            wordWrap = true
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold
        };

        turnLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold
        };

        checkWarningStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold
        };
        checkWarningStyle.normal.textColor = new Color(1f, 0.28f, 0.18f, 1f);

        resultStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 36,
            fontStyle = FontStyle.Bold
        };
    }
}
