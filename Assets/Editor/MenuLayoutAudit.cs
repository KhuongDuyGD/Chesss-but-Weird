using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Render the actual runtime menus at fixed resolutions, without changing the
/// Game view, connecting to a server, or spending currency. Run in Play mode.</summary>
public static class MenuLayoutAudit
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> checks = new List<string>();
    private static readonly Vector2Int[] Sizes = {
        new Vector2Int(1920, 1080), new Vector2Int(800, 600), new Vector2Int(1280, 720),
        new Vector2Int(1024, 768), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160),
        new Vector2Int(2560, 1080), new Vector2Int(5120, 1440), new Vector2Int(2340, 1080),
        new Vector2Int(1080, 1920), new Vector2Int(360, 800) };

    [MenuItem("Chess/UI Audit/Capture Menus")]
    public static void Capture()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode and wait for the menus to load first.");
        var view = UnityEngine.Object.FindAnyObjectByType<HandDrawnMenuView>(FindObjectsInactive.Include);
        GameObject fixture = null;
        if (!view || !view.IsReady)
        {
            fixture = new GameObject("UI Audit Menu Fixture", typeof(RectTransform));
            var assets = fixture.AddComponent<HandDrawnMenuAssets>();
            assets.LoadFromResources();
            var owner = fixture.AddComponent<ChessTurnSelectionUI>();
            owner.enabled = false;
            view = fixture.AddComponent<HandDrawnMenuView>();
            view.Initialize(owner, UnityEngine.Object.FindAnyObjectByType<ChessGame>(), assets);
        }
        string folder = Path.GetFullPath("Temp/MenuLayoutAudit");
        Directory.CreateDirectory(folder);
        checks.Clear();
        var rootCanvas = view.GetComponent<Canvas>();
        var previous = Field<RectTransform>(view, "currentScreen");
        bool wasEnabled = rootCanvas.enabled;
        try
        {
            rootCanvas.enabled = true;
            foreach (string field in new[] { "mainScreen", "modeScreen", "localModeScreen", "botDifficultyScreen", "gachaScreen", "sideScreen", "multiplayerModeScreen", "aramModeScreen" })
            {
                var screen = Field<RectTransform>(view, field);
                Invoke(view, "SetScreenImmediate", screen);
                CaptureSizes(rootCanvas, folder, field);
            }
            Invoke(view, "SetScreenImmediate", Field<RectTransform>(view, "modeScreen"));
            var profile = Field<RectTransform>(view, "profileOverlay");
            Invoke(view, "SetOverlayState", profile, true, 1f, 1f);
            Field<PlayerProfileMenuController>(view, "profileController").Open();
            CaptureSizes(rootCanvas, folder, "profile");
            Field<RectTransform>(Field<PlayerProfileMenuController>(view, "profileController"), "editPanel").gameObject.SetActive(true);
            CaptureSizes(rootCanvas, folder, "profile-edit");
            profile.gameObject.SetActive(false);
            var inventory = Field<RectTransform>(view, "inventoryOverlay");
            Invoke(view, "SetOverlayState", inventory, true, 1f, 1f);
            Field<InventoryMenuController>(view, "inventoryController").Open();
            CaptureSizes(rootCanvas, folder, "inventory");
            inventory.gameObject.SetActive(false);
            Invoke(view, "SetAllScreensActive", false);
            var settings = Field<RectTransform>(view, "settingsOverlay");
            Invoke(view, "SetOverlayState", settings, true, 1f, 1f);
            CaptureSizes(rootCanvas, folder, "settings");
            settings.gameObject.SetActive(false);
            CaptureAdditionalMenus(folder, view.GetComponent<HandDrawnMenuAssets>(), Field<ChessTurnSelectionUI>(view, "owner"));
            CaptureLobby(folder);
            File.WriteAllLines(Path.Combine(folder, "layout-checks.txt"), checks);
            Debug.Log("[MenuLayoutAudit] Completed " + checks.Count + " layout checks: " + folder);
        }
        finally
        {
            File.WriteAllLines(Path.Combine(folder, "layout-checks.txt"), checks);
            Invoke(view, "SetScreenImmediate", previous);
            rootCanvas.enabled = wasEnabled;
            if (fixture) UnityEngine.Object.Destroy(fixture);
        }
    }

    private static void CaptureAdditionalMenus(string folder, HandDrawnMenuAssets assets, ChessTurnSelectionUI owner)
    {
        var fixture = new GameObject("UI Audit Additional Menus", typeof(RectTransform));
        try
        {
            var auth = fixture.AddComponent<MainMenuAuthUI>();
            auth.Initialize(assets, "Layout preview", "");
            CaptureSizes(fixture.GetComponent<Canvas>(), folder, "auth-login");
            auth.ShowSignUp();
            CaptureSizes(fixture.GetComponent<Canvas>(), folder, "auth-signup");
            fixture.GetComponent<Canvas>().enabled = false;
            var pause = fixture.AddComponent<ChessPauseMenu>();
            pause.enabled = false;
            Invoke(pause, "BuildUi", PauseMenuAssetCatalog.Load());
            var pauseCanvas = Field<GameObject>(pause, "canvasRoot").GetComponent<Canvas>();
            CaptureSizes(pauseCanvas, folder, "pause");
            pauseCanvas.gameObject.SetActive(false);
            var result = fixture.AddComponent<ResultMenuView>();
            result.Initialize(owner, null, null);
            foreach (ResultMenuView.ResultKind kind in Enum.GetValues(typeof(ResultMenuView.ResultKind)))
            {
                var oldContent = Field<RectTransform>(result, "contentRoot");
                if (oldContent) { UnityEngine.Object.DestroyImmediate(oldContent.gameObject); }
                Invoke(result, "Rebuild", kind);
                var resultCanvas = Field<GameObject>(result, "canvasRoot").GetComponent<Canvas>();
                resultCanvas.gameObject.SetActive(true);
                CaptureSizes(resultCanvas, folder, "result-" + kind);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture); }
    }

    private static void CaptureLobby(string folder)
    {
        // An inactive owner fixture prevents transport startup and profile/network mutations.
        var fixture = new GameObject("UI Audit Lobby Owner");
        fixture.SetActive(false);
        var owner = fixture.AddComponent<ChessLanController>();
        Type type = typeof(ChessLanController).GetNestedType("NetworkLobbyUiController", BindingFlags.NonPublic);
        object lobby = Activator.CreateInstance(type, new object[] { owner });
        var canvasObject = Field<GameObject>(lobby, "canvasRoot");
        canvasObject.SetActive(true);
        try
        {
            FieldInfo mode = type.GetField("currentMode", Flags);
            foreach (string name in Enum.GetNames(mode.FieldType))
            {
                mode.SetValue(lobby, Enum.Parse(mode.FieldType, name));
                // Destroy immediately here to avoid old and new layouts overlapping in one frame.
                var content = Field<RectTransform>(lobby, "contentRoot");
                while (content.childCount > 0) UnityEngine.Object.DestroyImmediate(content.GetChild(0).gameObject);
                Invoke(lobby, "Rebuild");
                foreach (var text in canvasObject.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
                {
                    if (text.name == "Room Code Value") text.text = "ABC123";
                    if (text.name == "Host Player") text.text = "Host player — Ready";
                    if (text.name == "Guest Player") text.text = "Guest player — Waiting";
                    if (text.name == "Server Health") text.text = "Online";
                    if (text.name == "Lobby Status") text.text = "Waiting for players";
                }
                CaptureSizes(canvasObject.GetComponent<Canvas>(), folder, "lobby-" + name);
            }
        }
        finally
        {
            Invoke(lobby, "Destroy");
            UnityEngine.Object.Destroy(fixture);
        }
    }

    private static void CaptureSizes(Canvas canvas, string folder, string name)
    {
        foreach (var size in Sizes) Render(canvas, size, Path.Combine(folder, name + "-" + size.x + "x" + size.y + ".png"));
    }

    private static void Render(Canvas canvas, Vector2Int size, string file)
    {
        var rect = (RectTransform)canvas.transform;
        var scaler = canvas.GetComponent<CanvasScaler>();
        RenderMode mode = canvas.renderMode;
        Camera oldCamera = canvas.worldCamera;
        Vector2 oldSize = rect.sizeDelta;
        Vector3 oldPosition = rect.position, oldScale = rect.localScale;
        Quaternion oldRotation = rect.rotation;
        var layers = new Dictionary<GameObject, int>();
        var safeStates = new Dictionary<ResponsiveSafeArea, bool>();
        var cameraObject = new GameObject("UI Audit Camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = size.y * .5f;
        camera.transform.position = new Vector3(0, 0, -100);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.985f, .965f, .91f);
        camera.cullingMask = 1 << 31;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 200;
        var target = new RenderTexture(size.x, size.y, 24);
        RenderTexture oldTarget = RenderTexture.active;
        Texture2D image = null;
        bool scalerEnabled = scaler && scaler.enabled;
        try
        {
            if (scaler) scaler.enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            rect.position = Vector3.zero;
            rect.rotation = Quaternion.identity;
            // Match the production Expand scaler's logical viewport. Treating pixels
            // as design units gives false failures for menus with fixed-size panels.
            float canvasScale = scaler ? Mathf.Min(size.x / scaler.referenceResolution.x, size.y / scaler.referenceResolution.y) : 1f;
            rect.localScale = new Vector3(canvasScale, canvasScale, 1f);
            rect.sizeDelta = new Vector2(size.x / canvasScale, size.y / canvasScale);
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
            {
                layers[child.gameObject] = child.gameObject.layer;
                child.gameObject.layer = 31;
            }
            foreach (var safe in canvas.GetComponentsInChildren<ResponsiveSafeArea>(true))
            {
                safeStates[safe] = safe.enabled;
                safe.enabled = false;
                var safeRect = (RectTransform)safe.transform;
                // Landscape phone with an asymmetric camera cutout and bottom gesture inset.
                safeRect.anchorMin = size.x == 2340 ? new Vector2(100f / size.x, 24f / size.y) : Vector2.zero;
                safeRect.anchorMax = size.x == 2340 ? new Vector2(1f - 36f / size.x, 1f) : Vector2.one;
                safeRect.offsetMin = safeRect.offsetMax = Vector2.zero;
            }
            Canvas.ForceUpdateCanvases();
            foreach (var fitter in canvas.GetComponentsInChildren<MenuDesignFrame>(true)) Invoke(fitter, "Apply");
            foreach (var fitter in canvas.GetComponentsInChildren<InventoryContentRootFitter>(true)) Invoke(fitter, "Apply");
            foreach (var fitter in canvas.GetComponentsInChildren<GachaContentRootFitter>(true)) Invoke(fitter, "Apply");
            Canvas.ForceUpdateCanvases();
            ValidateControls(canvas, size, file);
            // Keep representative captures; all resolutions still run containment checks.
            if (size.x != 1920 && size.x != 2340 && size.x != 1080 && size.x != 360) return;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            image.Apply();
            File.WriteAllBytes(file, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = oldTarget;
            canvas.renderMode = mode;
            canvas.worldCamera = oldCamera;
            rect.position = oldPosition;
            rect.rotation = oldRotation;
            rect.localScale = oldScale;
            rect.sizeDelta = oldSize;
            if (scaler) scaler.enabled = scalerEnabled;
            foreach (var pair in layers) pair.Key.layer = pair.Value;
            foreach (var pair in safeStates) { pair.Key.enabled = pair.Value; Invoke(pair.Key, "Apply"); }
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(target);
            if (image) UnityEngine.Object.DestroyImmediate(image);
        }
    }

    private static void ValidateControls(Canvas canvas, Vector2Int size, string file)
    {
        int count = 0;
        var corners = new Vector3[4];
        foreach (var control in canvas.GetComponentsInChildren<Selectable>())
        {
            if (!control.IsActive()) continue;
            var rect = (RectTransform)control.transform;
            rect.GetWorldCorners(corners);
            float left = size.x == 2340 ? -size.x * .5f + 100 : -size.x * .5f;
            float right = size.x == 2340 ? size.x * .5f - 36 : size.x * .5f;
            float bottom = size.x == 2340 ? -size.y * .5f + 24 : -size.y * .5f;
            foreach (var corner in corners)
                if (corner.x < left - 1 || corner.x > right + 1 || corner.y < bottom - 1 || corner.y > size.y * .5f + 1)
                {
                    checks.Add("FAIL " + Path.GetFileNameWithoutExtension(file) + ": control outside safe area: " + control.name + " " + corner);
                    break;
                }
            count++;
        }
        string prefix = "FAIL " + Path.GetFileNameWithoutExtension(file) + ":";
        if (!checks.Exists(line => line.StartsWith(prefix, StringComparison.Ordinal)))
            checks.Add("PASS " + Path.GetFileNameWithoutExtension(file) + ": " + count + " controls inside safe area (not a readability or touch-size assertion)");
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
    private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags | BindingFlags.Public).Invoke(target, args);
}

