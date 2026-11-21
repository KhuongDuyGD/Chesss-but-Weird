using System;

[Serializable]
public class PlayerProfile
{
    public string playerId;
    public string username;
    public string displayName;
    public string createdAtUtc;
    public string lastLoginAtUtc;
    public int wins;
    public int losses;
    public int draws;
    public int totalGames;
    public int rating = 1000;
    public int level = 1;
    public int experience;
    public int gold = 12340;
    public int diamonds = 1320;
    public int tickets = 17;
    public int avatarIndex;
    public int loginStreakDays = 1;
    public string achievementTitle = "Beginner";

    public static PlayerProfile Create(string username)
    {
        string now = DateTime.UtcNow.ToString("O");
        return new PlayerProfile
        {
            playerId = Guid.NewGuid().ToString("N"),
            username = username,
            displayName = username,
            createdAtUtc = now,
            lastLoginAtUtc = now,
            rating = 1000,
            level = 1,
            gold = 12340,
            diamonds = 1320,
            tickets = 17,
            achievementTitle = "Beginner"
        };
    }
}
