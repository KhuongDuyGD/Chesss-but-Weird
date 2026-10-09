using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

// Local content checks only. Does not call inventory, gacha, or match APIs.
public static class GachaSkinAssetsAudit
{
    private const string Output = "Logs/gacha-skins";
    private static readonly string[] PieceAssets = { "TazjiCrystal", "TazjiWoodland" };
    private static readonly string[] BoardAssets = { "TazjiForestGarden", "TazjiMarble", "TazjiLowPoly" };
    private static readonly List<string> Checks = new List<string>();
    private static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
    private static double started;

    public static void Inspect()
    {
        Directory.CreateDirectory(Output);
        var lines = new List<string>();
        foreach (string folder in new[] { "Tazji'sCrystalChessSet", "Tazji'sWoodlandChessSet", "Tazji'sForestGardenBoard", "Tazji'sMarbleBoard" })
        foreach (string path in Directory.GetFiles("Assets/Skins/" + folder, "*.fbx"))
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!model) throw new InvalidOperationException("Model did not import: " + path);
            lines.Add(path);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                lines.Add("  " + renderer.name + " bounds=" + renderer.bounds);
                foreach (var material in renderer.sharedMaterials)
                    lines.Add("    " + (material ? material.name + " shader=" + material.shader.name + " color=" + material.color : "MISSING MATERIAL"));
            }
        }
        File.WriteAllLines(Output + "/import.txt", lines);
        Debug.Log("[GachaSkins] Imported all 26 models; details in " + Output + "/import.txt");
    }

    public static void InspectPrisonGeometry()
    {
        Directory.CreateDirectory(Output);
        foreach (string name in BoardAssets)
        {
            var data = AssetDatabase.LoadAssetAtPath<BoardSkinData>("Assets/Game/Data/Boards/" + name + ".asset");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(data.boardPrefab.AssetGUID));
            var filter = model.GetComponentsInChildren<MeshFilter>(true).First(value => GachaSkinAssetsSetup.IsPrisonPadMesh(value.name));
            var mesh = filter.sharedMesh;
            var lines = new List<string>();
            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector3 point = model.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                lines.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "v {0:R} {1:R} {2:R}", point.x, point.y, point.z));
            }
            int[] triangles = mesh.triangles;
            for (int index = 0; index < triangles.Length; index += 3)
                lines.Add("f " + (triangles[index] + 1) + " " + (triangles[index + 1] + 1) + " " + (triangles[index + 2] + 1));
            File.WriteAllLines(Output + "/" + name + "-pads.obj", lines);
        }
    }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        Checks.Clear();
        DefaultLowPolyCosmeticsSetup.Setup();
        GachaSkinAssetsSetup.Setup();
        Inspect();
        AddressableAssetSettings.BuildPlayerContent(out var result);
        if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
        Check(true, "Addressables content build succeeded");
        started = EditorApplication.timeSinceStartup;
        Routines.Push(Verify());
        EditorApplication.update += Tick;
    }

    public static void VerifyInventory()
    {
        Directory.CreateDirectory(Output);
        Checks.Clear();
        var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/Game/Data/CosmeticCatalog.asset");
        int pieceCount = catalog.pieceSkins.Count, boardCount = catalog.boards.Count;
        GachaSkinAssetsSetup.Setup();
        GachaSkinAssetsSetup.Setup();
        Check(catalog.pieceSkins.Count == pieceCount && catalog.boards.Count == boardCount,
            "Repeating setup does not duplicate catalog entries");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var host = new GameObject("Gacha inventory verification");
        var loading = host.AddComponent<LoadingManager>();
        typeof(LoadingManager).GetProperty("Catalog").SetValue(loading, catalog);
        var inventory = host.AddComponent<InventoryMenuController>();
        typeof(InventoryMenuController).GetField("cosmetics", flags).SetValue(inventory, loading);
        var itemType = typeof(InventoryMenuController).GetNestedType("InventoryEntry", BindingFlags.NonPublic);
        var apply = typeof(InventoryMenuController).GetMethod("ApplyLocalVisual", flags);
        var account = typeof(PlayerAuthService).GetProperty("CurrentApiUser");
        var guest = typeof(PlayerAuthService).GetProperty("IsGuestSession");
        object previousAccount = account.GetValue(null), previousGuest = guest.GetValue(null);
        const string saveKey = "chess.cosmetics.selection.v2.__gacha_skin_audit__";
        bool hadPreference = PlayerPrefs.HasKey(saveKey);
        string preference = PlayerPrefs.GetString(saveKey);
        try
        {
            account.SetValue(null, new UserMeResponse { userId = "__gacha_skin_audit__" });
            guest.SetValue(null, false);
            PlayerPrefs.DeleteKey(saveKey);
            foreach (var definition in new[] {
                new[] { "CHESS_PIECE_SKIN", GachaSkinAssetsSetup.WoodlandKey },
                new[] { "BOARD_SKIN", GachaSkinAssetsSetup.MarbleKey },
                new[] { "CHESS_PIECE_SKIN", GachaSkinAssetsSetup.CrystalKey },
                new[] { "BOARD_SKIN", GachaSkinAssetsSetup.ForestKey } })
            {
                var before = CosmeticSelection.Load();
                object item = Activator.CreateInstance(itemType, true);
                itemType.GetField("type").SetValue(item, definition[0]);
                itemType.GetField("unityAssetKey").SetValue(item, definition[1]);
                Check((bool)apply.Invoke(inventory, new[] { item }), definition[1] + " is supported by inventory");
                var after = CosmeticSelection.Load();
                bool pieces = definition[0] == "CHESS_PIECE_SKIN";
                Check(pieces ? after.whiteSkinId == definition[1] && after.blackSkinId == definition[1]
                    : after.boardId == definition[1], definition[1] + " persists selected visual");
                Check(pieces ? after.boardId == before.boardId : after.whiteSkinId == before.whiteSkinId && after.blackSkinId == before.blackSkinId,
                    definition[1] + " preserves the other equipment slot");
            }
            object unknown = Activator.CreateInstance(itemType, true);
            itemType.GetField("type").SetValue(unknown, "BOARD_SKIN");
            itemType.GetField("unityAssetKey").SetValue(unknown, "Boards/NotInstalled");
            Check(!(bool)apply.Invoke(inventory, new[] { unknown }) && CosmeticSelection.Load().boardId == CosmeticSelection.LowPolyId,
                "Uninstalled board art retains the Low Poly fallback");
        }
        finally
        {
            if (hadPreference) PlayerPrefs.SetString(saveKey, preference); else PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
            account.SetValue(null, previousAccount); guest.SetValue(null, previousGuest);
            UnityEngine.Object.DestroyImmediate(host);
        }
        File.WriteAllLines(Output + "/inventory-checks.txt", Checks);
        Debug.Log("[GachaSkins] " + Checks.Count + " inventory checks; PASS (no API calls)");
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 180)
                throw new TimeoutException("Skin loading exceeded 180 seconds.");
            while (Routines.Count > 0)
            {
                var routine = Routines.Peek();
                if (!routine.MoveNext()) { (Routines.Pop() as IDisposable)?.Dispose(); continue; }
                if (routine.Current is IEnumerator nested) Routines.Push(nested);
                else return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static IEnumerator Verify()
    {
        using (var assets = new AssetLoader())
        {
            CosmeticCatalog catalog = null;
            yield return assets.Load<CosmeticCatalog>(CosmeticCatalog.Address, value => catalog = value);
            Check(catalog, "Catalog loads through Addressables");
            foreach (string name in PieceAssets)
            {
                var expected = AssetDatabase.LoadAssetAtPath<PieceSkinData>("Assets/Game/Data/PieceSkins/" + name + ".asset");
                var skins = new SkinManager(assets);
                yield return skins.Load(catalog, expected.skinId, expected.skinId);
                Check(skins.GetData(PieceTeam.White)?.skinId == expected.skinId &&
                    skins.GetData(PieceTeam.Black)?.skinId == expected.skinId, name + " loads both sides without fallback");
                Check(!expected.applyTeamColor, name + " retains original material colors");
                foreach (PieceType type in Enum.GetValues(typeof(PieceType)))
                {
                    bool white = skins.TryGetPrefab(PieceTeam.White, type, out var whitePrefab);
                    bool black = skins.TryGetPrefab(PieceTeam.Black, type, out var blackPrefab);
                    Check(white && black && whitePrefab != blackPrefab, name + " uses separate " + type + " models per side");
                    Check(expected.GetPrefab(type, PieceTeam.White).AssetGUID != expected.GetPrefab(type, PieceTeam.Black).AssetGUID,
                        name + " " + type + " references are distinct");
                    var instance = skins.InstantiatePiece(PieceTeam.Black, type, null);
                    try { Check(instance && instance.GetComponentsInChildren<Renderer>(true).Length > 0, name + " instantiates black " + type); }
                    finally { if (instance) UnityEngine.Object.DestroyImmediate(instance); }
                }
                Sprite preview = null;
                yield return assets.Load<Sprite>(expected.preview, value => preview = value);
                Check(preview, name + " preview loads");
            }
            foreach (string name in BoardAssets)
            {
                var expected = AssetDatabase.LoadAssetAtPath<BoardSkinData>("Assets/Game/Data/Boards/" + name + ".asset");
                var board = new BoardSkinManager(assets);
                yield return board.Load(catalog, expected.boardId, "");
                Check(board.Data && board.Data.boardId == expected.boardId && board.BoardPrefab,
                    name + " loads without fallback");
                var instance = board.InstantiateBoard(null);
                try
                {
                    Check(instance && instance.GetComponentsInChildren<Renderer>(true).Length > 0, name + " instantiates");
                    Check(instance.transform.localScale == Vector3.one && instance.transform.localPosition == Vector3.zero,
                        name + " preserves exported board transform");
                    VerifyPrisonLayout(instance, expected);
                }
                finally { if (instance) UnityEngine.Object.DestroyImmediate(instance); }
                if (name != "TazjiLowPoly")
                {
                    Sprite preview = null;
                    yield return assets.Load<Sprite>(expected.preview, value => preview = value);
                    Check(preview, name + " preview loads");
                }
            }
            var defaults = new SkinManager(assets);
            yield return defaults.Load(catalog, CosmeticSelection.LowPolyId, CosmeticSelection.LowPolyId);
            foreach (PieceType type in Enum.GetValues(typeof(PieceType)))
                Check(defaults.TryGetPrefab(PieceTeam.White, type, out var white) &&
                    defaults.TryGetPrefab(PieceTeam.Black, type, out var black) && white == black,
                    "Low Poly still shares " + type + " model between sides");
        }
    }

    private static void VerifyPrisonLayout(GameObject model, BoardSkinData data)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var host = new GameObject("Prison layout verification");
        var cameraHost = new GameObject("Prison framing verification");
        var wrapper = new GameObject("Cosmetic board wrapper");
        Mesh probeMesh = null;
        GameObject probe = null;
        try
        {
            var board = host.AddComponent<Chessboard>();
            // MonoBehaviour.Awake is not automatically invoked by this EditMode audit.
            typeof(Chessboard).GetMethod("Awake", flags).Invoke(board, null);
            host.transform.SetPositionAndRotation(new Vector3(7f, .2f, -3f), Quaternion.Euler(0f, 23f, 0f));
            host.transform.localScale = new Vector3(1.3f, 1f, .8f);
            Vector3 firstTile = board.GetTileCenterWorld(Vector2Int.zero);
            model.transform.SetParent(wrapper.transform, false);
            board.AttachCosmeticBoard(wrapper.transform, model.transform, data.prisonLayoutYaw);
            Check(Vector3.Distance(firstTile, board.GetTileCenterWorld(Vector2Int.zero)) < .0001f,
                data.boardId + " keeps the logical chess grid unchanged");
            var pads = model.GetComponentsInChildren<MeshFilter>(true).First(filter =>
                GachaSkinAssetsSetup.IsPrisonPadMesh(filter.name));
            // Bundles discard CPU mesh data. Build a temporary readable collision
            // probe from the imported source without changing model import flags.
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(data.boardPrefab.AssetGUID));
            var sourcePads = source.GetComponentsInChildren<MeshFilter>(true).First(filter => filter.name == pads.name);
            probeMesh = new Mesh { vertices = sourcePads.sharedMesh.vertices, triangles = sourcePads.sharedMesh.triangles };
            probeMesh.RecalculateBounds();
            probe = new GameObject("Readable pad collision probe");
            probe.transform.SetParent(pads.transform, false);
            var collider = probe.AddComponent<MeshCollider>();
            collider.sharedMesh = probeMesh;
            Physics.SyncTransforms();
            var positions = new HashSet<Vector3>();
            for (int side = 0; side < 2; side++)
                for (int slot = 0; slot < 6; slot++)
                {
                    Vector3 point = board.GetPrisonSlotWorld(side == 0 ? PieceTeam.White : PieceTeam.Black, slot);
                    positions.Add(point);
                    bool hit = collider.Raycast(new Ray(point + board.transform.up, -board.transform.up), out var pad, 2f);
                    Check(hit && Mathf.Abs(Vector3.Dot(pad.point - point, board.transform.up)) < .025f,
                        data.boardId + " prisoner " + side + "/" + slot + " lands on an actual pad");
                }
            Check(positions.Count == 12, data.boardId + " has twelve distinct prisoner positions");
            Vector3 whiteRail = board.transform.InverseTransformPoint(board.GetPrisonSlotWorld(PieceTeam.White, 0)) -
                board.transform.InverseTransformPoint(board.GetBoardCenterWorld());
            Vector3 blackRail = board.transform.InverseTransformPoint(board.GetPrisonSlotWorld(PieceTeam.Black, 0)) -
                board.transform.InverseTransformPoint(board.GetBoardCenterWorld());
            Check(Mathf.Abs(whiteRail.z + 4.93f) < .001f && Mathf.Abs(blackRail.z - 4.93f) < .001f,
                data.boardId + " uses the near rail for White captures and far rail for Black captures");
            var camera = cameraHost.AddComponent<Camera>();
            var orbit = cameraHost.AddComponent<ChessOrbitCamera>();
            typeof(ChessOrbitCamera).GetField("chessboard", flags).SetValue(orbit, board);
            foreach (float aspect in new[] { 16f / 9f, 9f / 16f })
                foreach (PieceTeam team in new[] { PieceTeam.White, PieceTeam.Black })
                {
                    camera.aspect = aspect;
                    orbit.ConfigureForPlayerSide(team, true);
                    typeof(ChessOrbitCamera).GetMethod("ApplyParallelProjection", flags).Invoke(orbit, null);
                    Check(positions.All(point =>
                    {
                        Vector3 viewport = camera.WorldToViewportPoint(point);
                        return viewport.z > camera.nearClipPlane && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f;
                    }), data.boardId + " camera includes both rails for " + team + " at aspect " + aspect);
                }
            // The caller owns and destroys the loaded board instance.
            model.transform.SetParent(null, true);
        }
        finally
        {
            if (model) model.transform.SetParent(null, true);
            if (probe) UnityEngine.Object.DestroyImmediate(probe);
            if (probeMesh) UnityEngine.Object.DestroyImmediate(probeMesh);
            UnityEngine.Object.DestroyImmediate(cameraHost);
            UnityEngine.Object.DestroyImmediate(host);
            if (wrapper) UnityEngine.Object.DestroyImmediate(wrapper);
        }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        Checks.Add("PASS " + description);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        while (Routines.Count > 0) (Routines.Pop() as IDisposable)?.Dispose();
        if (error != null) Checks.Add("FAIL " + error);
        File.WriteAllLines(Output + "/checks.txt", Checks);
        Debug.Log("[GachaSkins] " + Checks.Count + " checks; " + (error == null ? "PASS" : "FAIL"));
        if (error != null) Debug.LogException(error);
        if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
    }
}
