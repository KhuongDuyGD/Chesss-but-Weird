using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UI = SketchbookUI;

[DisallowMultipleComponent]
public sealed class ResultMenuView : MonoBehaviour
{
    public enum ResultKind { Win, Lose, Draw, Stalemate }
    private ChessTurnSelectionUI owner;
    private ChessPauseMenu pauseMenu;
    private ChessGame game;
    private GameObject canvasRoot;
    private RectTransform contentRoot;
    private TextMeshProUGUI title, mode, message, players, elapsed;
    private HandDrawnRoundedGraphic badge;
    private SketchbookDoodle illustration;
    private Button spectate, replay, menu;
    private bool actionTaken;
    public bool IsReady => canvasRoot;

    public void Initialize(ChessTurnSelectionUI newOwner, ChessPauseMenu newPauseMenu, ChessGame chessGame)
    {
        owner = newOwner; pauseMenu = newPauseMenu; game = chessGame;
        canvasRoot = new GameObject("Result Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(chessGame ? chessGame.transform : transform, false);
        var canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2000;
        ResponsiveUi.ConfigureCanvasScaler(canvasRoot.GetComponent<CanvasScaler>(), new Vector2(1920, 1080));
        var dim = canvasRoot.AddComponent<Image>(); dim.color = new Color(.10f, .09f, .13f, .6f);
        contentRoot = MenuDesignFrame.Create(canvasRoot.transform, "Match Result", new Vector2(1920, 1080));
        var sheet = UI.Card(contentRoot, "Result Sheet", new Rect(470, 130, 980, 820), UI.White, 0, true, 18).rectTransform;
        UI.Tape(sheet, 408, -13, 164, -3);
        mode = UI.Text(sheet, "Result Mode", "MATCH COMPLETE", new Rect(50, 34, 880, 34), 23, UI.Muted, TextAlignmentOptions.Center);
        title = UI.Text(sheet, "Result Title", "YOU WIN!", new Rect(40, 88, 900, 91), 72, UI.Ink, TextAlignmentOptions.Center);
        badge = UI.Card(sheet, "Result Badge", new Rect(390, 201, 200, 180), UI.Yellow, -3, false, 28);
        illustration = UI.Doodle(badge.transform, "Result Illustration", new Rect(44, 24, 112, 130), SketchbookDoodle.Shape.Trophy, UI.Ink);
        message = UI.Text(sheet, "Result Detail", "", new Rect(62, 412, 856, 80), 30, UI.Ink, TextAlignmentOptions.Center, true);
        players = UI.Text(sheet, "Players", "", new Rect(62, 506, 856, 44), 26, UI.Muted, TextAlignmentOptions.Center);
        elapsed = UI.Text(sheet, "Elapsed", "", new Rect(62, 559, 856, 36), 23, UI.Muted, TextAlignmentOptions.Center);
        replay = UI.Button(sheet, "New Game", "PLAY AGAIN", new Rect(60, 635, 860, 76), UI.Yellow,
            () => RunAction(() => owner?.StartNewGameFromResult()), 30);
        spectate = UI.Button(sheet, "Spectated Game", "VIEW BOARD", new Rect(60, 736, 410, 60), UI.Blue,
            () => RunAction(() => owner?.SpectateFinishedGame()), 25);
        menu = UI.Button(sheet, "Main Menu", "MAIN MENU", new Rect(510, 736, 410, 60), UI.Paper,
            () => RunAction(() => owner?.ReturnToMainMenuFromResult()), 25);
        if (!EventSystem.current) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Hide();
    }

    public void Show(ResultKind kind, string neutralTitle = null)
    {
        if (!IsReady) return;
        GameMusicManager.PlayResultMusic(kind);
        pauseMenu?.SetResultSpectating(false);
        Present(kind, neutralTitle);
        canvasRoot.SetActive(true);
        replay.Select();
    }

    private void Present(ResultKind kind, string neutralTitle)
    {
        actionTaken = false;
        replay.interactable = spectate.interactable = menu.interactable = true;
        bool win = kind == ResultKind.Win, lose = kind == ResultKind.Lose;
        Color color = win ? UI.Yellow : lose ? UI.Pink : UI.Lavender;
        title.text = !string.IsNullOrWhiteSpace(neutralTitle) ? neutralTitle.ToUpperInvariant() :
            win ? "YOU WIN!" : lose ? "YOU LOSE" : kind == ResultKind.Stalemate ? "STALEMATE" : "IT'S A DRAW";
        badge.Configure(color, UI.Ink, 28, 2.6f, 1.8f, 12);
        illustration.Configure(win ? SketchbookDoodle.Shape.Trophy : lose ? SketchbookDoodle.Shape.Pawn : SketchbookDoodle.Shape.Board, UI.Ink);
        illustration.rectTransform.localRotation = Quaternion.Euler(0, 0, lose ? -14 : 0);
        mode.text = "MATCH COMPLETE  /  " + (game ? game.StatisticsMode.ToUpperInvariant() : "CHESS BUT WEIRD");
        message.text = !win && !lose ? (game && !string.IsNullOrWhiteSpace(game.DrawReason) ? PlayModeNames.DisplayText(game.DrawReason) :
            kind == ResultKind.Stalemate ? "No legal moves. Neither king is in check." : "A shared finish. Well played!") :
            game ? game.StatisticsPlayerName(game.WinningTeam) + " wins as " + game.WinningTeam + "." : win ? "Well played!" : "A new game, a fresh start.";
        players.text = game ? "WHITE  ·  " + game.StatisticsPlayerName(PieceTeam.White) + "     /     BLACK  ·  " + game.StatisticsPlayerName(PieceTeam.Black) : "";
        int seconds = game ? Mathf.Max(0, Mathf.FloorToInt(game.MatchElapsedSeconds)) : 0;
        elapsed.text = game && game.StatisticsTimeKnown ? $"TIME PLAYED  {seconds / 60:00}:{seconds % 60:00}" : "TIME PLAYED  —";
        replay.GetComponentInChildren<TextMeshProUGUI>().text = game && game.UsesDotNetOnline ? "REQUEST REMATCH" : "PLAY AGAIN";
    }

    private void RunAction(Action action)
    {
        if (actionTaken || !canvasRoot.activeSelf) return;
        actionTaken = true;
        replay.interactable = spectate.interactable = menu.interactable = false;
        action();
    }

    public void Hide() { if (canvasRoot) canvasRoot.SetActive(false); }
    private void OnDestroy()
    {
        if (!canvasRoot) return;
        canvasRoot.SetActive(false);
        if (Application.isPlaying) Destroy(canvasRoot); else DestroyImmediate(canvasRoot);
    }
}
