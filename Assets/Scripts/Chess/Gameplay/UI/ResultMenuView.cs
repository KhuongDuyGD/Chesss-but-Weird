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
    private GameObject canvasRoot, reviewCanvas;
    private RectTransform contentRoot;
    private TextMeshProUGUI title, mode, message, players, elapsed, shareStatus, reviewStatus;
    private HandDrawnRoundedGraphic badge;
    private SketchbookDoodle illustration;
    private Button spectate, replay, menu, share, reviewLeave, reviewShare;
    private bool actionTaken;
    private MatchShareExportView shareExportView;
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
        share = UI.Button(sheet, "Share Result", "SHARE / EXPORT", new Rect(60, 624, 410, 70), UI.Green, ShareResult, 27);
        shareStatus = UI.Text(sheet, "Share Status", "", new Rect(60, 700, 860, 29), 21, UI.Muted, TextAlignmentOptions.Center);
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
        if (reviewCanvas) reviewCanvas.SetActive(false);
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
        bool canShare = MatchResultSharing.CanShare(game);
        share.gameObject.SetActive(canShare);
        // The share action has no shadow left behind in modes where it is unavailable.
        var shareShadow = share.transform.parent.Find("Share Result Pencil Shadow");
        if (shareShadow) shareShadow.gameObject.SetActive(canShare);
        SetReplayArea(canShare ? new Rect(510, 624, 410, 70) : new Rect(60, 635, 860, 76));
        share.interactable = true;
        shareStatus.text = "";
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
        replay.interactable = spectate.interactable = menu.interactable = share.interactable = false;
        action();
    }

    private void SetReplayArea(Rect area)
    {
        var rect = (RectTransform)replay.transform;
        rect.anchoredPosition = new Vector2(area.center.x, -area.center.y);
        rect.sizeDelta = area.size;
        var label = replay.GetComponentInChildren<TextMeshProUGUI>().rectTransform;
        label.anchoredPosition = new Vector2(area.width / 2, -area.height / 2);
        label.sizeDelta = new Vector2(area.width - 28, area.height - 10);
        var shadow = replay.transform.parent.Find("New Game Pencil Shadow") as RectTransform;
        if (shadow) { shadow.anchoredPosition = rect.anchoredPosition + new Vector2(5, -7); shadow.sizeDelta = area.size; }
    }

    private void ShareResult()
    {
        if ((actionTaken && canvasRoot.activeSelf) || !MatchResultSharing.CanShare(game)) return;
        try
        {
            if (!shareExportView) shareExportView = gameObject.AddComponent<MatchShareExportView>();
            shareExportView.Show(game);
        }
        catch (Exception error)
        {
            string reason = "Could not open Share / Export: " + PlayerNotificationText.FromException(error);
            SetShareStatus(reason);
            if (Application.isPlaying) AuthNotificationView.Show("Share / Export unavailable", reason, AuthNotificationView.ResultKind.Error);
        }
    }

    private void SetShareStatus(string text)
    {
        shareStatus.text = text;
        if (reviewStatus)
        {
            reviewStatus.text = text;
            reviewStatus.transform.parent.gameObject.SetActive(true);
        }
    }

    public void ShowBoardReview()
    {
        Hide();
        if (!reviewCanvas) BuildBoardReview();
        reviewLeave.interactable = reviewShare.interactable = true;
        reviewShare.gameObject.SetActive(MatchResultSharing.CanShare(game));
        reviewStatus.transform.parent.gameObject.SetActive(false);
        reviewCanvas.SetActive(true);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    private void BuildBoardReview()
    {
        reviewCanvas = new GameObject("Finished Board Review Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        reviewCanvas.transform.SetParent(game.transform, false);
        var canvas = reviewCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 25;
        ResponsiveUi.ConfigureCanvasScaler(reviewCanvas.GetComponent<CanvasScaler>(), new Vector2(1280, 720));
        var root = MatchHudStyle.Rect(reviewCanvas.transform, "Review Safe Area", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.gameObject.AddComponent<ResponsiveSafeArea>();
        var tooltip = reviewCanvas.AddComponent<MatchHudTooltip>(); tooltip.Initialize(root);
        var red = new Color(.76f, .12f, .17f);
        reviewLeave = ReviewButton(root, "Leave to Lobby", MatchHudIconGraphic.Symbol.ExitDoor, new Vector2(130, 45),
            () => owner?.LeaveFinishedGameToLobby(), tooltip, "Leave to lobby", "Leave this finished game and return to the lobby you came from.");
        reviewLeave.GetComponent<HandDrawnRoundedGraphic>().Configure(new Color(1f, .88f, .87f), red, 10, 2f, .25f, 0);
        reviewLeave.GetComponentInChildren<MatchHudIconGraphic>().Configure(MatchHudIconGraphic.Symbol.ExitDoor, red);
        var colors = reviewLeave.colors;
        colors.highlightedColor = colors.selectedColor = new Color(1f, .78f, .78f);
        colors.pressedColor = new Color(.92f, .56f, .56f); reviewLeave.colors = colors;
        reviewShare = ReviewButton(root, "Share Result", MatchHudIconGraphic.Symbol.Share, new Vector2(210, 45),
            ShareResult, tooltip, "Share / Export", "Preview and save a result card, copy the result, or export available chess data.");
        var statusPanel = MatchHudStyle.Rect(root, "Review Action Status", Vector2.zero, Vector2.zero, new Vector2(500, 58), new Vector2(266, 113));
        MatchHudStyle.Surface(statusPanel, false);
        reviewStatus = MatchHudStyle.Text(statusPanel, "Status", "", 18);
        reviewStatus.textWrappingMode = TextWrappingModes.Normal;
    }

    private static Button ReviewButton(RectTransform root, string name, MatchHudIconGraphic.Symbol symbol, Vector2 position,
        UnityEngine.Events.UnityAction action, MatchHudTooltip tooltip, string heading, string detail)
    {
        var button = MatchHudStyle.Button(root, name, "", action);
        MatchHudStyle.Place((RectTransform)button.transform, Vector2.zero, new Vector2(68, 58), position);
        var icon = MatchHudStyle.Rect(button.transform, "Icon", Vector2.one * .5f, Vector2.one * .5f, new Vector2(36, 36), Vector2.zero);
        icon.gameObject.AddComponent<MatchHudIconGraphic>().Configure(symbol, MatchHudStyle.Ink);
        button.gameObject.AddComponent<MatchHudTooltipTarget>().Configure(tooltip, heading, detail);
        return button;
    }

    public void SetReviewExitPending(bool pending, string status = null)
    {
        if (!reviewCanvas) return;
        reviewLeave.interactable = reviewShare.interactable = !pending;
        reviewStatus.text = status ?? "";
        reviewStatus.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(status));
    }

    public void Hide()
    {
        shareExportView?.Close();
        if (canvasRoot) canvasRoot.SetActive(false);
        if (reviewCanvas) reviewCanvas.SetActive(false);
    }
    private void OnDestroy()
    {
        Hide();
        foreach (var canvas in new[] { canvasRoot, reviewCanvas })
            if (canvas) { if (Application.isPlaying) Destroy(canvas); else DestroyImmediate(canvas); }
    }
}
