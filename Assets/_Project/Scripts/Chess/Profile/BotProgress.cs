using System;

[Serializable]
public sealed class BotProgress
{
    // Stable enum IDs, rather than the bot's position in the roster.
    public StockfishDifficulty difficulty;
    public int practiceWins, practiceLosses, practiceDraws;
    public int challengeWins, challengeLosses, challengeDraws;
    public int hintsUsed, undosUsed;
    public bool ChallengeBeaten => challengeWins > 0;
}

/// <summary>Local bot rewards. Practice shares three rewarded wins per Vietnam calendar day.</summary>
public static class BotProgressPolicy
{
    public const int DailyPracticeRewardLimit = 3;

    public static int ChallengeGold(StockfishDifficulty difficulty)
    {
        int level = StockfishDifficultyProfiles.Get(difficulty).Level;
        return 100 + (int)Math.Round((level - 1) * 2400d / 11d, MidpointRounding.AwayFromZero);
    }

    public static int PracticeGold(StockfishDifficulty difficulty)
    {
        switch (StockfishDifficultyProfiles.Get(difficulty).Level)
        {
            case 10: return 100;
            case 11: return 200;
            case 12: return 500;
            default: return 0;
        }
    }

    public static string PracticeRewardDay(DateTime utcNow) =>
        utcNow.ToUniversalTime().AddHours(7).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
