using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PlayerProfileSaveData
{
    public string playerId;
    public string username;
    public string displayName;
    public int avatarIndex;
    public int level = 1;
    public int experience;
    public int gold = 12340;
    public int diamonds = 1320;
    public int tickets = 17;
    public int loginStreakDays = 1;
    public string lastLoginDate = string.Empty;
    public string achievementTitle = "Beginner";
    public int wins;
    public int losses;
    public int draws;
    public int totalGames;
    public List<PlayerMatchHistoryEntry> matchHistory = new List<PlayerMatchHistoryEntry>();

    public static PlayerProfileSaveData Create(PlayerProfile profile)
    {
        string username = profile != null && !string.IsNullOrWhiteSpace(profile.username) ? profile.username : "Guest";
        string playerId = profile != null && !string.IsNullOrWhiteSpace(profile.playerId) ? profile.playerId : username;
        return new PlayerProfileSaveData
        {
            playerId = playerId,
            username = username,
            displayName = profile != null && !string.IsNullOrWhiteSpace(profile.displayName) ? profile.displayName : username,
            level = profile != null && profile.level > 0 ? profile.level : 1,
            experience = profile != null ? Mathf.Max(0, profile.experience) : 0,
            gold = profile != null && profile.gold > 0 ? profile.gold : 12340,
            diamonds = profile != null && profile.diamonds > 0 ? profile.diamonds : 1320,
            tickets = profile != null && profile.tickets > 0 ? profile.tickets : 17,
            achievementTitle = "Beginner",
            loginStreakDays = 1,
            lastLoginDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
        };
    }
}

[Serializable]
public sealed class PlayerMatchHistoryEntry
{
    public string playedAtUtc;
    public string opponent;
    public string mode;
    public string result;
    public string detail;
}

public static class PlayerProfileStore
{
    private const string SaveKeyPrefix = "chess_but_weird_player_profile_";
    private const int MaxHistoryEntries = 20;
    private static PlayerProfileSaveData data;
    private static string loadedKey;

    public static PlayerProfileSaveData Data
    {
        get
        {
            EnsureLoaded();
            return data;
        }
    }

    public static string CurrentSaveKey
    {
        get
        {
            string id = PlayerAuthService.UserId;
            if (string.IsNullOrWhiteSpace(id))
                id = PlayerAuthService.Username;
            if (string.IsNullOrWhiteSpace(id))
                id = PlayerAuthService.CurrentDisplayName;
            if (string.IsNullOrWhiteSpace(id))
                id = "guest";

            return SaveKeyPrefix + SanitizeKey(id);
        }
    }

    public static void EnsureLoaded()
    {
        string key = CurrentSaveKey;
        if (data != null && string.Equals(loadedKey, key, StringComparison.Ordinal))
            return;

        loadedKey = key;
        string json = PlayerPrefs.GetString(key, string.Empty);
        data = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                data = JsonUtility.FromJson<PlayerProfileSaveData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayerProfileStore] Failed to load profile save: {exception.Message}");
            }
        }

        if (data == null)
            data = PlayerProfileSaveData.Create(PlayerAuthService.CurrentProfile);

        Normalize();
        UpdateLoginStreak();
        ApplyToSessionProfile();
        Save();
    }

    public static void Reload()
    {
        data = null;
        loadedKey = null;
        EnsureLoaded();
    }

    public static void Save()
    {
        if (data == null)
            return;

        Normalize();
        PlayerPrefs.SetString(CurrentSaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        ApplyToSessionProfile();
    }

    public static void SetIdentity(string displayName, int avatarIndex)
    {
        EnsureLoaded();
        data.displayName = CleanDisplayName(displayName, data.username);
        data.avatarIndex = Mathf.Clamp(avatarIndex, 0, 5);
        Save();
    }

    public static bool TrySpendTickets(int amount)
    {
        EnsureLoaded();
        amount = Mathf.Max(0, amount);
        if (data.tickets < amount)
            return false;
        data.tickets -= amount;
        Save();
        return true;
    }

    public static bool TrySpendDiamonds(int amount)
    {
        EnsureLoaded();
        amount = Mathf.Max(0, amount);
        if (data.diamonds < amount)
            return false;
        data.diamonds -= amount;
        Save();
        return true;
    }

    public static bool TrySpendGold(int amount)
    {
        EnsureLoaded();
        amount = Mathf.Max(0, amount);
        if (data.gold < amount)
            return false;
        data.gold -= amount;
        Save();
        return true;
    }

    public static void AddCurrency(int gold, int diamonds, int tickets)
    {
        EnsureLoaded();
        data.gold = Mathf.Max(0, data.gold + gold);
        data.diamonds = Mathf.Max(0, data.diamonds + diamonds);
        data.tickets = Mathf.Max(0, data.tickets + tickets);
        Save();
    }

    public static void SetCurrency(int gold, int diamonds, int tickets)
    {
        EnsureLoaded();
        data.gold = Mathf.Max(0, gold);
        data.diamonds = Mathf.Max(0, diamonds);
        data.tickets = Mathf.Max(0, tickets);
        Save();
    }

    public static void RecordMatch(string mode, string opponent, string result, string detail)
    {
        EnsureLoaded();
        if (data.matchHistory == null)
            data.matchHistory = new List<PlayerMatchHistoryEntry>();

        string normalizedResult = string.IsNullOrWhiteSpace(result) ? "Draw" : result.Trim();
        if (string.Equals(normalizedResult, "Win", StringComparison.OrdinalIgnoreCase))
            data.wins++;
        else if (string.Equals(normalizedResult, "Lose", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(normalizedResult, "Loss", StringComparison.OrdinalIgnoreCase))
            data.losses++;
        else
            data.draws++;

        data.totalGames++;
        data.experience += 35 + (string.Equals(normalizedResult, "Win", StringComparison.OrdinalIgnoreCase) ? 25 : 0);
        RecalculateLevel();

        data.matchHistory.Insert(0, new PlayerMatchHistoryEntry
        {
            playedAtUtc = DateTime.UtcNow.ToString("O"),
            opponent = Limit(opponent, 28),
            mode = Limit(mode, 24),
            result = Limit(normalizedResult, 12),
            detail = Limit(detail, 42)
        });

        while (data.matchHistory.Count > MaxHistoryEntries)
            data.matchHistory.RemoveAt(data.matchHistory.Count - 1);

        Save();
    }

    private static void Normalize()
    {
        if (data == null)
            return;

        PlayerProfile profile = PlayerAuthService.CurrentProfile;
        if (string.IsNullOrWhiteSpace(data.playerId))
            data.playerId = profile != null && !string.IsNullOrWhiteSpace(profile.playerId) ? profile.playerId : "guest";
        if (string.IsNullOrWhiteSpace(data.username))
            data.username = profile != null && !string.IsNullOrWhiteSpace(profile.username) ? profile.username : "Guest";
        data.displayName = CleanDisplayName(data.displayName, data.username);
        data.avatarIndex = Mathf.Clamp(data.avatarIndex, 0, 5);
        data.level = Mathf.Max(1, data.level);
        data.experience = Mathf.Max(0, data.experience);
        data.gold = Mathf.Max(0, data.gold);
        data.diamonds = Mathf.Max(0, data.diamonds);
        data.tickets = Mathf.Max(0, data.tickets);
        data.loginStreakDays = Mathf.Max(1, data.loginStreakDays);
        if (string.IsNullOrWhiteSpace(data.achievementTitle))
            data.achievementTitle = "Beginner";
        if (data.matchHistory == null)
            data.matchHistory = new List<PlayerMatchHistoryEntry>();
    }

    private static void UpdateLoginStreak()
    {
        DateTime today = DateTime.UtcNow.Date;
        if (DateTime.TryParse(data.lastLoginDate, out DateTime lastLogin))
        {
            int days = Mathf.FloorToInt((float)(today - lastLogin.Date).TotalDays);
            if (days == 1)
                data.loginStreakDays++;
            else if (days > 1)
                data.loginStreakDays = 1;
        }

        data.lastLoginDate = today.ToString("yyyy-MM-dd");
    }

    private static void RecalculateLevel()
    {
        data.level = Mathf.Max(1, data.experience / 100 + 1);
    }

    private static void ApplyToSessionProfile()
    {
        PlayerProfile profile = PlayerAuthService.CurrentProfile;
        if (profile == null || data == null)
            return;

        profile.displayName = data.displayName;
        profile.level = data.level;
        profile.experience = data.experience;
        profile.gold = data.gold;
        profile.diamonds = data.diamonds;
        profile.tickets = data.tickets;
        profile.avatarIndex = data.avatarIndex;
        profile.loginStreakDays = data.loginStreakDays;
        profile.achievementTitle = data.achievementTitle;
        profile.wins = data.wins;
        profile.losses = data.losses;
        profile.draws = data.draws;
        profile.totalGames = data.totalGames;
    }

    public static string Limit(string value, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim();
        if (trimmed.Length <= maxCharacters)
            return trimmed;
        return trimmed.Substring(0, Mathf.Max(0, maxCharacters - 3)) + "...";
    }

    private static string CleanDisplayName(string displayName, string fallback)
    {
        string cleaned = string.IsNullOrWhiteSpace(displayName) ? fallback : displayName.Trim();
        cleaned = cleaned.Replace('\n', ' ').Replace('\r', ' ');
        return Limit(cleaned, 18);
    }

    private static string SanitizeKey(string raw)
    {
        char[] chars = raw.ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        return new string(chars);
    }
}
