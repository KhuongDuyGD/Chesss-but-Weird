#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Opt-in diagnostics in the real game scenes; never runs in a normal player session.
public sealed class CosmeticsRuntimeVerification : MonoBehaviour
{
    [Serializable] public class Sample
    {
        public string stage;
        public double seconds, allocatedMiB, reservedMiB, privateMiB;
        public float p95Ms, maxMs;
        public int pieces;
    }
    [Serializable] public class Report
    {
        public bool passed;
        public string error, device;
        public List<Sample> samples = new List<Sample>();
    }
    private readonly Report report = new Report();
    private readonly List<float> frames = new List<float>();
    private string output;
    private double started;
    private bool finished;
    private CosmeticSelection saved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Launch()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--verify-cosmetics") < 0) return;
        var go = new GameObject("Cosmetics runtime diagnostics");
        DontDestroyOnLoad(go);
        go.AddComponent<CosmeticsRuntimeVerification>();
    }
    private void Awake()
    {
        output = Path.GetFullPath("Logs/cosmetics-runtime");
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--verification-output");
        if (index >= 0 && index + 1 < args.Length) output = args[index + 1];
        Directory.CreateDirectory(output);
        report.device = SystemInfo.graphicsDeviceName;
        saved = CosmeticSelection.Load().Copy();
        Application.runInBackground = true;
        Application.logMessageReceived += OnLog;
    }
    private void Update() { if (!finished) frames.Add(Time.unscaledDeltaTime * 1000f); }
    private void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Exception && report.error == null) report.error = message;
    }
    private IEnumerator Start()
    {
        yield return Guard(Run());
    }
    private IEnumerator Run()
    {
        yield return Wait(() => FindAnyObjectByType<MainMenuAuthUI>() != null || PlayerAuthService.IsAuthenticated, 90);
        if (PlayerAuthService.IsAuthenticated) throw new InvalidOperationException("Existing authenticated session detected; guest diagnostics will not replace it.");
        Button guest = null;
        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (button.name == "PlayAsGuestButton") { guest = button; break; }
        if (!guest) throw new InvalidOperationException("Guest button missing.");
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "auth.png"));
        yield return null;
        guest.onClick.Invoke();
        yield return Wait(() => PlayerAuthService.IsGuestSession && FindAnyObjectByType<ChessTurnSelectionUI>() && !FindAnyObjectByType<SessionLoadingController>().IsLoading, 120);
        var game = FindAnyObjectByType<ChessGame>();
        var ui = FindAnyObjectByType<ChessTurnSelectionUI>();
        var loader = LoadingManager.For(game);
        yield return new WaitForSecondsRealtime(2);
        Capture("menu");
        foreach (string id in new[] { "default", "dc", "corn", "missing-diagnostic-skin", "corn" })
        {
            var selection = CosmeticSelection.Load();
            selection.whiteSkinId = selection.blackSkinId = id;
            selection.Save();
            ui.ShowTwoPlayerSkinSelection();
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "selection-" + id + ".png"));
            Button play = null;
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (button.name == "Start Local Match") { play = button; break; }
            if (!play) throw new InvalidOperationException("Start Local Match button missing.");
            frames.Clear(); started = Time.realtimeSinceStartupAsDouble;
            play.onClick.Invoke();
            yield return Wait(() => !loader.IsBusy && game.GameStarted, 180);
            Capture(id + "-load");
            if (game.GetComponentsInChildren<ChessPiece>().Length != 32) throw new InvalidOperationException("Expected 32 pieces for " + id);
            string expected = id.StartsWith("missing") ? "default" : id;
            if (loader.Skins.GetData(PieceTeam.White)?.skinId != expected || loader.Skins.GetData(PieceTeam.Black)?.skinId != expected)
                throw new InvalidOperationException("Unexpected loaded skin for " + id);
            ChessPiece pawn = null;
            foreach (var piece in game.GetComponentsInChildren<ChessPiece>())
                if (piece.Team == PieceTeam.White && piece.BoardPosition == new Vector2Int(4, 1)) pawn = piece;
            if (!pawn || !game.TrySelectPiece(pawn) || !game.TryMoveSelectedPiece(new Vector2Int(4, 3)))
                throw new InvalidOperationException("Opening pawn move failed for " + id);
            yield return new WaitForSecondsRealtime(1);
            if (pawn.BoardPosition != new Vector2Int(4, 3)) throw new InvalidOperationException("Pawn did not reach e4.");
            frames.Clear(); started = Time.realtimeSinceStartupAsDouble;
            yield return new WaitForSecondsRealtime(5);
            Capture(id + "-gameplay");
            ScreenCapture.CaptureScreenshot(Path.Combine(output, report.samples.Count + "-" + id + ".png"));
            yield return new WaitForSecondsRealtime(.5f);
            game.RestartToMainMenu();
            yield return Wait(() => !loader.IsBusy && !loader.HasMatchContent, 90);
            yield return Resources.UnloadUnusedAssets();
            yield return new WaitForSecondsRealtime(2);
            Capture(id + "-returned");
            if (game.GetComponentsInChildren<ChessPiece>().Length != 0 || SceneManager.GetSceneByName("ChessMatch").isLoaded)
                throw new InvalidOperationException("Match consumers remain after return.");
        }
        Complete(report.error == null);
    }
    private IEnumerator Wait(Func<bool> predicate, float timeout)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + timeout;
        while (!predicate())
        {
            if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Runtime verification timed out.");
            yield return null;
        }
    }
    private IEnumerator Guard(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(routine);
        while (stack.Count > 0)
        {
            bool more = false; object next = null; Exception failure = null;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (Exception e) { failure = e; }
            if (failure != null) { report.error = failure.ToString(); Complete(false); yield break; }
            if (!more) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
    }
    private void Capture(string stage)
    {
        frames.Sort();
        using (var process = System.Diagnostics.Process.GetCurrentProcess())
        {
            report.samples.Add(new Sample { stage = stage, seconds = Time.realtimeSinceStartupAsDouble - started,
                allocatedMiB = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576d,
                reservedMiB = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1048576d,
                privateMiB = process.PrivateMemorySize64 / 1048576d,
                pieces = FindAnyObjectByType<ChessGame>().GetComponentsInChildren<ChessPiece>().Length,
                p95Ms = frames.Count == 0 ? 0 : frames[Mathf.Min(frames.Count - 1, Mathf.FloorToInt(frames.Count * .95f))],
                maxMs = frames.Count == 0 ? 0 : frames[frames.Count - 1] });
        }
        Debug.Log("[CosmeticsVerification] " + JsonUtility.ToJson(report.samples[report.samples.Count - 1]));
        File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
    }
    private void Complete(bool passed)
    {
        finished = true; report.passed = passed;
        saved?.Save();
        Application.logMessageReceived -= OnLog;
        File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
        Application.Quit(passed ? 0 : 1);
    }
}
#endif
