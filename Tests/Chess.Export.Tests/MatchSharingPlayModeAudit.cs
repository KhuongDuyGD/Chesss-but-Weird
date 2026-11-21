// Runs only in the isolated fixture. Dispatches production UI events without
// opening a user scene, starting an engine, authenticating or calling a server.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ChessButWeird.Application;
using ChessButWeird.Domain;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MatchSharingPlayModeAudit
{
    private const string Pending = "CbW.SharingPlayModeAudit";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    private static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Private).GetValue(obj);
    private static readonly List<string> lines = new List<string>();
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); lines.Add("PASS " + message); }

    static MatchSharingPlayModeAudit()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
            { SessionState.SetBool(Pending, false); RunPlaying(); }
        };
    }

    public static void Run()
    {
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    private static async Task Until(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new Exception("Timed out waiting for the production UI action.");
            await Task.Delay(20);
        }
    }

    private static Button Action(MatchShareExportView view, string name) =>
        Get<GameObject>(view, "canvasRoot").GetComponentsInChildren<Button>().Single(b => b.name == name);

    private static void Click(Button button)
    {
        Check(button.interactable && button.gameObject.activeInHierarchy, "Action is enabled: " + button.name);
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
            "Visible modal receives the pointer before the result screen: " + button.name);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static async Task CheckNotice(string title, string detail)
    {
        var notice = UnityEngine.Object.FindAnyObjectByType<AuthNotificationView>();
        Check(notice && Get<TextMeshProUGUI>(notice, "titleLabel").text == title,
            "Notification title: " + title);
        string actualDetail = Get<TextMeshProUGUI>(notice, "messageLabel").text;
        Check(actualDetail.Contains(detail), "Notification contains the action result: " + title + " / " + actualDetail);
        await Until(() => Get<CanvasGroup>(notice, "group").alpha >= .99f);
        Check(notice.GetComponent<Canvas>().isRootCanvas && notice.GetComponent<Canvas>().sortingOrder > 2100,
            "Notification is visible above Share / Export even while time is paused");
    }

    private static async void RunPlaying()
    {
        int exitCode = 0; GameObject host = null; ChessFontCatalog catalog = null;
        var clipboard = new MatchClipboardAudit();
        var ownedFiles = new List<string>();
        try
        {
            catalog = ScriptableObject.CreateInstance<ChessFontCatalog>();
            Set(catalog, "tmpFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/Schoolbell/Schoolbell TMP.asset"));
            Set(catalog, "resultFallbackFont", AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf"));
            typeof(ChessFontCatalog).GetField("cachedCatalog", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, catalog);
            host = new GameObject("Sharing component regression");
            var game = host.AddComponent<ChessGame>(); game.enabled = false; // Never run auth/startup/input.
            var menuHost = new GameObject("Hidden hand drawn menu", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            menuHost.transform.SetParent(host.transform, false);
            menuHost.GetComponent<Canvas>().enabled = false;
            menuHost.GetComponent<GraphicRaycaster>().enabled = false;
            var menu = menuHost.AddComponent<ChessTurnSelectionUI>(); menu.enabled = false;
            Set(menu, "chessGame", game); Set(game, "turnSelectionUI", menu);
            menu.SetMatchPlayers("Alice", new string('B', 90));
            Set(game, "botMode", true); Set(game, "botDifficulty", StockfishDifficulty.Beginner);
            Set(game, "statisticsBotPlayerName", "Minh Nguyễn");
            Set(game, "playerTeam", PieceTeam.White); Set(game, "winningTeam", PieceTeam.White);
            Set(game, "gameOver", true); Set(game, "status", ChessGame.ChessGameStatus.Win);
            Set(game, "frozenMatchDurationSeconds", 73f);
            var coordinator = ClassicMatchCoordinator.CreateClassic(Team.White, new BoardOrientation(Team.White));
            foreach (string uci in new[] { "e2e4", "e7e5", "g1f3", "b8c6" })
            {
                StockfishMoveAdapter.TryParseUci(uci, out var move); coordinator.Submit(move, out _);
                game.Statistics.Confirm(coordinator.AcceptedMoves.Count, (Team)(1 - (int)coordinator.Turn), uci, null, true);
            }
            Set(game, "localClassicCoordinator", coordinator);
            var result = menuHost.AddComponent<ResultMenuView>(); result.Initialize(menu, null, game);
            result.Show(ResultMenuView.ResultKind.Win);
            Time.timeScale = 0;
            GUIUtility.systemCopyBuffer = "Clipboard from the previous match";
            Get<Button>(result, "share").onClick.Invoke();
            var view = menuHost.GetComponent<MatchShareExportView>();
            Check(view && MatchShareExportView.IsAnyOpen, "Result Share / Export opens the production modal");
            await Until(() => !Get<bool>(view, "busy") && Get<byte[]>(view, "png") != null && Get<ChessExportResult>(view, "pgnResult") != null);
            Check(Get<byte[]>(view, "png").Length > 10000, "Production preview coroutine produces a populated PNG");
            Check(Get<GameObject>(view, "canvasRoot").GetComponent<Canvas>().isRootCanvas, "Production modal is independent of the disabled menu canvas");
            Click(Action(view, "Copy Result"));
            Check(GUIUtility.systemCopyBuffer.Contains("White: Minh Nguyễn") && GUIUtility.systemCopyBuffer.Contains("Black: Pip") &&
                !GUIUtility.systemCopyBuffer.Contains("Alice") && !GUIUtility.systemCopyBuffer.Contains("BBBB"),
                "Copy Result replaces stale clipboard data with the real Pip participants");
            await CheckNotice("Result copied", "Result copied");

            Click(Action(view, "Copy PGN")); await Until(() => !Get<bool>(view, "busy"));
            Check(GUIUtility.systemCopyBuffer.Contains("[White \"Minh Nguyễn\"]") && GUIUtility.systemCopyBuffer.Contains("[Black \"Pip\"]") &&
                GUIUtility.systemCopyBuffer.Contains("1. e4 e5 2. Nf3 Nc6"), "PGN button copies current participants and accepted commands");
            await CheckNotice("PGN copied", "PGN copied");
            Click(Action(view, "Copy FEN")); await Until(() => !Get<bool>(view, "busy"));
            Check(GUIUtility.systemCopyBuffer == FenCodec.Write(coordinator.Snapshot), "FEN button copies the current authoritative six-field position");
            await CheckNotice("FEN copied", "FEN copied");

            foreach (string name in new[] { "Save Image", "Export PGN", "Export FEN" })
            {
                Click(Action(view, name));
                Check(Get<bool>(view, "busy"), "Action immediately prevents repeat exports: " + name);
                Action(view, name).onClick.Invoke();
                await Until(() => !Get<bool>(view, "busy"));
                string status = Get<TextMeshProUGUI>(view, "status").text;
                string path = status.Split('\n').Last(); ownedFiles.Add(path);
                Check(File.Exists(path), "Action persists the exported file: " + name);
                if (name.Contains("Image")) Check(File.ReadAllBytes(path).SequenceEqual(Get<byte[]>(view, "png")), "Saved PNG equals the preview");
                else Check(File.ReadAllText(path) == MatchExportService.Export(game.CaptureExportRecord(), name == "Export PGN" ? ChessExportKind.Pgn : ChessExportKind.Fen).Text,
                    "Saved chess data equals the validated current match");
                await CheckNotice(name == "Save Image" ? "Image saved" : name == "Export PGN" ? "PGN saved" : "FEN saved", path);
            }

            var exportsBeforeCopy = new HashSet<string>(Directory.GetFiles(MatchExportFiles.ExportDirectory));
            Click(Action(view, "Copy Image"));
            Check(Get<bool>(view, "busy"), "Copy Image prevents repeated work while copying");
            await Until(() => !Get<bool>(view, "busy"));
            await CheckNotice("Image copied", "Image copied");
            MatchClipboardAudit.CheckImage(Get<byte[]>(view, "png"), Check);
            Check(exportsBeforeCopy.SetEquals(Directory.GetFiles(MatchExportFiles.ExportDirectory)), "Copy Image creates no unwanted saved export");
            using (MatchClipboardAudit.HoldOpen())
            {
                Click(Action(view, "Copy Image")); await Until(() => !Get<bool>(view, "busy"));
                await CheckNotice("Clipboard unavailable", "clipboard is busy");
            }
            MatchClipboardAudit.CheckImage(Get<byte[]>(view, "png"), Check);

            // Recover a missing preview and stale notation status through the
            // actual Refresh button, including while the game clock is paused.
            var oldRoot = Get<GameObject>(view, "canvasRoot");
            var oldTexture = Get<RawImage>(view, "preview").texture as RenderTexture;
            Set(view, "png", null); Get<RawImage>(view, "preview").texture = null;
            Set(view, "pgnResult", ChessExportResult.Fail("Simulated stale validation"));
            Click(Action(view, "Refresh"));
            Check(Get<bool>(view, "busy"), "Refresh starts a new render immediately");
            await Until(() => !Get<bool>(view, "busy") && Get<ChessExportResult>(view, "pgnResult") != null);
            Check(Get<GameObject>(view, "canvasRoot") != oldRoot && Get<byte[]>(view, "png") != null &&
                Get<RawImage>(view, "preview").texture != null, "Refresh rebuilds the complete view and restores the missing preview");
            Check(!oldTexture || !oldTexture.IsCreated(), "Refresh releases the old render target");
            Check(Get<ChessExportResult>(view, "pgnResult").Success && Action(view, "Copy PGN").interactable,
                "Refresh revalidates notation instead of retaining a stale error");

            Set(game, "aramMode", true);
            string beforeRejection = GUIUtility.systemCopyBuffer;
            Click(Action(view, "Copy PGN")); await Until(() => !Get<bool>(view, "busy"));
            Check(GUIUtility.systemCopyBuffer == beforeRejection, "Rejected notation cannot replace the clipboard");
            await CheckNotice("Export unavailable", "Chaos");
            Check(Get<TextMeshProUGUI>(view, "status").color.r > Get<TextMeshProUGUI>(view, "status").color.g,
                "Rejected export shows an inline error state");
            view.Close();
            view.Show(game); await Until(() => !Get<bool>(view, "busy"));
            Check(!Get<GameObject>(view, "canvasRoot").GetComponentsInChildren<Button>().Any(b => b.name.Contains("PGN") || b.name.Contains("FEN")),
                "Opening an actual Chaos result creates no notation actions");
            Click(Action(view, "Copy Image")); await Until(() => !Get<bool>(view, "busy"));
            MatchClipboardAudit.CheckImage(Get<byte[]>(view, "png"), Check);
            Click(Action(view, "Refresh")); await Until(() => !Get<bool>(view, "busy"));
            Check(!Get<GameObject>(view, "canvasRoot").GetComponentsInChildren<Button>().Any(b => b.name.Contains("PGN") || b.name.Contains("FEN")),
                "Refresh preserves Chaos notation restrictions");
            view.Close(); Set(game, "aramMode", false);

            // A new completed session must regenerate both card and metadata.
            Set(game, "botDifficulty", StockfishDifficulty.Learner); Set(game, "playerTeam", PieceTeam.Black);
            Set(game, "status", ChessGame.ChessGameStatus.Draw); Set(game, "drawReason", "Draw by agreement");
            view.Show(game); await Until(() => !Get<bool>(view, "busy") && Get<ChessExportResult>(view, "pgnResult") != null);
            Click(Action(view, "Copy Result"));
            Check(GUIUtility.systemCopyBuffer.Contains("White: Pebble") && GUIUtility.systemCopyBuffer.Contains("Black: Minh Nguyễn") &&
                GUIUtility.systemCopyBuffer.Contains("1/2-1/2") && !GUIUtility.systemCopyBuffer.Contains("Pip"),
                "Reopening for a different bot, side and result never reuses Pip metadata");
            GUIUtility.systemCopyBuffer = "Clipboard preserved after closing";
            Click(Action(view, "Copy PGN")); view.Close();
            await Task.Delay(200);
            Check(GUIUtility.systemCopyBuffer == "Clipboard preserved after closing", "A closed modal ignores late async clipboard completion");
            Check(!MatchShareExportView.IsAnyOpen && Get<MatchResultCardRenderer>(view, "cardRenderer") == null,
                "Closing releases preview resources and modal ownership");
            File.WriteAllLines("sharing-playmode-result.txt", lines);
            Debug.Log("PASS " + lines.Count + " component-level sharing play-mode checks");
        }
        catch (Exception error)
        { exitCode = 1; Debug.LogException(error); File.WriteAllText("sharing-playmode-result.txt", "FAIL " + error); }
        finally
        {
            // Restore before requesting Editor exit: no test summary may remain
            // in the user's shared Windows clipboard if Unity exits immediately.
            clipboard.Dispose(); Time.timeScale = 1;
            if (host) UnityEngine.Object.DestroyImmediate(host);
            if (catalog) UnityEngine.Object.DestroyImmediate(catalog);
            var notice = UnityEngine.Object.FindAnyObjectByType<AuthNotificationView>();
            if (notice) UnityEngine.Object.DestroyImmediate(notice.gameObject);
            foreach (string path in ownedFiles) if (File.Exists(path)) File.Delete(path);
        }
        EditorApplication.Exit(exitCode);
    }
}
