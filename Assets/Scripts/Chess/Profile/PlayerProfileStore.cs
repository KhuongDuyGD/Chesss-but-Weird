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
    public List<string> recordedMatchIds = new List<string>();
    public List<BotProgress> botProgress = new List<BotProgress>();
    public List<string> recordedBotMatchIds = new List<string>();
    public string practiceRewardDay = string.Empty;
    public int practiceRewardsClaimed;
    // Unspent, locally earned bot gold is preserved when a server wallet refreshes.
    public int localBotGold;

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
    public string matchId;
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

    public static void ApplyServerSnapshot(UserMeResponse user)
    {
        if (user == null) return;
        EnsureLoaded();
        data.displayName = string.IsNullOrWhiteSpace(user.profile?.displayName) ? user.username : user.profile.displayName;
        if (user.wallet != null)
        {
            data.gold = user.wallet.golds + data.localBotGold;
            data.diamonds = user.wallet.diamonds;
            data.tickets = user.wallet.tickets;
        }
        if (user.stats != null)
        {
            data.wins = user.stats.wins;
            data.losses = user.stats.losses;
            data.draws = user.stats.draws;
            data.totalGames = user.stats.gamesPlayed;
        }
        Save();
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
        data.localBotGold = Mathf.Max(0, data.localBotGold - amount);
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
        data.gold = Mathf.Max(0, gold) + data.localBotGold;
        data.diamonds = Mathf.Max(0, diamonds);
        data.tickets = Mathf.Max(0, tickets);
        Save();
    }

    public static void RecordMatch(
        string mode,
        string opponent,
        string result,
        string detail,
        int rewardGold = 0,
        int rewardDiamonds = 0,
        int rewardTickets = 0,
        string matchId = "",
        bool countStats = true)
    {
        EnsureLoaded();
        if (data.matchHistory == null)
            data.matchHistory = new List<PlayerMatchHistoryEntry>();

        string normalizedMatchId = Limit(matchId, 96);
        if (!string.IsNullOrWhiteSpace(normalizedMatchId))
        {
            if (data.recordedMatchIds != null && data.recordedMatchIds.Contains(normalizedMatchId))
                return;

            if (data.recordedMatchIds == null)
                data.recordedMatchIds = new List<string>();

            data.recordedMatchIds.Insert(0, normalizedMatchId);
            while (data.recordedMatchIds.Count > 128)
                data.recordedMatchIds.RemoveAt(data.recordedMatchIds.Count - 1);
        }

        string normalizedResult = string.IsNullOrWhiteSpace(result) ? "Draw" : result.Trim();
        if (countStats)
        {
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
        }

        data.gold = Mathf.Max(0, data.gold + Mathf.Max(0, rewardGold));
        data.diamonds = Mathf.Max(0, data.diamonds + Mathf.Max(0, rewardDiamonds));
        data.tickets = Mathf.Max(0, data.tickets + Mathf.Max(0, rewardTickets));

        data.matchHistory.Insert(0, new PlayerMatchHistoryEntry
        {
            matchId = normalizedMatchId,
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

    public static BotProgress GetBotProgress(StockfishDifficulty difficulty)
    {
        EnsureLoaded();
        difficulty = StockfishDifficultyProfiles.Get(difficulty).Difficulty;
        foreach (var progress in data.botProgress)
            if (progress.difficulty == difficulty) return progress;
        var created = new BotProgress { difficulty = difficulty };
        data.botProgress.Add(created);
        return created;
    }

    public static void RecordBotPracticeAssist(StockfishDifficulty difficulty, bool hint)
    {
        var progress = GetBotProgress(difficulty);
        if (hint) progress.hintsUsed++;
        else progress.undosUsed++;
        Save();
    }

    public static int PracticeRewardsRemaining(DateTime utcNow)
    {
        EnsureLoaded();
        string day = BotProgressPolicy.PracticeRewardDay(utcNow);
        // A backwards clock change must not renew an already used quota.
        return string.CompareOrdinal(day, data.practiceRewardDay) > 0
            ? BotProgressPolicy.DailyPracticeRewardLimit
            : Mathf.Max(0, BotProgressPolicy.DailyPracticeRewardLimit - data.practiceRewardsClaimed);
    }

    public static bool RecordBotMatch(StockfishDifficulty difficulty, BotGameMode mode,
        string result, string detail, string matchId, DateTime utcNow)
    {
        EnsureLoaded();
        string id = Limit(matchId, 96);
        if (string.IsNullOrWhiteSpace(id) || data.recordedBotMatchIds.Contains(id) || data.recordedMatchIds.Contains(id))
            return false;
        var profile = StockfishDifficultyProfiles.Get(difficulty);
        var progress = GetBotProgress(profile.Difficulty);
        bool won = string.Equals(result, "Win", StringComparison.OrdinalIgnoreCase);
        bool lost = string.Equals(result, "Lose", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(result, "Loss", StringComparison.OrdinalIgnoreCase);
        bool practice = mode == BotGameMode.Practice;
        int gold = 0;
        if (practice)
        {
            if (won && BotProgressPolicy.PracticeGold(profile.Difficulty) > 0 && PracticeRewardsRemaining(utcNow) > 0)
            {
                string day = BotProgressPolicy.PracticeRewardDay(utcNow);
                if (string.CompareOrdinal(day, data.practiceRewardDay) > 0)
                { data.practiceRewardDay = day; data.practiceRewardsClaimed = 0; }
                gold = BotProgressPolicy.PracticeGold(profile.Difficulty);
                data.practiceRewardsClaimed++;
            }
            if (won) progress.practiceWins++;
            else if (lost) progress.practiceLosses++;
            else progress.practiceDraws++;
        }
        else
        {
            if (won)
            {
                gold = BotProgressPolicy.ChallengeGold(profile.Difficulty) * (progress.ChallengeBeaten ? 1 : 3);
                progress.challengeWins++;
            }
            else if (lost) progress.challengeLosses++;
            else progress.challengeDraws++;
        }
        // Persist progress, wallet, reward quota and match identity together in RecordMatch's single save.
        data.recordedBotMatchIds.Add(id);
        data.localBotGold += gold;
        RecordMatch(practice ? "Bot Practice" : "Bot Challenge", profile.BotName + " / " + profile.DisplayName,
            won ? "Win" : lost ? "Lose" : "Draw",
            MatchRewardPolicy.AppendRewardDetail(detail, new MatchRewardPolicy.MatchReward(gold, 0, 0)),
            rewardGold: gold, matchId: id, countStats: !practice);
        return true;
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
        if (data.recordedMatchIds == null)
            data.recordedMatchIds = new List<string>();
        if (data.botProgress == null) data.botProgress = new List<BotProgress>();
        if (data.recordedBotMatchIds == null) data.recordedBotMatchIds = new List<string>();
        if (data.practiceRewardDay == null) data.practiceRewardDay = string.Empty;
        data.practiceRewardsClaimed = Mathf.Clamp(data.practiceRewardsClaimed, 0, BotProgressPolicy.DailyPracticeRewardLimit);
        data.localBotGold = Mathf.Clamp(data.localBotGold, 0, data.gold);
        data.botProgress.RemoveAll(progress => progress == null);
        foreach (var progress in data.botProgress)
        {
            progress.practiceWins = Mathf.Max(0, progress.practiceWins);
            progress.practiceLosses = Mathf.Max(0, progress.practiceLosses);
            progress.practiceDraws = Mathf.Max(0, progress.practiceDraws);
            progress.challengeWins = Mathf.Max(0, progress.challengeWins);
            progress.challengeLosses = Mathf.Max(0, progress.challengeLosses);
            progress.challengeDraws = Mathf.Max(0, progress.challengeDraws);
            progress.hintsUsed = Mathf.Max(0, progress.hintsUsed);
            progress.undosUsed = Mathf.Max(0, progress.undosUsed);
        }
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
