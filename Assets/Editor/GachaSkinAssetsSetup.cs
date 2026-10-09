using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;

// Keys match the existing MongoDB items.unityAssetKey values.
public static class GachaSkinAssetsSetup
{
    private const string Root = "Assets/Game/Data";
    public const string CrystalKey = "ChessSets/CrystalBlue";
    public const string WoodlandKey = "ChessSets/Woodland";
    public const string ForestKey = "Boards/ForestGarden";
    public const string MarbleKey = "Boards/ClassicMarble";
    private static readonly string[] Types = { "Pawn", "Rook", "Knight", "Bishop", "Queen", "King" };

    [MenuItem("Chess/Setup Gacha Skin Assets")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before registering skins.");
        var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(Root + "/CosmeticCatalog.asset");
        if (!catalog) throw new InvalidOperationException("Cosmetic catalog is missing.");
        ValidateModels("Tazji'sCrystalChessSet", "CR_Glacier", "CR_Amethyst");
        ValidateModels("Tazji'sWoodlandChessSet", "WL_Oak", "WL_Nightgrove");
        RequireModel("Assets/Skins/Tazji'sForestGardenBoard/Forest_Garden_Board.fbx");
        RequireModel("Assets/Skins/Tazji'sMarbleBoard/Classic_Marble_Board.fbx");

        var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        Register(settings, catalog, "CoreCatalog", CosmeticCatalog.Address);
        Pieces(settings, catalog, "TazjiCrystal", CrystalKey, "Blue Crystal Chess Set",
            "Tazji'sCrystalChessSet", "CR_Glacier", "CR_Amethyst", "Crystal_Chess_Set_Preview.png");
        Pieces(settings, catalog, "TazjiWoodland", WoodlandKey, "Woodland Chess Set",
            "Tazji'sWoodlandChessSet", "WL_Oak", "WL_Nightgrove", "Woodland_Chess_Set_Preview.png");
        Board(settings, catalog, "TazjiForestGarden", ForestKey, "Forest Garden Board",
            "Tazji'sForestGardenBoard", "Forest_Garden_Board");
        Board(settings, catalog, "TazjiMarble", MarbleKey, "Classic Marble Board",
            "Tazji'sMarbleBoard", "Classic_Marble_Board");
        EditorUtility.SetDirty(catalog);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log("[GachaSkins] Registered Crystal, Woodland, Forest Garden and Marble with backend keys.");
    }

    private static void ValidateModels(string folder, params string[] prefixes)
    {
        foreach (string prefix in prefixes)
            foreach (string type in Types)
                RequireModel("Assets/Skins/" + folder + "/" + prefix + "_" + type + ".fbx");
    }

    private static GameObject RequireModel(string path)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!model || model.GetComponentsInChildren<Renderer>(true).Length == 0)
            throw new InvalidOperationException("Missing model or renderers: " + path);
        return model;
    }

    private static void Pieces(AddressableAssetSettings settings, CosmeticCatalog catalog,
        string assetName, string key, string displayName, string folder, string white, string black, string preview)
    {
        string path = Root + "/PieceSkins/" + assetName + ".asset";
        var data = LoadOrCreate<PieceSkinData>(path);
        EnsureStableKey(data.skinId, key, catalog.pieceSkins, path);
        data.skinId = key;
        data.displayName = displayName;
        data.applyTeamColor = false;
        var serialized = new SerializedObject(data);
        foreach (string type in Types)
        {
            string field = char.ToLowerInvariant(type[0]) + type.Substring(1);
            SetReference(serialized.FindProperty(field), Model(settings, folder, white + "_" + type, "PieceSkin_" + assetName));
            SetReference(serialized.FindProperty("black" + type), Model(settings, folder, black + "_" + type, "PieceSkin_" + assetName));
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        data.preview = Preview(settings, folder, preview, "PieceSkin_" + assetName);
        EditorUtility.SetDirty(data);
        Register(settings, data, "PieceSkin_" + assetName, "PieceSkin/" + key);
        Upsert(catalog.pieceSkins, key, displayName, data);
    }

    private static void Board(AddressableAssetSettings settings, CosmeticCatalog catalog,
        string assetName, string key, string displayName, string folder, string modelName)
    {
        string path = Root + "/Boards/" + assetName + ".asset";
        var data = LoadOrCreate<BoardSkinData>(path);
        EnsureStableKey(data.boardId, key, catalog.boards, path);
        data.boardId = key;
        data.displayName = displayName;
        data.boardPrefab = new AssetReferenceGameObject(Model(settings, folder, modelName, "Board_" + assetName));
        data.preview = Preview(settings, folder, modelName + "_Preview.png", "Board_" + assetName);
        data.localPosition = Vector3.zero;
        data.localScale = Vector3.one;
        data.prisonLayoutYaw = DetectPrisonLayoutYaw(RequireModel("Assets/Skins/" + folder + "/" + modelName + ".fbx"));
        data.localRotation = data.prisonLayoutYaw == 0f ? new Vector3(0f, -90f, 0f) : Vector3.zero;
        EditorUtility.SetDirty(data);
        Register(settings, data, "Board_" + assetName, "Board/" + key);
        Upsert(catalog.boards, key, displayName, data);
    }

    public static float DetectPrisonLayoutYaw(GameObject model)
    {
        var pads = model.GetComponentsInChildren<Renderer>(true).FirstOrDefault(renderer =>
            IsPrisonPadMesh(renderer.name));
        if (!pads) throw new InvalidOperationException("Missing prisoner pad mesh in " + model.name);
        // A baked quarter-turn moves the rails from +/-X to +/-Z. Assign White
        // the near (-Z) rail and Black the far (+Z) rail in that layout.
        return pads.bounds.size.z > pads.bounds.size.x ? -90f : 0f;
    }

    public static bool IsPrisonPadMesh(string name) =>
        name.IndexOf("Reserve_Inlays", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("Arena_Floor_Inlays", StringComparison.OrdinalIgnoreCase) >= 0;

    private static void EnsureStableKey(string previous, string key, List<CosmeticCatalogEntry> entries, string path)
    {
        if (!string.Equals(previous, key, StringComparison.OrdinalIgnoreCase) &&
            entries.Any(entry => entry != null && entry.id == previous &&
                entry.data != null && entry.data.AssetGUID == AssetDatabase.AssetPathToGUID(path)))
            throw new InvalidOperationException("Skin is already registered as '" + previous + "'. Update its backend mapping explicitly before changing the key.");
        var existing = entries.FirstOrDefault(entry => entry != null && string.Equals(entry.id, key, StringComparison.OrdinalIgnoreCase));
        if (existing != null && existing.data != null && existing.data.AssetGUID != AssetDatabase.AssetPathToGUID(path))
            throw new InvalidOperationException("Backend key is already assigned to another skin: " + key);
    }

    private static string Model(AddressableAssetSettings settings, string folder, string name, string group)
    {
        var model = RequireModel("Assets/Skins/" + folder + "/" + name + ".fbx");
        Register(settings, model, group, "Models/" + folder + "/" + name);
        return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(model));
    }

    private static AssetReferenceSprite Preview(AddressableAssetSettings settings, string folder, string name, string group)
    {
        string path = "Assets/Skins/" + folder + "/" + name;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new InvalidOperationException("Missing skin preview: " + path);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
        Register(settings, AssetDatabase.LoadAssetAtPath<Sprite>(path), group, "Previews/" + folder);
        return new AssetReferenceSprite(AssetDatabase.AssetPathToGUID(path));
    }

    private static void SetReference(SerializedProperty property, string guid)
    {
        property.FindPropertyRelative("m_AssetGUID").stringValue = guid;
        property.FindPropertyRelative("m_SubObjectName").stringValue = "";
        property.FindPropertyRelative("m_SubObjectType").stringValue = "";
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var data = AssetDatabase.LoadAssetAtPath<T>(path);
        if (data) return data;
        data = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(data, path);
        return data;
    }

    private static void Upsert(List<CosmeticCatalogEntry> entries, string key, string displayName, UnityEngine.Object data)
    {
        var entry = entries.FirstOrDefault(value => value != null && string.Equals(value.id, key, StringComparison.OrdinalIgnoreCase));
        if (entry == null) { entry = new CosmeticCatalogEntry(); entries.Add(entry); }
        entry.id = key;
        entry.displayName = displayName;
        entry.data = new AssetReference(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data)));
    }

    private static void Register(AddressableAssetSettings settings, UnityEngine.Object asset, string groupName, string address)
    {
        if (!asset) throw new InvalidOperationException("Missing asset for " + address);
        var group = settings.FindGroup(groupName);
        if (!group)
        {
            group = settings.CreateGroup(groupName, false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = false;
        }
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)), group).address = address;
        EditorUtility.SetDirty(group);
    }
}
