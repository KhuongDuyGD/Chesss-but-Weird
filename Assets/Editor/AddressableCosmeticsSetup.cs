using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

// Migration is explicit and repeatable. Existing user-authored data/scenes are not overwritten.
public static class AddressableCosmeticsSetup
{
    private const string Root = "Assets/Game";
    private static AddressableAssetSettings settings;

    [MenuItem("Chess/Build Addressable Cosmetics")]
    public static void BuildContent()
    {
        Setup();
        AddressableAssetSettings.BuildPlayerContent(out var result);
        if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
        Debug.Log("[Cosmetics] Addressables content build succeeded: " + result.OutputPath);
    }

    public static void BuildVerificationPlayer()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Root + "/Scenes/Boot.unity", Root + "/Scenes/MainMenu.unity", Root + "/Scenes/ChessMatch.unity" },
            locationPathName = "Builds/CosmeticsVerification/Chess.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException("Verification player build failed: " + report.summary.result);
        Debug.Log("[Cosmetics] Verification player built.");
    }

    [MenuItem("Chess/Setup Addressable Cosmetics")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before setting up content.");
        if (!Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            throw new InvalidOperationException("Save the current scene or open a saved scene, then run setup again.");
        settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        Folder(Root + "/Data/PieceSkins"); Folder(Root + "/Data/Boards");
        Folder(Root + "/Data/Environments");
        Folder(Root + "/Materials"); Folder(Root + "/Scenes");
        var catalog = GetOrCreate<CosmeticCatalog>(Root + "/Data/CosmeticCatalog.asset");
        Register(catalog, "CoreCatalog", CosmeticCatalog.Address);
        DefaultLowPolyCosmeticsSetup.Setup();
        GachaSkinAssetsSetup.Setup();
        foreach (string folder in new[] { "Main_Menu", "Gacha_menu", "GameplayUI", "LANUI", "MultiplayerUI", "PlayerProfile", "Result_Menu", "SettingsMenu" })
        {
            string path = "Assets/Materials/" + folder;
            if (!AssetDatabase.IsValidFolder(path)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { path }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var entry = Register(AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath), "CoreUI", assetPath);
                entry.SetLabel("CoreUI", true, true);
            }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/SFX" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Register(AssetDatabase.LoadAssetAtPath<AudioClip>(path), "CoreAudio", path);
        }
        Group("Environments"); Group("VFX");
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        SetupScenes();
        Debug.Log("[Cosmetics] Ready. Open Assets/Game/Scenes/Boot.unity. Build Addressables before a player build. Configure Remote paths only for groups you want to host.");
    }

    private static void SetupScenes()
    {
        // Additive editor creation leaves the user's open scene untouched.
        string boot = Root + "/Scenes/Boot.unity", menu = Root + "/Scenes/MainMenu.unity", match = Root + "/Scenes/ChessMatch.unity";
        Scene previous = SceneManager.GetActiveScene();
        foreach (string path in new[] { boot, menu, match })
        {
            if (File.Exists(path)) continue;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            if (path != match) new GameObject("Bootstrap").AddComponent<GameBootstrap>();
            else
            {
                var board = new GameObject("Logical Chessboard").AddComponent<Chessboard>();
                var serialized = new SerializedObject(board);
                serialized.FindProperty("tileMaterial").objectReferenceValue = Material("BoardLight", new Color(.82f, .76f, .64f));
                serialized.FindProperty("showGeneratedTiles").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(4, 9, -6);
                camera.transform.LookAt(new Vector3(4, 0, 4));
                camera.gameObject.AddComponent<AudioListener>();
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
            }
            EditorSceneManager.SaveScene(scene, path);
            if (!Application.isBatchMode) EditorSceneManager.CloseScene(scene, true);
        }
        if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        var paths = new[] { boot, menu, match };
        EditorBuildSettings.scenes = paths.Select(p => new EditorBuildSettingsScene(p, true))
            .Concat(EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path))).ToArray();
    }

    [MenuItem("Chess/Make Selected Addressables Group Remote")]
    private static void MakeRemote()
    {
        settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        var group = Selection.activeObject as AddressableAssetGroup;
        if (!group) { Debug.LogWarning("Select an Addressables group asset first."); return; }
        var schema = group.GetSchema<BundledAssetGroupSchema>();
        schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
        schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
        settings.BuildRemoteCatalog = true;
        EditorUtility.SetDirty(schema); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        Debug.Log("Configure Remote.LoadPath in Addressables Profiles for your CDN, then build content.");
    }

    private static AddressableAssetEntry Register(UnityEngine.Object asset, string group, string address)
    {
        if (!asset) throw new InvalidOperationException("Missing asset for " + address);
        var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)), Group(group));
        entry.address = address;
        return entry;
    }
    private static AddressableAssetGroup Group(string name)
    {
        var group = settings.FindGroup(name);
        if (group) return group;
        group = settings.CreateGroup(name, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        var schema = group.GetSchema<BundledAssetGroupSchema>();
        schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
        schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
        schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
        group.GetSchema<ContentUpdateGroupSchema>().StaticContent = false;
        return group;
    }
    private static Material Material(string name, Color color)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.color = color; AssetDatabase.CreateAsset(material, path); return material;
    }
    private static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        var value = AssetDatabase.LoadAssetAtPath<T>(path);
        if (value) return value;
        value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); return value;
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
