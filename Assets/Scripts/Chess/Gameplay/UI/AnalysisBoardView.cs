using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Compact match HUD. Journal expansion never moves the camera or mutates the match.</summary>
[DisallowMultipleComponent]
public sealed class AnalysisBoardView : MonoBehaviour
{
    private static AnalysisBoardView activeInstance;
    private ChessGame game;
    private GameObject canvasObject;
    private RectTransform root, top, whiteCard, blackCard, journal, content, statusRect, controls,buffSummary;
    private GraphicRaycaster raycaster;
    private TextMeshProUGUI whiteName,blackName,whiteState,blackState,status,elapsed,history,hint;
    private Button journalButton,focusButton,latestButton,historyTab,capturesTab;
    private ScrollRect scroll;
    private Camera cameraOwner;
    private Rect originalCameraRect;
    private float originalFieldOfView;
    private bool cameraCaptured,visible,expanded,focus,historyLayoutDirty,updatingHistory,showCaptures;
    private bool followLatest=true;
    private int lastRevision=-1;
    private float nextRefresh,lastBottom;
    private bool lastAram;
    private bool lastOnline;
    private bool lastBot;
    private float lastCompanionBottom;
    private float lastPracticeWidth;
    private Vector2 lastSize;
    private string preferenceMode;
    private TextMeshProUGUI whiteMoves,blackMoves,whiteBuff,blackBuff;
    private MatchHudTooltip tooltip;
    private ChessOrbitCamera orbitCamera;
    private RectTransform cameraHint;
    private TextMeshProUGUI cameraHintState,cameraHintControls;

    public void Initialize(ChessGame source)
    {
        if(activeInstance&&activeInstance!=this)activeInstance.Release();
        activeInstance=this;game=source;if(!canvasObject)Build();SetVisible(false);
    }
    public void SetVisible(bool value)
    {
        if(activeInstance!=this||!canvasObject)return;
        if(visible==value){canvasObject.SetActive(value);if(value)Refresh();else RestoreCamera();return;}
        visible=value;canvasObject.SetActive(value);
        if(!value){RestoreCamera();tooltip?.Hide();}else{LoadPreference();Layout();Refresh();}
    }
    public void SetInteractionEnabled(bool value)
    {if(raycaster)raycaster.enabled=value||(game&&game.GameOver);}
    private void OnDestroy(){Release();if(activeInstance==this)activeInstance=null;}
    private void OnDisable(){RestoreCamera();}
    private void OnEnable()
    {
        if(!game)return;
        if(activeInstance&&activeInstance!=this)activeInstance.Release();
        activeInstance=this;
        if(visible&&canvasObject){Layout();Refresh();}
    }
    private void Release(){RestoreCamera();visible=false;if(canvasObject){canvasObject.SetActive(false);Destroy(canvasObject);canvasObject=null;}}
    private bool LoadPreference()
    {
        if(preferenceMode==game.StatisticsMode)return false;preferenceMode=game.StatisticsMode;
        expanded=PlayerPrefs.GetInt("MatchHud.Journal."+preferenceMode,0)!=0;
        focus=PlayerPrefs.GetInt("MatchHud.Focus."+preferenceMode,0)!=0;
        if(focus)expanded=false;lastRevision=-1;return true;
    }
    private void Update()
    {
        if(!visible||!game||activeInstance!=this)return;
        bool modeChanged=LoadPreference();
        if(modeChanged||root.rect.size!=lastSize||lastBottom!=game.StatisticsHudBottom||lastAram!=game.IsAramGame||lastOnline!=game.UsesDotNetOnline||lastBot!=game.IsBotGame||lastCompanionBottom!=BotCompanionView.BottomInset(game)||lastPracticeWidth!=BotPracticeView.ToolbarWidth(game))Layout();
        RefreshCameraHint();
        if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.1f;Refresh();
    }
    private void Build()
    {
        if(!EventSystem.current)new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        canvasObject=new GameObject("Analysis Board Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(game.transform,false);var canvas=canvasObject.GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=18;
        ResponsiveUi.ConfigureCanvasScaler(canvasObject.GetComponent<CanvasScaler>(),new Vector2(1280,720));
        root=MatchHudStyle.Rect(canvasObject.transform,"Statistics Safe Area",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        root.gameObject.AddComponent<ResponsiveSafeArea>();
        raycaster=canvasObject.GetComponent<GraphicRaycaster>();
        cameraHint=MatchHudStyle.Rect(root,"Camera lock hint",Vector2.zero,Vector2.zero,new Vector2(310,78),new Vector2(175,59));
        MatchHudStyle.Surface(cameraHint,false);
        cameraHintState=MatchHudStyle.Text(cameraHint,"Camera state","Camera locked · Y to unlock",20);
        MatchHudStyle.Place(cameraHintState.rectTransform,new Vector2(.5f,1),new Vector2(286,28),new Vector2(0,-19));
        cameraHintState.enableAutoSizing=true;cameraHintState.fontSizeMin=16;cameraHintState.fontSizeMax=20;
        cameraHintControls=MatchHudStyle.Text(cameraHint,"Camera controls","Scroll to zoom",16);
        MatchHudStyle.Place(cameraHintControls.rectTransform,new Vector2(.5f,0),new Vector2(286,38),new Vector2(0,24));
        cameraHintControls.color=MatchHudStyle.Muted;
        top=MatchHudStyle.Rect(root,"Players",Vector2.one,Vector2.one,new Vector2(900,88),new Vector2(0,-62));
        whiteCard=Player(top,"White",out whiteName,out whiteState);blackCard=Player(top,"Black",out blackName,out blackState);
        statusRect=MatchHudStyle.Rect(root,"Match status",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(660,32),new Vector2(0,-104));
        MatchHudStyle.Surface(statusRect,false);status=MatchHudStyle.Text(statusRect,"State","",20);status.alignment=TextAlignmentOptions.Center;
        status.rectTransform.sizeDelta=new Vector2(-24,-4);
        controls=MatchHudStyle.Rect(root,"Journal controls",Vector2.one,Vector2.one,new Vector2(330,48),new Vector2(-185,-128));
        journalButton=MatchHudStyle.Button(controls,"Journal","Match journal",ToggleJournal);
        MatchHudStyle.Place((RectTransform)journalButton.transform,new Vector2(0,.5f),new Vector2(198,46),new Vector2(99,0));
        focusButton=MatchHudStyle.Button(controls,"Focus","Focus",ToggleFocus);
        MatchHudStyle.Place((RectTransform)focusButton.transform,new Vector2(1,.5f),new Vector2(120,46),new Vector2(-60,0));
        buffSummary=MatchHudStyle.Rect(root,"ARAM move and buff summary",Vector2.one,Vector2.one,new Vector2(330,70),new Vector2(-185,-91));
        MatchHudStyle.Surface(buffSummary,false);
        whiteMoves=SummaryLabel(buffSummary,"White moves",new Vector2(-112,17),new Vector2(86,30));
        blackMoves=SummaryLabel(buffSummary,"Black moves",new Vector2(-112,-17),new Vector2(86,30));
        whiteBuff=SummaryLabel(buffSummary,"White buff",new Vector2(46,17),new Vector2(220,30));
        blackBuff=SummaryLabel(buffSummary,"Black buff",new Vector2(46,-17),new Vector2(220,30));
        canvasObject.AddComponent<SettingsUiScale>();
        tooltip=canvasObject.AddComponent<MatchHudTooltip>();tooltip.Initialize(root);
        whiteBuff.raycastTarget=blackBuff.raycastTarget=true;
        whiteBuff.gameObject.AddComponent<MatchHudTooltipTarget>();blackBuff.gameObject.AddComponent<MatchHudTooltipTarget>();
        journal=MatchHudStyle.Rect(root,"Match journal",Vector2.one,Vector2.one,new Vector2(390,650),new Vector2(-215,-539));MatchHudStyle.Surface(journal);
        var title=MatchHudStyle.Text(journal,"Title","Match journal",24);
        MatchHudStyle.Place(title.rectTransform,new Vector2(0,1),new Vector2(190,34),new Vector2(111,-23));
        elapsed=MatchHudStyle.Text(journal,"Elapsed","",18);elapsed.alignment=TextAlignmentOptions.MidlineRight;
        MatchHudStyle.Place(elapsed.rectTransform,Vector2.one,new Vector2(172,34),new Vector2(-102,-23));
        var tabs=MatchHudStyle.Rect(journal,"Journal tabs",new Vector2(0,1),Vector2.one,new Vector2(-32,36),new Vector2(0,-62));
        historyTab=MatchHudStyle.Button(tabs,"History","History",()=>SelectJournalTab(false));
        capturesTab=MatchHudStyle.Button(tabs,"Captures","Captures",()=>SelectJournalTab(true));
        var hr=(RectTransform)historyTab.transform;hr.anchorMin=Vector2.zero;hr.anchorMax=new Vector2(.5f,1);hr.offsetMin=Vector2.zero;hr.offsetMax=new Vector2(-4,0);
        var cr=(RectTransform)capturesTab.transform;cr.anchorMin=new Vector2(.5f,0);cr.anchorMax=Vector2.one;cr.offsetMin=new Vector2(4,0);cr.offsetMax=Vector2.zero;
        historyTab.GetComponentInChildren<TextMeshProUGUI>().rectTransform.sizeDelta=new Vector2(-16,-4);
        capturesTab.GetComponentInChildren<TextMeshProUGUI>().rectTransform.sizeDelta=new Vector2(-16,-4);
        hint=MatchHudStyle.Text(journal,"Availability","",17);PlaceText(hint,28,-94);
        var viewport=MatchHudStyle.Rect(journal,"History viewport",Vector2.zero,Vector2.one,new Vector2(-32,-154),new Vector2(0,-31));
        viewport.gameObject.AddComponent<Image>().color=new Color(1,1,1,.01f);viewport.gameObject.AddComponent<RectMask2D>();
        scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.viewport=viewport;
        scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
        content=MatchHudStyle.Rect(viewport,"Rows",Vector2.up,Vector2.one,Vector2.zero,Vector2.zero);content.pivot=new Vector2(.5f,1);
        history=content.gameObject.AddComponent<TextMeshProUGUI>();history.font=elapsed.font;history.fontSize=22;history.color=MatchHudStyle.Ink;
        history.richText=false;history.alignment=TextAlignmentOptions.TopLeft;history.overflowMode=TextOverflowModes.Overflow;history.raycastTarget=false;scroll.content=content;
        latestButton=MatchHudStyle.Button(journal,"Latest","Jump to latest",()=>{followLatest=true;scroll.verticalNormalizedPosition=0;latestButton.gameObject.SetActive(false);});
        MatchHudStyle.Place((RectTransform)latestButton.transform,new Vector2(.5f,0),new Vector2(350,34),new Vector2(0,23));latestButton.gameObject.SetActive(false);
        var latestLabel=latestButton.GetComponentInChildren<TextMeshProUGUI>();latestLabel.fontSize=18;latestLabel.rectTransform.sizeDelta=new Vector2(-16,-4);
        scroll.onValueChanged.AddListener(_=>
        {
            if(updatingHistory)return;
            if(showCaptures)return;
            followLatest=content.rect.height<=scroll.viewport.rect.height||scroll.verticalNormalizedPosition<=.025f;
            latestButton.gameObject.SetActive(!followLatest);
        });
    }
    private static void PlaceText(TextMeshProUGUI text,float height,float y)
    {
        var r=text.rectTransform;r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;
        r.sizeDelta=new Vector2(-32,height);r.anchoredPosition=new Vector2(0,y);
    }
    private static RectTransform Player(Transform parent,string label,out TextMeshProUGUI name,out TextMeshProUGUI state)
    {
        var r=MatchHudStyle.Rect(parent,label,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);MatchHudStyle.Surface(r,false);
        name=MatchHudStyle.Text(r,"Name",label,20);
        name.enableAutoSizing=true;name.fontSizeMin=14;name.fontSizeMax=20;
        name.rectTransform.anchorMin=name.rectTransform.anchorMax=Vector2.one*.5f;
        name.rectTransform.sizeDelta=new Vector2(280,32);name.rectTransform.anchoredPosition=Vector2.zero;
        state=MatchHudStyle.Text(r,"Turn",label,20);
        state.rectTransform.anchorMin=state.rectTransform.anchorMax=Vector2.one*.5f;
        state.rectTransform.sizeDelta=new Vector2(350,27);state.rectTransform.anchoredPosition=new Vector2(0,-18);
        state.color=MatchHudStyle.Muted;state.gameObject.SetActive(false);return r;
    }
    private static TextMeshProUGUI SummaryLabel(RectTransform parent,string name,Vector2 position,Vector2 size)
    {var label=MatchHudStyle.Text(parent,name,"",19);MatchHudStyle.Place(label.rectTransform,Vector2.one*.5f,size,position);return label;}
    private void Layout()
    {
        Canvas.ForceUpdateCanvases();lastSize=root.rect.size;lastPracticeWidth=BotPracticeView.ToolbarWidth(game);
        float width=Mathf.Min(620,lastSize.x-410-lastPracticeWidth),card=(width-10)/2;
        float cardHeight=game.UsesDotNetOnline?78:42;
        MatchHudStyle.Place(top,new Vector2(0,1),new Vector2(width,cardHeight),new Vector2(20+width/2,-10-cardHeight/2));
        MatchHudStyle.Place(whiteCard,new Vector2(0,.5f),new Vector2(card,cardHeight),new Vector2(card/2,0));
        MatchHudStyle.Place(blackCard,new Vector2(1,.5f),new Vector2(card,cardHeight),new Vector2(-card/2,0));
        whiteName.rectTransform.sizeDelta=blackName.rectTransform.sizeDelta=new Vector2(card-24,32);
        whiteState.rectTransform.sizeDelta=blackState.rectTransform.sizeDelta=new Vector2(card-32,36);
        whiteState.fontSize=blackState.fontSize=28;
        whiteState.rectTransform.anchoredPosition=blackState.rectTransform.anchoredPosition=new Vector2(0,-17);
        whiteName.rectTransform.anchoredPosition=blackName.rectTransform.anchoredPosition=new Vector2(0,game.UsesDotNetOnline?19:0);
        whiteState.gameObject.SetActive(game.UsesDotNetOnline);blackState.gameObject.SetActive(game.UsesDotNetOnline);
        statusRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Min(660,lastSize.x-40));
        float jw=Mathf.Min(410,lastSize.x-40),bottom=Mathf.Max(20,game.StatisticsHudBottom+12);
        if(game.IsBotGame)bottom=Mathf.Max(bottom,BotCompanionView.BottomInset(game)+12);
        float cameraHintWidth=Mathf.Min(310,lastSize.x-40);
        MatchHudStyle.Place(cameraHint,Vector2.zero,new Vector2(cameraHintWidth,78),new Vector2(20+cameraHintWidth/2,bottom+39));
        cameraHintState.rectTransform.sizeDelta=new Vector2(cameraHintWidth-24,28);
        cameraHintControls.rectTransform.sizeDelta=new Vector2(cameraHintWidth-24,38);
        float start=game.IsAramGame?138:58,jh=Mathf.Max(190,Mathf.Min(650,lastSize.y-start-bottom));
        MatchHudStyle.Place(journal,Vector2.one,new Vector2(jw,jh),new Vector2(-20-jw/2,-start-jh/2));ApplyPanels();
        lastBottom=game.StatisticsHudBottom;lastAram=game.IsAramGame;lastOnline=game.UsesDotNetOnline;lastBot=game.IsBotGame;
        lastCompanionBottom=BotCompanionView.BottomInset(game);
        if(!cameraOwner){cameraOwner=Camera.main;cameraCaptured=false;}
        if(cameraOwner)
        {
            if(!cameraCaptured){originalCameraRect=cameraOwner.rect;originalFieldOfView=cameraOwner.fieldOfView;cameraCaptured=true;}
            // Keep rendering the entire world. A clipped viewport leaves unrendered bars in Game View.
            cameraOwner.rect=new Rect(0,0,1,1);
            if(!cameraOwner.orthographic&&!cameraOwner.GetComponent<ChessOrbitCamera>())
            {
                float safeFraction=game.IsAramGame?.76f:.88f;
                cameraOwner.fieldOfView=2*Mathf.Atan(Mathf.Tan(originalFieldOfView*Mathf.Deg2Rad*.5f)/safeFraction)*Mathf.Rad2Deg;
            }
        }
        historyLayoutDirty=true;
    }
    private void RestoreCamera(){if(cameraOwner&&cameraCaptured){cameraOwner.rect=originalCameraRect;cameraOwner.fieldOfView=originalFieldOfView;}cameraCaptured=false;}
    private void ApplyPanels()
    {
        top.gameObject.SetActive(!focus||game.UsesDotNetOnline);journal.gameObject.SetActive(expanded);
        buffSummary.gameObject.SetActive(game.IsAramGame);
        float statusWidth=Mathf.Min(620,lastSize.x-410-(focus?lastPracticeWidth:0));
        MatchHudStyle.Place(statusRect,new Vector2(0,1),new Vector2(statusWidth,36),new Vector2(20+statusWidth/2,game.UsesDotNetOnline?-112:focus?-30:-72));
        MatchHudStyle.Place(controls,Vector2.one,new Vector2(330,38),new Vector2(-185,-30));
        ((RectTransform)journalButton.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,38);
        ((RectTransform)focusButton.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,38);
        journalButton.GetComponentInChildren<TextMeshProUGUI>().rectTransform.sizeDelta=new Vector2(-16,-4);
        focusButton.GetComponentInChildren<TextMeshProUGUI>().rectTransform.sizeDelta=new Vector2(-16,-4);
        journalButton.GetComponentInChildren<TextMeshProUGUI>().text=expanded?"Close journal":"Match journal";
        focusButton.GetComponentInChildren<TextMeshProUGUI>().text=focus?"Focus: on":"Focus";
    }
    private void ToggleJournal(){expanded=!expanded;if(expanded)focus=false;SavePreference();ApplyPanels();Refresh();}
    private void ToggleFocus(){focus=!focus;if(focus)expanded=false;SavePreference();ApplyPanels();Refresh();}
    private void SelectJournalTab(bool capturesSelected)
    {if(showCaptures==capturesSelected)return;showCaptures=capturesSelected;historyLayoutDirty=true;followLatest=true;Refresh();}
    private void SavePreference(){PlayerPrefs.SetInt("MatchHud.Journal."+preferenceMode,expanded?1:0);PlayerPrefs.SetInt("MatchHud.Focus."+preferenceMode,focus?1:0);}
    private void RefreshCameraHint()
    {
        if(!cameraHint)return;
        if(!orbitCamera)
        {
            var boardCamera=Camera.main;
            if(boardCamera)orbitCamera=boardCamera.GetComponent<ChessOrbitCamera>();
        }
        bool show=game&&game.GameStarted&&!game.PauseLocked&&orbitCamera&&orbitCamera.IsBoardViewActive;
        cameraHint.gameObject.SetActive(show);
        if(!show)return;
        bool unlocked=orbitCamera.IsOrbitUnlocked;
        string shortcut=ChessButWeird.Settings.SettingsRegistry.BindingKeys[(int)UserSettings.Get("key_orbit")];
        string state=unlocked?"Camera unlocked · "+shortcut+" to lock":"Camera locked · "+shortcut+" to unlock";
        string zoom=UserSettings.Enabled("wheel_zoom")?"Scroll to zoom":"Wheel zoom disabled";
        string drag=UserSettings.Enabled("right_orbit")?(UserSettings.Enabled("middle_orbit")?"Right / middle drag to orbit":"Right drag to orbit"):
            UserSettings.Enabled("middle_orbit")?"Middle drag to orbit":"Orbit drag disabled";
        string controls=unlocked?drag+"\n"+zoom:zoom;
        if(cameraHintState.text==state&&cameraHintControls.text==controls)return;
        cameraHintState.text=state;
        cameraHintState.color=unlocked?MatchHudStyle.Accent:MatchHudStyle.Ink;
        cameraHintControls.text=controls;
        MatchHudStyle.Highlight(cameraHint,unlocked);
    }
    private void Refresh()
    {
        RefreshCameraHint();
        if(!game||!history)return;whiteName.text="WHITE · "+game.StatisticsPlayerName(PieceTeam.White);blackName.text="BLACK · "+game.StatisticsPlayerName(PieceTeam.Black);
        bool ended=game.GameOver;
        whiteState.text=game.UsesDotNetOnline?game.OnlineClockLabel(PieceTeam.White):TurnLabel(PieceTeam.White,ended);
        blackState.text=game.UsesDotNetOnline?game.OnlineClockLabel(PieceTeam.Black):TurnLabel(PieceTeam.Black,ended);
        whiteState.color=game.CurrentTurn==PieceTeam.White&&!ended?MatchHudStyle.Accent:MatchHudStyle.Muted;
        blackState.color=game.CurrentTurn==PieceTeam.Black&&!ended?MatchHudStyle.Accent:MatchHudStyle.Muted;
        MatchHudStyle.Highlight(whiteCard,!ended&&game.CurrentTurn==PieceTeam.White);
        MatchHudStyle.Highlight(blackCard,!ended&&game.CurrentTurn==PieceTeam.Black);
        string state=ended?(game.Status==ChessGame.ChessGameStatus.Draw?"Draw · "+game.DrawReason:game.WinningTeam+" wins"):
            game.PauseLocked?"Paused":game.HasPendingPromotion?"Choose promotion":game.IsCurrentTeamChecked?"CHECK · "+game.CurrentTurn+" King":game.CurrentTurn+" to move";
        if(!ended&&game.OnlineMatchPhase=="AwaitingReady")state="Waiting for players to finish loading/setup";
        status.text=game.StatisticsMode+" · "+state+(game.IsNetworkGame?" · "+game.StatisticsConnectionState:"");
        elapsed.text=game.StatisticsTimeKnown?"Elapsed · "+TimeLabel(game.MatchElapsedSeconds):"Time unavailable";
        if(game.IsAramGame)RefreshBuffSummary();
        if(lastRevision==game.Statistics.Revision&&!historyLayoutDirty)return;
        bool follow=lastRevision<0||followLatest;
        float readingOffset=content.anchoredPosition.y;lastRevision=game.Statistics.Revision;historyLayoutDirty=false;
        updatingHistory=true;
        var builder=new StringBuilder();foreach(var row in game.Statistics.Entries)
        {
            if(builder.Length>0)builder.Append('\n');builder.Append(row.IsMove?MatchHudStyle.MoveCountLabel(row.MoveNumber):"·").Append("  ").Append(row.Actor).Append("  ").Append(row.Text);
            if(row.Pending)builder.Append("  [pending]");
        }
        string captureSummary=game.Statistics.CapturesComplete?"White captured: "+CaptureLabel(PieceTeam.White)+"\nBlack captured: "+CaptureLabel(PieceTeam.Black):"Capture history unavailable\nPosition alone cannot recover captures.";
        if(!game.IsAramGame&&game.Statistics.CapturesComplete)
        {
            int balance=game.GetCapturedMaterialScore(PieceTeam.White)-game.GetCapturedMaterialScore(PieceTeam.Black);
            captureSummary+="\nCaptured material: "+(balance>0?"White +"+balance:balance<0?"Black +"+(-balance):"equal");
        }
        history.text=showCaptures?captureSummary:builder.Length==0?"No moves yet":builder.ToString();
        MatchHudStyle.Highlight((RectTransform)historyTab.transform,!showCaptures);
        MatchHudStyle.Highlight((RectTransform)capturesTab.transform,showCaptures);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,history.GetPreferredValues(history.text,Mathf.Max(80,content.rect.width),0).y+16);
        Canvas.ForceUpdateCanvases();
        if(showCaptures)scroll.verticalNormalizedPosition=1;
        else if(follow)scroll.verticalNormalizedPosition=0;
        else content.anchoredPosition=new Vector2(content.anchoredPosition.x,Mathf.Clamp(readingOffset,0,Mathf.Max(0,content.rect.height-scroll.viewport.rect.height)));
        updatingHistory=false;followLatest=follow;latestButton.gameObject.SetActive(!showCaptures&&!follow&&content.rect.height>scroll.viewport.rect.height);
        hint.text=showCaptures?"Captures exclude sacrifice and destruction":game.Statistics.HistoryComplete?"Move · Side · Notation / event":"Partial history · server sync needed";
    }
    private void RefreshBuffSummary()
    {
        int white=0,black=0;foreach(var row in game.Statistics.Entries)if(row.IsMove){if((PieceTeam)row.Actor==PieceTeam.White)white++;else black++;}
        whiteMoves.text="W · "+(game.Statistics.HistoryComplete?MatchHudStyle.MoveCountLabel(white):"?");
        blackMoves.text="B · "+(game.Statistics.HistoryComplete?MatchHudStyle.MoveCountLabel(black):"?");
        RefreshBuffLabel(PieceTeam.White,whiteBuff);RefreshBuffLabel(PieceTeam.Black,blackBuff);
    }
    private void RefreshBuffLabel(PieceTeam side,TextMeshProUGUI label)
    {
        if(game.TryGetOnlineBuffSummary(side,out string onlineName,out string onlineDetails))
        {
            label.text=onlineName;
            label.GetComponent<MatchHudTooltipTarget>().Configure(tooltip,UserSettings.Enabled("buff_source")?side+" online buff":"Online buff",TooltipPreferences.Description(onlineDetails));
            return;
        }
        var names=new StringBuilder();var details=new StringBuilder();
        if(!game.StatisticsBuffsVisible(side)){label.text="Buff hidden";label.GetComponent<MatchHudTooltipTarget>().Configure(tooltip,"Hidden buff","Your opponent's buff is private.");return;}
        foreach(var buff in game.StatisticsBuffs(side))if(buff)
        {
            if(names.Length>0){names.Append(", ");details.Append("\n\n");}
            names.Append(buff.ShortName);
            details.Append(TooltipPreferences.Buff(buff,game.StatisticsBuffProgress(side,buff.Id)));
        }
        label.text=names.Length>0?names.ToString():"No buff";
        label.GetComponent<MatchHudTooltipTarget>().Configure(tooltip,UserSettings.Enabled("buff_source")?side+" buffs":"Buffs",details.Length>0?details.ToString():"No buff selected.");
    }
    private string TurnLabel(PieceTeam team,bool ended)
    {
        if(ended)return "Match finished";if(game.CurrentTurn!=team)return "Waiting";
        return (game.IsNetworkGame||game.IsBotGame)&&game.PlayerTeam==team?"Your turn":"To move";
    }
    private string CaptureLabel(PieceTeam team)
    {
        var pieces=game.GetCapturedPieces(team);if(pieces.Count==0)return "—";var text=new StringBuilder();
        foreach(PieceType type in new[]{PieceType.Pawn,PieceType.Knight,PieceType.Bishop,PieceType.Rook,PieceType.Queen})
        {int count=0;foreach(var p in pieces)if(p==type)count++;if(count==0)continue;if(text.Length>0)text.Append("  ");text.Append(type==PieceType.Knight?"N":type.ToString().Substring(0,1)).Append(" ×").Append(count);}
        return text.ToString();
    }
    private static string TimeLabel(float seconds){var t=TimeSpan.FromSeconds(Mathf.Max(0,seconds));return t.TotalHours>=1?t.ToString(@"hh\:mm\:ss"):t.ToString(@"mm\:ss");}
}
