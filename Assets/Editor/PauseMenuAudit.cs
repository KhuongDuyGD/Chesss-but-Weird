using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChessButWeird.Online;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>Isolated Play Mode checks using real buttons, chess input and a loopback SignalR peer.</summary>
[InitializeOnLoad]
public static class PauseMenuAudit
{
    private const string Key = "Chess.PauseAudit.Active", Folder = "Documentation/UI/PauseMenu";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> checks = new List<string>();
    private static ChessGame game;
    private static ChessPauseMenu pause;
    private static ChessTurnSelectionUI ui;
    private static ChessLanController lan;
    private static HandDrawnMenuView menu;
    private static GameObject root;
    private static readonly List<LoopbackPeer> peers = new List<LoopbackPeer>();
    private static readonly List<OnlineSession> sessions = new List<OnlineSession>();
    private static string runtimeError;

    static PauseMenuAudit()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += RunFixtures;
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                string scene = SessionState.GetString(Key + ".Scene", "");
                if (!string.IsNullOrEmpty(scene)) EditorSceneManager.OpenScene(scene);
            }
        };
    }

    [MenuItem("Chess/UI Audit/Test Pause Menu")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.sceneCount != 1 || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Run the audit from a saved scene in Edit Mode.");
        SessionState.SetString(Key + ".Scene", EditorSceneManager.GetActiveScene().path);
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static async void RunFixtures()
    {
        checks.Clear(); peers.Clear(); sessions.Clear(); runtimeError = null;
        Directory.CreateDirectory(Folder);
        var authProperties = new[] { "CurrentProfile", "CurrentApiUser", "IsGuestSession" }.Select(n => typeof(PlayerAuthService).GetProperty(n)).ToArray();
        var authValues = authProperties.Select(p => p.GetValue(null)).ToArray();
        var keys = new[] { "api.auth.accessToken", "api.auth.refreshToken", "api.auth.accessExpiresAt", "api.auth.refreshExpiresAt" };
        var existed = keys.Select(PlayerPrefs.HasKey).ToArray();
        var values = keys.Select(k => PlayerPrefs.GetString(k)).ToArray();
        float oldFocus = UserSettings.Get("pause_unfocused"), oldMotion = UserSettings.Get("reduce_motion");
        Application.logMessageReceived += OnLog;
        try
        {
            UserSettings.Manager.Set("pause_unfocused", 0);
            authProperties[0].SetValue(null, new PlayerProfile { playerId = "__pause_audit__", username = "Pause Audit" });
            authProperties[1].SetValue(null, new UserMeResponse { userId = "__pause_audit__" });
            authProperties[2].SetValue(null, false);
            string expiry = DateTimeOffset.UtcNow.AddHours(1).ToString("O");
            AuthStorage.SaveTokens("loopback-only", "loopback-only", expiry, expiry);
            BuildFixture();
            await Frame();
            await Singleplayer();
            await LoadedMatchExit();
            await Multiplayer();
            Check(runtimeError == null, "No unexpected project runtime exceptions");
            checks.Add("RESULT PASS: all 13 singleplayer and 15 multiplayer required cases, plus regression checks.");
            Debug.Log("[PauseMenuAudit] PASS " + checks.Count + " assertions. Report: " + Folder);
        }
        catch (Exception error)
        {
            checks.Add("RESULT FAIL: " + error);
            Debug.LogException(error);
        }
        finally
        {
            lan?.CloseSessionFromPause();
            foreach (var session in sessions) session.Dispose();
            foreach (var peer in peers) peer.Dispose();
            if (root) UnityEngine.Object.Destroy(root);
            Time.timeScale = 1;
            for (int i = 0; i < authProperties.Length; i++) authProperties[i].SetValue(null, authValues[i]);
            for (int i = 0; i < keys.Length; i++)
                if (existed[i]) PlayerPrefs.SetString(keys[i], values[i]); else PlayerPrefs.DeleteKey(keys[i]);
            PlayerPrefs.DeleteKey("chess.online.room.v1.__pause_audit__");
            UserSettings.Manager.Set("pause_unfocused", oldFocus);
            UserSettings.Manager.Set("reduce_motion", oldMotion);
            UserSettings.Manager.Flush(); PlayerPrefs.Save();
            File.WriteAllLines(Path.Combine(Folder, "checks.txt"), checks);
            Application.logMessageReceived -= OnLog;
            EditorApplication.ExitPlaymode();
        }
    }

    private static void BuildFixture()
    {
        root = new GameObject("Pause Audit Fixture", typeof(AudioListener));
        var board = root.AddComponent<Chessboard>(); board.enabled = false;
        game = root.AddComponent<ChessGame>(); game.enabled = false; game.AttachBoard(board);
        var uiRoot = new GameObject("Fixture Match UI", typeof(RectTransform)); uiRoot.transform.SetParent(root.transform);
        ui = uiRoot.AddComponent<ChessTurnSelectionUI>(); ui.enabled = false;
        Set(ui, "chessGame", game); Set(game, "turnSelectionUI", ui);
        Call(ui, "TryCreateHandDrawnMenu"); Call(ui, "TryCreateLanController"); Call(ui, "TryCreatePauseMenu");
        menu = Get<HandDrawnMenuView>(ui, "handDrawnMenu");
        lan = Get<ChessLanController>(ui, "lanController"); lan.enabled = false;
        pause = Get<ChessPauseMenu>(ui, "pauseMenu");
        // Keep navigation rendering real, but don't fetch inventory/profile data in an isolated fixture.
        foreach (var component in root.GetComponentsInChildren<InventoryMenuController>(true)) component.enabled = false;
        Check(menu && menu.IsReady, "Existing menu destinations are available");
    }

    private static void BeginLocal() { game.RestartToMainMenu(); game.BeginBotGame(PieceTeam.White, StockfishDifficulty.Easy); }
    private static async Task Singleplayer()
    {
        BeginLocal(); await Frame();
        pause.Open(); Check(pause.IsOpen && Get<bool>(game, "pauseLocked") && Time.timeScale == 0, "SP 01: Open Pause Menu pauses local play");
        Check(!game.TrySelectPiece(game.GetActivePiecesForAram(PieceTeam.White).First()), "Local pause actually prevents board selection");
        Capture("Singleplayer");
        Click("Resume"); Check(!pause.IsOpen && !game.InputLocked && Time.timeScale == 1, "SP 02: Resume restores control immediately");
        await Escape(); Check(pause.IsOpen, "SP 03a: physical ESC opens menu");
        await Escape(); Check(!pause.IsOpen && !game.InputLocked, "SP 03b: physical ESC closes menu");
        pause.Open(); Click("Restart Match");
        Check(Get<RectTransform>(pause, "confirmation").gameObject.activeSelf, "SP 04a: Restart opens confirmation");
        Capture("Restart-Confirmation");
        Click("Cancel Pause Action"); Check(pause.IsOpen && game.GameStarted, "SP 05: Cancel Restart keeps current match");
        Click("Restart Match"); Click("Confirm Pause Action");
        Check(!pause.IsOpen && game.GameStarted && !game.InputLocked && game.ExportFen() == ChessButWeird.Domain.FenCodec.InitialPosition,
            "SP 04b: Confirm Restart resets board and resumes");
        Check(game.IsBotGame && Get<StockfishDifficulty>(game, "botDifficulty") == StockfishDifficulty.Easy, "Restart preserves bot mode and difficulty");
        pause.Open(); Click("Settings");
        Check(Get<SettingsMenuController>(pause, "settingsMenu").IsOpen && Get<bool>(game, "pauseLocked"), "SP 06: Existing Settings opens while paused");
        Call(Get<SettingsMenuController>(pause, "settingsMenu"), "Close"); await Frame();
        Check(pause.IsOpen && Get<CanvasGroup>(pause, "controls").interactable && Time.timeScale == 0, "SP 07: Settings returns to Pause Menu");
        Click("Leave Match"); Check(Get<RectTransform>(pause, "confirmation").gameObject.activeSelf, "SP 08: Leave opens confirmation");
        Click("Cancel Pause Action"); Check(pause.IsOpen && game.GameStarted, "SP 09: Cancel Leave preserves match");
        Click("Leave Match"); Click("Confirm Pause Action"); await Wait(() => ScreenIs("botDifficultyScreen"));
        Check(!game.GameStarted && !pause.IsOpen && !game.InputLocked && Time.timeScale == 1, "SP 10: Leave clears match and returns to Bot Selection");
        BeginLocal(); pause.Open(); Click("Main Menu"); Click("Confirm Pause Action"); await Wait(() => ScreenIs("modeScreen"));
        Check(!game.GameStarted && !pause.IsOpen, "SP 11: Main Menu returns directly to existing Main Menu 2");
        BeginLocal(); pause.Open(); int quits = 0; Set(pause, "quitDesktop", (Action)(() => quits++));
        Click("Quit Desktop"); Click("Cancel Pause Action"); Check(quits == 0, "Quit cancellation does not invoke desktop exit");
        Click("Quit Desktop"); Click("Confirm Pause Action"); Check(quits == 1 && !pause.IsOpen && Time.timeScale == 1, "SP 12: Confirm Quit invokes desktop exit once (intercepted in Editor)");
        BeginLocal(); pause.Open(); Click("Resume");
        var pawn = game.GetActivePiecesForAram(PieceTeam.White).First(p => p.BoardPosition == new Vector2Int(4, 1));
        Check(game.TrySelectPiece(pawn), "SP 13: Singleplayer accepts board selection after closing menu");
        await Escape(); Check(!pause.IsOpen, "ESC preserves the existing first-press deselect behavior");
        Check(game.TrySelectPiece(pawn) && game.TryMoveSelectedPiece(new Vector2Int(4, 3)), "Singleplayer can actually commit a move after Resume");
        await Wait(() => !game.InputLocked);
        Time.timeScale = .75f; pause.Open(); Click("Resume"); Check(Time.timeScale == .75f, "Resume restores an existing local time scale"); Time.timeScale = 1;
        UserSettings.Manager.Set("reduce_motion", 1); pause.Open();
        Check(pause.IsOpen && Get<Button>(pause, "resumeButton").colors.fadeDuration == 0, "Reduce Motion: no menu animation or delayed control"); Click("Resume");
        UserSettings.Manager.Set("reduce_motion", 0);
    }

    private static async Task LoadedMatchExit()
    {
        var loader = LoadingManager.For(game); var board = game.Board;
        BeginLocal(); pause.Open();
        typeof(LoadingManager).GetProperty("IsBusy").SetValue(loader, true);
        Click("Leave Match"); Click("Confirm Pause Action");
        Check(pause.IsOpen && game.GameStarted && !Get<bool>(pause, "quitting") && Get<CanvasGroup>(pause, "controls").interactable,
            "Busy content owner does not swallow a Pause exit or lock controls indefinitely");
        typeof(LoadingManager).GetProperty("IsBusy").SetValue(loader, false); Click("Resume");
        foreach (bool mainMenu in new[] { false, true })
        {
            BeginLocal();
            var assets = new AssetLoader(); Set(loader, "matchAssets", assets);
            var contentScene = UnityEngine.SceneManagement.SceneManager.CreateScene("Pause Audit Match Content"); Set(loader, "ownedScene", contentScene);
            pause.Open(); Click(mainMenu ? "Main Menu" : "Leave Match"); Click("Confirm Pause Action");
            Check(loader.IsBusy, "Loaded match exit uses existing asynchronous content owner");
            await Wait(() => !loader.IsBusy && ScreenIs(mainMenu ? "modeScreen" : "botDifficultyScreen"));
            Check(assets.IsDisposed && !contentScene.isLoaded && !game.GameStarted && !pause.IsOpen && Time.timeScale == 1,
                "Content handles released and additive scene unloaded before " + (mainMenu ? "Main Menu 2" : "Bot Selection"));
            game.AttachBoard(board);
        }
    }

    private static async Task Multiplayer()
    {
        var pair = await BeginNetwork("Multiplayer", "Classic");
        pause.Open(); Check(pause.IsOpen && Get<bool>(game, "pauseLocked") && Time.timeScale == 1, "MP 01: Match Menu locks only local interaction"); Capture("Multiplayer");
        Click("Return to Match"); Check(!pause.IsOpen && !game.InputLocked, "MP 02: Return restores local interaction immediately");
        await Escape(); Check(pause.IsOpen, "MP 03a: ESC opens Match Menu"); await Escape(); Check(!pause.IsOpen, "MP 03b: ESC closes Match Menu");
        pause.Open(); Check(!ActiveButtons().Any(b => b.name == "Restart Match"), "MP 04: No Restart button exists in multiplayer actions");
        Click("Settings"); Check(Get<SettingsMenuController>(pause, "settingsMenu").IsOpen, "MP 05: Open existing Settings");
        // Exercise Settings' own ESC handling as well as its callback.
        await Escape(); Check(!Get<SettingsMenuController>(pause, "settingsMenu").IsOpen && pause.IsOpen, "MP 06: ESC from Settings returns to Match Menu, not gameplay");
        Click("Match Info"); Check(Get<RectTransform>(pause, "matchInfo").gameObject.activeSelf, "MP 07: Match Info shows existing players/mode/clocks/connection"); Capture("Match-Info"); Click("Back from Match Info");
        Click("Leave Match"); Check(Get<RectTransform>(pause, "confirmation").gameObject.activeSelf, "MP 08: Leave opens confirmation"); Click("Cancel Pause Action");
        Check(pair.session.Connected && pair.peer.ResignCalls == 0 && game.GameStarted, "MP 09: Cancel Leave keeps session and match intact");
        string beforeClock = game.OnlineClockLabel(PieceTeam.White);
        await Task.Delay(1100);
        Check(game.OnlineClockLabel(PieceTeam.White) != beforeClock && pause.IsOpen, "Online clock advances in real time while Match Menu is open");
        int stateEvents = 0; pair.session.StateChanged += _ => { stateEvents++; ui.SetTurn(PieceTeam.Black); };
        await pair.peer.PushState(); await Wait(() => stateEvents > 0);
        Check(pause.IsOpen && pair.session.Connected && pair.session.State.eventSequence == 1 && Time.timeScale == 1,
            "MP 14: Real WebSocket snapshot still arrives while menu is open; turn updates do not close menu");
        pair.peer.HoldExit = true;
        // Prevent REST lobby refresh only in this fixture; still render the real destination lobby.
        Action noExternalRefresh = () => Set(lan, "requestInFlight", true); game.ReturnedToMainMenu += noExternalRefresh;
        int navigations = 0; Action counted = () => navigations++; game.ReturnedToMainMenu += counted;
        Click("Leave Match"); var confirm = ActiveButtons().First(b => b.name == "Confirm Pause Action");
        confirm.onClick.Invoke(); confirm.onClick.Invoke(); Call(pause, "ConfirmAction");
        await Wait(() => pair.peer.ResignCalls == 1);
        var exit1 = lan.LeaveMatchFromPauseAsync(); var exit2 = lan.LeaveMatchFromPauseAsync();
        Check(ReferenceEquals(exit1, exit2) && navigations == 0, "MP 15a: Repeated confirms/exits share one pending disconnect; no early transition");
        pair.peer.ReleaseExit(); await exit1; await Wait(() => Get<bool>(lan, "showLanPanel"));
        Check(Get<object>(lan, "lobbyMode").ToString() == "Multiplayer" && Get<string>(lan, "requestedGameMode") == "Classic" && !game.GameStarted && !pause.IsOpen,
            "MP 10: Leave returns to originating Classic Multiplayer Lobby");
        Check(!pair.session.Connected && pair.session.State == null && Get<bool>(pair.session, "disposed"), "MP 13: Old session state and WebSocket disposed after acknowledged leave");
        Check(pair.peer.ResignCalls == 1 && navigations == 1, "MP 15b: Exactly one resign and one match transition after repeated clicks");
        game.ReturnedToMainMenu -= noExternalRefresh; game.ReturnedToMainMenu -= counted;

        foreach (var origin in new[] { new[] { "Lan", "Classic" }, new[] { "Lan", "Aram" }, new[] { "Multiplayer", "Aram" } })
        {
            await BeginNetwork(origin[0], origin[1]); game.ReturnedToMainMenu += noExternalRefresh;
            pause.Open(); Click("Leave Match"); Click("Confirm Pause Action"); await Wait(() => Get<bool>(lan, "showLanPanel"));
            Check(Get<object>(lan, "lobbyMode").ToString() == origin[0] && Get<string>(lan, "requestedGameMode") == origin[1],
                "Originating lobby preserved: " + origin[0] + " / " + origin[1]);
            game.ReturnedToMainMenu -= noExternalRefresh;
        }
        pair = await BeginNetwork("Multiplayer", "Classic"); pause.Open(); Click("Main Menu"); Click("Confirm Pause Action"); await Wait(() => ScreenIs("modeScreen"));
        Check(!pair.session.Connected && !game.GameStarted && !pause.IsOpen, "MP 11: Main Menu cleans session and returns to Main Menu 2");
        pair = await BeginNetwork("Multiplayer", "Classic"); int quits = 0; Set(pause, "quitDesktop", (Action)(() => quits++)); pause.Open(); Click("Quit Desktop"); Click("Confirm Pause Action"); await Wait(() => quits == 1);
        Check(!pair.session.Connected && Get<OnlineSession>(lan, "online") == null && !pause.IsOpen, "MP 12: Desktop exit cleans the session before invoking quit once (intercepted)");

        pair = await BeginNetwork("Multiplayer", "Classic"); pair.peer.RejectExit = true;
        quits = 0; Set(pause, "quitDesktop", (Action)(() => quits++)); pause.Open(); Click("Quit Desktop"); Click("Confirm Pause Action"); await Wait(() => quits == 1);
        Check(!pair.session.Connected && Get<OnlineSession>(lan, "online") == null, "Desktop still disposes transport and quits when server rejects exit acknowledgement");

        pair = await BeginNetwork("Multiplayer", "Classic"); pair.peer.HoldExit = true;
        quits = 0; Set(pause, "quitDesktop", (Action)(() => quits++)); pause.Open(); Click("Quit Desktop"); Click("Confirm Pause Action"); await Wait(() => quits == 1);
        Check(!pair.session.Connected && pair.peer.ResignCalls == 1, "Unresponsive server cannot block desktop exit beyond five seconds; local transport is cleaned");
        pair.peer.ReleaseExit();

        pair = await BeginNetwork("Multiplayer", "Classic"); pair.peer.RejectExit = true; pause.Open(); Click("Leave Match"); Click("Confirm Pause Action");
        await Wait(() => !Get<bool>(pause, "quitting"));
        Check(pause.IsOpen && Get<CanvasGroup>(pause, "controls").interactable && pair.session.Connected && pair.session.State.IsActive,
            "Rejected server exit keeps match/session and offers retry or Resume");
        pair.peer.RejectExit = false; Click("Return to Match"); Check(!pause.IsOpen && !game.InputLocked, "Can resume after failed exit");
        Set(game, "gameOver", true); Set(game, "gameStarted", false); pause.SetResultSpectating(true); pause.Open();
        Check(!ActiveButtons().Any(b => b.name == "Restart Match"), "Finished multiplayer spectating still has no local Restart");
        typeof(OnlineSession).GetProperty("State").SetValue(pair.session, new MatchState { matchId = pair.peer.State.matchId, status = "Finished" });
        int beforeResigns = pair.peer.ResignCalls; game.ReturnedToMainMenu += noExternalRefresh;
        Click("Leave Match"); Click("Confirm Pause Action"); await Wait(() => Get<bool>(lan, "showLanPanel"));
        Check(pair.peer.ResignCalls == beforeResigns && !pair.session.Connected, "Leaving finished multiplayer closes session without another surrender");
        game.ReturnedToMainMenu -= noExternalRefresh;
        lan.CloseSessionFromPause();
    }

    private static async Task<(OnlineSession session, LoopbackPeer peer)> BeginNetwork(string mode, string variant)
    {
        lan.CloseSessionFromPause(); Set(lan, "requestInFlight", false); BeginLocal(); await Frame();
        var peer = new LoopbackPeer(variant); peers.Add(peer);
        var socket = new ClientWebSocket(); await socket.ConnectAsync(peer.Uri, CancellationToken.None); await peer.Ready.Task;
        var session = new OnlineSession(); sessions.Add(session);
        var hub = Get<SignalRGameTransport>(session, "hub"); var cancel = new CancellationTokenSource();
        Set(hub, "socket", socket); Set(hub, "lifetime", cancel); Set(hub, "connected", true);
        Set(hub, "handshake", new TaskCompletionSource<bool>());
        Call(hub, "ReceiveAsync", socket, cancel, Get<int>(hub, "generation"));
        var initial = JsonConvert.DeserializeObject<MatchState>(JsonConvert.SerializeObject(peer.State));
        typeof(OnlineSession).GetProperty("State").SetValue(session, initial);
        Set(lan, "online", session); SetEnum(lan, "lobbyMode", mode); Set(lan, "requestedGameMode", variant);
        Set(game, "serverAuthoritativeMode", true); Set(game, "dotNetState", initial);
        Set(game, "botMode", false); Set(game, "aramMode", variant == "Aram");
        ui.SetMatchPlayers("Audit Player", "Loopback Opponent");
        Set(game, "dotNetSnapshotReceivedAt", Time.realtimeSinceStartup);
        return (session, peer);
    }

    private static async Task Escape()
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(UnityEngine.InputSystem.Key.Escape)); await Frame(); await Frame();
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); await Frame(); await Frame();
    }
    private static bool ScreenIs(string field) => Get<RectTransform>(menu, "currentScreen") == Get<RectTransform>(menu, field) && Get<RectTransform>(menu, field).gameObject.activeSelf;
    private static Button[] ActiveButtons() => Get<GameObject>(pause, "canvasRoot").GetComponentsInChildren<Button>();
    private static void Click(string name) => ActiveButtons().First(b => b.name == name).onClick.Invoke();
    private static async Task Frame() { await Task.Yield(); await Task.Delay(20); }
    private static async Task Wait(Func<bool> condition)
    {
        double start = EditorApplication.timeSinceStartup;
        while (!condition()) { if (EditorApplication.timeSinceStartup - start > 12) throw new TimeoutException("Pause audit condition timed out."); await Frame(); }
    }
    private static void Check(bool value, string text)
    { checks.Add((value ? "PASS " : "FAIL ") + text); if (!value) throw new InvalidOperationException(text); }
    private static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Flags).GetValue(obj);
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
    private static void SetEnum(object obj, string field, string value)
    { var info = obj.GetType().GetField(field, Flags); info.SetValue(obj, Enum.Parse(info.FieldType, value)); }
    private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);
    private static void OnLog(string message, string trace, LogType type)
    { if ((type == LogType.Exception || type == LogType.Assert || type == LogType.Error) && trace.Contains("Assets/Scripts")) runtimeError = message; }

    private static void Capture(string label)
    {
        var canvas = Get<GameObject>(pause, "canvasRoot").GetComponent<Canvas>();
        var rect = (RectTransform)canvas.transform;
        var oldMode = canvas.renderMode; var oldScale = rect.localScale;
        var cameraRoot = new GameObject("Pause Capture", typeof(Camera)); var camera = cameraRoot.GetComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = SketchbookUI.Paper;
        camera.cullingMask = 1 << 31; camera.transform.position = new Vector3(0, 0, -100); camera.farClipPlane = 200;
        var layers = canvas.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.gameObject.layer);
        canvas.GetComponent<CanvasScaler>().enabled = false; canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
        foreach (var t in layers.Keys) t.gameObject.layer = 31;
        try
        {
            foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(800, 600), new Vector2Int(2560, 1080), new Vector2Int(1080, 1920) })
            {
                float scale = Mathf.Min(size.x / 1920f, size.y / 1080f); rect.position = Vector3.zero;
                rect.localScale = Vector3.one * scale; rect.sizeDelta = new Vector2(size.x / scale, size.y / scale);
                foreach (var frame in canvas.GetComponentsInChildren<MenuDesignFrame>()) frame.Configure(new Vector2(1920, 1080));
                Canvas.ForceUpdateCanvases(); camera.orthographicSize = size.y * .5f;
                foreach (var text in canvas.GetComponentsInChildren<TextMeshProUGUI>()) { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, label + " " + size + ": " + text.name + " fits"); }
                var corners = new Vector3[4];
                foreach (var button in canvas.GetComponentsInChildren<Button>())
                { ((RectTransform)button.transform).GetWorldCorners(corners); Check(corners.All(p => Mathf.Abs(p.x) <= size.x * .5f + 1 && Mathf.Abs(p.y) <= size.y * .5f + 1), label + " " + size + ": " + button.name + " stays in viewport"); }
                var target = new RenderTexture(size.x, size.y, 24); var previous = RenderTexture.active;
                var capture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                try
                {
                    camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                    capture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); capture.Apply();
                    Check(capture.GetPixels32().Count(p => p.r < 120 && p.g < 120 && p.b < 120) > 200, label + " " + size + ": nonblank rendered ink");
                    File.WriteAllBytes(Path.Combine(Folder, label + "-" + size.x + "x" + size.y + ".png"), capture.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; camera.targetTexture = null; UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(capture); }
            }
        }
        finally
        {
            foreach (var entry in layers) entry.Key.gameObject.layer = entry.Value;
            canvas.renderMode = oldMode; rect.localScale = oldScale; canvas.worldCamera = null; canvas.GetComponent<CanvasScaler>().enabled = true;
            UnityEngine.Object.Destroy(cameraRoot);
        }
    }

    // Only the test peer performs the WebSocket upgrade; production uses the actual transport and session.
    private sealed class LoopbackPeer : IDisposable
    {
        private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly SemaphoreSlim sendGate = new SemaphoreSlim(1, 1);
        private readonly TaskCompletionSource<bool> exitGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private WebSocket socket;
        private TcpClient client;
        public readonly TaskCompletionSource<bool> Ready = new TaskCompletionSource<bool>();
        public readonly MatchState State;
        public Uri Uri { get; }
        public bool HoldExit, RejectExit;
        public int ResignCalls;
        public LoopbackPeer(string variant)
        {
            State = new MatchState { matchId = Guid.NewGuid().ToString("N"), status = "InProgress", turn = "White", settings = new GameSettings { mode = variant },
                clocks = new Clocks { whiteMilliseconds = 600000, blackMilliseconds = 600000, runningColor = "White", serverTime = DateTime.UtcNow },
                players = new List<Player> { new Player { userId = "__pause_audit__", displayName = "Audit Player", color = "White", connected = true }, new Player { userId = "loopback", displayName = "Loopback Opponent", color = "Black", connected = true } } };
            listener.Start(); Uri = new Uri("ws://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/pause-audit");
            _ = Task.Run(Serve);
        }
        private async Task Serve()
        {
            try
            {
                client = await listener.AcceptTcpClientAsync(); var stream = client.GetStream();
                var header = new StringBuilder(); var one = new byte[1];
                while (!header.ToString().EndsWith("\r\n\r\n")) { if (await stream.ReadAsync(one, 0, 1, lifetime.Token) == 0) throw new IOException(); header.Append((char)one[0]); }
                string key = header.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None).First(l => l.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)).Split(':')[1].Trim();
                string accept; using (var sha = SHA1.Create()) accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n");
                await stream.WriteAsync(response, 0, response.Length, lifetime.Token);
                socket = WebSocket.CreateFromStream(stream, true, null, TimeSpan.FromSeconds(30)); Ready.TrySetResult(true);
                var buffer = new byte[8192]; var message = new StringBuilder();
                while (!lifetime.IsCancellationRequested)
                {
                    var received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), lifetime.Token);
                    if (received.MessageType == WebSocketMessageType.Close) break;
                    message.Append(Encoding.UTF8.GetString(buffer, 0, received.Count)); if (!received.EndOfMessage) continue;
                    foreach (string json in message.ToString().Split('\u001e').Where(s => s.Length > 0))
                    {
                        var request = JObject.Parse(json); string target = (string)request["target"]; object result;
                        if (target == "Resign")
                        {
                            Interlocked.Increment(ref ResignCalls); if (HoldExit) await exitGate.Task;
                            if (!RejectExit) State.status = "Finished";
                            result = new CommandAck { accepted = !RejectExit, errorCode = RejectExit ? "AuditRejected" : null };
                        }
                        else if (target == "SubscribeMatch") result = new Subscription { state = State, resumeAfterSequence = State.eventSequence };
                        else throw new InvalidOperationException("Unexpected loopback request: " + target);
                        await Send(new { type = 3, invocationId = (string)request["invocationId"], result });
                    }
                    message.Clear();
                }
            }
            catch (Exception error) { Ready.TrySetException(error); }
        }
        private async Task Send(object payload)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload) + '\u001e');
            await sendGate.WaitAsync();
            try { await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, lifetime.Token); }
            finally { sendGate.Release(); }
        }
        public Task PushState()
        {
            State.eventSequence++; State.stateVersion++; State.turn = "Black";
            return Send(new { type = 1, target = "GameEvent", arguments = new[] { new ServerEvent { type = "GameStateUpdated", matchId = State.matchId, sequence = State.eventSequence, payload = JObject.FromObject(new { state = State }) } } });
        }
        public void ReleaseExit() => exitGate.TrySetResult(true);
        public void Dispose() { ReleaseExit(); lifetime.Cancel(); socket?.Abort(); socket?.Dispose(); client?.Dispose(); listener.Stop(); }
    }
}
