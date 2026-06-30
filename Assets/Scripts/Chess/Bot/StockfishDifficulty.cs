using System;
using System.Collections.Generic;

public enum StockfishDifficulty
{
    Beginner,
    Easy,
    Medium,
    Hard,
    Expert
}

public sealed class StockfishDifficultyProfile
{
    public StockfishDifficulty Difficulty { get; }
    public string DisplayName { get; }
    public string EstimatedRating { get; }
    public int SearchDepth { get; }
    public int MultiPv { get; }
    public int MinimumThinkTimeMs { get; }
    public float BestMoveChance { get; }
    public int TargetCentipawnLoss { get; }
    public int MaximumCentipawnLoss { get; }

    public StockfishDifficultyProfile(
        StockfishDifficulty difficulty,
        string displayName,
        string estimatedRating,
        int searchDepth,
        int multiPv,
        int minimumThinkTimeMs,
        float bestMoveChance,
        int targetCentipawnLoss,
        int maximumCentipawnLoss)
    {
        Difficulty = difficulty;
        DisplayName = displayName;
        EstimatedRating = estimatedRating;
        SearchDepth = searchDepth;
        MultiPv = multiPv;
        MinimumThinkTimeMs = minimumThinkTimeMs;
        BestMoveChance = bestMoveChance;
        TargetCentipawnLoss = targetCentipawnLoss;
        MaximumCentipawnLoss = maximumCentipawnLoss;
    }
}

public static class StockfishDifficultyProfiles
{
    private static readonly IReadOnlyDictionary<StockfishDifficulty, StockfishDifficultyProfile> Profiles =
        new Dictionary<StockfishDifficulty, StockfishDifficultyProfile>
        {
            [StockfishDifficulty.Beginner] = new StockfishDifficultyProfile(
                StockfishDifficulty.Beginner, "Beginner", "~400-600", 4, 32, 650, 0.06f, 520, 1100),
            [StockfishDifficulty.Easy] = new StockfishDifficultyProfile(
                StockfishDifficulty.Easy, "Easy", "~700-900", 6, 20, 750, 0.16f, 300, 650),
            [StockfishDifficulty.Medium] = new StockfishDifficultyProfile(
                StockfishDifficulty.Medium, "Medium", "~1000-1200", 8, 12, 850, 0.38f, 150, 360),
            [StockfishDifficulty.Hard] = new StockfishDifficultyProfile(
                StockfishDifficulty.Hard, "Hard", "~1300-1600", 11, 5, 950, 0.72f, 65, 170),
            [StockfishDifficulty.Expert] = new StockfishDifficultyProfile(
                StockfishDifficulty.Expert, "Expert", "~1700+", 14, 3, 1050, 0.94f, 15, 55)
        };

    public static StockfishDifficultyProfile Get(StockfishDifficulty difficulty)
    {
        return Profiles[difficulty];
    }
}
