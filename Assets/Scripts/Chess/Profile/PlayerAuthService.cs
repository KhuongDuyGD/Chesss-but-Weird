using System;
using UnityEngine;

public static class PlayerAuthService
{
    private const string GuestProfileKey = "guest.profile";

    public static PlayerProfile CurrentProfile { get; private set; }
    public static UserMeResponse CurrentApiUser { get; private set; }
    public static bool IsGuestSession { get; private set; }
    public static bool IsAuthenticated => !IsGuestSession && CurrentProfile != null && AuthStorage.HasSession();
    public static string CurrentDisplayName => CurrentProfile != null ? CurrentProfile.displayName : Environment.MachineName;
    public static string UserId => CurrentProfile != null ? CurrentProfile.playerId : string.Empty;
    public static string Username => CurrentProfile != null ? CurrentProfile.username : string.Empty;

    public static void Logout()
    {
        ClearGuestSessionInternal(false);
        CurrentProfile = null;
        CurrentApiUser = null;
        IsGuestSession = false;
        AuthStorage.Clear();
    }

    public static void BeginGuestSession()
    {
        CurrentApiUser = null;
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
    public static void RecordHotseatResult(string mode, string result, string detail, string matchId)
    {
        if (CurrentProfile == null) return;
        PlayerProfileStore.RecordMatch(mode, "Local players", result, detail, matchId: matchId, countStats: false);
        if (IsGuestSession) SaveGuestProfile();
    }
    public static bool CanUseOnlineFeatures => IsAuthenticated && !IsGuestSession && AuthStorage.HasSession();
    public static void RecordPracticeResult(string opponent, string result, string detail, string matchId)
    {
        if (CurrentProfile == null) return;
        PlayerProfileStore.RecordMatch("Bot Practice", opponent, result, detail, matchId: matchId, countStats: false);
        if (IsGuestSession) SaveGuestProfile();
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
