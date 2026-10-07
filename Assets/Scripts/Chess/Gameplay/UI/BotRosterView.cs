using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The roster, details and launch action all use the same ordered difficulty configuration.</summary>
internal static class BotRosterView
{
    internal static Action Build(RectTransform root, Action<StockfishDifficulty,BotGameOptions> play, Action back)
    {
        SketchbookUI.Background(root);
        SketchbookUI.Text(root,"Bot roster title","Meet your next opponent",new Rect(75,35,1350,80),64);
        SketchbookUI.Text(root,"Bot roster subtitle","Pick an opponent, choose Practice or Challenge, then choose your side.",new Rect(80,118,1700,50),30,SketchbookUI.Muted);
        var tooltip=root.gameObject.AddComponent<MatchHudTooltip>();tooltip.Initialize(root);
        var details = SketchbookUI.Card(root,"Opponent details",new Rect(1320,202,520,820),SketchbookUI.White).rectTransform;
        var portrait = SketchbookUI.Image(details,"Selected avatar",null,new Rect(180,18,160,160));
        var name = SketchbookUI.Text(details,"Opponent name","",new Rect(25,190,470,54),42,null,TextAlignmentOptions.Center);
        var level = SketchbookUI.Text(details,"Opponent level","",new Rect(25,245,470,38),25,SketchbookUI.Muted,TextAlignmentOptions.Center);
        var rating = SketchbookUI.Text(details,"Opponent Elo","",new Rect(25,287,470,38),28,SketchbookUI.Muted,TextAlignmentOptions.Center);
        rating.raycastTarget=true;var ratingTarget=rating.gameObject.AddComponent<MatchHudTooltipTarget>();
        var story = SketchbookUI.Text(details,"Opponent story","",new Rect(34,334,452,78),22,null,TextAlignmentOptions.TopLeft,true);
        var selectedBadge = CreateBadge(details, "Selected challenge badge", new Rect(426,38,60,60), tooltip);
        var progressCard = SketchbookUI.Card(details,"Bot progress",new Rect(30,422,460,112),SketchbookUI.Paper,shadow:false,radius:12).rectTransform;
        var practiceStats = SketchbookUI.Text(progressCard,"Practice record","",new Rect(14,7,432,30),24);
        var assistStats = SketchbookUI.Text(progressCard,"Practice assists","",new Rect(14,39,432,26),21,SketchbookUI.Muted);
        var challengeStats = SketchbookUI.Text(progressCard,"Challenge record","",new Rect(14,73,432,30),24);
        StockfishDifficultyProfile selected = StockfishDifficultyProfiles.All[0];
        BotGameMode mode=BotGameMode.Practice;bool allowHints=true,allowUndo=true;
        var launch = SketchbookUI.Button(details,"Play selected bot","",new Rect(36,754,448,48),SketchbookUI.Green,
            ()=>play(selected.Difficulty,new BotGameOptions(mode,allowHints,allowUndo)),28);
        var launchLabel = launch.GetComponentInChildren<TextMeshProUGUI>();
        SketchbookUI.Text(details,"Game mode heading","Choose your game",new Rect(34,546,452,30),24);
        Button practice=null,challenge=null,hints=null,undo=null;
        var modeInfo=SketchbookUI.Text(details,"Game mode guidance","",new Rect(34,636,452,62),22,SketchbookUI.Muted,TextAlignmentOptions.TopLeft,true);
        Action refreshMode=()=>
        {
            bool training=mode==BotGameMode.Practice;
            practice.GetComponent<HandDrawnRoundedGraphic>().Configure(training?SketchbookUI.Blue:SketchbookUI.Paper,SketchbookUI.Ink,15,training?4:2.6f,1.2f,1);
            challenge.GetComponent<HandDrawnRoundedGraphic>().Configure(!training?SketchbookUI.Yellow:SketchbookUI.Paper,SketchbookUI.Ink,15,!training?4:2.6f,1.2f,2);
            hints.interactable=undo.interactable=training;
            hints.GetComponentInChildren<TextMeshProUGUI>().text="Hints: "+(training&&allowHints?"On":"Off");
            undo.GetComponentInChildren<TextMeshProUGUI>().text="Undo: "+(training&&allowUndo?"On":"Off");
            var progress=PlayerProfileStore.GetBotProgress(selected.Difficulty);
            practiceStats.text=$"Practice   Wins {progress.practiceWins}   Losses {progress.practiceLosses}   Draws {progress.practiceDraws}";
            assistStats.text=$"Hints used: {progress.hintsUsed}   /   Undos used: {progress.undosUsed}";
            challengeStats.text=$"Challenge   Wins {progress.challengeWins}   Losses {progress.challengeLosses}   Draws {progress.challengeDraws}";
            selectedBadge.SetActive(progress.ChallengeBeaten);
            int practiceGold=BotProgressPolicy.PracticeGold(selected.Difficulty);
            int winGold=BotProgressPolicy.ChallengeGold(selected.Difficulty);
            modeInfo.text=training ? (practiceGold>0
                ? $"Win: {practiceGold} gold. Shared daily rewards: {PlayerProfileStore.PracticeRewardsRemaining(DateTime.UtcNow)}/3 left. Resets at 00:00 (UTC+7)."
                : "Learn with hints and takebacks. Your record and assist use are saved. No gold reward.")
                : (progress.ChallengeBeaten ? $"Win: {winGold} gold. No hints or takebacks."
                    : $"First win: {winGold*3} gold (x3). Then {winGold} gold per win. No hints or takebacks.");
            launchLabel.text=(training?"Practise with ":"Challenge ")+selected.BotName;
        };
        practice=SketchbookUI.Button(details,"Practice mode","Practice",new Rect(34,583,216,40),SketchbookUI.Blue,()=>{mode=BotGameMode.Practice;refreshMode();},27);
        challenge=SketchbookUI.Button(details,"Challenge mode","Challenge",new Rect(270,583,216,40),SketchbookUI.Paper,()=>{mode=BotGameMode.Challenge;refreshMode();},27);
        hints=SketchbookUI.Button(details,"Toggle hints","",new Rect(34,708,216,34),SketchbookUI.Paper,()=>{allowHints=!allowHints;refreshMode();},24);
        undo=SketchbookUI.Button(details,"Toggle undo","",new Rect(270,708,216,34),SketchbookUI.Paper,()=>{allowUndo=!allowUndo;refreshMode();},24);
        var cards = new HandDrawnRoundedGraphic[StockfishDifficultyProfiles.All.Count];
        var badges = new GameObject[cards.Length];
        Action<StockfishDifficultyProfile> select = profile =>
        {
            selected=profile; portrait.sprite=BotAvatarCatalog.Get(profile);
            name.text=profile.BotName; level.text=profile.DisplayName + " / " + profile.Style;
            rating.text="~"+profile.EstimatedRating+" Elo · hover for guidance";
            ratingTarget.Configure(tooltip,profile.BotName+" · ~"+profile.EstimatedRating+" Elo",profile.RatingTooltip);
            story.text=profile.Story;refreshMode();
            for(int j=0;j<cards.Length;j++) if(cards[j])
                cards[j].Configure(j==profile.Level-1?SketchbookUI.Green:SketchbookUI.White,SketchbookUI.Ink,18, j==profile.Level-1?4:2.6f,1.2f,j);
        };
        for(int i=0;i<StockfishDifficultyProfiles.All.Count;i++)
        {
            var profile = StockfishDifficultyProfiles.All[i];
            var area=new Rect(80+(i%4)*304,202+(i/4)*238,282,216);
            var card=SketchbookUI.Card(root,"Bot " + profile.Level,area,SketchbookUI.White,radius:18);
            cards[i]=card; card.raycastTarget=true;
            var button=card.gameObject.AddComponent<Button>();button.targetGraphic=card;
            button.onClick.AddListener(()=>select(profile));
            badges[i]=CreateBadge(card.transform,"Challenge badge",new Rect(228,12,40,40),tooltip);
            SketchbookUI.Image(card.transform,"Avatar",BotAvatarCatalog.Get(profile),new Rect(93,8,96,96));
            SketchbookUI.Text(card.transform,"Name",profile.BotName,new Rect(12,108,258,38),30,null,TextAlignmentOptions.Center);
            SketchbookUI.Text(card.transform,"Level",profile.DisplayName,new Rect(12,147,258,29),23,SketchbookUI.Muted,TextAlignmentOptions.Center);
            SketchbookUI.Text(card.transform,"Estimated Elo","~"+profile.EstimatedRating+" Elo",new Rect(12,179,258,28),23,SketchbookUI.Muted,TextAlignmentOptions.Center);
        }
        Action refresh=()=>
        {
            for(int i=0;i<badges.Length;i++)
                badges[i].SetActive(PlayerProfileStore.GetBotProgress(StockfishDifficultyProfiles.All[i].Difficulty).ChallengeBeaten);
            select(selected);
        };
        refresh();
        SketchbookUI.Button(root,"Back to modes","Back",new Rect(80,964,220,64),SketchbookUI.Paper,()=>back());
        SketchbookUI.Text(root,"Level guidance","All bots are open. Gold badges mark Challenge wins.",new Rect(360,960,910,62),28,SketchbookUI.Muted);
        return refresh;
    }

    private static GameObject CreateBadge(Transform parent, string name, Rect area, MatchHudTooltip tooltip)
    {
        var badge=SketchbookUI.Card(parent,name,area,SketchbookUI.Yellow,shadow:false,radius:area.width/2);
        SketchbookUI.Doodle(badge.transform,"Challenge star",new Rect(area.width*.2f,area.height*.2f,area.width*.6f,area.height*.6f),
            SketchbookDoodle.Shape.Star,SketchbookUI.White);
        badge.raycastTarget=true;
        badge.gameObject.AddComponent<MatchHudTooltipTarget>().Configure(tooltip,"Challenge beaten","Earned by winning a Challenge game against this bot.");
        return badge.gameObject;
    }
}
