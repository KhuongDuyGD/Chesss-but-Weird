using System.Text;

/// <summary>Reward calculation only; persistence remains in the existing profile service.</summary>
internal static class MatchRewardPolicy
{
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
