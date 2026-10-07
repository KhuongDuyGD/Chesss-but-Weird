using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Avatar stays at the bottom-right. The balloon exists only during speech or recovery.</summary>
public sealed class BotCompanionView : MonoBehaviour
{
    public const float ReservedHeight=416;
    private const float AvatarSize=136, BottomMargin=20, BubbleBottom=150;
    private static BotCompanionView activeInstance;
    private ChessGame game;
    private StockfishBotController controller;
    private GameObject canvasObject;
    private RectTransform bubble;
    private RectTransform activityPanel;
    private TextMeshProUGUI activityLabel;
    private TextMeshProUGUI nameLabel,speech,muteLabel;
    private Image portrait;
    private Button avatarButton,retry,menu,mute;
    private readonly System.Random random=new System.Random();
    private readonly BotSpeechPlayback playback=new BotSpeechPlayback();
    private readonly Dictionary<int,string> lastMomentLines=new Dictionary<int,string>();
    private string lastLine,lastError;
    private float nextClick;
    private bool muted,wasOver,hasInteracted,profileLoaded,openingPending;
    private StockfishDifficulty loadedDifficulty;
    private BotMoveQuality? pendingReaction;

    public static float BottomInset(ChessGame source)
    {
        if(activeInstance&&activeInstance.game==source&&activeInstance.canvasObject&&activeInstance.canvasObject.activeSelf&&activeInstance.bubble.gameObject.activeSelf)
            return BottomMargin+BubbleBottom+activeInstance.bubble.sizeDelta.y;
        return BottomMargin+AvatarSize;
    }

    public void Initialize(ChessGame source,StockfishBotController bot)
    {
        game=source;controller=bot;
        if(activeInstance&&activeInstance!=this)activeInstance.Hide();
        activeInstance=this;
        canvasObject=new GameObject("Bot Companion Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(game.transform,false);
        var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=19;
        ResponsiveUi.ConfigureCanvasScaler(canvasObject.GetComponent<CanvasScaler>(),new Vector2(1280,720));
        var safe=MatchHudStyle.Rect(canvasObject.transform,"Bot Safe Area",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        safe.gameObject.AddComponent<ResponsiveSafeArea>();
        var panel=MatchHudStyle.Rect(safe,"Bot companion",new Vector2(1,0),new Vector2(1,0),new Vector2(380,396),new Vector2(-210,218));
        var avatar=MatchHudStyle.Rect(panel,"Bot avatar",new Vector2(1,0),new Vector2(1,0),new Vector2(AvatarSize,AvatarSize),new Vector2(-AvatarSize/2,AvatarSize/2));
        var frame=avatar.gameObject.AddComponent<HandDrawnRoundedGraphic>();
        frame.Configure(MatchHudStyle.Paper,MatchHudStyle.Ink,AvatarSize/2,2,.55f,14);
        frame.raycastTarget=true;avatar.gameObject.AddComponent<Mask>().showMaskGraphic=true;
        avatarButton=avatar.gameObject.AddComponent<Button>();avatarButton.targetGraphic=frame;avatarButton.onClick.AddListener(OnAvatarPressed);
        var image=MatchHudStyle.Rect(avatar,"Portrait",Vector2.zero,Vector2.one,new Vector2(-12,-12),Vector2.zero);
        portrait=image.gameObject.AddComponent<Image>();portrait.preserveAspect=true;portrait.raycastTarget=false;

        activityPanel=MatchHudStyle.Rect(panel,"Bot activity",new Vector2(1,0),new Vector2(1,0),new Vector2(224,38),new Vector2(-260,68));
        var activityFrame=activityPanel.gameObject.AddComponent<HandDrawnRoundedGraphic>();
        activityFrame.Configure(MatchHudStyle.Paper,MatchHudStyle.Ink,12,1.5f,.4f,10);activityFrame.raycastTarget=false;
        activityLabel=MatchHudStyle.Text(activityPanel,"Bot activity text","",19);
        activityLabel.alignment=TextAlignmentOptions.Center;
        activityLabel.enableAutoSizing=true;activityLabel.fontSizeMin=16;activityLabel.fontSizeMax=19;
        activityLabel.rectTransform.sizeDelta=new Vector2(-12,-4);
        activityPanel.gameObject.SetActive(false);

        bubble=MatchHudStyle.Rect(panel,"Bot speech bubble",new Vector2(1,0),new Vector2(1,0),new Vector2(380,170),new Vector2(-190,BubbleBottom+85));
        bubble.gameObject.AddComponent<BotSpeechBubbleGraphic>().raycastTarget=false;
        nameLabel=Label(bubble,"Opponent",new Vector2(136,-24),new Vector2(228,28),17);
        nameLabel.color=MatchHudStyle.Muted;nameLabel.enableAutoSizing=true;nameLabel.fontSizeMin=15;nameLabel.fontSizeMax=17;
        speech=Label(bubble,"Bot dialogue",new Vector2(190,-90),new Vector2(328,80),22);
        speech.alignment=TextAlignmentOptions.TopLeft;speech.textWrappingMode=TextWrappingModes.Normal;
        speech.enableAutoSizing=true;speech.fontSizeMin=18;speech.fontSizeMax=22;
        mute=MatchHudStyle.Button(bubble,"Toggle bot chat","Mute chat",MuteChat);
        MatchHudStyle.Place((RectTransform)mute.transform,Vector2.one,new Vector2(92,28),new Vector2(-65,-24));
        muteLabel=mute.GetComponentInChildren<TextMeshProUGUI>();muteLabel.fontSize=16;muteLabel.rectTransform.sizeDelta=new Vector2(-10,-2);
        retry=MatchHudStyle.Button(bubble,"Retry Stockfish","Retry",controller.RetryBotTurn);
        menu=MatchHudStyle.Button(bubble,"Leave bot game","Main menu",game.RestartToMainMenu);
        MatchHudStyle.Place((RectTransform)retry.transform,new Vector2(0,0),new Vector2(145,34),new Vector2(100,52));
        MatchHudStyle.Place((RectTransform)menu.transform,new Vector2(1,0),new Vector2(145,34),new Vector2(-100,52));
        foreach(var button in new[]{retry,menu})
        {
            var label=button.GetComponentInChildren<TextMeshProUGUI>();
            label.enableAutoSizing=true;label.fontSizeMin=14;label.fontSizeMax=18;
            label.rectTransform.sizeDelta=new Vector2(-12,-4);
        }
        muted=PlayerPrefs.GetInt("Bot.ChatMuted",0)!=0;
        game.LocalGameRestarted+=Reset;game.ReturnedToMainMenu+=Hide;
        controller.PlayerMoveAssessed+=OnMoveAssessed;
        controller.ActivityChanged+=OnActivityChanged;
        DismissDialogue();canvasObject.SetActive(false);
    }
    private static TextMeshProUGUI Label(RectTransform parent,string label,Vector2 position,Vector2 size,float fontSize)
    {var text=MatchHudStyle.Text(parent,label,"",fontSize);MatchHudStyle.Place(text.rectTransform,new Vector2(0,1),size,position);return text;}

    private void Reset()
    {
        wasOver=false;hasInteracted=false;openingPending=true;pendingReaction=null;lastError=null;lastLine=null;nextClick=0;
        var profile=StockfishDifficultyProfiles.Get(controller.Difficulty);
        loadedDifficulty=controller.Difficulty;profileLoaded=true;
        portrait.sprite=BotAvatarCatalog.Get(profile);nameLabel.text=profile.BotName+" / "+profile.DisplayName;
        avatarButton.gameObject.name="Bot avatar";
        DismissDialogue();canvasObject.SetActive(true);RefreshActivity();
    }
    private void Hide(){DismissDialogue();activityPanel.gameObject.SetActive(false);activityLabel.text=string.Empty;canvasObject.SetActive(false);wasOver=false;profileLoaded=false;openingPending=false;pendingReaction=null;}
    private void OnActivityChanged(string value)=>RefreshActivity();
    private void RefreshActivity()
    {
        bool visible=canvasObject.activeSelf&&controller.IsThinking&&!game.GameOver&&!game.IsMatchEnding&&
            !game.PauseLocked&&controller.ErrorMessage==null&&!string.IsNullOrEmpty(controller.Activity);
        activityLabel.text=visible?controller.Activity:string.Empty;
        activityPanel.gameObject.SetActive(visible);
    }
    private void MuteChat(){muted=true;PlayerPrefs.SetInt("Bot.ChatMuted",1);DismissDialogue();}
    private void OnAvatarPressed()
    {
        if(!game||game.PauseLocked||!canvasObject.activeSelf||lastError!=null)return;
        if(playback.IsTyping){playback.RevealAll();speech.maxVisibleCharacters=playback.VisibleCharacters;return;}
        if(Time.unscaledTime<nextClick)return;
        muted=false;PlayerPrefs.SetInt("Bot.ChatMuted",0);
        openingPending=false;
        Say(hasInteracted?BotSpeechMoment.Clicked:BotSpeechMoment.Opening);hasInteracted=true;
        nextClick=Time.unscaledTime+2;
    }
    private void OnMoveAssessed(BotMoveQuality quality)
    {
        if(quality==BotMoveQuality.Ordinary||muted||!game.IsBotGame||game.GameOver)return;
        pendingReaction=quality;
    }
    private void Say(BotSpeechMoment moment)
    {
        if(muted)return;
        int level=StockfishDifficultyProfiles.Get(controller.Difficulty).Level;
        int key=level*100+(int)moment;
        var line=BotDialogue.Line(level,moment,random,lastMomentLines.TryGetValue(key,out var previous)?previous:lastLine);
        lastMomentLines[key]=line;
        ShowDialogue(line,false);
    }
    private void ShowDialogue(string line,bool recovery)
    {
        if(string.IsNullOrWhiteSpace(line)){DismissDialogue();return;}
        lastLine=line;bubble.gameObject.SetActive(true);speech.text=line;
        speech.maxVisibleCharacters=int.MaxValue;
        float preferred=speech.GetPreferredValues(line,328,float.PositiveInfinity).y;
        float bodyHeight=Mathf.Clamp(preferred+58+(recovery?44:0),116,220);
        float height=bodyHeight+BotSpeechBubbleGraphic.TailHeight;
        bubble.sizeDelta=new Vector2(380,height);bubble.anchoredPosition=new Vector2(-190,BubbleBottom+height/2);
        MatchHudStyle.Place(speech.rectTransform,new Vector2(0,1),new Vector2(328,bodyHeight-54-(recovery?44:0)),
            new Vector2(190,-44-(bodyHeight-54-(recovery?44:0))/2));
        retry.gameObject.SetActive(recovery);menu.gameObject.SetActive(recovery);mute.gameObject.SetActive(!recovery);
        speech.ForceMeshUpdate(true,true);playback.Start(speech.textInfo.characterCount);speech.maxVisibleCharacters=0;
    }
    private void DismissDialogue()
    {
        playback.Stop();
        if(bubble)bubble.gameObject.SetActive(false);
        if(speech){speech.text=string.Empty;speech.maxVisibleCharacters=0;}
    }
    private void Update()
    {
        if(!game||!controller||!canvasObject)return;
        bool show=controller.IsBotGameActive&&game.IsBotGame&&(game.GameStarted||game.GameOver);
        if(!show){Hide();return;}
        if(!canvasObject.activeSelf||!profileLoaded||loadedDifficulty!=controller.Difficulty)Reset();
        // Let the final sentence remain visible above ResultMenuView's dim overlay (order 2000).
        canvasObject.GetComponent<Canvas>().sortingOrder=game.GameOver?2001:19;
        RefreshActivity();
        string error=game.GameOver||game.IsMatchEnding?null:controller.ErrorMessage;
        if(!string.IsNullOrEmpty(error))
        {
            if(lastError!=error){lastError=error;ShowDialogue(error,true);}
        }
        else if(lastError!=null){lastError=null;DismissDialogue();}
        if(lastError==null&&!game.PauseLocked)
        {
            if(game.GameOver&&!wasOver)
            {wasOver=true;openingPending=false;pendingReaction=null;Say(game.Status==ChessGame.ChessGameStatus.Draw?BotSpeechMoment.Draw:game.WinningTeam==game.PlayerTeam?BotSpeechMoment.Lose:BotSpeechMoment.Win);}
            else if(openingPending&&!game.GameOver)
            {openingPending=false;hasInteracted=true;Say(BotSpeechMoment.Opening);}
            else if(pendingReaction.HasValue)
            {
                var quality=pendingReaction.Value;pendingReaction=null;
                Say(quality==BotMoveQuality.Brilliant?BotSpeechMoment.Brilliant:quality==BotMoveQuality.Great?BotSpeechMoment.Great:BotSpeechMoment.Blunder);
            }
        }
        playback.Advance(Time.unscaledDeltaTime,game.PauseLocked,lastError!=null);
        if(playback.IsVisible)speech.maxVisibleCharacters=playback.VisibleCharacters;
        else if(bubble.gameObject.activeSelf)DismissDialogue();
    }
    private void OnDestroy()
    {
        if(game){game.LocalGameRestarted-=Reset;game.ReturnedToMainMenu-=Hide;}
        if(controller){controller.PlayerMoveAssessed-=OnMoveAssessed;controller.ActivityChanged-=OnActivityChanged;}
        if(canvasObject)Destroy(canvasObject);
        if(activeInstance==this)activeInstance=null;
    }
}
