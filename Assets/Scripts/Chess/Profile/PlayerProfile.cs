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
            rating = 1000
        };
    }
}
