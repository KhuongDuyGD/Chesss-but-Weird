using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The roster, details and launch action all use the same ordered difficulty configuration.</summary>
internal static class BotRosterView
{
    internal static void Build(RectTransform root, Action<StockfishDifficulty> play, Action back)
    {
        SketchbookUI.Background(root);
        SketchbookUI.Text(root,"Bot roster title","Meet your next opponent",new Rect(75,35,1350,80),64);
        SketchbookUI.Text(root,"Bot roster subtitle","12 personalities. Pick a level, then choose your side.",new Rect(80,118,1500,50),30,SketchbookUI.Muted);
        var details = SketchbookUI.Card(root,"Opponent details",new Rect(1320,202,520,706),SketchbookUI.White).rectTransform;
        var portrait = SketchbookUI.Image(details,"Selected avatar",null,new Rect(145,20,230,230));
        var name = SketchbookUI.Text(details,"Opponent name","",new Rect(25,262,470,65),48,null,TextAlignmentOptions.Center);
        var level = SketchbookUI.Text(details,"Opponent level","",new Rect(25,328,470,42),28,SketchbookUI.Muted,TextAlignmentOptions.Center);
        var story = SketchbookUI.Text(details,"Opponent story","",new Rect(34,384,452,190),30,null,TextAlignmentOptions.TopLeft,true);
        story.overflowMode=TextOverflowModes.Overflow;
        StockfishDifficultyProfile selected = StockfishDifficultyProfiles.All[0];
        var launch = SketchbookUI.Button(details,"Play selected bot","",new Rect(36,610,448,62),SketchbookUI.Green,()=>play(selected.Difficulty),30);
        var launchLabel = launch.GetComponentInChildren<TextMeshProUGUI>();
        var cards = new HandDrawnRoundedGraphic[StockfishDifficultyProfiles.All.Count];
        Action<StockfishDifficultyProfile> select = profile =>
        {
            selected=profile; portrait.sprite=BotAvatarCatalog.Get(profile);
            name.text=profile.BotName; level.text=profile.DisplayName + " / " + profile.Style;
            story.text=profile.Story; launchLabel.text="Play with " + profile.BotName;
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
            SketchbookUI.Image(card.transform,"Avatar",BotAvatarCatalog.Get(profile),new Rect(87,8,108,108));
            SketchbookUI.Text(card.transform,"Name",profile.BotName,new Rect(12,117,258,42),32,null,TextAlignmentOptions.Center);
            SketchbookUI.Text(card.transform,"Level",profile.DisplayName,new Rect(12,159,258,36),25,SketchbookUI.Muted,TextAlignmentOptions.Center);
        }
        select(selected);
        SketchbookUI.Button(root,"Back to modes","Back",new Rect(80,964,220,64),SketchbookUI.Paper,()=>back());
        SketchbookUI.Text(root,"Level guidance","Start small. Move up when you feel ready.",new Rect(360,960,1320,62),30,SketchbookUI.Muted);
    }
}
