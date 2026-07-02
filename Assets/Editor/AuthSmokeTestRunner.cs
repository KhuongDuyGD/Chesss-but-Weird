using System;
using UnityEditor;
using UnityEngine;

public static class AuthSmokeTestRunner
{
    [MenuItem("Tools/Chess But Weird/Run Auth Smoke Tests")]
    public static void Run()
    {
        try
        {
            BackendSessionStore.Clear();
            PlayerAuthService.Logout();

            AssertFalse(PlayerAuthService.IsAuthenticated, "Expected logged-out state after clearing session.");

            PlayerAuthService.BeginGuestSession();
            AssertTrue(PlayerAuthService.IsGuestSession, "Guest session should be active.");
            AssertFalse(PlayerAuthService.CanUseOnlineFeatures, "Guest must not have online access.");
            PlayerAuthService.Logout();
            AssertFalse(PlayerAuthService.IsGuestSession, "Logout should clear the Guest flag.");

            BackendAuthResponseDto auth = new BackendAuthResponseDto
            {
                token = "smoke-token",
                // Zero reproduces a server response that omitted the optional
                // lifetime. The client should apply its safe default instead of
                // expiring an otherwise valid login immediately.
                expiresInSeconds = 0,
                user = new BackendUserProfileDto
                {
                    id = "smoke-user-id",
                    username = "smoke_player",
                    elo = 1337,
                    createdAt = DateTime.UtcNow.ToString("O")
                }
            };

            PlayerAuthService.ApplyAuthResponse(auth);
            AssertTrue(PlayerAuthService.IsAuthenticated, "ApplyAuthResponse should authenticate the player.");
            AssertTrue(PlayerAuthService.CanUseOnlineFeatures, "Guest -> logout -> backend login should unlock multiplayer.");
            AssertFalse(PlayerAuthService.IsGuestSession, "Backend login must replace stale Guest state.");
            AssertEqual("smoke_player", PlayerAuthService.Username, "Username mismatch.");
            AssertEqual("smoke_player", PlayerAuthService.CurrentDisplayName, "Display name mismatch.");
            AssertEqual(1337, PlayerAuthService.CurrentProfile.rating, "Rating mismatch.");

            PlayerAuthService.RecordGameResult(true, false, false);
            PlayerAuthService.RecordGameResult(false, true, false);
            PlayerAuthService.RecordGameResult(false, false, true);
            AssertEqual(1, PlayerAuthService.CurrentProfile.wins, "Wins mismatch.");
            AssertEqual(1, PlayerAuthService.CurrentProfile.losses, "Losses mismatch.");
            AssertEqual(1, PlayerAuthService.CurrentProfile.draws, "Draws mismatch.");
            AssertEqual(3, PlayerAuthService.CurrentProfile.totalGames, "Total games mismatch.");

            PlayerAuthService.Logout();
            AssertFalse(PlayerAuthService.IsAuthenticated, "Logout should clear authentication.");

            PlayerAuthService.TryRestoreSession();
            AssertFalse(PlayerAuthService.IsAuthenticated, "Session should not restore after logout.");

            Debug.Log("[ChessAuthTest] Backend auth smoke tests passed.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[ChessAuthTest] Auth smoke tests failed: {exception.Message}");
        }
        finally
        {
            BackendSessionStore.Clear();
            PlayerAuthService.Logout();
        }
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
