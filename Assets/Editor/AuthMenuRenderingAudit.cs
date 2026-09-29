using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Cold overlay rendering and auth UI recreation, with no server or account changes.</summary>
[InitializeOnLoad]
public static class AuthMenuRenderingAudit
{
    private const string ActiveKey = "Chess.AuthMenuRenderingAudit.Active";
    private const string OutputVariable = "CHESS_AUTH_MENU_AUDIT_OUTPUT";
    private const AdditionalCanvasShaderChannels ColourChannels =
        AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
    private static readonly List<string> Checks = new List<string>();
    private static MainMenuAuthUI auth;
    private static int stage, createdFrame, logoutCount;
    private static double deadline;
    private static string runtimeError;
    private static string Folder => Environment.GetEnvironmentVariable(OutputVariable);

    static AuthMenuRenderingAudit()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void Run()
    {
        // This runner owns its empty scene and play lifecycle in the isolated CLI
        // project. It must never replace the user's open scene or auth session.
        if (!Application.isBatchMode || string.IsNullOrEmpty(Folder))
            throw new InvalidOperationException("Run tools/verify-main-menu.ps1 -Menu Auth in an isolated project.");
        Directory.CreateDirectory(Folder);
        SessionState.SetBool(ActiveKey, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false) || state != PlayModeStateChange.EnteredPlayMode) return;
        try
        {
            Checks.Clear(); stage = logoutCount = 0; runtimeError = null;
            deadline = EditorApplication.timeSinceStartup + 90;
            // Compile shaders synchronously in this isolated Editor only, so a
            // cyan async-compilation placeholder cannot pass visual verification.
            ShaderUtil.allowAsyncCompilation = false;
            var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            var gameView = EditorWindow.GetWindow(gameViewType, true, "Auth rendering audit");
            gameView.position = new Rect(0, 0, 1280, 760);
            gameView.Show();
            var camera = new GameObject("Auth Audit Camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta;
            VerifyGraphicLifecycle();
            CreateAuth();
        }
        catch (Exception error) { Finish(error); }
    }

    private static void CreateAuth()
    {
        auth = new GameObject("Auth Rendering Fixture").AddComponent<MainMenuAuthUI>();
        auth.Initialize(null, "", "");
        createdFrame = Time.frameCount;
        Check((auth.GetComponent<Canvas>().additionalShaderChannels & ColourChannels) == ColourChannels,
            (logoutCount == 0 ? "Startup" : "Logout " + logoutCount) + ": colour channels exist before the first canvas rebuild");
    }

    private static void VerifyGraphicLifecycle()
    {
        var root = new GameObject("Canvas lifecycle fixture", typeof(RectTransform), typeof(Canvas));
        var canvas = root.GetComponent<Canvas>();
        var preserved = AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        var rounded = new GameObject("Created before parenting", typeof(RectTransform), typeof(HandDrawnRoundedGraphic))
            .GetComponent<HandDrawnRoundedGraphic>();
        var image = new GameObject("Rectangle created before parenting", typeof(RectTransform), typeof(AntialiasedMenuImage))
            .GetComponent<AntialiasedMenuImage>();
        try
        {
            canvas.additionalShaderChannels = preserved;
            rounded.transform.SetParent(root.transform, false);
            Check((canvas.additionalShaderChannels & (ColourChannels | preserved)) == (ColourChannels | preserved),
                "Parenting a live doodle graphic adds colour channels and preserves existing channels");
            canvas.additionalShaderChannels = preserved;
            image.transform.SetParent(root.transform, false);
            Check((canvas.additionalShaderChannels & ColourChannels) == ColourChannels,
                "Parenting a live solid Image adds colour channels");
            canvas.additionalShaderChannels = preserved;
            rounded.SetVerticesDirty(); rounded.Rebuild(CanvasUpdate.PreRender);
            Check((canvas.additionalShaderChannels & ColourChannels) == ColourChannels,
                "Doodle mesh submission restores missing channels before rendering");
            canvas.additionalShaderChannels = preserved;
            image.SetVerticesDirty(); image.Rebuild(CanvasUpdate.PreRender);
            Check((canvas.additionalShaderChannels & ColourChannels) == ColourChannels,
                "Solid Image mesh submission restores missing channels before rendering");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
        try
        {
            if (runtimeError != null) throw new InvalidOperationException(runtimeError);
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Auth rendering audit timed out.");
            if (stage == 4)
            {
                if (Time.frameCount <= createdFrame) return; // Allow the old auth object's Destroy to complete.
                logoutCount++;
                CreateAuth();
                stage = 5;
                return;
            }
            // GameView presents the previous buffer from EditorApplication.update.
            // Allow its initial resize/layout to settle without interacting with
            // the UI; first-frame channel setup is checked separately in CreateAuth.
            int framesToWait = stage == 1 ? 12 : 8;
            if (Time.frameCount <= createdFrame + framesToWait) return;
            var capture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                // Batch GameView may not have presented its first buffer yet.
                // Wait only for the paper background, never for the broken card.
                if (capture.width != Screen.width || capture.height != Screen.height ||
                    capture.GetPixel(capture.width / 2, capture.height - 4).g < .8f) return;
                string name = stage == 0 ? "Login-Startup" : stage == 1 ? "Login-Untouched" :
                    stage == 2 ? "Signup" : stage == 3 ? "Login-After-Switch" : "Login-After-Logout-" + logoutCount;
                File.WriteAllBytes(Path.Combine(Folder, name + ".png"), capture.EncodeToPNG());
                VerifyVisibleAuth(capture, name);
            }
            finally { UnityEngine.Object.DestroyImmediate(capture); }
            switch (stage)
            {
                case 0: stage = 1; break;
                case 1:
                    auth.LoginMenuPanel.transform.Find("SignUpButton").GetComponent<Button>().onClick.Invoke();
                    createdFrame = Time.frameCount; stage = 2; break;
                case 2:
                    auth.SignUpMenuPanel.transform.Find("LoginButton").GetComponent<Button>().onClick.Invoke();
                    createdFrame = Time.frameCount; stage = 3; break;
                case 3: BeginLogout(); break;
                case 5:
                    if (logoutCount < 3) BeginLogout();
                    else Finish(null);
                    break;
            }
        }
        catch (Exception error) { Finish(error); }
    }

    private static void BeginLogout()
    {
        // AuthController.Create builds the same MainMenuAuthUI after logout. The
        // fixture recreates this UI without logging out any real player account.
        UnityEngine.Object.Destroy(auth.gameObject);
        createdFrame = Time.frameCount;
        stage = 4;
    }

    private static void VerifyVisibleAuth(Texture2D capture, string name)
    {
        var panel = auth.CurrentMode == MainMenuAuthUI.AuthMode.Login ? auth.LoginMenuPanel : auth.SignUpMenuPanel;
        Check((auth.GetComponent<Canvas>().additionalShaderChannels & ColourChannels) == ColourChannels,
            name + ": both colour streams survive actual overlay rendering");
        Color card = Sample(capture, (RectTransform)panel.transform.Find("Account Form"), new Vector2(.93f, .5f));
        Check(card.r > .9f && card.g > .9f && card.b > .85f,
            name + ": account card renders its light paper fill, rather than black");
        var button = panel.transform.Find(auth.CurrentMode == MainMenuAuthUI.AuthMode.Login ? "LoginButton" : "SignUpButton");
        Color fill = Sample(capture, (RectTransform)button, new Vector2(.2f, .65f));
        Check(fill.r > .65f && fill.g > .65f && fill.b > .25f,
            name + ": selected menu button renders its coloured fill");
        Check(auth.CurrentMode == MainMenuAuthUI.AuthMode.SignUp ? fill.b > .6f : fill.b < .58f,
            name + ": captured buffer shows the selected auth mode");
        var heading = panel.transform.Find("Account Form/Form Heading").GetComponent<TextMeshProUGUI>();
        Check(heading.canvasRenderer.GetMesh() && heading.canvasRenderer.GetMesh().vertexCount > 0 && !heading.canvasRenderer.cull,
            name + ": heading has visible text geometry");
        Check(CountInk(capture, heading.rectTransform) > 30, name + ": heading renders readable ink pixels");
    }

    private static int CountInk(Texture2D capture, RectTransform rect)
    {
        Vector2 minimum = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.min));
        Vector2 maximum = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.max));
        int count = 0;
        for (int y = Mathf.Max(0, Mathf.CeilToInt(minimum.y)); y < Mathf.Min(capture.height, Mathf.FloorToInt(maximum.y)); y++)
            for (int x = Mathf.Max(0, Mathf.CeilToInt(minimum.x)); x < Mathf.Min(capture.width, Mathf.FloorToInt(maximum.x)); x++)
            {
                Color pixel = capture.GetPixel(x, y);
                if (pixel.r < .3f && pixel.g < .3f && pixel.b < .3f) count++;
            }
        return count;
    }

    private static Color Sample(Texture2D capture, RectTransform rect, Vector2 normalized)
    {
        Vector2 local = rect.rect.min + Vector2.Scale(rect.rect.size, normalized);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local));
        return capture.GetPixel(Mathf.Clamp(Mathf.RoundToInt(screen.x), 0, capture.width - 1),
            Mathf.Clamp(Mathf.RoundToInt(screen.y), 0, capture.height - 1));
    }

    private static void Check(bool condition, string message)
    {
        Checks.Add((condition ? "PASS " : "FAIL ") + message);
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        // A known batch-only Search index startup exception is unrelated to UI.
        if (stack != null && stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        if (type == LogType.Exception || type == LogType.Assert || type == LogType.Error)
            runtimeError = type + ": " + message + "\n" + stack;
    }

    private static void Finish(Exception error)
    {
        SessionState.SetBool(ActiveKey, false);
        if (error != null)
        {
            Checks.Add("FAIL " + error.Message);
            Debug.LogException(error);
        }
        File.WriteAllLines(Path.Combine(Folder, "AuthMenuRenderingChecks.txt"), Checks);
        Debug.Log("[AuthMenuRenderingAudit] " + (error == null ? "PASS " : "FAIL ") + Checks.Count +
            " checks on " + SystemInfo.graphicsDeviceType + ". Captures: " + Folder);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
