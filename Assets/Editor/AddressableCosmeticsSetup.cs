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
        Folder(Root + "/Data/Environments"); Folder(Root + "/Prefabs/Classic");
        Folder(Root + "/Materials"); Folder(Root + "/Scenes");
        var catalog = GetOrCreate<CosmeticCatalog>(Root + "/Data/CosmeticCatalog.asset");
        Register(catalog, "CoreCatalog", CosmeticCatalog.Address);
        SetupClassic(catalog);
        SetupExistingSkin(catalog, "dc", "DC", "Assets/Skins/DC/Prefabs", "DC");
        SetupExistingSkin(catalog, "corn", "Corn", "Assets/Skins/Corn/Prefabs", "Corn");
        SetupBoard(catalog);
        foreach (string folder in new[] { "Main_Menu", "Gacha_menu", "GameplayUI", "Inventory", "LANUI", "MultiplayerUI", "PlayerProfile", "Result_Menu", "SettingsMenu" })
        {
            string path = "Assets/Materials/" + folder;
            if (!AssetDatabase.IsValidFolder(path)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { path }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var entry = Register(AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath), "CoreUI", assetPath);
                entry.SetLabel("CoreUI", true, true);
                string name = Path.GetFileNameWithoutExtension(assetPath);
                if (name == "LoginMainMenu" || name == "SignUpMainMenu" || name == "ConfirmLogin" || name == "ConfirmSignUp" || name == "PlayAsGuest")
                    entry.SetLabel("AuthUI", true, true);
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

    private static void SetupClassic(CosmeticCatalog catalog)
    {
        string dataPath = Root + "/Data/PieceSkins/Classic.asset";
        var data = AssetDatabase.LoadAssetAtPath<PieceSkinData>(dataPath);
        if (!data)
        {
            data = ScriptableObject.CreateInstance<PieceSkinData>();
            data.skinId = "default"; data.displayName = "Classic";
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Materials/Chessboard/source/Chess_Set.fbx");
            using (var factory = new PieceVisualFactory())
            {
                foreach (PieceType type in Enum.GetValues(typeof(PieceType)))
                {
                    string name = type == PieceType.Knight ? "Knight_1" : type + "_1";
                    var transform = source ? source.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name) : null;
                    var filter = transform ? transform.GetComponent<MeshFilter>() : null;
                    string prefabPath = Root + "/Prefabs/Classic/" + type + ".prefab";
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (!prefab)
                    {
                        GameObject visual;
                        if (filter && filter.sharedMesh && filter.sharedMesh.isReadable)
                        {
                            int count = type == PieceType.Pawn ? 8 : type == PieceType.King || type == PieceType.Queen ? 1 : 2;
                            var parts = factory.SplitMeshIntoSpatialGroups(filter.sharedMesh, count);
                            visual = new GameObject(type.ToString());
                            var mesh = UnityEngine.Object.Instantiate(parts[0].mesh);
                            AssetDatabase.CreateAsset(mesh, Root + "/Prefabs/Classic/" + type + ".asset");
                            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                            visual.AddComponent<MeshRenderer>().sharedMaterials = transform.GetComponent<MeshRenderer>().sharedMaterials;
                        }
                        else visual = CreateSimplePiece(type);
                        prefab = PrefabUtility.SaveAsPrefabAsset(visual, prefabPath);
                        UnityEngine.Object.DestroyImmediate(visual);
                    }
                    Assign(data, type, prefab);
                    Register(prefab, "PieceSkin_Classic", "Pieces/Classic/" + type);
                }
            }
            AssetDatabase.CreateAsset(data, dataPath);
        }
        Register(data, "PieceSkin_Classic", "PieceSkin/default");
        AddEntry(catalog.pieceSkins, "default", "Classic", data);
    }

    private static void SetupExistingSkin(CosmeticCatalog catalog, string id, string title, string folder, string prefix)
    {
        string path = Root + "/Data/PieceSkins/" + title + ".asset";
        var data = AssetDatabase.LoadAssetAtPath<PieceSkinData>(path);
        if (!data)
        {
            data = ScriptableObject.CreateInstance<PieceSkinData>();
            data.skinId = id; data.displayName = title;
            foreach (PieceType type in Enum.GetValues(typeof(PieceType)))
            {
                string file = id == "dc" && type == PieceType.King ? "DC_KIng" : prefix + "_" + type;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + file + ".prefab");
                if (!prefab) { Debug.LogWarning("[Cosmetics] Missing " + folder + "/" + file); continue; }
                Assign(data, type, prefab);
                Register(prefab, "PieceSkin_" + title, "Pieces/" + title + "/" + type);
                data.tuning.Add(new PieceSkinTuning
                {
                    pieceType = type,
                    rotationEuler = type == PieceType.Knight ? new Vector3(0, -90, 0) : Vector3.zero,
                    heightMultiplier = id == "dc" && type == PieceType.Pawn ? 1.08f : id == "dc" && type == PieceType.Rook ? 1.15f : 1,
                    maxFootprintMultiplier = 1
                });
            }
            AssetDatabase.CreateAsset(data, path);
        }
        Register(data, "PieceSkin_" + title, "PieceSkin/" + id);
        AddEntry(catalog.pieceSkins, id, title, data);
    }

    private static void SetupBoard(CosmeticCatalog catalog)
    {
        string path = Root + "/Data/Boards/Classic.asset";
        var data = AssetDatabase.LoadAssetAtPath<BoardSkinData>(path);
        if (!data)
        {
            data = ScriptableObject.CreateInstance<BoardSkinData>();
            data.boardId = "default"; data.displayName = "Classic";
            var root = new GameObject("Classic board - playable area 8x8 centered at origin");
            var light = Material("BoardLight", new Color(.82f, .76f, .64f));
            var dark = Material("BoardDark", new Color(.23f, .28f, .3f));
            for (int x = 0; x < 8; x++) for (int z = 0; z < 8; z++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.transform.SetParent(root.transform, false);
                tile.transform.localPosition = new Vector3(x - 3.5f, -.06f, z - 3.5f);
                tile.transform.localScale = new Vector3(1, .12f, 1);
                tile.GetComponent<Renderer>().sharedMaterial = (x + z) % 2 == 0 ? light : dark;
                UnityEngine.Object.DestroyImmediate(tile.GetComponent<Collider>());
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/Classic/Board.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            data.boardPrefab = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)));
            AssetDatabase.CreateAsset(data, path);
        }
        Register(data, "Board_Classic", "Board/default");
        if (data.boardPrefab != null) Register(data.boardPrefab.editorAsset, "Board_Classic", "Boards/Classic");
        AddEntry(catalog.boards, "default", "Classic", data);
    }

    private static GameObject CreateSimplePiece(PieceType type)
    {
        var root = new GameObject(type.ToString());
        var basePart = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        basePart.transform.SetParent(root.transform, false);
        basePart.transform.localPosition = new Vector3(0, .12f, 0);
        basePart.transform.localScale = new Vector3(.7f, .12f, .7f);
        var top = GameObject.CreatePrimitive(type == PieceType.Rook || type == PieceType.King ? PrimitiveType.Cube : type == PieceType.Bishop ? PrimitiveType.Capsule : PrimitiveType.Sphere);
        top.transform.SetParent(root.transform, false);
        top.transform.localPosition = new Vector3(0, .6f, 0);
        top.transform.localScale = new Vector3(.42f, type == PieceType.Pawn ? .42f : .75f, .42f);
        if (type == PieceType.Knight) top.transform.localRotation = Quaternion.Euler(0, 0, -30);
        foreach (var c in root.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
        return root;
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

    private static void Assign(PieceSkinData data, PieceType type, GameObject prefab)
    {
        var reference = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)));
        switch (type)
        {
            case PieceType.Pawn: data.pawn = reference; break; case PieceType.Rook: data.rook = reference; break;
            case PieceType.Knight: data.knight = reference; break; case PieceType.Bishop: data.bishop = reference; break;
            case PieceType.Queen: data.queen = reference; break; case PieceType.King: data.king = reference; break;
        }
    }
    private static void AddEntry(System.Collections.Generic.List<CosmeticCatalogEntry> entries, string id, string name, UnityEngine.Object data)
    {
        if (entries.Any(e => e != null && e.id == id)) return;
        entries.Add(new CosmeticCatalogEntry { id = id, displayName = name, data = new AssetReference(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data))) });
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
