public sealed class UserMeResponse
{
    public string userId;
    public string username;
    public string email;
    public UserProfileResponse profile;
    public UserWalletResponse wallet;
    public UserStatsResponse stats;
    public UserEquippedResponse equipped;
    public string createdAt;
}

public sealed class UserProfileResponse
{
    public string displayName;
    public string avatarId;
}

public sealed class UserWalletResponse
{
    public int golds;
    public int diamonds;
    public int tickets;
}

public sealed class UserStatsResponse
{
    public int elo;
    public int wins;
    public int losses;
    public int draws;
    public int gamesPlayed;
}

public sealed class UserEquippedResponse
{
    public string chessSkinId;
    public string boardSkinId;
}
