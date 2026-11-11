using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChessButWeird.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Read-only inspection of the original collections. The two faction boards are excluded.
public static class LegacyQualityAudit
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private sealed class MemoryStore : ISettingsStore
    {
        public string Read(string id) => null;
        public void Write(string id, string value) { }
        public void Flush() { }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);

    [MenuItem("Chess/Verify Menu and Legacy Quality")]
    public static void Verify()
    {
        Directory.CreateDirectory("Logs/legacy-quality");
        var checks = new List<string>();
        var managerField = typeof(UserSettings).GetField("manager", BindingFlags.Static | BindingFlags.NonPublic);
        var preferencesField = typeof(UserSettings).GetField("preferences", BindingFlags.Static | BindingFlags.NonPublic);
        var previousManager = managerField.GetValue(null); var previousPreferences = preferencesField.GetValue(null);
        var previousPipeline = QualitySettings.renderPipeline;
        var pipeline = UnityEngine.Object.Instantiate(GraphicsSettings.currentRenderPipeline);
        QualitySettings.renderPipeline = pipeline;
        try
        {
            var settings = new SettingsManager(SettingsRegistry.Create(), new MemoryStore());
            managerField.SetValue(null, settings); preferencesField.SetValue(null, null);
            settings.Set("graphics", 3); settings.Set("shadows", 3); settings.Set("aa", (int)GameAntiAliasingMode.SMAA); settings.Set("effects", 2);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Quality audit camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true;
            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f; sun.transform.rotation = Quaternion.Euler(45, -35, 0);
            sun.shadows = LightShadows.None;
            GameRuntimeSettings.RefreshMatchCameras();
            Check(sun.shadows == LightShadows.Soft, "High shadows did not activate the original scene Sun");
            Check(camera.GetUniversalAdditionalCameraData().antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing, "SMAA was not applied");
            settings.Set("shadows", 0); GameRuntimeSettings.RefreshMatchCameras();
            Check(sun.shadows == LightShadows.None, "Off did not disable the Sun's shadows");
            settings.Set("shadows", 3); GameRuntimeSettings.RefreshMatchCameras();
            checks.Add("PASS High/Off shadows on the scene Sun and per-camera SMAA");

            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/Game/Data/CosmeticCatalog.asset");
            Check(catalog.FindEnvironment("kingdom_rush") == null && catalog.FindEnvironment("kingdom_rush_denas") != null && catalog.FindEnvironment("kingdom_rush_veznan") != null, "Environment retirement affected the new maps");
            var classic = AssetDatabase.LoadAssetAtPath<EnvironmentData>("Assets/Game/Data/Environments/Classic.asset");
            Check(classic.skybox && classic.skybox.name == "VoidPix_Blink_Skybox", "Classic is not the purple galaxy");
            using (var assets = new AssetLoader())
            {
                var board = new BoardSkinManager(assets);
                typeof(BoardSkinManager).GetProperty("Environment").SetValue(board, classic);
                camera.clearFlags = CameraClearFlags.SolidColor;
                var sky = board.InstantiateEnvironment(null);
                Check(sky && RenderSettings.skybox == classic.skybox && camera.clearFlags == CameraClearFlags.Skybox, "Prefab-free galaxy did not load");
                sky.SetActive(false); sky.SetActive(true);
                Check(RenderSettings.skybox == classic.skybox && camera.clearFlags == CameraClearFlags.Skybox, "Galaxy was lost after re-enabling the environment");
                checks.Add("PASS retired Valley is unavailable; galaxy loads without an environment prefab");
                foreach (var entry in catalog.boards.Where(e => !e.id.StartsWith("kingdom_rush", StringComparison.Ordinal)))
                {
                    var data = AssetDatabase.LoadAssetAtPath<BoardSkinData>(AssetDatabase.GUIDToAssetPath(entry.data.AssetGUID));
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(data.boardPrefab.AssetGUID));
                    typeof(BoardSkinManager).GetProperty("Data").SetValue(board, data);
                    typeof(BoardSkinManager).GetProperty("BoardPrefab").SetValue(board, prefab);
                    var instance = board.InstantiateBoard(null);
                    Check(instance && instance.GetComponent<LegacyBoardPresentation>(), "Missing quality presentation: " + entry.id);
                    foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                        Check(renderer.receiveShadows && renderer.shadowCastingMode == ShadowCastingMode.On, "Board shadow flags missing: " + entry.id);
                    var presentation = instance.GetComponent<LegacyBoardPresentation>();
                    var profile = ScriptableObject.CreateInstance<VolumeProfile>(); var bloom = profile.Add<Bloom>(true);
                    Set(presentation, "profile", profile); Call(presentation, "Refresh", new object[] { null });
                    Check(bloom.intensity.value > 0 && bloom.highQualityFiltering.value, "High VFX missing: " + entry.id);
                    settings.Set("effects", 0); Call(presentation, "Refresh", new object[] { null });
                    Check(bloom.intensity.value == 0, "Low VFX ignored: " + entry.id);
                    settings.Set("effects", 2); Call(presentation, "Refresh", new object[] { null });
                    if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) Capture(camera, instance, data.name);
                    UnityEngine.Object.DestroyImmediate(instance);
                    checks.Add("PASS " + entry.id + " mesh shadows, High bloom and Low effects");
                }
                UnityEngine.Object.DestroyImmediate(sky);
            }
            Check(PlayerAuthService.CurrentApiUser == null, "UI audit must not use an authenticated account");
            var canvas = new GameObject("Loadout audit", typeof(RectTransform), typeof(Canvas));
            var picker = canvas.AddComponent<MatchLoadoutController>();
            var loader = canvas.AddComponent<LoadingManager>();
            typeof(LoadingManager).GetProperty("Catalog").SetValue(loader, catalog);
            picker.Initialize((RectTransform)canvas.transform, loader); picker.OpenOnline(null, null, "FIND MATCH");
            foreach (var name in new[] { "White Loadout", "Black Loadout", "Background", "Category Background" })
                Check(!canvas.GetComponentsInChildren<Button>(true).Single(b => b.name == name).gameObject.activeSelf, "Online picker exposes " + name);
            checks.Add("PASS multiplayer hides both side buttons and both background entry points");
            UnityEngine.Object.DestroyImmediate(canvas);
            var authHost = new GameObject("Auth audit"); var auth = authHost.AddComponent<MainMenuAuthUI>();
            auth.Initialize(null, "", "");
            foreach (bool register in new[] { false, true })
            {
                if (register) auth.ShowSignUp(); else auth.ShowLogin();
                Check(authHost.GetComponentsInChildren<Button>().Any(b => b.name == "Quit Game" && b.interactable), "Auth quit button missing");
            }
            UnityEngine.Object.DestroyImmediate(authHost);
            checks.Add("PASS Quit is available on both login and registration");
            Inspect();
            File.WriteAllLines("Logs/legacy-quality/checks.txt", checks);
            Debug.Log("[LegacyQuality] PASS " + checks.Count + " checks. " + SystemInfo.graphicsDeviceType);
        }
        catch (Exception error)
        {
            File.WriteAllText("Logs/legacy-quality/checks.txt", "FAIL " + error); throw;
        }
        finally
        {
            managerField.SetValue(null, previousManager); preferencesField.SetValue(null, previousPreferences);
            QualitySettings.renderPipeline = previousPipeline; UnityEngine.Object.DestroyImmediate(pipeline);
        }
    }
    private static void Capture(Camera camera, GameObject model, string name)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds; foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        camera.orthographicSize = size * .7f; camera.aspect = 16f / 9f;
        camera.nearClipPlane = .01f; camera.farClipPlane = size * 20;
        camera.transform.position = bounds.center + new Vector3(-.9f, 1.2f, -1.4f) * size;
        camera.transform.LookAt(bounds.center);
        var target = new RenderTexture(1280, 720, 24); target.Create();
        var previous = RenderTexture.active;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            File.WriteAllBytes("Logs/legacy-quality/" + name + ".png", texture.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    }
    [MenuItem("Chess/Audit Legacy Collection Quality")]
    public static void Inspect()
    {
        var lines = new List<string>();
        string[] folders = { "Tazji'sLowPoly", "Tazji'sCrystalChessSet", "Tazji'sWoodlandChessSet",
            "Tazji'sForestGardenBoard", "Tazji'sMarbleBoard", "Tazji'sCelestialBoard" };
        foreach (var folder in folders)
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Skins/" + folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!model) throw new InvalidOperationException("Model missing: " + path);
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            Check(renderers.Length > 0, "No renderers: " + path);
            lines.Add(path + " renderers=" + renderers.Length);
            foreach (var mat in renderers.SelectMany(r => r.sharedMaterials).Distinct())
            {
                Check(mat && mat.shader.name == "Universal Render Pipeline/Lit" && mat.GetFloat("_ReceiveShadows") == 1, "Material cannot receive lit shadows: " + path);
                lines.Add("  " + (mat ? mat.name + " shader=" + mat.shader.name + " receiveShadows=" +
                    (mat.HasProperty("_ReceiveShadows") ? mat.GetFloat("_ReceiveShadows").ToString() : "n/a") : "MISSING MATERIAL"));
            }
        }
        Directory.CreateDirectory("Logs/legacy-quality");
        File.WriteAllLines("Logs/legacy-quality/materials.txt", lines);
        Debug.Log("[LegacyQuality] Inspected " + lines.Count + " model/material rows.");
    }
}
