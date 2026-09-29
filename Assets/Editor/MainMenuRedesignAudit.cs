using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Render the production first-screen builder in a preview scene, with no login, network or scene edits.</summary>
public static class MainMenuRedesignAudit
{
    private static string Folder => Environment.GetEnvironmentVariable("CHESS_MAIN_MENU_AUDIT_OUTPUT") ?? "Documentation/UI/MainMenuRedesign";
    private static readonly List<string> Checks = new List<string>();

    [MenuItem("Chess/UI Audit/Capture Redesigned Main Menu")]
    public static void Capture()
    {
        Directory.CreateDirectory(Folder);
        Checks.Clear();
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Main Menu Audit Fixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;
        root.GetComponent<CanvasScaler>().enabled = false;
        var paper = new GameObject("Main Menu Paper Surface", typeof(RectTransform), typeof(MainMenuPaperGraphic));
        paper.transform.SetParent(root.transform, false);
        Stretch((RectTransform)paper.transform);
        var frame = MenuDesignFrame.Create(root.transform, "Main Menu", new Vector2(1920, 1080));
        var page = new GameObject("Main Menu", typeof(RectTransform)).GetComponent<RectTransform>();
        page.SetParent(frame, false); Stretch(page);
        var presentation = page.gameObject.AddComponent<MainMenuPresentation>();
        int starts = 0, settings = 0, logouts = 0;
        Sprite logo = MainMenuLogoOriginal.LoadSprite();
        GameObject selectedBefore = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        try
        {
            Require(logo, "Descriptively named original logo loads from Resources in a player build");
            Require(logo.name == "MainMenuLogoOriginal", "Main menu uses the original drawing");
            Require(Mathf.Abs(logo.rect.width / logo.texture.width - 1294f / 1448f) < .001f,
                "Original export padding is trimmed without changing the artwork proportions");
            presentation.Build(logo, () => starts++, () => settings++, () => logouts++);
            var controls = page.GetComponentsInChildren<Button>(true).ToDictionary(button => button.name);
            Require(controls.Count == 5, "START, SETTINGS, CREDITS, logout and modal back controls exist");
            Require(!page.GetComponentsInChildren<Transform>(true).Any(child => child.name.Contains("Focus Arrow")),
                "Menu buttons have no hover or selection pointer arrows");
            Require(controls["Start"].GetComponent<RectTransform>().rect.width > controls["Settings"].GetComponent<RectTransform>().rect.width * 2,
                "START has over twice the width of each secondary action");
            Require(page.GetComponentsInChildren<Image>(true).Count(image => image.sprite) == 2,
                "One illustrated logo with its shadow; no repeated raster background doodles");
            controls["Start"].onClick.Invoke(); controls["Settings"].onClick.Invoke(); controls["Logout"].onClick.Invoke();
            Require(starts == 1 && settings == 1 && logouts == 1, "Existing start, settings and logout callbacks fire exactly once");
            Require(controls["Start"].navigation.selectOnDown == controls["Settings"] &&
                controls["Settings"].navigation.selectOnRight == controls["Credits"], "Explicit keyboard/controller navigation connects primary and secondary buttons");
            foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1024,768),
                new Vector2Int(800,600), new Vector2Int(2560,1080), new Vector2Int(3840,2160), new Vector2Int(1080,1920) })
                Render(canvas, size, "MainMenu-" + size.x + "x" + size.y + ".png");
            VerifyFeedback(canvas, controls["Start"]);
            controls["Credits"].onClick.Invoke();
            var overlay = page.Find("Main Menu Credits Overlay");
            Require(overlay && overlay.gameObject.activeSelf, "CREDITS opens a readable modal without account access");
            Require(!controls["Start"].IsInteractable() && !controls["Settings"].IsInteractable(), "Credits modal blocks underlying menu actions");
            Render(canvas, new Vector2Int(1920,1080), "MainMenuCredits-1920x1080.png");
            Render(canvas, new Vector2Int(1024,768), "MainMenuCredits-1024x768.png");
            controls["Close Credits"].onClick.Invoke();
            Require(!overlay.gameObject.activeSelf && controls["Start"].IsInteractable(), "Closing credits restores main menu input");
            Debug.Log("[MainMenuRedesignAudit] PASS " + Checks.Count + " checks. Captures: " + Path.GetFullPath(Folder));
        }
        finally
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(selectedBefore);
            UnityEngine.Object.DestroyImmediate(root);
            if (logo) UnityEngine.Object.DestroyImmediate(logo);
            EditorSceneManager.ClosePreviewScene(scene);
            File.WriteAllLines(Path.Combine(Folder, "MainMenuAuditChecks.txt"), Checks);
        }
    }

    private static void VerifyFeedback(Canvas canvas, Button button)
    {
        var motion = button.GetComponent<MainMenuButtonMotion>();
        var rect = (RectTransform)button.transform;
        Vector2 restingPosition = rect.anchoredPosition;
        Vector3 restingScale = rect.localScale;
        var advance = typeof(MainMenuButtonMotion).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic);
        Action<int> tick = frames => { for (int i = 0; i < frames; i++) advance.Invoke(motion, new object[] { 1f / 60f, i / 60f }); };
        motion.OnPointerEnter(null); tick(40);
        Require(rect.anchoredPosition.y > restingPosition.y + 4f && rect.localScale.x > restingScale.x * 1.02f,
            "Pointer hover produces a bounded soft lift and scale feedback");
        Render(canvas, new Vector2Int(1920,1080), "MainMenuStart-Hover.png");
        motion.OnPointerDown(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }); tick(20);
        Require(rect.localScale.x < restingScale.x * .99f, "Press compresses the button cleanly");
        Render(canvas, new Vector2Int(1920,1080), "MainMenuStart-Pressed.png");
        motion.OnPointerExit(null); tick(70);
        Require((rect.anchoredPosition - restingPosition).sqrMagnitude < .001f && (rect.localScale - restingScale).sqrMagnitude < .001f,
            "Pointer exit settles to the original transform");
        motion.OnSelect(null); tick(40);
        Require(rect.localScale.x > restingScale.x * 1.02f, "Keyboard/controller selection shares hover feedback");
        button.interactable = false; tick(70);
        Require((rect.anchoredPosition - restingPosition).sqrMagnitude < .001f && rect.localScale == restingScale,
            "Disabled buttons clear focus feedback and return to rest");
        motion.OnDeselect(null); button.interactable = true;
    }

    private static void Render(Canvas canvas, Vector2Int size, string name)
    {
        var cameraObject = new GameObject("Main Menu Audit Camera", typeof(Camera));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, canvas.gameObject.scene);
        var camera = cameraObject.GetComponent<Camera>();
        camera.scene = canvas.gameObject.scene;
        camera.enabled = false; camera.orthographic = true;
        camera.orthographicSize = size.y * .5f;
        camera.transform.position = new Vector3(0, 0, -100);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.98f, .963f, .925f);
        camera.cullingMask = 1 << 31;
        camera.nearClipPlane = .1f; camera.farClipPlane = 200f;
        var target = new RenderTexture(size.x, size.y, 24);
        var previous = RenderTexture.active;
        Texture2D capture = null;
        try
        {
            var root = (RectTransform)canvas.transform;
            float scale = Mathf.Min(size.x / 1920f, size.y / 1080f);
            root.position = Vector3.zero; root.localScale = Vector3.one * scale;
            root.sizeDelta = new Vector2(size.x / scale, size.y / scale);
            canvas.worldCamera = camera;
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            foreach (var safe in canvas.GetComponentsInChildren<ResponsiveSafeArea>(true)) { safe.enabled = false; Stretch((RectTransform)safe.transform); }
            Canvas.ForceUpdateCanvases();
            foreach (var text in canvas.GetComponentsInChildren<TextMeshProUGUI>())
            {
                text.ForceMeshUpdate();
                Require(!text.isTextOverflowing, name + ": " + text.name + " fits without clipped copy");
            }
            foreach (var frame in canvas.GetComponentsInChildren<MenuDesignFrame>(true)) frame.Configure(new Vector2(1920,1080));
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            foreach (var button in canvas.GetComponentsInChildren<Button>())
            {
                ((RectTransform)button.transform).GetWorldCorners(corners);
                Require(corners.All(corner => Mathf.Abs(corner.x) <= size.x * .5f + 1f && Mathf.Abs(corner.y) <= size.y * .5f + 1f),
                    name + ": " + button.name + " stays in viewport");
            }
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            capture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0,0,size.x,size.y),0,0); capture.Apply();
            File.WriteAllBytes(Path.Combine(Folder,name),capture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            canvas.worldCamera = null;
            if (capture) UnityEngine.Object.DestroyImmediate(capture);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void Require(bool condition, string description)
    { Checks.Add((condition ? "PASS " : "FAIL ") + description); if (!condition) throw new InvalidOperationException(description); }
}
