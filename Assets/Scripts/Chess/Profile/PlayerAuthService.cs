using System;
using UnityEngine;

public static class PlayerAuthService
{
    private const string GuestProfileKey = "guest.profile";

    public static PlayerProfile CurrentProfile { get; private set; }
    public static UserMeResponse CurrentApiUser { get; private set; }
    public static bool IsGuestSession { get; private set; }
    public static bool IsAuthenticated => !IsGuestSession && CurrentProfile != null && (AuthStorage.HasSession() || (BackendSessionStore.HasToken && !BackendSessionStore.IsTokenExpired));
    public static string CurrentDisplayName => CurrentProfile != null ? CurrentProfile.displayName : Environment.MachineName;
    public static string Token => BackendSessionStore.Token;
    public static string UserId => CurrentProfile != null ? CurrentProfile.playerId : string.Empty;
    public static string Username => CurrentProfile != null ? CurrentProfile.username : string.Empty;

    public static bool TryRestoreSession()
    {
        if (AuthStorage.HasSession()) return false; // Restore through /users/me.
        if (!BackendSessionStore.HasToken || BackendSessionStore.IsTokenExpired)
        {
            BackendSessionStore.Clear();
            ClearGuestSessionInternal(true);
            return false;
        }

        BackendUserProfileDto storedUser = BackendSessionStore.GetStoredUser();
        if (storedUser == null)
        {
            BackendSessionStore.Clear();
            ClearGuestSessionInternal(true);
            return false;
        }

        IsGuestSession = false;
        ApplyUser(storedUser);
        return true;
    }

    public static void ApplyAuthResponse(BackendAuthResponseDto auth)
    {
        TryApplyAuthResponse(auth, out _);
    }

    public static bool TryApplyAuthResponse(BackendAuthResponseDto auth, out string error)
    {
        if (auth == null || auth.user == null || string.IsNullOrWhiteSpace(auth.token) ||
            string.IsNullOrWhiteSpace(auth.user.id) || string.IsNullOrWhiteSpace(auth.user.username))
        {
            Logout();
            error = "The server returned an incomplete login session. Please try again.";
            return false;
        }

        ClearGuestSessionInternal(false);
        BackendSessionStore.Clear();
        BackendSessionStore.SaveAuth(auth);
        IsGuestSession = false;
        ApplyUser(auth.user);

        if (!IsAuthenticated)
        {
            Logout();
            error = "The login token is missing or already expired. Please sign in again.";
            return false;
        }

        error = string.Empty;
        return true;
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
        ClearGuestSessionInternal(false);
        CurrentProfile = null;
        CurrentApiUser = null;
        IsGuestSession = false;
        BackendSessionStore.Clear();
        AuthStorage.Clear();
    }

    public static void BeginGuestSession()
    {
        CurrentApiUser = null;
        BackendSessionStore.Clear();
        AuthStorage.Clear();
        IsGuestSession = true;
        CurrentProfile = LoadGuestProfile();
        CurrentProfile.lastLoginAtUtc = DateTime.UtcNow.ToString("O");
        PlayerProfileStore.Reload();
        SaveGuestProfile();
        Debug.Log($"[PlayerAuthService] Guest session started for '{CurrentDisplayName}'.");
    }

    public static void RecordGameResult(
        bool won,
        bool lost,
        bool draw,
        string mode = "Local",
        string opponent = "",
        string detail = "",
        int rewardGold = 0,
        int rewardDiamonds = 0,
        int rewardTickets = 0,
        string matchId = "")
    {
        if (CurrentProfile == null)
            return;

        string result = won ? "Win" : lost ? "Lose" : "Draw";
        PlayerProfileStore.RecordMatch(mode, opponent, result, detail, rewardGold, rewardDiamonds, rewardTickets, matchId);
        if (IsGuestSession)
            SaveGuestProfile();
    }

    public static void ApplyApiUser(UserMeResponse user)
    {
        if (user == null || string.IsNullOrWhiteSpace(user.userId))
            throw new ApiException("The account profile is incomplete.");

        ClearGuestSessionInternal(false);
        BackendSessionStore.Clear();
        IsGuestSession = false;
        CurrentApiUser = user;
        CurrentProfile = new PlayerProfile
        {
            playerId = user.userId,
            username = user.username ?? string.Empty,
            displayName = string.IsNullOrWhiteSpace(user.profile?.displayName) ? user.username : user.profile.displayName,
            createdAtUtc = user.createdAt ?? string.Empty,
            lastLoginAtUtc = DateTime.UtcNow.ToString("O"),
            rating = user.stats?.elo ?? 0,
            wins = user.stats?.wins ?? 0,
            losses = user.stats?.losses ?? 0,
            draws = user.stats?.draws ?? 0,
            totalGames = user.stats?.gamesPlayed ?? 0,
            gold = user.wallet?.golds ?? 0,
            diamonds = user.wallet?.diamonds ?? 0,
            tickets = user.wallet?.tickets ?? 0
        };
        PlayerProfileStore.Reload();
        PlayerProfileStore.ApplyServerSnapshot(user);
    }
    public static bool CanUseOnlineFeatures => IsAuthenticated && !IsGuestSession && BackendSessionStore.HasToken && !BackendSessionStore.IsTokenExpired;

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
        PlayerProfileStore.Reload();
    }

    private static PlayerProfile LoadGuestProfile()
    {
        string json = PlayerPrefs.GetString(GuestProfileKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                PlayerProfile existing = JsonUtility.FromJson<PlayerProfile>(json);
                if (existing != null)
                    return existing;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayerAuthService] Failed to load existing guest profile: {exception.Message}");
            }
        }

        PlayerProfile profile = PlayerProfile.Create("Guest");
        profile.displayName = "Guest";
        return profile;
    }

    private static void SaveGuestProfile()
    {
        if (CurrentProfile == null || !IsGuestSession)
            return;

        string json = JsonUtility.ToJson(CurrentProfile);
        PlayerPrefs.SetString(GuestProfileKey, json);
        PlayerPrefs.Save();
    }

    private static void ClearGuestSessionInternal(bool clearCurrentProfile)
    {
        PlayerPrefs.DeleteKey(GuestProfileKey);
        if (clearCurrentProfile)
            CurrentProfile = null;
        IsGuestSession = false;
    }
}
