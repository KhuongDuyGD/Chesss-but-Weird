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

/// <summary>Render and exercise the real hub builder without network access, account changes or scene edits.</summary>
public static class ModeSelectionRedesignAudit
{
    private static string Folder => Environment.GetEnvironmentVariable("CHESS_MODE_MENU_AUDIT_OUTPUT") ?? "Documentation/UI/ModeSelectionRedesign";
    private static readonly List<string> Checks = new List<string>();

    [MenuItem("Chess/UI Audit/Capture Redesigned Mode Selection")]
    public static void Capture()
    {
        Directory.CreateDirectory(Folder);
        Checks.Clear();
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Mode Selection Audit Fixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;
        root.GetComponent<CanvasScaler>().enabled = false;
        var paper = new GameObject("Shared Main Menu Paper", typeof(RectTransform), typeof(MainMenuPaperGraphic));
        paper.transform.SetParent(root.transform, false); Stretch((RectTransform)paper.transform);
        var frame = MenuDesignFrame.Create(root.transform, "Mode Selection", new Vector2(1920,1080));
        var page = new GameObject("Mode Selection", typeof(RectTransform)).GetComponent<RectTransform>();
        page.SetParent(frame, false); Stretch(page);
        var presentation = page.gameObject.AddComponent<ModeSelectionPresentation>();
        Sprite logo = MainMenuLogoOriginal.LoadSprite();
        var previousSystem = EventSystem.current;
        var selectedBefore = previousSystem ? previousSystem.currentSelectedGameObject : null;
        var eventRoot = new GameObject("Mode Selection Audit Event System", typeof(EventSystem));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(eventRoot, scene);
        var auditSystem = eventRoot.GetComponent<EventSystem>();
        var systems = (List<EventSystem>)typeof(EventSystem).GetField("m_EventSystems", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        bool manuallyRegistered = !systems.Contains(auditSystem);
        // Regular EventSystem lifecycle callbacks do not run in an edit-mode preview scene.
        if (manuallyRegistered) typeof(EventSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(auditSystem,null);
        EventSystem.current = auditSystem;
        var clicks = new Dictionary<string,int>();
        UnityEngine.Events.UnityAction Callback(string name) => () => clicks[name] = clicks.ContainsKey(name) ? clicks[name]+1 : 1;
        try
        {
            Require(logo && logo.name == "MainMenuLogoOriginal", "Original logo is reused with a descriptive player-resource name");
            presentation.Build(logo, new ModeSelectionPresentation.Actions
            {
                Local = Callback("Local"), Online = Callback("Online"), Aram = Callback("Aram"), Shop = Callback("Shop"),
                Inventory = Callback("Inventory"), Gacha = Callback("Gacha"), Profile = Callback("Profile"), Settings = Callback("Settings"),
                Back = Callback("Back"), Logout = Callback("Logout")
            });
            var buttons = page.GetComponentsInChildren<Button>(true)
                .Where(button => button.transform.parent.name == "Mode Hub Controls" && button.name != "Music Record")
                .ToDictionary(button => button.name);
            Require(buttons.Count == 10, "Four mode cards, four utility controls, Back and Logout exist");
            foreach (var pair in buttons)
            {
                pair.Value.onClick.Invoke();
                Require(clicks[pair.Key] == 1, pair.Key + " forwards its action exactly once");
                Require(pair.Value.navigation.mode == Navigation.Mode.Explicit, pair.Key + " has explicit controller navigation");
            }
            Require(buttons["Local"].navigation.selectOnRight == buttons["Online"] && buttons["Online"].navigation.selectOnDown == buttons["Shop"] &&
                buttons["Online"].navigation.selectOnRight == buttons["Inventory"] && buttons["Settings"].navigation.selectOnLeft == buttons["Shop"],
                "Navigation connects the mode grid and utility column");
            Require(!page.GetComponentsInChildren<Transform>(true).Any(child => child.name.Contains("Focus Arrow") || child.name.Contains("Battle Doodle")),
                "No hover pointer arrows or repeated battle backgrounds");
            Require(page.GetComponentsInChildren<Image>(true).Count(image => image.sprite) == 2,
                "The only raster images are the preserved logo and its faint shadow");
            var drawings = page.GetComponentsInChildren<ModeMenuDoodleGraphic>(true);
            Require(drawings.Length == 8 && drawings.Select(icon => icon.Illustration).Distinct().Count() == 8,
                "All modes and all four upgraded utility icons have distinct editable drawings");
            Require(page.GetComponentsInChildren<HandDrawnIdleWiggle>(true).Length == 1,
                "Only one small icon animates continuously at idle");
            Require(page.Find("Mode Hub Controls/Shop/Shop Note").GetComponent<TextMeshProUGUI>().text == "COMING SOON",
                "The existing shop placeholder has an honest visible state");

            foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1024,768),
                new Vector2Int(800,600), new Vector2Int(2560,1080), new Vector2Int(3840,2160), new Vector2Int(1080,1920) })
                Render(canvas, size, "ModeSelection-"+size.x+"x"+size.y+".png");

            int vertices = 0;
            foreach (var drawing in drawings)
            {
                var mesh = drawing.canvasRenderer.GetMesh();
                int count = mesh ? mesh.vertexCount : 0;
                vertices += count;
                Require(count > 0 && count < 4000, drawing.name + " has a bounded static mesh");
            }
            Require(vertices < 16000, "All eight illustrations use fewer than 16000 cached vertices (actual: " + vertices + ")");
            VerifyFeedback(canvas, buttons["Local"], "ModeSelectionLocal");
            VerifyFeedback(canvas, buttons["Inventory"], "ModeSelectionInventory");
            EventSystem.current.SetSelectedGameObject(buttons["Gacha"].gameObject);
            presentation.SetInputEnabled(false);
            Require(buttons.Values.All(button => !button.IsInteractable()), "Opening a modal blocks pointer and keyboard activation of the hub");
            Require(!EventSystem.current.currentSelectedGameObject, "Opening a modal clears the underlying selected control");
            presentation.SetInputEnabled(true);
            Require(buttons.Values.All(button => button.IsInteractable()) && EventSystem.current.currentSelectedGameObject == buttons["Gacha"].gameObject,
                "Closing a modal restores hub input and the previous selection");
            presentation.SetInputEnabled(false);
            var modalBack = new GameObject("Audit Modal Back",typeof(RectTransform),typeof(Button));
            modalBack.transform.SetParent(root.transform,false);
            EventSystem.current.SetSelectedGameObject(modalBack);
            modalBack.SetActive(false);
            presentation.SetInputEnabled(true);
            Require(EventSystem.current.currentSelectedGameObject == buttons["Gacha"].gameObject,
                "Closing a modal restores focus when its Back button remains selected but inactive");
            var transitionGroup = page.gameObject.AddComponent<CanvasGroup>();
            transitionGroup.interactable = false;
            Require(buttons.Values.All(button => !button.IsInteractable()), "A screen transition blocks keyboard actions through the parent CanvasGroup");
            transitionGroup.interactable = true;
            page.gameObject.SetActive(false);
            typeof(ModeSelectionPresentation).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(presentation,null);
            Require(!EventSystem.current.currentSelectedGameObject, "Leaving the hub clears stale EventSystem selection");
            Debug.Log("[ModeSelectionRedesignAudit] PASS " + Checks.Count + " checks. Captures: " + Path.GetFullPath(Folder));
        }
        finally
        {
            if (manuallyRegistered) typeof(EventSystem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(auditSystem,null);
            if (previousSystem) EventSystem.current = previousSystem;
            if (previousSystem) previousSystem.SetSelectedGameObject(selectedBefore);
            UnityEngine.Object.DestroyImmediate(eventRoot);
            UnityEngine.Object.DestroyImmediate(root);
            if (logo) UnityEngine.Object.DestroyImmediate(logo);
            EditorSceneManager.ClosePreviewScene(scene);
            File.WriteAllLines(Path.Combine(Folder,"ModeSelectionAuditChecks.txt"), Checks);
        }
    }

    private static void VerifyFeedback(Canvas canvas, Button button, string name)
    {
        var motion = button.GetComponent<MainMenuButtonMotion>();
        var rect = (RectTransform)button.transform;
        Vector2 position = rect.anchoredPosition;
        Vector3 scale = rect.localScale;
        var advance = typeof(MainMenuButtonMotion).GetMethod("Advance", BindingFlags.Instance|BindingFlags.NonPublic);
        Action<int> tick = frames => { for (int i=0;i<frames;i++) advance.Invoke(motion,new object[] { 1f/60f,i/60f }); };
        motion.OnPointerEnter(null); tick(40);
        Require(rect.anchoredPosition.y > position.y+4 && rect.localScale.x > scale.x*1.02f, name + " uses soft hover lift and scale");
        Render(canvas,new Vector2Int(1920,1080),name+"-Hover.png");
        motion.OnPointerDown(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }); tick(20);
        Require(rect.localScale.x < scale.x*.99f,name+" compresses on press");
        Render(canvas,new Vector2Int(1920,1080),name+"-Pressed.png");
        motion.OnPointerExit(null); tick(70);
        Require((rect.anchoredPosition-position).sqrMagnitude < .001f && (rect.localScale-scale).sqrMagnitude < .001f,
            name+" settles to its original transform after pointer exit");
    }

    private static void Render(Canvas canvas, Vector2Int size, string name)
    {
        var cameraObject = new GameObject("Mode Selection Audit Camera", typeof(Camera));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject,canvas.gameObject.scene);
        var camera = cameraObject.GetComponent<Camera>();
        camera.scene = canvas.gameObject.scene;
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = size.y*.5f;
        camera.transform.position = new Vector3(0,0,-100);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.98f,.963f,.925f);
        camera.cullingMask = 1<<31; camera.nearClipPlane = .1f; camera.farClipPlane = 200f;
        var target = new RenderTexture(size.x,size.y,24);
        var previous = RenderTexture.active;
        Texture2D capture = null;
        try
        {
            var root = (RectTransform)canvas.transform;
            float scale = Mathf.Min(size.x/1920f,size.y/1080f);
            root.position = Vector3.zero; root.localScale = Vector3.one*scale;
            root.sizeDelta = new Vector2(size.x/scale,size.y/scale);
            canvas.worldCamera = camera;
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            foreach (var safe in canvas.GetComponentsInChildren<ResponsiveSafeArea>(true)) { safe.enabled=false; Stretch((RectTransform)safe.transform); }
            foreach (var frame in canvas.GetComponentsInChildren<MenuDesignFrame>(true)) frame.Configure(new Vector2(1920,1080));
            Canvas.ForceUpdateCanvases();
            foreach (var text in canvas.GetComponentsInChildren<TextMeshProUGUI>())
            {
                text.ForceMeshUpdate();
                Require(!text.isTextOverflowing,name+": "+text.name+" fits without clipped copy (preferred: "+text.preferredWidth+" x "+text.preferredHeight+")");
            }
            var corners = new Vector3[4];
            foreach (var button in canvas.GetComponentsInChildren<Button>())
            {
                ((RectTransform)button.transform).GetWorldCorners(corners);
                Require(corners.All(corner=>Mathf.Abs(corner.x)<=size.x*.5f+1 && Mathf.Abs(corner.y)<=size.y*.5f+1),name+": "+button.name+" stays in viewport");
            }
            camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
            capture = new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            capture.ReadPixels(new Rect(0,0,size.x,size.y),0,0); capture.Apply();
            File.WriteAllBytes(Path.Combine(Folder,name),capture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous; canvas.worldCamera=null;
            if(capture) UnityEngine.Object.DestroyImmediate(capture);
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static void Stretch(RectTransform rect)
    { rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero; }
    private static void Require(bool condition, string description)
    { Checks.Add((condition?"PASS ":"FAIL ")+description);if(!condition)throw new InvalidOperationException(description); }
}
