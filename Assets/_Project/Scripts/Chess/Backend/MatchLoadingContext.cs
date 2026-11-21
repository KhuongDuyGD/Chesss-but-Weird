using System.Linq;

namespace ChessButWeird.Online
{
    // Presentation snapshot only. Never derives an opponent's tier from rating.
    public sealed class MatchLoadingContext
    {
        public MatchState State { get; private set; }
        public string MatchId { get; }
        public string LocalUserId { get; }
        public string Heading { get; }
        private readonly RankOverview localRank;

        public MatchLoadingContext(MatchState state, UserMeResponse user, string heading)
        {
            State = state; MatchId = state.matchId; LocalUserId = user?.userId; Heading = heading;
            localRank = user?.ranks?.For(state.settings.mode);
        }

        public void Update(MatchState state)
        {
            if (state != null && state.matchId == MatchId && state.eventSequence >= State.eventSequence) State = state;
        }
        public Player Player(string color) => State.players?.FirstOrDefault(p => p.color == color);
        public RankOverview Rank(Player player) => player?.rank ?? (player?.userId == LocalUserId ? localRank : null);
    }
}
