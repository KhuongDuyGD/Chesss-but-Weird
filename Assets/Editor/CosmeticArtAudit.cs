using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Asset and UI interaction checks with local fixtures only; never calls account APIs.
public static class CosmeticArtAudit
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> checks = new List<string>();

    // Projection math only, using the two actual scene cameras. Never starts a match
    // or authentication, and never saves the preview scenes.
    public static void VerifyCameraProjection()
    {
        var results = new List<string>();
        foreach (string path in new[] { "Assets/Game/Scenes/ChessMatch.unity", "Assets/Scenes/ChessClassic.unity" })
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var roots = scene.GetRootGameObjects();
                var board = roots.SelectMany(root => root.GetComponentsInChildren<Chessboard>(true)).Single();
                var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single(c => c.CompareTag("MainCamera"));
                if (!camera.orthographic || !camera.GetComponent<ChessOrbitCamera>())
                    throw new InvalidOperationException(path + " must store an orthographic camera with its orbit controller.");
                results.Add("PASS " + path + " stores parallel projection before match initialization");
                Call(board, "Awake");
                var owner = new GameObject("Camera projection fixture");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owner, scene);
                var game = owner.AddComponent<ChessGame>();
                game.AttachBoard(board);
                var orbit = camera.GetComponent<ChessOrbitCamera>();
                if ((Camera)typeof(ChessGame).GetField("gameplayCamera", Private).GetValue(game) != camera)
                    throw new InvalidOperationException("Match binding selected a camera outside its board scene.");
                results.Add("PASS " + path + " binds the owner to its board scene camera");
                typeof(ChessGame).GetField("gameStarted", Private).SetValue(game, true);
                foreach (bool chaos in new[] { false, true })
                foreach (float aspect in new[] { 16f / 9f, 4f / 3f, 9f / 16f })
                foreach (PieceTeam team in new[] { PieceTeam.White, PieceTeam.Black })
                {
                    typeof(ChessGame).GetField("aramMode", Private).SetValue(game, chaos);
                    camera.aspect = aspect;
                    orbit.ConfigureForPlayerSide(team, true);
                    foreach (float yaw in new[] { 0f, 32f, 122f })
                    foreach (float pitch in new[] { -15f, 0f, 12f })
                    foreach (float zoom in new[] { .55f, 1f, 1.65f })
                    {
                        typeof(ChessOrbitCamera).GetField("orbitYaw", Private).SetValue(orbit, yaw);
                        typeof(ChessOrbitCamera).GetField("orbitPitch", Private).SetValue(orbit, pitch);
                        typeof(ChessOrbitCamera).GetField("currentZoom", Private).SetValue(orbit, zoom);
                        Call(orbit, "FrameBoard");
                        AssertParallelGrid(camera, board);
                        results.Add($"PASS {path} {(chaos ? "Chaos" : "Classic/PVE")} {team} aspect={aspect:F3} yaw={yaw} pitch={pitch} zoom={zoom}: equal rows and parallel columns");
                    }
                }
                // Repair a stale perspective matrix at the rendering boundary as well.
                camera.orthographic = false;
                camera.projectionMatrix = Matrix4x4.Perspective(60, camera.aspect, .3f, 1000);
                Call(orbit, "BeforeCameraRendering", default(UnityEngine.Rendering.ScriptableRenderContext), camera);
                AssertParallelGrid(camera, board);
                results.Add("PASS " + path + " restores parallel projection before rendering after a perspective override");
                orbit.enabled = false;
                camera.orthographic = false; camera.ResetProjectionMatrix();
                if (Mathf.Abs(camera.projectionMatrix.m32) < .5f)
                    throw new InvalidOperationException("Disabled orbit must allow the separate rifle perspective view.");
                orbit.enabled = true; orbit.Bind(game, board);
                AssertParallelGrid(camera, board);
                results.Add("PASS " + path + " returns from rifle perspective to a parallel board view");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        Directory.CreateDirectory("Logs/camera-projection");
        File.WriteAllLines("Logs/camera-projection/checks.txt", results);
        Debug.Log("[CameraProjectionAudit] PASS " + results.Count + " scene/projection checks. No API calls, rendered frames or live matches.");
    }

    private static void AssertParallelGrid(Camera camera, Chessboard board)
    {
        if (!camera.orthographic || Mathf.Abs(camera.projectionMatrix.m32) > .00001f ||
            Mathf.Abs(camera.projectionMatrix.m33 - 1) > .00001f ||
            Mathf.Abs(camera.nonJitteredProjectionMatrix.m33 - 1) > .00001f)
            throw new InvalidOperationException("Camera is using perspective projection.");
        Vector2 a = camera.WorldToViewportPoint(board.GetTileCenterWorld(Vector2Int.zero));
        Vector2 dx = (Vector2)camera.WorldToViewportPoint(board.GetTileCenterWorld(Vector2Int.right)) - a;
        Vector2 dz = (Vector2)camera.WorldToViewportPoint(board.GetTileCenterWorld(Vector2Int.up)) - a;
        if (dx.sqrMagnitude < .0000001f || dz.sqrMagnitude < .0000001f)
            throw new InvalidOperationException("Projection collapsed a board axis.");
        for (int x = 0; x < 8; x++) for (int z = 0; z < 8; z++)
        {
            Vector2 actual = camera.WorldToViewportPoint(board.GetTileCenterWorld(new Vector2Int(x, z)));
            if (Vector2.Distance(actual, a + dx * x + dz * z) > .0001f)
                throw new InvalidOperationException("Board rows converge or change size with depth.");
        }
    }

    public static void SetupAndVerify()
    {
        CosmeticArtSetup.Setup();
        Verify();
        AddressableAssetSettings.BuildPlayerContent(out var result);
        Require(string.IsNullOrEmpty(result.Error), "Addressables content builds: " + result.Error);
        File.WriteAllLines("Logs/skin-update/checks.txt", checks);
        Debug.Log("[CosmeticArtAudit] PASS " + checks.Count + " checks, including content build. No API calls or live matches.");
    }

    public static void Verify()
    {
        checks.Clear();
        var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/Game/Data/CosmeticCatalog.asset");
        Require(catalog && catalog.pieceSkins.Count == 3 && catalog.boards.Count == 4, "Three mapped chess sets and four mapped boards");
        foreach (var entry in catalog.pieceSkins.Concat(catalog.boards))
        {
            Require(entry.HasArt, entry.id + " has usable data and a thumbnail");
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.thumbnail));
            Require(AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(guid)?.parentGroup.Name == "CosmeticThumbnails",
                entry.id + " thumbnail is separated from model bundles");
        }
        var defaultBoard = AssetDatabase.LoadAssetAtPath<BoardSkinData>("Assets/Game/Data/Boards/TazjiLowPoly.asset");
        Require(defaultBoard.localRotation == Vector3.zero && defaultBoard.prisonLayoutYaw == -90f, "Updated default keeps native orientation and front/back prisoner rails");
        VerifyPrisonPads(defaultBoard);
        var celestial = AssetDatabase.LoadAssetAtPath<BoardSkinData>("Assets/Game/Data/Boards/TazjiCelestial.asset");
        Require(celestial.boardId == "Boards/Celestial" && celestial.loopAnimation, "Celestial key and autoplay configuration");
        VerifyPrisonPads(celestial);
        string modelPath = AssetDatabase.GUIDToAssetPath(celestial.boardPrefab.AssetGUID);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var clip = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        Require(clip.legacy && clip.length > 0 && clip.wrapMode == WrapMode.Loop, "Celestial imports a looping animation clip");
        var instance = UnityEngine.Object.Instantiate(model);
        try
        {
            var animation = instance.GetComponentInChildren<Animation>();
            Require(animation && animation.clip, "Celestial prefab contains a playable Animation component");
            var transforms = instance.GetComponentsInChildren<Transform>();
            clip.SampleAnimation(instance, .2f);
            var before = transforms.Select(t => (t.localPosition, t.localRotation, t.localScale)).ToArray();
            clip.SampleAnimation(instance, 2.6f);
            Require(transforms.Where((t, i) => Vector3.Distance(t.localPosition, before[i].localPosition) > .001f ||
                Quaternion.Angle(t.localRotation, before[i].localRotation) > .01f || Vector3.Distance(t.localScale, before[i].localScale) > .001f).Any(),
                "Sampling Celestial animation changes decoration transforms");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }

        var scene = EditorSceneManager.NewPreviewScene();
        var host = new GameObject("Cosmetic local audit", typeof(RectTransform), typeof(Canvas));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, scene);
        var account = typeof(PlayerAuthService).GetProperty("CurrentApiUser");
        var guest = typeof(PlayerAuthService).GetProperty("IsGuestSession");
        object previousAccount = account.GetValue(null), previousGuest = guest.GetValue(null);
        try
        {
            account.SetValue(null, new UserMeResponse { userId = "__cosmetic_art_fixture__" }); guest.SetValue(null, false);
            var loading = host.AddComponent<LoadingManager>();
            typeof(LoadingManager).GetProperty("Catalog").SetValue(loading, catalog);
            var page = SketchbookUI.Node(host.transform, "Loadout", new Rect(0, 0, 1672, 941));
            var picker = page.gameObject.AddComponent<MatchLoadoutController>(); picker.Initialize(page, loading);
            Set(picker, "selection", new CosmeticSelection()); Set(picker, "canChooseBlack", true); Set(picker, "accountReady", true);
            Call(picker, "AddBuiltInChoices");
            Call(picker, "AddChoice", "Boards/Celestial", "Celestial Board", "BOARD_SKIN", true);
            Call(picker, "RenderChoices");
            var summary = Field<RectTransform>(picker, "summaryRoot");
            Require(summary.GetComponentsInChildren<Button>().Length == 2, "Summary has exactly Board and Chess cards");
            summary.GetComponentsInChildren<Button>().First(b => b.name == "BOARD Slot").onClick.Invoke();
            var overlay = Field<RectTransform>(picker, "overlay");
            Require(overlay.gameObject.activeSelf && !Field<CanvasGroup>(picker, "contentInput").blocksRaycasts, "Board card opens modal and blocks underlying controls");
            Require(overlay.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Category ")) == 3, "Popup exposes Chess, Board and Move Effect tabs");
            Require(overlay.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text.StartsWith("LEGENDARY  /")), "Popup groups Celestial under Legendary");
            var choiceRoot = Field<RectTransform>(picker, "choiceRoot");
            var celestialButton = choiceRoot.GetComponentsInChildren<Button>().First(b => b.name.EndsWith("Boards/Celestial"));
            celestialButton.onClick.Invoke();
            Require(Field<CosmeticSelection>(picker, "selection").boardId == "Boards/Celestial", "Owned board selection updates summary");
            Require(choiceRoot.GetComponentsInChildren<TextMeshProUGUI>().Count(t => t.text == "Equipped") == 1, "Selected board has one Equipped badge");
            var equippedFrame = choiceRoot.GetComponentsInChildren<Image>().First(i => i.name == "Thumbnail" && i.color.r > .99f && i.color.g < .8f);
            Require(equippedFrame, "Equipped thumbnail uses a gold border");
            Require(choiceRoot.GetComponentsInChildren<Button>().Where(b => b.name.Contains("ForestGarden")).All(b => !b.interactable), "Unowned art remains locked");
            var tabs = Field<Button[]>(picker, "categoryButtons"); tabs[0].onClick.Invoke();
            Require(Field<RectTransform>(picker, "choiceRoot").GetComponentsInChildren<Button>().Any(b => b.name.Contains("ChessSets/")), "Chess tab shows mapped thumbnails");
            tabs[2].onClick.Invoke();
            Require(Field<RectTransform>(picker, "choiceRoot").GetComponentsInChildren<Button>().Length == 3, "Move Effect tab retains three working effects");
            Call(picker, "ClosePopup");
            Require(!overlay.gameObject.activeSelf && Field<CanvasGroup>(picker, "contentInput").blocksRaycasts, "Closing restores main loadout input");

            var inventoryPage = SketchbookUI.Node(host.transform, "Inventory", new Rect(0, 0, 1672, 941));
            var inventory = inventoryPage.gameObject.AddComponent<InventoryMenuController>(); inventory.Initialize(inventoryPage, () => { }, catalog);
            Call(inventory, "MergeItems", new List<ItemCatalogResponse> {
                new ItemCatalogResponse { itemId = "good", name = "Celestial", type = "BOARD_SKIN", unityAssetKey = "Boards/Celestial", isActive = true, rarity = "LEGENDARY" },
                new ItemCatalogResponse { itemId = "no-art", name = "Not ready", type = "BOARD_SKIN", unityAssetKey = "Boards/NoArt", isActive = true }
            }, new List<InventoryItemResponse> { new InventoryItemResponse { itemId = "good", isEquipped = true }, new InventoryItemResponse { itemId = "no-art" } });
            Call(inventory, "RefreshView");
            Require(((IList)Call(inventory, "GetVisibleItems")).Count == 4, "Inventory hides missing-art skin and retains one board plus three effects");
            Require(inventoryPage.GetComponentsInChildren<Image>().Any(i => i.sprite == catalog.FindBoard("Boards/Celestial").thumbnail), "Inventory displays actual Celestial thumbnail");
            Require(typeof(InventoryMenuController).GetMethod("EquipItem", Private) == null, "Inventory remains collection browsing only");
        }
        finally
        {
            account.SetValue(null, previousAccount); guest.SetValue(null, previousGuest);
            UnityEngine.Object.DestroyImmediate(host); EditorSceneManager.ClosePreviewScene(scene);
        }
        Directory.CreateDirectory("Logs/skin-update"); File.WriteAllLines("Logs/skin-update/checks.txt", checks);
        Debug.Log("[CosmeticArtAudit] PASS " + checks.Count + " local content/UI checks.");
    }

    private static object Call(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, Private).Invoke(instance, args);

    // Use the real scene layouts: ChessClassic has ~0.477-unit tiles while
    // ChessMatch uses one-unit tiles. Unit-only fixtures missed vertical stretch.
    public static void VerifyBoardProportions()
    {
        var results = new List<string>();
        var data = AssetDatabase.LoadAssetAtPath<BoardSkinData>("Assets/Game/Data/Boards/TazjiCelestial.asset");
        string path = AssetDatabase.GUIDToAssetPath(data.boardPrefab.AssetGUID);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        foreach (string scenePath in new[] { "Assets/Scenes/ChessClassic.unity", "Assets/Game/Scenes/ChessMatch.unity" })
        {
            var scene = EditorSceneManager.OpenPreviewScene(scenePath);
            try
            {
                var board = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Chessboard>(true)).First();
                Call(board, "Awake");
                var centers = new List<Vector3>();
                for (int x = 0; x < 8; x++) for (int z = 0; z < 8; z++) centers.Add(board.GetTileCenterWorld(new Vector2Int(x, z)));
                var wrapper = new GameObject("Celestial proportion fixture");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(wrapper, scene);
                var model = UnityEngine.Object.Instantiate(prefab, wrapper.transform, false);
                model.transform.localPosition = data.localPosition;
                model.transform.localRotation = Quaternion.Euler(data.localRotation);
                model.transform.localScale = data.localScale;
                var reference = UnityEngine.Object.Instantiate(prefab);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(reference, scene);
                board.AttachCosmeticBoard(wrapper.transform, model.transform, data.prisonLayoutYaw);
                int index = 0;
                for (int x = 0; x < 8; x++) for (int z = 0; z < 8; z++)
                    if (Vector3.Distance(centers[index++], board.GetTileCenterWorld(new Vector2Int(x, z))) > .00001f)
                        throw new InvalidOperationException("Cosmetic scale moved a logical tile in " + scenePath);
                results.Add("PASS " + scenePath + " preserves all 64 logical tile centers; visual scale=" + wrapper.transform.localScale.ToString("F6"));

                var actualRenderers = model.GetComponentsInChildren<Renderer>(true);
                var referenceRenderers = reference.GetComponentsInChildren<Renderer>(true);
                foreach (float time in new[] { 0f, .5f, 3f, 6f, 9f, 11.99f })
                {
                    clip.SampleAnimation(model, time); clip.SampleAnimation(reference, time);
                    float worstStretch = 1f; string worstNode = "";
                    for (int i = 0; i < actualRenderers.Length; i++)
                    {
                        Matrix4x4 actual = board.transform.worldToLocalMatrix * actualRenderers[i].localToWorldMatrix;
                        Matrix4x4 original = reference.transform.worldToLocalMatrix * referenceRenderers[i].localToWorldMatrix;
                        float min = float.MaxValue, max = 0f;
                        foreach (Vector3 axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
                        {
                            float sourceLength = original.MultiplyVector(axis).magnitude;
                            if (sourceLength < .000001f) continue;
                            float ratio = actual.MultiplyVector(axis).magnitude / sourceLength;
                            min = Mathf.Min(min, ratio); max = Mathf.Max(max, ratio);
                        }
                        if (min > .000001f && max / min > worstStretch)
                        { worstStretch = max / min; worstNode = actualRenderers[i].name; }
                    }
                    if (worstStretch > 1.002f)
                        throw new InvalidOperationException(scenePath + " stretches " + worstNode + " by " + worstStretch + " at " + time + "s");
                    results.Add("PASS " + scenePath + " at " + time + "s: " + actualRenderers.Length + " renderer transforms retain proportions; max axis ratio=" + worstStretch.ToString("F6"));
                }
                var filters = model.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.Contains("Reserve_Inlays")).ToArray();
                var colliders = filters.Select(f => { var collider = f.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = f.sharedMesh; return collider; }).ToArray();
                Physics.SyncTransforms();
                foreach (PieceTeam team in new[] { PieceTeam.White, PieceTeam.Black })
                    for (int slot = 0; slot < 6; slot++)
                    {
                        Vector3 point = board.GetPrisonSlotWorld(team, slot);
                        var ray = new Ray(point + board.transform.up, -board.transform.up);
                        if (!colliders.Any(c => c.Raycast(ray, out var hit, 2f) && Mathf.Abs(Vector3.Dot(hit.point - point, board.transform.up)) < .025f))
                            throw new InvalidOperationException(scenePath + " prisoner misses pad: " + team + "/" + slot);
                    }
                results.Add("PASS " + scenePath + " keeps all 12 prisoner positions on actual pads");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        Directory.CreateDirectory("Logs/skin-update");
        File.WriteAllLines("Logs/skin-update/proportions.txt", results);
        Debug.Log("[CelestialProportions] PASS " + results.Count + " checks across both scene layouts and the animation loop.");
    }

    private static void VerifyPrisonPads(BoardSkinData data)
    {
        var host = new GameObject("Prison pad fixture");
        var wrapper = new GameObject("Board wrapper");
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(data.boardPrefab.AssetGUID)));
        var meshes = new List<Mesh>();
        try
        {
            var board = host.AddComponent<Chessboard>(); Call(board, "Awake");
            host.transform.SetPositionAndRotation(new Vector3(7, .2f, -3), Quaternion.Euler(0, 23, 0));
            host.transform.localScale = new Vector3(1.3f, 1, .8f);
            model.transform.SetParent(wrapper.transform, false);
            model.transform.localRotation = Quaternion.Euler(data.localRotation);
            board.AttachCosmeticBoard(wrapper.transform, model.transform, data.prisonLayoutYaw);
            var filters = model.GetComponentsInChildren<MeshFilter>().Where(f => f.name.Contains("Prisoner_")).ToArray();
            if (filters.Length == 0) filters = model.GetComponentsInChildren<MeshFilter>().Where(f => f.name.Contains("Reserve_Inlays")).ToArray();
            Require(filters.Length > 0, data.boardId + " prisoner pad geometry is present");
            var colliders = new List<MeshCollider>();
            foreach (var filter in filters)
            {
                var mesh = new Mesh { vertices = filter.sharedMesh.vertices, triangles = filter.sharedMesh.triangles };
                mesh.RecalculateBounds(); meshes.Add(mesh);
                var collider = filter.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = mesh; colliders.Add(collider);
            }
            Physics.SyncTransforms();
            foreach (PieceTeam team in new[] { PieceTeam.White, PieceTeam.Black })
            for (int slot = 0; slot < 6; slot++)
            {
                Vector3 point = board.GetPrisonSlotWorld(team, slot);
                var ray = new Ray(point + board.transform.up, -board.transform.up);
                Require(colliders.Any(c => c.Raycast(ray, out var hit, 2) && Mathf.Abs(Vector3.Dot(hit.point - point, board.transform.up)) < .025f),
                    data.boardId + " prisoner " + team + "/" + slot + " lands on its pad");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(wrapper); UnityEngine.Object.DestroyImmediate(host);
            foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
        }
    }
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, Private).GetValue(instance);
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Private).SetValue(instance, value);
    private static void Require(bool condition, string text)
    { if (!condition) throw new InvalidOperationException("[CosmeticArtAudit] " + text); checks.Add("PASS " + text); }
}
