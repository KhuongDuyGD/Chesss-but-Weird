using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UI = SketchbookUI;

public static class InventoryMusicRedesignAudit
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Folder = "Documentation/UI/InventoryMusicRedesign";
    private static readonly List<string> Checks = new List<string>();

    [MenuItem("Chess/UI Audit/Capture Inventory and Music Redesign")]
    public static void Capture()
    {
        Directory.CreateDirectory(Folder); Checks.Clear();
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Inventory Music Audit", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var page = UI.Node(root.transform, "Inventory", new Rect(0, 0, 1672, 941));
        page.anchorMin = page.anchorMax = page.pivot = new Vector2(.5f, .5f); page.anchoredPosition = Vector2.zero;
        var controller = page.gameObject.AddComponent<InventoryMenuController>();
        Sprite logo = null;
        const string musicSaveKey = "chess_but_weird_music_pack";
        bool hadMusicPreference = PlayerPrefs.HasKey(musicSaveKey);
        string savedMusicPreference = PlayerPrefs.GetString(musicSaveKey);
        var musicInstanceField = typeof(GameMusicManager).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        var previousMusicManager = musicInstanceField.GetValue(null);
        var accountProperty = typeof(PlayerAuthService).GetProperty("CurrentApiUser");
        var guestProperty = typeof(PlayerAuthService).GetProperty("IsGuestSession");
        var previousAccount = accountProperty.GetValue(null);
        var previousGuest = guestProperty.GetValue(null);
        const string effectSaveKey = "chess.cosmetics.selection.v2.__inventory_ui_audit__";
        bool hadEffectPreference = PlayerPrefs.HasKey(effectSaveKey);
        string savedEffectPreference = PlayerPrefs.GetString(effectSaveKey);
        try
        {
            accountProperty.SetValue(null, new UserMeResponse { userId = "__inventory_ui_audit__" });
            guestProperty.SetValue(null, false);
            PlayerPrefs.DeleteKey(effectSaveKey);
            int back = 0;
            controller.Initialize(page, () => back++);
            var catalog = new List<ItemCatalogResponse>(); var owned = new List<InventoryItemResponse>();
            for (int i = 0; i < 12; i++)
            {
                catalog.Add(new ItemCatalogResponse { itemId = "fixture-" + i,
                    name = i == 0 ? "Tazji Low Poly" : i == 2 ? "Mid-Autumn Lantern Chess Set" : "Sketch Set " + i,
                    type = i % 2 == 0 ? "CHESS_SKIN" : "BOARD_SKIN", rarity = i % 3 == 0 ? "LEGENDARY" : i % 3 == 1 ? "RARE" : "COMMON",
                    unityAssetKey = CosmeticSelection.LowPolyId, isActive = true, tags = new[] { i % 2 == 0 ? "DOODLE" : "CHECKERED" } });
                if (i < 10) owned.Add(new InventoryItemResponse { itemId = "fixture-" + i, isEquipped = i < 2 });
            }
            Call(controller, "MergeItems", catalog, owned); Call(controller, "RefreshView");
            Require(Visible(controller).Count == 13, "Default collection contains owned skins and three built-in effects");
            Require(Visible(controller).Cast<object>().All(item => (bool)item.GetType().GetField("owned").GetValue(item)), "Default filter excludes locked items");
            Require(page.GetComponentsInChildren<MenuMusicWidget>(true).Length == 0, "Inventory is restricted to cosmetics");
            Field<Button>(controller, "nextButton").onClick.Invoke();
            Require(Field<int>(controller, "page") == 1, "Next page moves to page two");
            Field<Button>(controller, "previousButton").onClick.Invoke();
            var search = Field<TMP_InputField>(controller, "searchInput"); search.text = "Lantern";
            Require(Visible(controller).Count == 1, "Search matches an owned skin by name"); search.text = "";
            SetEnum(controller, "ownership", "All"); Call(controller, "FilterChanged");
            Require(Visible(controller).Count == 15, "All-items filter also shows locked skins");
            SetEnum(controller, "ownership", "Equipped"); Call(controller, "FilterChanged");
            Require(Visible(controller).Count == 3, "Equipped filter includes both skins and the selected effect");
            SetEnum(controller, "ownership", "Owned");
            var tags = Field<HashSet<string>>(controller, "selectedTags"); tags.Add("DOODLE"); Call(controller, "FilterChanged");
            Require(Visible(controller).Count == 5, "Metadata tags filter the player's collection"); tags.Clear();
            tags.Add("LEGENDARY"); Call(controller, "FilterChanged");
            Require(Visible(controller).Count == 4, "Rarity tags work without metadata tags"); tags.Clear();
            SetEnum(controller, "category", "Effects"); Call(controller, "RefreshView");
            Require(Visible(controller).Count == 3, "Effects category contains three supported move effects");
            object confetti = Visible(controller).Cast<object>().First(item => (string)item.GetType().GetField("unityAssetKey").GetValue(item) == "confetti");
            Call(controller, "EquipItem", confetti);
            Require(CosmeticSelection.Load().moveEffectId == "confetti", "Equipping an effect persists it for gameplay");
            Call(controller, "MergeItems", catalog, owned); Call(controller, "RefreshView");
            Require(Visible(controller).Cast<object>().Any(item => (string)item.GetType().GetField("unityAssetKey").GetValue(item) == "confetti" &&
                (bool)item.GetType().GetField("isEquipped").GetValue(item)), "Refreshing inventory preserves the equipped effect");
            SetEnum(controller, "category", "All"); Call(controller, "RefreshView");
            foreach (var size in Sizes) Render(canvas, size, "Inventory", 1672, 941, controller);
            Field<Button>(controller, "filterButton").onClick.Invoke();
            Require(Field<RectTransform>(controller, "filterOverlay").gameObject.activeSelf && !Field<CanvasGroup>(controller, "contentInput").interactable,
                "Opening filters blocks the underlying inventory");
            Require(Field<List<Button>>(controller, "tagButtons").Any(button => button.name == "Tag DOODLE"), "Filter popup exposes catalog tags");
            foreach (var size in Sizes) Render(canvas, size, "Inventory-Filters", 1672, 941, controller);
            Call(controller, "CloseFilters");
            Require(!Field<RectTransform>(controller, "filterOverlay").gameObject.activeSelf && Field<CanvasGroup>(controller, "contentInput").interactable,
                "Closing filters restores inventory controls");
            page.GetComponentsInChildren<Button>().First(button => button.name == "Back to Menu").onClick.Invoke();
            Require(back == 1, "Back forwards the menu action once");
            UnityEngine.Object.DestroyImmediate(page.gameObject);

            var paper = root.AddComponent<MainMenuPaperGraphic>();
            var frame = MenuDesignFrame.Create(root.transform, "Hub Frame", new Vector2(1920, 1080));
            var hub = UI.Node(frame, "Mode Hub", new Rect(0, 0, 1920, 1080));
            var presentation = hub.gameObject.AddComponent<ModeSelectionPresentation>();
            logo = MainMenuLogoOriginal.LoadSprite();
            presentation.Build(logo, new ModeSelectionPresentation.Actions());
            var widget = hub.GetComponent<MenuMusicWidget>();
            // Isolate pack selection from live playback while exercising the real saved setting.
            var fixtureMusic = root.AddComponent<GameMusicManager>();
            musicInstanceField.SetValue(null, fixtureMusic);
            Call(widget, "SelectPack", GameMusicPack.EpicSeven);
            Require(GameMusicManager.ActivePack == GameMusicPack.EpicSeven && PlayerPrefs.GetString(musicSaveKey) == "EpicSeven",
                "Music picker updates the playback manager and persists the selected pack");
            Require(Field<TextMeshProUGUI>(widget, "packName").text == "Epic Seven", "Hub displays the selected music pack");
            Require(hub.GetComponentsInChildren<MusicRecordGraphic>().Length == 1, "Hub contains the antialiased record drawing");
            Require(Field<Button>(widget, "trigger").navigation.mode == Navigation.Mode.Explicit, "Record participates in hub keyboard navigation");
            var recordRect = Field<RectTransform>(widget, "record");
            Quaternion beforeSpin = recordRect.localRotation;
            Call(widget, "SpinRecord", .5f);
            Require(Quaternion.Angle(beforeSpin, recordRect.localRotation) > 14, "Record animation rotates the vinyl");
            foreach (var size in Sizes) Render(canvas, size, "MainMenu2-Music", 1920, 1080, null);
            Field<Button>(widget, "trigger").onClick.Invoke();
            Require(Field<RectTransform>(widget, "popup").gameObject.activeSelf, "Clicking the record opens the music picker");
            Require(Field<Button[]>(widget, "options").Length == Enum.GetValues(typeof(GameMusicPack)).Length, "Music picker offers every existing pack");
            Require(!Field<Button>(widget, "trigger").IsInteractable(), "Music popup blocks underlying record and hub");
            foreach (var size in Sizes) Render(canvas, size, "Music-Picker", 1920, 1080, null);
            widget.Close();
            Require(!Field<RectTransform>(widget, "popup").gameObject.activeSelf && Field<Button>(widget, "trigger").IsInteractable(), "Closing the music picker restores hub controls");
            Debug.Log("[InventoryMusicRedesignAudit] PASS " + Checks.Count + " checks. Captures: " + Path.GetFullPath(Folder));
        }
        catch (Exception error) { Debug.LogException(error); throw; }
        finally
        {
            musicInstanceField.SetValue(null, previousMusicManager);
            accountProperty.SetValue(null, previousAccount);
            guestProperty.SetValue(null, previousGuest);
            if (hadEffectPreference) PlayerPrefs.SetString(effectSaveKey, savedEffectPreference);
            else PlayerPrefs.DeleteKey(effectSaveKey);
            if (hadMusicPreference) PlayerPrefs.SetString(musicSaveKey, savedMusicPreference);
            else PlayerPrefs.DeleteKey(musicSaveKey);
            PlayerPrefs.Save();
            File.WriteAllLines(Path.Combine(Folder, "checks.txt"), Checks);
            UnityEngine.Object.DestroyImmediate(root);
            if (logo) UnityEngine.Object.DestroyImmediate(logo);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720),
        new Vector2Int(800, 600), new Vector2Int(2560, 1080), new Vector2Int(1080, 1920) };

    private static void Render(Canvas canvas, Vector2Int size, string label, float width, float height, InventoryMenuController inventory)
    {
        var cameraRoot = new GameObject("Audit Camera", typeof(Camera));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraRoot, canvas.gameObject.scene);
        var camera = cameraRoot.GetComponent<Camera>(); camera.scene = canvas.gameObject.scene;
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = size.y * .5f;
        camera.transform.position = new Vector3(0, 0, -100); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = UI.Paper; camera.cullingMask = 1 << 31; camera.nearClipPlane = .1f; camera.farClipPlane = 200;
        var target = new RenderTexture(size.x, size.y, 24) { antiAliasing = 1 };
        var previous = RenderTexture.active; Texture2D capture = null;
        try
        {
            var rect = (RectTransform)canvas.transform;
            float scale = Mathf.Min(size.x / width, size.y / height);
            rect.position = Vector3.zero; rect.localScale = Vector3.one * scale;
            rect.sizeDelta = new Vector2(size.x / scale, size.y / scale);
            canvas.worldCamera = camera;
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            foreach (var frame in canvas.GetComponentsInChildren<MenuDesignFrame>()) frame.Configure(new Vector2(width, height));
            if (inventory) { ((RectTransform)inventory.transform).sizeDelta = rect.sizeDelta; Call(inventory.GetComponentInChildren<InventoryContentRootFitter>(), "Apply"); }
            Canvas.ForceUpdateCanvases();
            foreach (var record in canvas.GetComponentsInChildren<MusicRecordGraphic>())
            {
                var mesh = record.canvasRenderer.GetMesh();
                Require(mesh && mesh.vertexCount > 0, "Music record has visible mesh geometry");
            }
            foreach (var text in canvas.GetComponentsInChildren<TextMeshProUGUI>())
            {
                text.ForceMeshUpdate(); Require(!text.isTextOverflowing, label + " " + size + ": " + text.name + " fits");
            }
            var corners = new Vector3[4];
            foreach (var button in canvas.GetComponentsInChildren<Button>())
            {
                if (button.GetComponentInParent<ScrollRect>()) continue;
                ((RectTransform)button.transform).GetWorldCorners(corners);
                Require(corners.All(point => Mathf.Abs(point.x) <= size.x * .5f + 1 && Mathf.Abs(point.y) <= size.y * .5f + 1),
                    label + " " + size + ": " + button.name + " stays in viewport");
            }
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            capture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); capture.Apply();
            Require(capture.GetPixels32().Count(pixel => pixel.r < 140 && pixel.g < 140 && pixel.b < 140) > 200,
                label + " " + size + ": rendered image contains visible ink");
            File.WriteAllBytes(Path.Combine(Folder, label + "-" + size.x + "x" + size.y + ".png"), capture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; canvas.worldCamera = null;
            if (capture) UnityEngine.Object.DestroyImmediate(capture);
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cameraRoot);
        }
    }

    private static void Require(bool value, string message) { Checks.Add((value ? "PASS " : "FAIL ") + message); if (!value) throw new InvalidOperationException(message); }
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, Flags).GetValue(obj);
    private static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);
    private static IList Visible(object obj) => (IList)obj.GetType().GetMethod("GetVisibleItems", Flags).Invoke(obj, null);
    private static void SetEnum(object obj, string field, string value)
    { var info = obj.GetType().GetField(field, Flags); info.SetValue(obj, Enum.Parse(info.FieldType, value)); }
}
