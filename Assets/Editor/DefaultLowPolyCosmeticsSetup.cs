using System;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;

public static class DefaultLowPolyCosmeticsSetup
{
    public const string CatalogId = CosmeticSelection.LowPolyId;
    private const string Root = "Assets/Game/Data";
    private const string SetFolder = "Assets/Skins/Tazji'sLowPoly/Chess_Set";
    private const string ArenaPath = "Assets/Skins/Tazji'sLowPoly/Chess_Arena/Chess_Arena.fbx";

    [MenuItem("Chess/Setup Tazji Low Poly Default Cosmetics")]
    public static void Setup()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        CosmeticCatalog catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(Root + "/CosmeticCatalog.asset");
        if (!catalog) throw new InvalidOperationException("CosmeticCatalog.asset is missing.");

        PieceSkinData pieces = LoadOrCreate<PieceSkinData>(Root + "/PieceSkins/TazjiLowPoly.asset");
        pieces.skinId = CatalogId;
        pieces.displayName = "Tazji's Low Poly Chess Set";
        pieces.applyTeamColor = true;
        foreach (PieceType type in Enum.GetValues(typeof(PieceType)))
        {
            string modelPath = SetFolder + "/Chess_" + type + ".fbx";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (!model) throw new InvalidOperationException("Missing chess piece: " + modelPath);
            var reference = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(modelPath));
            AssignPiece(pieces, type, reference);
            Register(settings, model, "PieceSkin_TazjiLowPoly", "Pieces/TazjiLowPoly/" + type);
        }
        EditorUtility.SetDirty(pieces);
        Register(settings, pieces, "PieceSkin_TazjiLowPoly", "PieceSkin/" + CatalogId);
        UpsertEntry(catalog.pieceSkins, CatalogId, pieces.displayName, pieces);

        BoardSkinData board = LoadOrCreate<BoardSkinData>(Root + "/Boards/TazjiLowPoly.asset");
        GameObject arena = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPath);
        if (!arena) throw new InvalidOperationException("Missing arena: " + ArenaPath);
        board.boardId = CatalogId;
        board.displayName = "Tazji's Low Poly Arena";
        board.boardPrefab = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(ArenaPath));
        board.localPosition = Vector3.zero;
        board.localRotation = Vector3.zero;
        board.localScale = Vector3.one;
        EditorUtility.SetDirty(board);
        Register(settings, arena, "Board_TazjiLowPoly", "Boards/TazjiLowPoly");
        Register(settings, board, "Board_TazjiLowPoly", "Board/" + CatalogId);
        UpsertEntry(catalog.boards, CatalogId, board.displayName, board);

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("[Cosmetics] Tazji Low Poly board and six chess pieces registered.");
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void AssignPiece(PieceSkinData data, PieceType type, AssetReferenceGameObject reference)
    {
        switch (type)
        {
            case PieceType.Pawn: data.pawn = reference; break;
            case PieceType.Rook: data.rook = reference; break;
            case PieceType.Knight: data.knight = reference; break;
            case PieceType.Bishop: data.bishop = reference; break;
            case PieceType.Queen: data.queen = reference; break;
            case PieceType.King: data.king = reference; break;
            default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private static void UpsertEntry(System.Collections.Generic.List<CosmeticCatalogEntry> entries,
        string id, string displayName, UnityEngine.Object data)
    {
        CosmeticCatalogEntry entry = entries.FirstOrDefault(item => item != null && item.id == id);
        if (entry == null)
        {
            entry = new CosmeticCatalogEntry();
            entries.Add(entry);
        }
        entry.id = id;
        entry.displayName = displayName;
        entry.data = new AssetReference(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data)));
    }

    private static void Register(AddressableAssetSettings settings, UnityEngine.Object asset,
        string groupName, string address)
    {
        AddressableAssetGroup group = settings.FindGroup(groupName);
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

        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        if (string.IsNullOrWhiteSpace(guid)) throw new InvalidOperationException("Missing GUID for " + asset);
        settings.CreateOrMoveEntry(guid, group).address = address;
    }
}

