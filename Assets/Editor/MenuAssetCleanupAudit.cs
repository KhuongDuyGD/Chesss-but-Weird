using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Loads the remaining production menu artwork after removing superseded assets.</summary>
[InitializeOnLoad]
public static class MenuAssetCleanupAudit
{
    private const string ActiveKey = "Chess.MenuAssetCleanupAudit.Active";
    private static readonly List<string> Checks = new List<string>();
    private static int completedFrame;
    private static bool completed;
    private static string Folder => Environment.GetEnvironmentVariable("CHESS_MENU_ASSET_CLEANUP_OUTPUT");

    static MenuAssetCleanupAudit()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (!Application.isBatchMode || string.IsNullOrEmpty(Folder))
            throw new InvalidOperationException("Run tools/verify-main-menu.ps1 -Menu Cleanup in the isolated project.");
        Directory.CreateDirectory(Folder);
        SessionState.SetBool(ActiveKey, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false) || state != PlayModeStateChange.EnteredPlayMode) return;
        var fixture = new GameObject("Menu Artwork Cleanup Fixture");
        var artwork = fixture.AddComponent<HandDrawnMenuAssets>();
        try
        {
            Checks.Clear();
            Require(!artwork.HasRequiredSprites, "Menu is not ready before artwork preparation");
            artwork.LoadFromResources();
            Require(artwork.IsPrepared && artwork.HasRequiredSprites,
                "Production artwork preparation succeeds after removing old Main Menu 1/2 sprites");
            Require(artwork.mainMenuLogoOriginal && artwork.mainMenuLogoOriginal.name == "MainMenuLogoOriginal",
                "Both redesigned menus use the single preserved original logo");
            Require(artwork.HasMultiplayerModeSprites, "Multiplayer title and both mode cards remain available");
            Require(artwork.chooseYourSide && artwork.whiteSideButton && artwork.blackSideButton,
                "Side-selection title and both cards remain available");
            Require(artwork.backButton, "Shared submenu Back button remains available");
            Require(artwork.backgroundDecoration1 && artwork.backgroundDecoration2 &&
                artwork.backgroundDecoration3 && artwork.backgroundDecoration4 && artwork.gameVersion,
                "Artwork used by local, multiplayer, side and transition screens remains available");
            Require(artwork.optionFrame, "ARAM option frame still loads from the LAN blank artwork");
            Require(artwork.CreatePreparationSteps().Count == 0, "Repeated preparation does not reload artwork");

            var difficulty = BotDifficultyAssetCatalog.Load();
            Require(difficulty && difficulty.HasRequiredTextures, "Bot-difficulty catalog retains all six texture references");
            Require(AssetDatabase.GetDependencies("Assets/Resources/Chess/BotDifficultyAssets.asset", true).Length >= 8,
                "Unity resolves the protected bot-difficulty asset dependencies");
            foreach (string field in new[] { "gameLogo", "startButton", "settingsButton", "creditsButton", "logoutButton",
                "gameSlogan", "localGameplayButton", "onlinePlayButton", "aramModeButton", "shopButton", "inventoryButton",
                "gachaButton", "playerProfile", "settingsIcon", "doodleFaceDecoration", "paperBackground" })
                Require(typeof(HandDrawnMenuAssets).GetField(field) == null, "Retired sprite slot is removed: " + field);

            completedFrame = Time.frameCount;
            completed = true;
        }
        catch (Exception error) { Finish(error); }
        finally { UnityEngine.Object.Destroy(fixture); }
    }

    private static void Tick()
    {
        if (SessionState.GetBool(ActiveKey, false) && completed && Time.frameCount > completedFrame)
            Finish(null);
    }

    private static void Require(bool condition, string description)
    {
        Checks.Add((condition ? "PASS " : "FAIL ") + description);
        if (!condition) throw new InvalidOperationException(description);
    }

    private static void Finish(Exception error)
    {
        SessionState.SetBool(ActiveKey, false);
        completed = false;
        if (error != null) { Checks.Add("FAIL " + error.Message); Debug.LogException(error); }
        File.WriteAllLines(Path.Combine(Folder, "MenuAssetCleanupRuntimeChecks.txt"), Checks);
        Debug.Log("[MenuAssetCleanupAudit] " + (error == null ? "PASS " : "FAIL ") + Checks.Count + " checks.");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
