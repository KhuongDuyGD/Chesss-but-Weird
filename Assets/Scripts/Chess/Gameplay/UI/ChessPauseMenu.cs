using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI = SketchbookUI;

public sealed class ChessPauseMenu : MonoBehaviour
{
    private enum PendingAction { None, Restart, Leave, MainMenu, QuitDesktop }
    private ChessGame chessGame;
    private ChessLanController lanController;
    private ChessTurnSelectionUI turnSelection;
    private GameObject canvasRoot, overlay, buttonGroup, settingsRoot;
    private RectTransform frame, confirmation, matchInfo;
    private CanvasGroup controls;
    private Button resumeButton, restartButton, settingsButton, cancelButton;
    private TextMeshProUGUI title, statusText, confirmationText, infoText;
    private readonly List<Button> buttons = new List<Button>();
    private SettingsMenuController settingsMenu;
    private PendingAction pendingAction;
    private bool localPaused, quitting, resultSpectating;
    private float previousTimeScale = 1;
    private bool ownsTimeScale;
    private int lastEscapeFrame = -1;
    private Action quitDesktop = QuitApplication;

    public bool IsOpen => localPaused && overlay && overlay.activeSelf;
    private bool IsMultiplayer => chessGame && (chessGame.IsNetworkGame || chessGame.UsesDotNetOnline) ||
        lanController && lanController.IsNetworkGameActive;
    private bool CanOpen => chessGame && (chessGame.GameStarted || resultSpectating && chessGame.GameOver);

    public void Initialize(ChessGame game, ChessLanController networkController)
    {
        chessGame = game; lanController = networkController;
        turnSelection = GetComponent<ChessTurnSelectionUI>();
        BuildUi(PauseMenuAssetCatalog.Load());
        if (chessGame) chessGame.ReturnedToMainMenu += ResetPauseState;
        overlay.SetActive(false);
    }

    public void Open() => PauseLocally();

    private void BuildUi(PauseMenuAssetCatalog assets)
    {
        // Independent of the main-menu canvas, which is disabled during a match.
        canvasRoot = new GameObject("Pause Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(chessGame ? chessGame.transform : null, false);
        var canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 2500;
        ResponsiveUi.ConfigureCanvasScaler(canvasRoot.GetComponent<CanvasScaler>(), new Vector2(1920, 1080));
        overlay = new GameObject("Pause Overlay", typeof(RectTransform));
        overlay.transform.SetParent(canvasRoot.transform, false); Stretch((RectTransform)overlay.transform);
        var shade = overlay.AddComponent<AntialiasedMenuImage>(); shade.color = new Color(0, 0, 0, .48f);
        frame = MenuDesignFrame.Create(overlay.transform, "Pause Menu", new Vector2(1920, 1080));
        var sheet = UI.Card(frame, "Pause Sheet", new Rect(590, 114, 740, 852), UI.White, 0, true, 8).rectTransform;
        UI.Tape(sheet, 294, -10, 150, -3);
        UI.Doodle(sheet, "Pawn Doodle", new Rect(43, 33, 64, 68), SketchbookDoodle.Shape.Pawn, UI.Ink, -4);
        title = UI.Text(sheet, "Pause Title", "PAUSED", new Rect(126, 28, 570, 67), 43);
        statusText = UI.Text(sheet, "Pause Status", "", new Rect(42, 754, 656, 69), 23, UI.Muted, TextAlignmentOptions.Center, true);
        buttonGroup = UI.Node(sheet, "Pause Actions", new Rect(42, 137, 656, 591)).gameObject;
        controls = buttonGroup.AddComponent<CanvasGroup>();
        BuildConfirmation();
        BuildMatchInfo();
        RebuildActions();
        if (!EventSystem.current)
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        overlay.SetActive(false);
    }

    private void RebuildActions()
    {
        for (int i = buttonGroup.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = buttonGroup.transform.GetChild(i);
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }
        buttons.Clear(); restartButton = null;
        resumeButton = AddAction(IsMultiplayer ? "Return to Match" : "Resume", IsMultiplayer ? "RETURN TO MATCH" : "RESUME", UI.Green, ResumeLocalPause);
        if (!IsMultiplayer) restartButton = AddAction("Restart Match", "RESTART MATCH", UI.White, () => Ask(PendingAction.Restart));
        settingsButton = AddAction("Settings", "SETTINGS", UI.Blue, OpenSettings);
        if (IsMultiplayer) AddAction("Match Info", "MATCH INFO", UI.White, OpenMatchInfo);
        AddAction("Leave Match", "LEAVE MATCH", UI.Pink, () => Ask(PendingAction.Leave));
        AddAction("Main Menu", "MAIN MENU", UI.White, () => Ask(PendingAction.MainMenu));
        AddAction("Quit Desktop", "QUIT DESKTOP", UI.Paper, () => Ask(PendingAction.QuitDesktop));
        for (int i = 0; i < buttons.Count; i++)
            buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(i + buttons.Count - 1) % buttons.Count], selectOnDown = buttons[(i + 1) % buttons.Count] };
    }

    private Button AddAction(string name, string caption, Color fill, UnityEngine.Events.UnityAction action)
    {
        var button = UI.Button(buttonGroup.transform, name, caption, new Rect(0, buttons.Count * 94, 656, 73), fill, action, 29);
        // Immediate color feedback avoids motion and waits, including with Reduce Motion enabled.
        var colors = button.colors; colors.fadeDuration = 0; colors.disabledColor = Color.white; button.colors = colors;
        buttons.Add(button); return button;
    }

    private void BuildConfirmation()
    {
        confirmation = UI.Node(frame, "Pause Confirmation", new Rect(0, 0, 1920, 1080));
        var shade = confirmation.gameObject.AddComponent<AntialiasedMenuImage>(); shade.color = new Color(0, 0, 0, .26f);
        var card = UI.Card(confirmation, "Confirmation Sheet", new Rect(535, 310, 850, 460), UI.White, 0, true, 8).rectTransform;
        card.GetComponent<HandDrawnRoundedGraphic>().raycastTarget = true;
        UI.Text(card, "Confirmation Heading", "ARE YOU SURE?", new Rect(36, 28, 778, 63), 39);
        confirmationText = UI.Text(card, "Confirmation Message", "", new Rect(36, 109, 778, 187), 29, UI.Ink, TextAlignmentOptions.Center, true);
        cancelButton = UI.Button(card, "Cancel Pause Action", "CANCEL", new Rect(36, 338, 361, 75), UI.Paper, CancelConfirmation, 29);
        var accept = UI.Button(card, "Confirm Pause Action", "CONFIRM", new Rect(453, 338, 361, 75), UI.Pink, ConfirmAction, 29);
        cancelButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = accept };
        accept.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = cancelButton };
        confirmation.gameObject.SetActive(false);
    }

    private void BuildMatchInfo()
    {
        matchInfo = UI.Node(frame, "Pause Match Info", new Rect(0, 0, 1920, 1080));
        var shade = matchInfo.gameObject.AddComponent<AntialiasedMenuImage>(); shade.color = new Color(0, 0, 0, .26f);
        var card = UI.Card(matchInfo, "Match Info Sheet", new Rect(535, 250, 850, 580), UI.White, 0, true, 8).rectTransform;
        UI.Text(card, "Match Info Heading", "MATCH INFO", new Rect(36, 26, 778, 65), 39);
        infoText = UI.Text(card, "Match Details", "", new Rect(44, 117, 762, 314), 27, UI.Ink, TextAlignmentOptions.MidlineLeft, true);
        UI.Button(card, "Back from Match Info", "BACK", new Rect(453, 471, 361, 70), UI.Green, CloseMatchInfo, 29);
        matchInfo.gameObject.SetActive(false);
    }

    private void PauseLocally()
    {
        if (!overlay || !CanOpen || quitting || localPaused) return;
        localPaused = true;
        ownsTimeScale = !IsMultiplayer && !resultSpectating;
        if (ownsTimeScale) { previousTimeScale = Time.timeScale; Time.timeScale = 0; }
        chessGame.SetPauseLocked(true);
        RebuildActions();
        title.text = IsMultiplayer ? "MATCH MENU" : "PAUSED";
        statusText.text = IsMultiplayer ? "The match and online clocks continue." : "";
        overlay.SetActive(true);
        controls.interactable = controls.blocksRaycasts = true;
        resumeButton.Select();
    }

    private void ResumeLocalPause()
    {
        if (!localPaused || quitting || pendingAction != PendingAction.None || settingsMenu && settingsMenu.IsOpen || matchInfo.gameObject.activeSelf) return;
        ResetPauseState();
    }

    private void Ask(PendingAction action)
    {
        if (!localPaused || quitting || pendingAction != PendingAction.None || !controls.interactable) return;
        if (action == PendingAction.Restart && IsMultiplayer) return;
        pendingAction = action;
        string destination = action == PendingAction.Leave ? IsMultiplayer ? "your multiplayer lobby" : "Bot Selection" : "Main Menu 2";
        confirmationText.text = action == PendingAction.Restart ? "Restart this match?\nYour current position will be cleared." :
            action == PendingAction.QuitDesktop ? "Quit to desktop?" + (IsMultiplayer ? "\nLeaving an active match uses the existing surrender rules." : "") :
            "Leave this match and return to " + destination + "?" + (IsMultiplayer ? "\nLeaving an active match uses the existing surrender rules." : "");
        controls.interactable = controls.blocksRaycasts = false;
        confirmation.gameObject.SetActive(true); confirmation.SetAsLastSibling(); cancelButton.Select();
    }

    private void CancelConfirmation()
    {
        if (quitting || pendingAction == PendingAction.None) return;
        pendingAction = PendingAction.None; confirmation.gameObject.SetActive(false);
        controls.interactable = controls.blocksRaycasts = true; resumeButton.Select();
    }

    private async void ConfirmAction()
    {
        if (quitting || pendingAction == PendingAction.None) return;
        PendingAction action = pendingAction;
        pendingAction = PendingAction.None; quitting = true;
        confirmation.gameObject.SetActive(false);
        statusText.text = action == PendingAction.Restart ? "Restarting..." : "Leaving match...";
        try
        {
            var loading = chessGame.GetComponent<LoadingManager>();
            if ((action == PendingAction.Leave || action == PendingAction.MainMenu) && loading && loading.IsBusy)
                throw new InvalidOperationException("Match content is busy. Please try again when it finishes.");
            if (action == PendingAction.Restart)
            {
                if (IsMultiplayer) throw new InvalidOperationException("Multiplayer matches cannot be restarted locally.");
                ResetPauseState();
                if (!chessGame.RestartCurrentLocalGame()) throw new InvalidOperationException("The match could not be restarted.");
                return;
            }
            bool onlineMatch = IsMultiplayer;
            if (onlineMatch)
            {
                try
                {
                    if (!lanController) throw new InvalidOperationException("The online session controller is unavailable.");
                    Task exit = lanController.LeaveMatchFromPauseAsync();
                    // Desktop exit must remain available even when the server is unreachable.
                    if (action == PendingAction.QuitDesktop && await Task.WhenAny(exit, Task.Delay(5000)) != exit)
                    {
                        _ = exit.ContinueWith(faulted => { _ = faulted.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                        throw new TimeoutException("The server did not acknowledge desktop exit within five seconds.");
                    }
                    await exit;
                }
                catch (Exception error) when (action == PendingAction.QuitDesktop)
                {
                    Debug.LogWarning("[PauseMenu] Server exit could not be acknowledged: " + error.Message);
                    if (lanController) lanController.CloseSessionFromPause();
                }
                if (!this) return;
            }
            if (action == PendingAction.QuitDesktop)
            {
                ResetPauseState(); quitDesktop(); return;
            }
            // Keep the action guard set until the existing content owner finishes releasing the match.
            if (ownsTimeScale) Time.timeScale = previousTimeScale;
            ownsTimeScale = false;
            chessGame.RestartToMainMenu(() =>
            {
                if (!this) return;
                ResetPauseState();
                if (action == PendingAction.MainMenu) turnSelection?.ShowTurnSelection();
                else if (onlineMatch) lanController.ShowLobbyAfterPauseExit();
                else turnSelection?.ShowBotDifficultySelection();
            });
        }
        catch (Exception error)
        {
            if (!this) return;
            quitting = false;
            controls.interactable = controls.blocksRaycasts = true;
            statusText.text = "Could not complete action: " + PlayerNotificationText.FromException(error);
            overlay.SetActive(true); localPaused = true;
            chessGame.SetPauseLocked(true); resumeButton.Select();
        }
    }

    private void OpenSettings()
    {
        if (quitting || pendingAction != PendingAction.None || matchInfo.gameObject.activeSelf || settingsMenu && settingsMenu.IsOpen) return;
        if (!localPaused) PauseLocally();
        if (!localPaused) return;
        if (!settingsRoot)
        {
            settingsRoot = new GameObject("Pause Settings", typeof(RectTransform));
            settingsRoot.transform.SetParent(canvasRoot.transform, false); Stretch((RectTransform)settingsRoot.transform);
            settingsMenu = settingsRoot.AddComponent<SettingsMenuController>();
            settingsMenu.Initialize((RectTransform)settingsRoot.transform, () =>
            {
                settingsRoot.SetActive(false);
                controls.interactable = controls.blocksRaycasts = true; settingsButton.Select();
            });
        }
        controls.interactable = controls.blocksRaycasts = false;
        settingsRoot.SetActive(true); settingsRoot.transform.SetAsLastSibling(); settingsMenu.Open();
    }

    private void OpenMatchInfo()
    {
        if (!localPaused || quitting || !IsMultiplayer || !controls.interactable) return;
        RefreshMatchInfo();
        controls.interactable = controls.blocksRaycasts = false;
        matchInfo.gameObject.SetActive(true); matchInfo.SetAsLastSibling();
        matchInfo.GetComponentInChildren<Button>().Select();
    }
    private void RefreshMatchInfo()
    {
        infoText.text = chessGame.StatisticsPlayerName(PieceTeam.White) + "  /  WHITE\n" +
            chessGame.StatisticsPlayerName(PieceTeam.Black) + "  /  BLACK\n\n" +
            chessGame.StatisticsMode + "  /  " + (lanController ? lanController.MatchConnectionState : chessGame.StatisticsConnectionState) + "\n" +
            "White  " + chessGame.OnlineClockLabel(PieceTeam.White) + "     Black  " + chessGame.OnlineClockLabel(PieceTeam.Black) + "\n" +
            "Last move: " + chessGame.LastMoveSummary;
    }
    private void CloseMatchInfo()
    {
        matchInfo.gameObject.SetActive(false);
        controls.interactable = controls.blocksRaycasts = true; resumeButton.Select();
    }

    private void TryToggleFromEscape()
    {
        if (!overlay || !CanOpen || quitting || lastEscapeFrame == Time.frameCount || SettingsMenuController.EscapeConsumedFrame == Time.frameCount) return;
        lastEscapeFrame = Time.frameCount;
        if (settingsMenu && settingsMenu.IsOpen) return;
        if (pendingAction != PendingAction.None) { CancelConfirmation(); return; }
        if (matchInfo.gameObject.activeSelf) { CloseMatchInfo(); return; }
        if (localPaused) ResumeLocalPause();
        else if (!chessGame.TryCancelSelectionFromEscape()) PauseLocally();
    }

    private void Update()
    {
        if (!overlay || quitting) return;
        if (!CanOpen) { if (localPaused) ResetPauseState(); return; }
        if (settingsMenu && settingsMenu.IsOpen || SettingsMenuController.EscapeConsumedFrame == Time.frameCount) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) TryToggleFromEscape();
        else if (UserSettings.KeyPressed("key_settings") && pendingAction == PendingAction.None) OpenSettings();
        if (matchInfo.gameObject.activeSelf) RefreshMatchInfo();
    }

    private void OnGUI()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        Event key = Event.current;
        if (key != null && key.type == EventType.KeyDown && key.keyCode == KeyCode.Escape) TryToggleFromEscape();
#endif
    }

    public void SetResultSpectating(bool spectating)
    {
        bool wasSpectating = resultSpectating; resultSpectating = spectating;
        // A normal network turn update also calls this with false; keep its local menu open.
        if (wasSpectating && !spectating) ResetPauseState();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && UserSettings.Enabled("pause_unfocused") && CanOpen && !IsMultiplayer && !localPaused) PauseLocally();
    }

    private void ResetPauseState()
    {
        if (ownsTimeScale) Time.timeScale = previousTimeScale;
        ownsTimeScale = false; localPaused = quitting = false; pendingAction = PendingAction.None;
        if (chessGame) chessGame.SetPauseLocked(false);
        if (settingsRoot) settingsRoot.SetActive(false);
        if (confirmation) confirmation.gameObject.SetActive(false);
        if (matchInfo) matchInfo.gameObject.SetActive(false);
        if (overlay) overlay.SetActive(false);
        if (EventSystem.current && EventSystem.current.currentSelectedGameObject && canvasRoot &&
            EventSystem.current.currentSelectedGameObject.transform.IsChildOf(canvasRoot.transform))
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void OnDestroy()
    {
        if (chessGame) chessGame.ReturnedToMainMenu -= ResetPauseState;
        ResetPauseState();
        if (canvasRoot) Destroy(canvasRoot);
    }
    private static void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
}
