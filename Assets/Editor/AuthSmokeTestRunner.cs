using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AuthSmokeTestRunner
{
    [MenuItem("Tools/Chess But Weird/Run Auth Smoke Tests")]
    public static void Run()
    {
        string storePath = ProfileStore.StorePath;
        string backupPath = storePath + ".smoke_backup";
        bool hadProfileStore = File.Exists(storePath);

        try
        {
            PlayerAuthService.Logout();
            ResetProfileStoreCache();

            if (File.Exists(backupPath))
                File.Delete(backupPath);

            if (hadProfileStore)
                File.Move(storePath, backupPath);

            ResetProfileStoreCache();

            AssertTrue(PlayerAuthService.Register("smoke_player", "secret1", out string registerMessage), registerMessage);
            AssertTrue(PlayerAuthService.IsAuthenticated, "Register should authenticate the new player.");
            AssertEqual("smoke_player", PlayerAuthService.CurrentProfile.username, "Registered username mismatch.");
            AssertEqual("smoke_player", PlayerAuthService.CurrentDisplayName, "Display name mismatch.");

            PlayerAuthService.Logout();
            AssertFalse(PlayerAuthService.Login("smoke_player", "wrongpw", out string badLoginMessage), "Bad password should fail.");
            AssertEqual("Incorrect password.", badLoginMessage, "Bad password message mismatch.");

            AssertTrue(PlayerAuthService.Login("smoke_player", "secret1", out string loginMessage), loginMessage);
            PlayerAuthService.RecordGameResult(true, false, false);
            PlayerAuthService.RecordGameResult(false, true, false);
            PlayerAuthService.RecordGameResult(false, false, true);
            AssertEqual(1, PlayerAuthService.CurrentProfile.wins, "Wins mismatch.");
            AssertEqual(1, PlayerAuthService.CurrentProfile.losses, "Losses mismatch.");
            AssertEqual(1, PlayerAuthService.CurrentProfile.draws, "Draws mismatch.");
            AssertEqual(3, PlayerAuthService.CurrentProfile.totalGames, "Total games mismatch.");

            PlayerAuthService.Logout();
            AssertFalse(PlayerAuthService.Register("smoke_player", "secret2", out string duplicateMessage), "Duplicate username should fail.");
            AssertEqual("Username already exists.", duplicateMessage, "Duplicate username message mismatch.");

            Debug.Log("[ChessAuthTest] Auth smoke tests passed.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[ChessAuthTest] Auth smoke tests failed: {exception.Message}");
        }
        finally
        {
            PlayerAuthService.Logout();
            ResetProfileStoreCache();

            if (File.Exists(storePath))
                File.Delete(storePath);

            if (hadProfileStore && File.Exists(backupPath))
                File.Move(backupPath, storePath);

            ResetProfileStoreCache();
        }
    }

    private static void ResetProfileStoreCache()
    {
        FieldInfo dataField = typeof(ProfileStore).GetField("data", BindingFlags.NonPublic | BindingFlags.Static);
        dataField?.SetValue(null, null);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        if (value)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException($"{message} Expected '{expected}', got '{actual}'.");
    }
}
