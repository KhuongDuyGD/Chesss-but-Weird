using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ChessButWeird.Online;

// Owns match content handles. Gameplay keeps its existing controllers and rules.
public sealed class LoadingManager : MonoBehaviour
{
    private AssetLoader coreAssets;
    private AssetLoader matchAssets;
    private LoadingUI ui;
    private Coroutine operation;
    private ChessGame game;
    private Scene ownedScene;
    private GameObject boardVisual;
    private GameObject environmentVisual;
    private Action lastStart;
    private CosmeticSelection lastSelection;
    private bool lastReplaceCurrentMatch;
    private MatchLoadingContext lastMatchup;
    private bool cancelMatchLoad;
    private Action returned;
    public CosmeticCatalog Catalog { get; private set; }
    public SkinManager Skins { get; private set; }
    public BoardSkinManager Board { get; private set; }
    public bool IsBusy { get; private set; }
    public bool HasMatchContent => matchAssets != null;

    public static LoadingManager For(ChessGame owner)
    {
        var manager = owner.GetComponent<LoadingManager>();
        if (!manager) manager = owner.gameObject.AddComponent<LoadingManager>();
        manager.game = owner;
        return manager;
    }

    public IEnumerator Initialize(Action<float> progress = null)
    {
        yield return AssetLoader.Initialize(progress);
        if (Catalog) yield break;
        coreAssets?.Dispose();
        coreAssets = new AssetLoader();
        yield return coreAssets.Load<CosmeticCatalog>(CosmeticCatalog.Address, value => Catalog = value, progress);
        PieceSkinCatalog.UseCatalog(Catalog);
    }

    public void StartMatch(CosmeticSelection selection, Action startGameplay, bool replaceCurrentMatch = false, MatchLoadingContext matchup = null)
    {
        if (IsBusy || game.GameStarted && !replaceCurrentMatch) return;
        if (selection == null || startGameplay == null) throw new ArgumentNullException("Match selection and start callback are required.");
        selection = selection.Copy();
        lastSelection = selection;
        lastStart = startGameplay;
        lastReplaceCurrentMatch = replaceCurrentMatch;
        lastMatchup = matchup;
        cancelMatchLoad = false;
        IsBusy = true;
        game.SetContentLoading(true);
        ui = LoadingUI.Create(transform);
        ui.ShowMatchup(matchup);
        operation = StartCoroutine(Guard(LoadMatch(selection, startGameplay)));
    }

    private IEnumerator LoadMatch(CosmeticSelection selection, Action startGameplay)
    {
        LogMemory("start; white=" + selection.whiteSkinId + "; black=" + selection.blackSkinId);
        yield return null;
        yield return Initialize(p => ui.Report(p * .08f, "Preparing content..."));
        ui.SetCatalog(Catalog);
        if (cancelMatchLoad) { yield return Return(); yield break; }
        yield return ReleaseMatch();
        // Only reclaim at a loading boundary, after old instances and handles are gone.
        yield return Resources.UnloadUnusedAssets();
        LogMemory("previous content released");
        if (!game.Board)
        {
            if (ownedScene.IsValid() && ownedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(ownedScene);
            ownedScene = default;
            string sceneName = Application.CanStreamedLevelBeLoaded(GameBootstrap.MatchScene)
                ? GameBootstrap.MatchScene : "ChessClassic";
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
                throw new InvalidOperationException("Match scene is missing from Build Profiles. Run Chess > Setup Addressable Cosmetics.");
            var sceneLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (sceneLoad == null) throw new InvalidOperationException("Unable to load match scene.");
            while (!sceneLoad.isDone)
            {
                ui.Report(.08f + .22f * Mathf.Clamp01(sceneLoad.progress / .9f), "Preparing the board scene...");
                yield return null;
            }
            ownedScene = SceneManager.GetSceneByName(sceneName);
            SceneManager.SetActiveScene(ownedScene);
            foreach (var root in ownedScene.GetRootGameObjects())
            {
                var board = root.GetComponentInChildren<Chessboard>(true);
                if (board) { game.AttachBoard(board); break; }
            }
            if (!game.Board) throw new InvalidOperationException("Match scene has no Chessboard component.");
        }
        ui.Report(.3f, "Loading selected board...");
        if (cancelMatchLoad) { yield return Return(); yield break; }
        matchAssets = new AssetLoader();
        Board = new BoardSkinManager(matchAssets);
        Skins = new SkinManager(matchAssets);
        yield return Board.Load(Catalog, selection.boardId, selection.environmentId,
            p => ui.Report(.3f + p * .2f, "Loading board and environment..."));
        if (cancelMatchLoad) { yield return Return(); yield break; }
        yield return Skins.Load(Catalog, selection.whiteSkinId, selection.blackSkinId,
            p => ui.Report(.5f + p * .35f, "Loading selected pieces..."));
        LogMemory("selected assets loaded");
        if (!Skins.TryGetPrefab(PieceTeam.White, PieceType.Pawn, out _) ||
            !Skins.TryGetPrefab(PieceTeam.Black, PieceType.Pawn, out _))
            throw new InvalidOperationException("Default piece content is unavailable. Build the local Addressables content before starting a match.");
        if (Board.BoardPrefab)
        {
            boardVisual = new GameObject("Selected board visual");
            var model = Board.InstantiateBoard(boardVisual.transform);
            game.Board.AttachCosmeticBoard(boardVisual.transform, model ? model.transform : null,
                Board.Data ? Board.Data.prisonLayoutYaw : 0f);
        }
        else game.Board.ShowFallbackBoard();
        if (Board.EnvironmentPrefab)
        {
            environmentVisual = new GameObject("Selected environment");
            environmentVisual.transform.position = game.Board.GetBoardCenterWorld();
            Board.InstantiateEnvironment(environmentVisual.transform);
        }
        game.PrepareAuthenticatedBoard();
        LogMemory("pieces created");
        ui.Report(.87f, "Setting up the pieces...");
        yield return null;
        if (cancelMatchLoad) { yield return Return(); yield break; }
        startGameplay();
        // Input and bot updates remain locked even though existing BeginGame methods ran.
        Canvas.ForceUpdateCanvases();
        LogMemory("gameplay initialized");
        // Render the actual board, animated environment and pieces behind the opaque
        // blocker before allowing input, bot turns or online Ready acknowledgements.
        yield return PreparePresentation();
        if (cancelMatchLoad) { yield return Return(); yield break; }
        ui.Report(1, "Ready");
        yield return ui.Finish();
        if (cancelMatchLoad)
        {
            // A cancellation can arrive during the final fade. Return still owns
            // the loaded handles and releases them before reopening the lobby.
            ui = LoadingUI.Create(transform);
            yield return Return(); yield break;
        }
        ui = null;
        IsBusy = false;
        game.CompleteMatchLoading();
        operation = null;
    }

    public bool RestartMatch(Action startGameplay)
    {
        if (IsBusy) return false;
        StartMatch(lastSelection ?? CosmeticSelection.Load(), startGameplay, true);
        return true;
    }

    public bool CancelOnlineMatch(string matchId, Action onReturned)
    {
        if (!IsBusy || lastMatchup?.MatchId != matchId) return false;
        cancelMatchLoad = true; returned = onReturned;
        if (operation == null)
        {
            // An asset failure may already have left the loader on Retry/Back.
            // A terminal server event must also be able to leave that screen.
            if (ui) Destroy(ui.gameObject);
            ui = LoadingUI.Create(transform);
            operation = StartCoroutine(Guard(Return()));
        }
        // Finish the outstanding asset operation before releasing its handles.
        // The checkpoints above prevent gameplay/Ready after cancellation.
        return true;
    }

    private IEnumerator PreparePresentation()
    {
        ui.Report(.9f, "Preparing materials and textures...");
        // Two player frames allow the newly enabled renderers, camera and shader
        // passes to render before the loading canvas fades. No EndOfFrame yield:
        // that stalls when running Unity in batch mode or without a visible Game view.
        yield return null;
        yield return null;
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;

        var materials = new HashSet<Material>();
        var textures = new HashSet<Texture2D>();
        CollectMaterials(game.transform, materials);
        if (game.Board) CollectMaterials(game.Board.transform, materials);
        if (boardVisual) CollectMaterials(boardVisual.transform, materials);
        if (environmentVisual) CollectMaterials(environmentVisual.transform, materials);
        foreach (var material in materials)
            foreach (int property in material.GetTexturePropertyNameIDs())
                if (material.GetTexture(property) is Texture2D texture && texture.streamingMipmaps) textures.Add(texture);

        try
        {
            // Request only the mips needed by the match camera. Keep ordinary
            // streaming in control again as soon as this loading boundary completes.
            foreach (var texture in textures) texture.requestedMipmapLevel = texture.desiredMipmapLevel;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (true)
            {
                int ready = 0;
                foreach (var texture in textures) if (!texture || texture.IsRequestedMipmapLevelLoaded()) ready++;
                ui.Report(.92f + .06f * (textures.Count == 0 ? 1 : (float)ready / textures.Count), "Preparing materials and textures...");
                if (ready == textures.Count) break;
                if (Time.realtimeSinceStartup >= deadline)
                    throw new InvalidOperationException("Match textures did not finish streaming. Retry loading the match.");
                yield return null;
            }
            yield return null;
        }
        finally
        {
            foreach (var texture in textures) if (texture) texture.ClearRequestedMipmapLevel();
        }
        LogMemory("match presentation prepared");
    }

    private static void CollectMaterials(Transform root, HashSet<Material> materials)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials) if (material) materials.Add(material);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private static void LogMemory(string stage)
    {
        long mib = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
        Debug.Log("[LoadingMemory] " + stage + "; Unity allocated=" + mib + " MiB; scene=" + SceneManager.GetActiveScene().name);
    }

    public IEnumerator RefreshCatalog()
    {
        if (IsBusy || HasMatchContent) yield break;
        IsBusy = true;
        try
        {
            coreAssets?.Dispose(); coreAssets = null; Catalog = null;
            yield return AssetLoader.UpdateCatalogs();
            yield return Initialize();
        }
        finally { IsBusy = false; }
    }

    public void ReturnToMenu(Action onReturned = null)
    {
        if (IsBusy) return;
        returned = onReturned;
        IsBusy = true;
        game.SetContentLoading(true);
        ui = LoadingUI.Create(transform);
        operation = StartCoroutine(Guard(Return()));
    }

    private IEnumerator Return()
    {
        yield return ReleaseMatch();
        if (ownedScene.IsValid() && ownedScene.isLoaded)
        {
            game.AttachBoard(null);
            var menu = SceneManager.GetSceneByName(GameBootstrap.MenuScene);
            if (menu.IsValid() && menu.isLoaded) SceneManager.SetActiveScene(menu);
            yield return SceneManager.UnloadSceneAsync(ownedScene);
        }
        ownedScene = default;
        game.FinishReturnToMainMenu();
        yield return ui.Finish();
        ui = null;
        game.SetContentLoading(false);
        IsBusy = false;
        operation = null;
        var callback = returned;
        returned = null;
        callback?.Invoke();
    }

    private IEnumerator ReleaseMatch()
    {
        // Unity Destroy is deferred. Keep bundle handles alive until consumers are gone.
        game.ClearCosmeticPieces();
        if (game.Board) game.Board.DetachCosmeticBoard();
        if (boardVisual) Destroy(boardVisual);
        if (environmentVisual) Destroy(environmentVisual);
        boardVisual = environmentVisual = null;
        yield return null;
        Skins = null; Board = null;
        matchAssets?.Dispose(); matchAssets = null;
    }

    private IEnumerator Guard(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        try
        {
        while (stack.Count > 0)
        {
            object next = null;
            Exception failure = null;
            bool more = false;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (Exception ex) { failure = ex; }
            if (failure != null)
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                Debug.LogError("[Loading] " + failure.Message);
                operation = null;
                ui.Error("Content could not be prepared.",
                    () => { Destroy(ui.gameObject); IsBusy = false; StartMatch(lastSelection, lastStart, lastReplaceCurrentMatch || game.GameStarted, lastMatchup); },
                    () => { Destroy(ui.gameObject); IsBusy = false; ReturnToMenu(); });
                yield break;
            }
            if (!more) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (next is IEnumerator nested) stack.Push(nested);
            else yield return next;
        }
        }
        finally
        {
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        }
    }

    private void OnDestroy()
    {
        if (operation != null) StopCoroutine(operation);
        if (boardVisual) { boardVisual.SetActive(false); Destroy(boardVisual); }
        if (environmentVisual) { environmentVisual.SetActive(false); Destroy(environmentVisual); }
        if (game) game.ClearCosmeticPieces();
        DeferredAssetRelease.Schedule(matchAssets, coreAssets);
    }
}
