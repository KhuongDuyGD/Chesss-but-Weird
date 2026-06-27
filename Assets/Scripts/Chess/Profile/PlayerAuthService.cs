using System;
using UnityEngine;

public static class PlayerAuthService
{
    public static PlayerProfile CurrentProfile { get; private set; }
    public static bool IsAuthenticated => CurrentProfile != null && BackendSessionStore.HasToken && !BackendSessionStore.IsTokenExpired;
    public static string CurrentDisplayName => CurrentProfile != null ? CurrentProfile.displayName : Environment.MachineName;
    public static string Token => BackendSessionStore.Token;
    public static string UserId => CurrentProfile != null ? CurrentProfile.playerId : string.Empty;
    public static string Username => CurrentProfile != null ? CurrentProfile.username : string.Empty;

    public static bool TryRestoreSession()
    {
        if (!BackendSessionStore.HasToken || BackendSessionStore.IsTokenExpired)
        {
            BackendSessionStore.Clear();
            CurrentProfile = null;
            return false;
        }

        BackendUserProfileDto storedUser = BackendSessionStore.GetStoredUser();
        if (storedUser == null)
        {
            BackendSessionStore.Clear();
            CurrentProfile = null;
            return false;
        }

        ApplyUser(storedUser);
        return true;
    }

    public static void ApplyAuthResponse(BackendAuthResponseDto auth)
    {
        if (auth == null || auth.user == null)
            return;

        BackendSessionStore.SaveAuth(auth);
        ApplyUser(auth.user);
    }

    public static void UpdateProfile(BackendUserProfileDto user)
    {
        if (user == null)
            return;

        BackendAuthResponseDto current = new BackendAuthResponseDto
        {
            token = BackendSessionStore.Token,
            expiresInSeconds = Math.Max(0, (long)(BackendSessionStore.TokenExpiryUtc - DateTime.UtcNow).TotalSeconds),
            user = user
        };
        BackendSessionStore.SaveAuth(current);
        ApplyUser(user);
    }

    public static void Logout()
    {
        CurrentProfile = null;
        BackendSessionStore.Clear();
    }

    public static string GetLastUsername()
    {
        BackendUserProfileDto storedUser = BackendSessionStore.GetStoredUser();
        return storedUser != null ? storedUser.username ?? string.Empty : string.Empty;
    }

    public static void RecordGameResult(bool won, bool lost, bool draw)
    {
        if (CurrentProfile == null)
            return;

        if (won)
            CurrentProfile.wins++;
        else if (lost)
            CurrentProfile.losses++;
        else if (draw)
            CurrentProfile.draws++;

        CurrentProfile.totalGames++;
    }

    private static void ApplyUser(BackendUserProfileDto user)
    {
        CurrentProfile = new PlayerProfile
        {
            playerId = user.id ?? string.Empty,
            username = user.username ?? string.Empty,
            displayName = user.username ?? string.Empty,
            createdAtUtc = user.createdAt ?? string.Empty,
            lastLoginAtUtc = DateTime.UtcNow.ToString("O"),
            rating = user.elo
        };
    }
}
