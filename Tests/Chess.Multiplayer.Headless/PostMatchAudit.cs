using System;
using System.Collections.Generic;
using System.Reflection;
using ChessButWeird.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class PostMatchAudit
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    public static void CheckPostMatch(GameObject host, ChessGame game, ChessTurnSelectionUI menu, ChessLanController controller, List<string> lines)
    {
        Set(game, "turnSelectionUI", menu); Set(menu, "lanController", controller);
        Set(game, "botMode", false); Set(game, "gameStarted", false); Set(game, "gameOver", true);
        Set(game, "winningTeam", PieceTeam.White);
        Set(game, "frozenMatchDurationSeconds", 125f);
        menu.SetMatchPlayers("Alice\nPlayer", "Stockfish");
        foreach (var winner in new[] { PieceTeam.White, PieceTeam.Black })
        {
            Set(game, "winningTeam", winner);
            string summary = MatchResultSharing.BuildText(game);
            Check(summary.Contains(winner == PieceTeam.White ? "1-0" : "0-1"), "Wrong winning score");
            Check(summary.Contains("White: Alice Player") && summary.Contains("Black: Stockfish") && summary.Contains("02:05"), "Incomplete share summary");
        }
        Set(game, "status", ChessGame.ChessGameStatus.Draw); Set(game, "drawReason", "Stalemate");
        Check(MatchResultSharing.BuildText(game).Contains("1/2-1/2") && MatchResultSharing.BuildText(game).Contains("Stalemate"), "Draw share lost reason/score");
        game.MarkStatisticsTimeUnavailable();
        Check(MatchResultSharing.BuildText(game).Contains("Time played: unavailable"), "Recovery invented a duration");
        foreach (bool bot in new[] { false, true }) foreach (bool online in new[] { false, true }) foreach (bool chaos in new[] { false, true })
        {
            Set(game, "botMode", bot); Set(game, "serverAuthoritativeMode", online); Set(game, "aramMode", chaos);
            Check(MatchResultSharing.CanShare(game), "Completed game omitted sharing in a supported mode");
        }
        Set(game, "gameOver", false);
        Check(!MatchResultSharing.CanShare(game) && MatchResultSharing.BuildText(game) == "", "Unfinished game shared a result");
        Set(game, "gameOver", true); Set(game, "botMode", true); Set(game, "serverAuthoritativeMode", false); Set(game, "aramMode", false);
        lines.Add("PASS post-match sharing: every mode including Chaos/local, win/loss/draw scores, names, time and unavailable recovery time");

        var result = host.AddComponent<ResultMenuView>(); result.Initialize(menu, null, game); Set(menu, "resultMenu", result);
        var cameraHost = new GameObject("Post-match camera", typeof(Camera));
        var orbit = cameraHost.AddComponent<ChessOrbitCamera>(); orbit.Bind(game, null);
        try
        {
            result.Show(ResultMenuView.ResultKind.Win);
            Check(!game.IsReviewingFinishedGame, "Result menu prematurely enabled review input");
            orbit.ToggleOrbitLock(); Check(!orbit.IsOrbitUnlocked, "Result modal accepted camera toggle");
            var share = Get<Button>(result, "share"); var replay = Get<Button>(result, "replay");
            Check(share.gameObject.activeSelf, "Bot result omitted share action");
            Check(((RectTransform)share.transform).rect.width == ((RectTransform)replay.transform).rect.width, "Result actions have inconsistent widths");
            Get<Button>(result, "spectate").onClick.Invoke();
            Check(game.IsReviewingFinishedGame && Get<GameObject>(result, "reviewCanvas").activeSelf, "View board did not enter review");
            Call(orbit, "LateUpdate"); Check(orbit.IsOrbitUnlocked, "Finished review did not automatically unlock orbit");
            orbit.ToggleOrbitLock(); Check(!orbit.IsOrbitUnlocked, "Review cannot lock orbit");
            orbit.ToggleOrbitLock(); Check(orbit.IsOrbitUnlocked, "Review cannot unlock orbit");
            Set(game, "pauseLocked", true); orbit.ToggleOrbitLock();
            Check(orbit.IsOrbitUnlocked, "Pause did not block camera toggles"); Set(game, "pauseLocked", false);
            Set(game, "contentLoading", true); Check(!game.IsReviewingFinishedGame, "Loading accepted review input"); Set(game, "contentLoading", false);
            Check(Get<Button>(result, "reviewShare").gameObject.activeSelf, "Bot review omitted share action");
            Check(Get<Button>(result, "reviewLeave").GetComponentInChildren<MatchHudIconGraphic>() != null, "Review exit omitted its icon");

            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(960, 720), new Vector2(2560, 720), new Vector2(1280, 2276) })
            {
                foreach (var canvas in host.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.renderMode = RenderMode.WorldSpace; canvas.GetComponent<CanvasScaler>().enabled = false;
                    ((RectTransform)canvas.transform).sizeDelta = size;
                }
                Canvas.ForceUpdateCanvases();
                foreach (var frame in host.GetComponentsInChildren<MenuDesignFrame>(true)) Call(frame, "Apply");
                Canvas.ForceUpdateCanvases();
                foreach (var button in new[] { Get<Button>(result, "reviewLeave"), Get<Button>(result, "reviewShare") })
                {
                    var rect = (RectTransform)button.transform; var parent = (RectTransform)rect.parent;
                    var bounds = new Rect(rect.anchoredPosition + Vector2.Scale(parent.rect.size, rect.anchorMin) + parent.rect.min - rect.sizeDelta / 2, rect.sizeDelta);
                    Check(parent.rect.Contains(bounds.min) && parent.rect.Contains(bounds.max), "Review action outside safe area at " + size);
                }
            }
            result.SetReviewExitPending(true, "Returning to lobby...");
            Check(!Get<Button>(result, "reviewLeave").interactable && !Get<Button>(result, "reviewShare").interactable, "Exit accepted duplicate actions");
            result.SetReviewExitPending(false, "Could not leave. Try again.");
            Check(Get<Button>(result, "reviewLeave").interactable && Get<TextMeshProUGUI>(result, "reviewStatus").text.Contains("Try again"), "Exit failure cannot be retried");
            Set(game, "aramMode", true); result.Show(ResultMenuView.ResultKind.Draw);
            Check(share.gameObject.activeSelf && share.transform.parent.Find("Share Result Pencil Shadow").gameObject.activeSelf, "Chaos result omitted sharing");
            Check(((RectTransform)replay.transform).rect.width == 410, "Chaos result actions overlap");
            menu.SpectateFinishedGame();
            Check(Get<Button>(result, "reviewShare").gameObject.activeSelf, "Chaos finished board omitted sharing");
            Set(game, "aramMode", false);
            Get<Button>(result, "reviewLeave").onClick.Invoke();
            Check(!game.GameOver && !game.IsReviewingFinishedGame && !Get<GameObject>(result, "reviewCanvas").activeSelf && !Get<bool>(menu, "resultExitInFlight"), "Bot exit retained terminal/review/busy UI state");
            Check(Get<object>(menu, "state").ToString() == "TurnSelection", "Bot exit did not return to lobby selection");
            Call(orbit, "LateUpdate"); orbit.ToggleOrbitLock();
            Check(orbit.IsOrbitUnlocked, "Menu camera accepted gameplay input");
            orbit.ConfigureForPlayerSide(PieceTeam.Black, true);
            Check(!orbit.IsOrbitUnlocked, "New match retained review unlock state");
            lines.Add("PASS review: automatic camera unlock, lock toggle, pause/loading guards, bot lobby exit and next-match reset");
            lines.Add("PASS result/review UI: sharing visibility, replay layout, safe-area actions at 4 aspect ratios and retryable exit errors");

            foreach (var mode in new[] { PlayModeOption.ClassicNormal, PlayModeOption.ChaosNormal })
            {
                var session = (OnlineSession)controller.GetType().GetMethod("EnsureOnline", Private).Invoke(controller, null);
                typeof(OnlineSession).GetProperty("State").SetValue(session, new MatchState { matchId = "finished-" + mode, status = "Finished" });
                Set(controller, "directMatchmaking", true); Set(controller, "selectedOnlineMode", mode);
                controller.LeaveMatchFromPauseAsync().GetAwaiter().GetResult();
                Check(Get<OnlineSession>(controller, "online") == null && Get<bool>(controller, "directMatchmaking") && Get<PlayModeOption>(controller, "selectedOnlineMode") == mode,
                    "Finished online exit lost its lobby or session cleanup for " + mode);
            }
            lines.Add("PASS finished Classic/Chaos sessions dispose without resigning and preserve their destination lobby modes");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cameraHost);
            UnityEngine.Object.DestroyImmediate(result);
            Set(menu, "resultMenu", null);
            game.RestartToMainMenu();
        }
    }
}
