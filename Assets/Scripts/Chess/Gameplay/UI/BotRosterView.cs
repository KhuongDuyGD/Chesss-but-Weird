using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The roster, details and launch action all use the same ordered difficulty configuration.</summary>
internal static class BotRosterView
{
    internal static void Build(RectTransform root, Action<StockfishDifficulty,BotGameOptions> play, Action back)
    {
        SketchbookUI.Background(root);
        SketchbookUI.Text(root,"Bot roster title","Meet your next opponent",new Rect(75,35,1350,80),64);
        SketchbookUI.Text(root,"Bot roster subtitle","Pick an opponent, choose Practice or Challenge, then choose your side.",new Rect(80,118,1700,50),30,SketchbookUI.Muted);
        var tooltip=root.gameObject.AddComponent<MatchHudTooltip>();tooltip.Initialize(root);
        var details = SketchbookUI.Card(root,"Opponent details",new Rect(1320,202,520,772),SketchbookUI.White).rectTransform;
        var portrait = SketchbookUI.Image(details,"Selected avatar",null,new Rect(180,18,160,160));
        var name = SketchbookUI.Text(details,"Opponent name","",new Rect(25,190,470,54),42,null,TextAlignmentOptions.Center);
        var level = SketchbookUI.Text(details,"Opponent level","",new Rect(25,245,470,38),25,SketchbookUI.Muted,TextAlignmentOptions.Center);
        var rating = SketchbookUI.Text(details,"Opponent Elo","",new Rect(25,287,470,38),28,SketchbookUI.Muted,TextAlignmentOptions.Center);
        rating.raycastTarget=true;var ratingTarget=rating.gameObject.AddComponent<MatchHudTooltipTarget>();
        var story = SketchbookUI.Text(details,"Opponent story","",new Rect(34,342,452,118),24,null,TextAlignmentOptions.TopLeft,true);
        StockfishDifficultyProfile selected = StockfishDifficultyProfiles.All[0];
        BotGameMode mode=BotGameMode.Practice;bool allowHints=true,allowUndo=true;
        var launch = SketchbookUI.Button(details,"Play selected bot","",new Rect(36,706,448,48),SketchbookUI.Green,
            ()=>play(selected.Difficulty,new BotGameOptions(mode,allowHints,allowUndo)),28);
        var launchLabel = launch.GetComponentInChildren<TextMeshProUGUI>();
        SketchbookUI.Text(details,"Game mode heading","Choose your game",new Rect(34,472,452,30),24);
        Button practice=null,challenge=null,hints=null,undo=null;
        var modeInfo=SketchbookUI.Text(details,"Game mode guidance","",new Rect(34,575,452,58),22,SketchbookUI.Muted,TextAlignmentOptions.TopLeft,true);
        Action refreshMode=()=>
        {
            bool training=mode==BotGameMode.Practice;
            practice.GetComponent<HandDrawnRoundedGraphic>().Configure(training?SketchbookUI.Blue:SketchbookUI.Paper,SketchbookUI.Ink,15,training?4:2.6f,1.2f,1);
            challenge.GetComponent<HandDrawnRoundedGraphic>().Configure(!training?SketchbookUI.Yellow:SketchbookUI.Paper,SketchbookUI.Ink,15,!training?4:2.6f,1.2f,2);
            hints.interactable=undo.interactable=training;
            hints.GetComponentInChildren<TextMeshProUGUI>().text="Hints: "+(training&&allowHints?"On":"Off");
            undo.GetComponentInChildren<TextMeshProUGUI>().text="Undo: "+(training&&allowUndo?"On":"Off");
            modeInfo.text=training?"Learn with optional hints and takebacks. Practice games have no rewards or win/loss stats.":
                "Test yourself without hints or takebacks. Challenge games count toward your stats and rewards.";
            launchLabel.text=(training?"Practise with ":"Challenge ")+selected.BotName;
        };
        practice=SketchbookUI.Button(details,"Practice mode","Practice",new Rect(34,514,216,48),SketchbookUI.Blue,()=>{mode=BotGameMode.Practice;refreshMode();},27);
        challenge=SketchbookUI.Button(details,"Challenge mode","Challenge",new Rect(270,514,216,48),SketchbookUI.Paper,()=>{mode=BotGameMode.Challenge;refreshMode();},27);
        hints=SketchbookUI.Button(details,"Toggle hints","",new Rect(34,646,216,40),SketchbookUI.Paper,()=>{allowHints=!allowHints;refreshMode();},24);
        undo=SketchbookUI.Button(details,"Toggle undo","",new Rect(270,646,216,40),SketchbookUI.Paper,()=>{allowUndo=!allowUndo;refreshMode();},24);
        var cards = new HandDrawnRoundedGraphic[StockfishDifficultyProfiles.All.Count];
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
            SketchbookUI.Image(card.transform,"Avatar",BotAvatarCatalog.Get(profile),new Rect(93,8,96,96));
            SketchbookUI.Text(card.transform,"Name",profile.BotName,new Rect(12,108,258,38),30,null,TextAlignmentOptions.Center);
            SketchbookUI.Text(card.transform,"Level",profile.DisplayName,new Rect(12,147,258,29),23,SketchbookUI.Muted,TextAlignmentOptions.Center);
            SketchbookUI.Text(card.transform,"Estimated Elo","~"+profile.EstimatedRating+" Elo",new Rect(12,179,258,28),23,SketchbookUI.Muted,TextAlignmentOptions.Center);
        }
        select(selected);
        SketchbookUI.Button(root,"Back to modes","Back",new Rect(80,964,220,64),SketchbookUI.Paper,()=>back());
        SketchbookUI.Text(root,"Level guidance","Start small. Move up when you feel ready.",new Rect(360,960,1320,62),30,SketchbookUI.Muted);
    }
}
