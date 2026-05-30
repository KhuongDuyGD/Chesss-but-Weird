using UnityEngine;

public class ChessTurnSelectionUI : MonoBehaviour
{
    private ChessGame chessGame;
    private PieceTeam currentTurn;
    private bool waitingForChoice = true;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle turnLabelStyle;

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
        waitingForChoice = false;
    }

    private void OnGUI()
    {
        EnsureStyles();

        GUI.Label(new Rect(24f, 22f, 360f, 54f), waitingForChoice ? "Turn: -" : $"Turn: {currentTurn}", turnLabelStyle);

        if (!waitingForChoice)
            return;

        float panelWidth = Mathf.Clamp(Screen.width * 0.48f, 760f, 1040f);
        float panelHeight = Mathf.Clamp(Screen.height * 0.34f, 380f, 520f);
        Rect panelRect = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

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
    }
}
