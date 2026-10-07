using System;
using System.Collections.Generic;

public enum StockfishDifficulty
{
    Beginner,
    Easy,
    Medium,
    Hard,
    Expert,
    Learner, Casual, Club, Strategist, Challenger, Master, Grandmaster
}

public sealed class StockfishDifficultyProfile
{
    public StockfishDifficulty Difficulty { get; }
    public string DisplayName { get; }
    public string EstimatedRating { get; }
    public string RatingGuidance { get; }
    public string RatingTooltip => RatingGuidance + "\n\nEstimated training Elo. These bots have not been calibrated against human ratings yet; use this as a rough guide, then adjust after a few games.";
    public int SearchDepth { get; }
    public int MultiPv { get; }
    public int MinimumThinkTimeMs { get; }
    public float BestMoveChance { get; }
    public int TargetCentipawnLoss { get; }
    public int MaximumCentipawnLoss { get; }
    public int Level { get; private set; }
    public string BotName { get; private set; }
    public string Story { get; private set; }
    public string AvatarResource { get; private set; }
    public string Style { get; private set; }

    internal StockfishDifficultyProfile WithCharacter(int level, string name, string story, string avatar, string style)
    {
        Level = level; BotName = name; Story = story;
        AvatarResource = "Chess/Bots/Avatars/" + avatar; Style = style;
        return this;
    }

    public StockfishDifficultyProfile(
        StockfishDifficulty difficulty,
        string displayName,
        string estimatedRating,
        int searchDepth,
        int multiPv,
        int minimumThinkTimeMs,
        float bestMoveChance,
        int targetCentipawnLoss,
        int maximumCentipawnLoss,
        string ratingGuidance = "")
    {
        Difficulty = difficulty;
        DisplayName = displayName;
        EstimatedRating = estimatedRating;
        RatingGuidance = ratingGuidance;
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
    // IDs 0..4 retain their old meaning. Presentation order comes from this list, never enum ordinals.
    public static IReadOnlyList<StockfishDifficultyProfile> All { get; } = Array.AsReadOnly(new[]
    {
        Create(StockfishDifficulty.Beginner,1,"Pip","I learned chess yesterday. My pawn and I are doing our best. Please don't tell him we're in trouble.","pip","First steps",3,32,550,.03f,650,1400),
        Create(StockfishDifficulty.Learner,2,"Pebble","I play one tiny step at a time. If I hide in my shell, I'm probably trying to remember how knights move.","pebble","Take your time",4,28,600,.08f,510,1100),
        Create(StockfishDifficulty.Easy,3,"Noodle","Someone said knights move like an L. I heard 'nap on the board.' Let's see which lesson stuck.","noodle","Curious beginner",5,24,650,.15f,390,850),
        Create(StockfishDifficulty.Casual,4,"Mallow","I haunt the chess club after closing. Mostly I haunt my own hanging pieces. We'll get better together.","mallow","Learn the patterns",6,20,700,.23f,290,650),
        Create(StockfishDifficulty.Club,5,"Scout","Every game is a new trail. I like spotting little tactics, but sometimes I follow the wrong pawns home.","scout","Tactics explorer",7,16,750,.31f,220,500),
        Create(StockfishDifficulty.Medium,6,"Penny","I run the café's lunchtime chess table. Bring a plan and a snack. I promise to take only one of them.","penny","Club regular",8,12,800,.42f,160,360),
        Create(StockfishDifficulty.Strategist,7,"Rust","My gears were built for patient plans. They squeak whenever I see a fork. That's a feature, obviously.","rust","Patient planner",9,10,850,.53f,115,280),
        Create(StockfishDifficulty.Challenger,8,"Juniper","I've watched a thousand games from the club rafters. Quiet positions have a lot to say. Shall we listen?","juniper","Position reader",10,8,900,.64f,85,210),
        Create(StockfishDifficulty.Hard,9,"Rooki","I used to hustle blitz games behind the bakery. Relax. The only thing I'm stealing today is your initiative.","rooki","Sharp competitor",11,6,950,.74f,60,160),
        Create(StockfishDifficulty.Master,10,"Vex","I collect awkward positions and raised eyebrows. Keep your cool. I get terribly bored when you don't bite.","vex","Pressure player",12,5,1000,.84f,40,110),
        Create(StockfishDifficulty.Grandmaster,11,"Duchess Violet","At my table, every piece has an appointment. Yours seem to be running late. Tea while you find a plan?","violet","Cool calculation",13,4,1050,.92f,25,75),
        Create(StockfishDifficulty.Expert,12,"Zero","I was built to find the move everyone else overlooks. I can wait. The position usually can't.","zero","Final challenge",14,3,1100,.98f,10,40)
    });

    private static StockfishDifficultyProfile Create(StockfishDifficulty id,int level,string name,string story,string avatar,
        string style,int depth,int pv,int delay,float best,int target,int maximum)
    {
        string[] ratings={"250","400","550","700","850","1000","1200","1400","1600","1800","2100","2400"};
        string[] guidance={
            "Level 1 · First games. For players learning piece movement, checks and safe squares.",
            "Level 2 · New players. Practise defending pieces and noticing immediate threats.",
            "Level 3 · Early beginners. Try simple captures, forks and one-move tactics.",
            "Level 4 · Developing beginners. Work on opening basics and avoiding hanging pieces.",
            "Level 5 · Improving players. Practise short combinations and making a plan.",
            "Level 6 · Around 1000. For players comfortable with the rules and common tactical patterns.",
            "Level 7 · Club improvers. Practise calculating a few moves ahead and improving piece activity.",
            "Level 8 · Intermediate club play. Work on positional plans and converting an advantage.",
            "Level 9 · Strong club practice. Expect sharper tactics and fewer obvious opportunities.",
            "Level 10 · Advanced practice. Test calculation, defence and patience under pressure.",
            "Level 11 · Expert-level practice. For experienced players seeking demanding positions.",
            "Level 12 · The toughest opponent. For advanced players who want a serious calculation challenge."
        };
        return new StockfishDifficultyProfile(id,"Level " + level,ratings[level-1],depth,pv,delay,best,target,maximum,guidance[level-1])
            .WithCharacter(level,name,story,avatar,style);
    }

    public static StockfishDifficultyProfile Get(StockfishDifficulty difficulty)
    {
        foreach (var profile in All) if (profile.Difficulty == difficulty) return profile;
        return All[5];
    }

    public static StockfishDifficulty RewardTier(StockfishDifficulty difficulty)
    {
        int level = Get(difficulty).Level;
        return level <= 2 ? StockfishDifficulty.Beginner : level <= 4 ? StockfishDifficulty.Easy :
            level <= 8 ? StockfishDifficulty.Medium : level <= 10 ? StockfishDifficulty.Hard : StockfishDifficulty.Expert;
    }
}
