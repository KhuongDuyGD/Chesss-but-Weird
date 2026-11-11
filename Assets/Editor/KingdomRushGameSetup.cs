using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;

// Repeatable game registration. Does not grant ownership, change prices or select
// a skin in the player's saved account. Shop item keys match the backend catalog.
public static class KingdomRushGameSetup
{
    public const string Root = "Assets/Skins/KingdomRushCollab";
    public const string Id = CosmeticSelection.KingdomRushId;
    private const string Data = "Assets/Game/Data";
    public static readonly string[] Prefixes = { "01_Quan_tot", "02_Quan_xe", "03_Quan_ngua", "04_Quan_tuong", "05_Quan_hau", "06_Quan_vua" };
    public static readonly PieceType[] Types = { PieceType.Pawn, PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen, PieceType.King };

    [MenuItem("Chess/Kingdom Rush/Register Shop Cosmetics")]
    public static void Setup()
    {
        foreach (string folder in new[] { Root + "/Prefabs", Data + "/PieceSkins", Data + "/Boards", Data + "/Environments" }) Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        KingdomRushAdvancedArtSetup.Prepare();
        var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(Data + "/CosmeticCatalog.asset");
        if (!catalog) throw new InvalidOperationException("The game's CosmeticCatalog is missing.");
        var pieces = LoadOrCreate<PieceSkinData>(Data + "/PieceSkins/KingdomRush.asset");
        pieces.skinId = Id; pieces.displayName = "Kingdom Rush — Denas & Vez'Nan";
        pieces.applyTeamColor = false; pieces.useAuthoredBoardScale = true; pieces.faceOpponent = true; pieces.tuning.Clear();
        pieces.preview = Preview(settings, Root + "/Previews/KingdomRush_Pieces_Overview.jpg", "Pieces");
        for (int side = 0; side < 2; side++)
        for (int kind = 0; kind < Prefixes.Length; kind++)
        {
            string name = Prefixes[kind] + (side == 0 ? "_Denas" : "_Veznan");
            var root = new GameObject(name);
            try
            {
                var model = InstantiateModel(name, root.transform);
                var marker = model.GetComponentsInChildren<Transform>().Single(t => t.name == "KR_Forward");
                var front = marker.position - model.transform.position; front.y = 0;
                model.transform.rotation = Quaternion.FromToRotation(front.normalized, Vector3.forward) * model.transform.rotation;
                var bounds = BoundsOf(model);
                model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                KingdomRushAdvancedArtSetup.ConfigurePiece(root,name);
                string path = Root + "/Prefabs/" + name + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Register(settings, prefab, "PieceSkin_KingdomRush", "Pieces/KingdomRush/" + name);
                Assign(pieces, Types[kind], side == 1, new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(path)));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        Register(settings, pieces, "PieceSkin_KingdomRush", "PieceSkin/" + Id);
        Upsert(catalog.pieceSkins, pieces, Id, pieces.displayName, pieces.preview, "LEGENDARY");

        var board = LoadOrCreate<BoardSkinData>(Data + "/Boards/KingdomRush.asset");
        board.boardId = Id; board.displayName = "Kingdom Rush — Citadel Board";
        board.boardPrefab = ModelPrefab(settings, "KR_Citadel_Board", "Board_KingdomRush", "Boards/KingdomRush");
        board.preview = Preview(settings, Root + "/Previews/KR_Citadel_Board.png", "Board");
        board.localPosition = board.localRotation = Vector3.zero; board.localScale = Vector3.one;
        board.prisonLayoutYaw = 0; board.loopAnimation = false;
        board.frameFullModel = true;
        Register(settings, board, "Board_KingdomRush", "Board/" + Id);
        Upsert(catalog.boards, board, Id, board.displayName, board.preview, "LEGENDARY");

        var background = LoadOrCreate<EnvironmentData>(Data + "/Environments/KingdomRush.asset");
        background.environmentId = Id; background.displayName = "Kingdom Rush — Linirea Valley";
        background.environmentPrefab = ModelPrefab(settings, "KR_Linirea_Valley", "Environment_KingdomRush", "Environments/KingdomRush");
        background.preview = Preview(settings, Root + "/Previews/KR_Linirea_Background.png", "Background");
        background.localPosition = background.localRotation = Vector3.zero; background.localScale = Vector3.one; background.useBoardScale = true;
        // Linirea Valley is archived; do not add its environment to new content builds.
        catalog.environments.RemoveAll(entry => entry.id == Id);

        // The default explicitly owns the galaxy sky, even in the standalone match scene.
        var basic = LoadOrCreate<EnvironmentData>(Data + "/Environments/Classic.asset");
        basic.environmentId = "classic"; basic.displayName = "Purple Galaxy";
        basic.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Skyboxes/VoidPixFreePack/VoidPix_Blink_Skybox.mat");
        basic.environmentPrefab = new AssetReferenceGameObject("");
        string classicPreview = Root + "/Previews/Classic_Background.png";
        basic.preview = Preview(settings, File.Exists(classicPreview) ? classicPreview : "Assets/Skins/Tazji'sLowPoly/Chess_Arena/Chess_Arena_Preview.png", "ClassicBackground");
        Register(settings, basic, "CoreCatalog", "Environment/classic");
        Upsert(catalog.environments, basic, "classic", basic.displayName, basic.preview, "COMMON");
        KingdomRushFactionSetup.Register(settings, catalog);
        foreach (var asset in new UnityEngine.Object[] { pieces, board, background, basic, catalog, settings }) EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log("[KingdomRushGame] Registered 12 prefabs, separate board/background, thumbnails and shop keys; defaults preserved.");
    }

    public static void BuildContent()
    {
        Setup();
        AddressableAssetSettings.BuildPlayerContent(out var result);
        if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
        Debug.Log("[KingdomRushGame] ADDRESSABLES_BUILD_PASS " + result.OutputPath);
    }

    private static GameObject InstantiateModel(string name, Transform parent)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + name + ".fbx");
        if (!asset) throw new InvalidOperationException("Missing model: " + name);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        model.transform.SetParent(parent, false);
        return model;
    }
    internal static AssetReferenceGameObject ModelPrefab(AddressableAssetSettings settings, string name, string group, string address)
    {
        var root = new GameObject(name);
        try
        {
            InstantiateModel(name, root.transform);
            string path = Root + "/Prefabs/" + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path); Register(settings, prefab, group, address);
            return new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(path));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    public static Bounds BoundsOf(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    internal static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset) return asset;
        asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
    }
    internal static AssetReferenceSprite Preview(AddressableAssetSettings settings, string path, string label)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new InvalidOperationException("Missing preview: " + path);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false; importer.maxTextureSize = 2048; importer.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        // Avoid moving existing default thumbnails to a second address.
        string guid = AssetDatabase.AssetPathToGUID(path);
        if (settings.FindAssetEntry(guid) == null) Register(settings, sprite, "CosmeticThumbnails", "Previews/KingdomRush/" + label);
        return new AssetReferenceSprite(guid);
    }
    internal static void Upsert(List<CosmeticCatalogEntry> entries, UnityEngine.Object data, string id, string label, AssetReferenceSprite preview, string tier)
    {
        var entry = entries.SingleOrDefault(e => e != null && e.id == id);
        if (entry == null) { entry = new CosmeticCatalogEntry(); entries.Add(entry); }
        entry.id = id; entry.displayName = label; entry.tier = tier;
        entry.data = new AssetReference(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data)));
        entry.thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(preview.AssetGUID));
    }
    internal static void Register(AddressableAssetSettings settings, UnityEngine.Object asset, string groupName, string address)
    {
        var group = settings.FindGroup(groupName);
        if (!group)
        {
            group = settings.CreateGroup(groupName, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>(); schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath); schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = false;
        }
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)), group).address = address;
        EditorUtility.SetDirty(group);
    }
    private static void Assign(PieceSkinData data, PieceType type, bool black, AssetReferenceGameObject value)
    {
        switch (type)
        {
            case PieceType.Pawn: if (black) data.blackPawn = value; else data.pawn = value; break;
            case PieceType.Rook: if (black) data.blackRook = value; else data.rook = value; break;
            case PieceType.Knight: if (black) data.blackKnight = value; else data.knight = value; break;
            case PieceType.Bishop: if (black) data.blackBishop = value; else data.bishop = value; break;
            case PieceType.Queen: if (black) data.blackQueen = value; else data.queen = value; break;
            case PieceType.King: if (black) data.blackKing = value; else data.king = value; break;
        }
    }
}
