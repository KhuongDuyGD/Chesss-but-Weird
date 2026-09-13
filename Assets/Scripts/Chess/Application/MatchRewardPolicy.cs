using System.Text;

/// <summary>Reward calculation only; persistence remains in the existing profile service.</summary>
internal static class MatchRewardPolicy
{
    public static MatchReward CalculateBotReward(StockfishDifficulty difficulty, bool won, bool lost, bool draw)
    {
        MatchReward winReward;
        MatchReward drawReward;
        MatchReward lossReward;

        switch (difficulty)
        {
            case StockfishDifficulty.Beginner:
                winReward = new MatchReward(120, 3, 0);
                drawReward = new MatchReward(55, 1, 0);
                lossReward = new MatchReward(25, 0, 0);
                break;
            case StockfishDifficulty.Easy:
                winReward = new MatchReward(180, 5, 0);
                drawReward = new MatchReward(80, 2, 0);
                lossReward = new MatchReward(35, 1, 0);
                break;
            case StockfishDifficulty.Medium:
                winReward = new MatchReward(260, 8, 1);
                drawReward = new MatchReward(115, 3, 0);
                lossReward = new MatchReward(50, 1, 0);
                break;
            case StockfishDifficulty.Hard:
                winReward = new MatchReward(380, 12, 1);
                drawReward = new MatchReward(165, 5, 0);
                lossReward = new MatchReward(70, 2, 0);
                break;
            case StockfishDifficulty.Expert:
                winReward = new MatchReward(540, 18, 2);
                drawReward = new MatchReward(230, 7, 1);
                lossReward = new MatchReward(95, 3, 0);
                break;
            default:
                winReward = new MatchReward(260, 8, 1);
                drawReward = new MatchReward(115, 3, 0);
                lossReward = new MatchReward(50, 1, 0);
                break;
        }

        if (won)
            return winReward;
        if (draw)
            return drawReward;
        return lost ? lossReward : MatchReward.None;
    }

    public static MatchReward CalculateNetworkReward(bool won, bool lost, bool draw)
    {
        if (won)
            return new MatchReward(360, 12, 1);
        if (draw)
            return new MatchReward(180, 6, 0);
        return lost ? new MatchReward(85, 3, 0) : MatchReward.None;
    }

    public static MatchReward CalculateLocalReward(bool won, bool lost, bool draw)
    {
        if (won)
            return new MatchReward(70, 1, 0);
        if (draw)
            return new MatchReward(40, 1, 0);
        return lost ? new MatchReward(25, 0, 0) : MatchReward.None;
    }

    public static string AppendRewardDetail(string detail, MatchReward reward)
    {
        string rewardText = reward.ToHistoryText();
        if (string.IsNullOrWhiteSpace(rewardText))
            return detail ?? string.Empty;
        if (string.IsNullOrWhiteSpace(detail))
            return rewardText;
        return $"{detail} | {rewardText}";
    }

    public readonly struct MatchReward
    {
        public static readonly MatchReward None = new MatchReward(0, 0, 0);

        public readonly int gold;
        public readonly int diamonds;
        public readonly int tickets;

        public MatchReward(int rewardGold, int rewardDiamonds, int rewardTickets)
        {
            gold = System.Math.Max(0, rewardGold);
            diamonds = System.Math.Max(0, rewardDiamonds);
            tickets = System.Math.Max(0, rewardTickets);
        }

        public string ToHistoryText()
        {
            StringBuilder builder = new StringBuilder();
            AppendPart(builder, gold, "G");
            AppendPart(builder, diamonds, "D");
            AppendPart(builder, tickets, "T");
            return builder.ToString();
        }

        private static void AppendPart(StringBuilder builder, int amount, string suffix)
        {
            if (amount <= 0)
                return;
            if (builder.Length > 0)
                builder.Append(' ');
            builder.Append('+');
            builder.Append(amount);
            builder.Append(suffix);
        }
    }
}
