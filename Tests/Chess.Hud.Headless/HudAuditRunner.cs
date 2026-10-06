using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ChessButWeird.Domain;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class HudAuditRunner
{
    private static readonly List<string> report = new List<string>();
    private static int ticks;
    private static int checks;
    private static string reportPath;
    public static void Run()
    {
        // This helper must never run in the user's game project.
        if (!Application.isBatchMode || SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null ||
            !Application.dataPath.Replace('\\','/').Contains("/Logs/hud-headless/"))
            throw new InvalidOperationException("Requires the isolated headless fixture project and -nographics.");
        reportPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "audit-result.txt");
        // Preserve this explicitly registered callback through the test project's
        // Play transition. These options are never written to the game project.
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorApplication.update += WaitForRuntime;
        EditorApplication.EnterPlaymode();
    }
    private static void WaitForRuntime()
    {
        if (!EditorApplication.isPlaying || ++ticks < 12) return;
        EditorApplication.update -= WaitForRuntime;
        try { Audit(); report.Add("PASS " + checks + " headless HUD assertions. No graphics rendering or server/gameplay coverage."); Finish(0); }
        catch (Exception ex) { report.Add("FAIL " + ex); Finish(1); }
    }
    private static void Finish(int code)
    {
        File.WriteAllLines(reportPath, report);
        Debug.Log(string.Join("\n", report));
        EditorApplication.Exit(code);
    }
    private static T Field<T>(object source, string name) => (T)source.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(source);
    private static void Set(object source, string name, object value) => source.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(source,value);
    private static void Call(object source, string name) => source.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(source,null);
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void CanvasSize(GameObject source, Vector2 size)
    {
        foreach (var canvas in source.GetComponentsInChildren<Canvas>(true))
        {
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = canvas.GetComponent<CanvasScaler>(); if (scaler) scaler.enabled = false;
            var rect=(RectTransform)canvas.transform;rect.sizeDelta=size;rect.position=Vector3.zero;rect.localScale=Vector3.one;
        }
        Canvas.ForceUpdateCanvases();
    }
    private static Rect Bounds(RectTransform rect)
    {
        var corners=new Vector3[4];rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
    }
    private static void Within(RectTransform child,RectTransform parent,string label)
    {
        var a=Bounds(child);var b=Bounds(parent);
        Check(a.xMin>=b.xMin-1&&a.yMin>=b.yMin-1&&a.xMax<=b.xMax+1&&a.yMax<=b.yMax+1,label+" outside parent: "+a+" parent "+b);
    }
    private static void TextFits(TMP_Text text, string label)
    {
        text.ForceMeshUpdate(true,true);
        Check(text.textInfo.characterCount>0,label+" generated no characters; font="+text.font.name+", rect="+text.rectTransform.rect+", preferred="+text.GetPreferredValues(text.text));
        Check(!text.isTextOverflowing,label+" text clipped: "+text.text+" rect "+text.rectTransform.rect);
    }
    private static void Audit()
    {
        var camera=new GameObject("Fixture Camera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=60;
        var host=new GameObject("HUD fixture");var game=host.AddComponent<ChessGame>();
        var view=host.AddComponent<AnalysisBoardView>();view.Initialize(game);
        Check(!Field<GameObject>(view,"canvasObject").activeSelf,"Initialization left the HUD visible");
        view.SetVisible(true);
        var runtime=host.AddComponent<AramBuffRuntime>();var abilities=host.AddComponent<AramAbilityView>();abilities.Initialize(runtime);
        var buffs=host.AddComponent<AramBuffDraftView>();var definition=ScriptableObject.CreateInstance<AramBuffDefinition>();
        definition.Configure(AramBuffId.GachaBanner,AramBuffTier.Gold,"Gacha Banner","Battle Gacha","",Color.magenta);
        buffs.ShowHud(new[]{definition},new[]{definition});
        runtime.WhiteBuffs.Add(definition);runtime.BlackBuffs.Add(definition);
        Check(!Field<bool>(abilities,"expanded"),"Buff controls do not default to summary mode");
        foreach(var resolution in new[]{new Vector2(640,480),new Vector2(800,600),new Vector2(1168,541),new Vector2(1280,720),new Vector2(1366,768),new Vector2(1920,1080),new Vector2(1024,768),new Vector2(2560,1440),new Vector2(2560,1080),new Vector2(3440,1440),new Vector2(3840,2160),new Vector2(1080,1920)})
        {
            float scale=Mathf.Min(resolution.x/1280,resolution.y/720);var size=resolution/scale;
            CanvasSize(host,size);
            foreach(bool aram in new[]{false,true})
            {
                game.IsAramGame=aram;Set(view,"focus",false);Set(view,"expanded",true);Call(view,"Layout");Call(view,"Refresh");Canvas.ForceUpdateCanvases();
                var root=Field<RectTransform>(view,"root");
                foreach(string item in new[]{"top","statusRect","controls","journal"})Within(Field<RectTransform>(view,item),root,resolution+" "+item);
                foreach(string item in new[]{"whiteName","blackName","status","elapsed","hint"})TextFits(Field<TextMeshProUGUI>(view,item),resolution+" "+item);
                float fov=camera.fieldOfView;
                Field<Button>(view,"focusButton").onClick.Invoke();
                Check(!Field<RectTransform>(view,"top").gameObject.activeSelf,"Focus left player cards visible");
                Check(Field<RectTransform>(view,"statusRect").gameObject.activeSelf,"Focus hid mandatory status");
                Check(Mathf.Approximately(fov,camera.fieldOfView),"Focus changed camera framing");
                Field<Button>(view,"journalButton").onClick.Invoke();
                Check(Mathf.Approximately(fov,camera.fieldOfView),"Journal changed camera framing");
            }
            foreach(bool expandedBuffs in new[]{false,true})
            for(int count=0;count<=10;count++)
            {
                Set(abilities,"expanded",expandedBuffs);
                runtime.Actions.Clear();for(int i=0;i<count;i++)runtime.Actions.Add(new AramAbilityView.AbilityAction(i%2==0?"Gacha (64 tickets)":"Swap movement",()=>{},true,i%2==0?"Turn limit reached":"5 owner turns"));
                Set(abilities,"nextRefresh",0f);Call(abilities,"Update");runtime.AbilityPanelHeight=abilities.PanelHeight;Call(buffs,"Update");Canvas.ForceUpdateCanvases();
                var panel=Field<RectTransform>(abilities,"actionPanel");
                Within(panel,Field<RectTransform>(abilities,"frame"),resolution+" actions "+count);
                TextFits(Field<TextMeshProUGUI>(abilities,"status"),resolution+" action status "+count);
                TextFits(Field<TextMeshProUGUI>(abilities,"summary"),resolution+" buff summary "+count);
                Check(Field<GameObject>(abilities,"root").activeSelf&&abilities.PanelHeight>0,"Passive buff panel disappeared");
                var actionScroll=Field<ScrollRect>(abilities,"actionScroll");
                Within(actionScroll.viewport,panel,"Buff scroll viewport");
                if(count>0||expandedBuffs)Check(actionScroll.viewport.rect.height>20,"Buff controls have no usable scroll area");
                if(expandedBuffs)TextFits(Field<TextMeshProUGUI>(abilities,"details"),"Expanded buff description");
                foreach(var button in Field<List<Button>>(abilities,"buttons"))if(button.gameObject.activeSelf)
                {Within((RectTransform)button.transform,actionScroll.content,"Ability button");TextFits(button.GetComponentInChildren<TextMeshProUGUI>(),"Ability label");}
                Check(buffs.transform.Find("ARAM Buff HUD Canvas")==null,"Legacy bottom buff strip is still allocated");
                game.IsAramGame=true;game.AramHudBottom=buffs.GameplayHudTop;Set(view,"focus",false);Set(view,"expanded",true);Call(view,"Layout");Call(view,"Refresh");
                Check(Bounds(Field<RectTransform>(view,"journal")).xMin>=Bounds(panel).xMax+11,"ARAM journal overlaps left buff controls");
                Check(scrollHeight(view)>26,"ARAM journal has no readable history viewport");
                Within(Field<RectTransform>(view,"buffSummary"),Field<RectTransform>(view,"root"),"Compact buff summary");
                TextFits(Field<TextMeshProUGUI>(view,"whiteMoves"),"White move count");TextFits(Field<TextMeshProUGUI>(view,"blackMoves"),"Black move count");
            }
            buffs.ShowBuffToast(PieceTeam.White,"Rifle hit.",null);Call(buffs,"Update");
            Within(Field<RectTransform>(buffs,"toastPanel"),Field<RectTransform>(buffs,"gameplayFrame"),"Left buff notification");
            buffs.ShowTargetPrompt(PieceTeam.White,"Choose your Bishop.",null);Call(buffs,"Update");
            Within(Field<RectTransform>(buffs,"targetPromptPanel"),Field<RectTransform>(buffs,"gameplayFrame"),"Left setup prompt");
            buffs.ShowHud(new[]{definition},new[]{definition});
            var safeRects=new[]{Field<RectTransform>(view,"root"),Field<RectTransform>(abilities,"frame"),Field<RectTransform>(buffs,"gameplayFrame")};
            foreach(var safeRect in safeRects){safeRect.anchorMin=new Vector2(.03f,.08f);safeRect.anchorMax=new Vector2(.95f,.94f);}
            Canvas.ForceUpdateCanvases();Call(view,"Layout");Set(abilities,"nextRefresh",0f);Call(abilities,"Update");Call(buffs,"Update");Canvas.ForceUpdateCanvases();
            Within(Field<RectTransform>(view,"journal"),safeRects[0],"Inset journal");
            Within(Field<RectTransform>(abilities,"actionPanel"),safeRects[1],"Inset buff controls");
            Within(Field<RectTransform>(buffs,"toastPanel"),safeRects[2],"Inset toast");
            var centre=Bounds((RectTransform)Field<GameObject>(abilities,"root").transform).center;
            Check(Vector2.Distance(Bounds(Field<TextMeshProUGUI>(abilities,"crosshair").rectTransform).center,centre)<1,"Safe area shifted crosshair away from camera centre");
            foreach(var safeRect in safeRects){safeRect.anchorMin=Vector2.zero;safeRect.anchorMax=Vector2.one;}
            Canvas.ForceUpdateCanvases();
            report.Add("PASS layout/text: "+resolution+", Classic / ARAM, compact / expanded buff controls, actions 0..10");
        }
        runtime.AbilityInstructionRequired=false;
        var buffScroll=Field<ScrollRect>(abilities,"actionScroll");buffScroll.verticalNormalizedPosition=.5f;
        float buffOffset=buffScroll.content.anchoredPosition.y;
        runtime.AbilityContext="changed-statistics-revision";Set(abilities,"nextRefresh",0f);Call(abilities,"Update");Canvas.ForceUpdateCanvases();
        Check(Mathf.Abs(buffScroll.content.anchoredPosition.y-buffOffset)<1,"Statistics revision reset the expanded buff reading position");
        runtime.Actions.Clear();
        for(int turn=0;turn<100;turn++)
        {
            runtime.AbilityContext="turn-"+turn;abilities.SetVisible(true);
            Check(Field<GameObject>(abilities,"root").activeSelf,"Turn start pulsed canvas visibility");
            Set(abilities,"nextRefresh",Time.unscaledTime+10);Call(abilities,"Update");
            Check(Field<GameObject>(abilities,"root").activeSelf,"Throttled update hid passive panel");
            Set(abilities,"nextRefresh",0f);Call(abilities,"Update");
            Check(Field<GameObject>(abilities,"root").activeSelf,"Empty action update hid passive panel");
        }
        runtime.AbilityHudVisible=false;Call(abilities,"Update");Check(!Field<GameObject>(abilities,"root").activeSelf,"Paused HUD remained interactive");
        runtime.AbilityHudVisible=true;Set(abilities,"nextRefresh",0f);Call(abilities,"Update");
        Check(Field<GameObject>(abilities,"root").activeSelf,"Resume did not restore buff panel");
        runtime.RifleActive=true;Set(abilities,"nextRefresh",0f);Call(abilities,"Update");
        Check(Field<TextMeshProUGUI>(abilities,"crosshair").gameObject.activeSelf&&Field<TextMeshProUGUI>(abilities,"rifleHint").text.Contains("ALT"),"FPS crosshair or ALT hint missing");
        runtime.RifleActive=false;Set(abilities,"nextRefresh",0f);Call(abilities,"Update");
        Check(!Field<TextMeshProUGUI>(abilities,"crosshair").gameObject.activeSelf,"Crosshair persisted after FPS exit");
        Field<Button>(abilities,"expandButton").onClick.Invoke();
        Check(!Field<bool>(abilities,"expanded"),"Collapse button did not change details mode");
        report.Add("PASS persistent passive panel through 100 turn contexts, pause/resume, FPS hint and expansion");
        Set(view,"focus",false);Set(view,"expanded",true);Call(view,"ApplyPanels");
        for(int i=0;i<70;i++)game.Statistics.AddLocal(i%2==0?Team.White:Team.Black,"e2-e4",false,null);
        Call(view,"Refresh");var scroll=Field<ScrollRect>(view,"scroll");scroll.verticalNormalizedPosition=.65f;Canvas.ForceUpdateCanvases();
        // ScrollRect emits this during LateUpdate after a user drag; the test
        // runs synchronously and must deliver that event before the next move.
        scroll.onValueChanged.Invoke(new Vector2(0,.65f));
        var content=Field<RectTransform>(view,"content");float offset=content.anchoredPosition.y;
        game.Statistics.AddLocal(Team.White,"a2-a4",false,null);Call(view,"Refresh");
        Check(Mathf.Abs(content.anchoredPosition.y-offset)<1,"Appending a move moved the reading offset");
        Check(Field<Button>(view,"latestButton").gameObject.activeSelf,"Scrolled journal lacks jump button");
        Field<Button>(view,"latestButton").onClick.Invoke();Check(scroll.verticalNormalizedPosition<.01f,"Jump did not reach latest");
        report.Add("PASS journal scroll/follow/latest and camera toggle invariance");
        int oldInvoked=0,newInvoked=0;
        runtime.Actions.Clear();runtime.Actions.Add(new AramAbilityView.AbilityAction("Old",()=>oldInvoked++,true,"Ready","old"));
        Set(abilities,"nextRefresh",0f);Call(abilities,"Update");
        runtime.Actions.Clear();runtime.Actions.Add(new AramAbilityView.AbilityAction("New",()=>newInvoked++,true,"Ready","new"));
        Field<List<Button>>(abilities,"buttons")[0].onClick.Invoke();Check(oldInvoked==0&&newInvoked==0,"Stale button invoked a different ability");
        runtime.Actions.Clear();runtime.Actions.Add(new AramAbilityView.AbilityAction("Old renamed",()=>oldInvoked++,true,"Ready","old"));
        Field<List<Button>>(abilities,"buttons")[0].onClick.Invoke();Check(oldInvoked==1,"Stable action ID did not resolve updated label");
        runtime.AbilityContext="turn-2";Field<List<Button>>(abilities,"buttons")[0].onClick.Invoke();Check(oldInvoked==1,"Stale turn context executed an ability");
        report.Add("PASS stale ability identity / context guards");
        var counter=typeof(AnalysisBoardView).Assembly.GetType("MatchHudStyle").GetMethod("MoveCountLabel",BindingFlags.Static|BindingFlags.NonPublic);
        Check((string)counter.Invoke(null,new object[]{99})=="99"&&(string)counter.Invoke(null,new object[]{100})=="99+"&&(string)counter.Invoke(null,new object[]{500})=="99+","Move display does not cap at 99+");
        game.IsAramGame=true;Call(view,"Refresh");
        for(int i=0;i<101;i++)game.Statistics.AddLocal(Team.White,"e2-e4",false,null);Call(view,"Refresh");
        Check(Field<TextMeshProUGUI>(view,"whiteMoves").text.Contains("99+")&&game.Statistics.ConfirmedMoveNumber>99,"Capping display discarded underlying move history");
        var tooltip=Field<MatchHudTooltip>(view,"tooltip");var buffLabel=Field<TextMeshProUGUI>(view,"whiteBuff");
        buffLabel.GetComponent<MatchHudTooltipTarget>().OnPointerEnter(null);
        Check(Field<RectTransform>(tooltip,"panel").gameObject.activeSelf,"Hover did not open buff information");
        runtime.BuffProgressText="Tickets: 13 · Drop: 0/1";Call(view,"Refresh");
        Check(Field<TextMeshProUGUI>(tooltip,"body").text.Contains("Tickets: 13"),"Hovered buff information did not update live progress");
        Within(Field<RectTransform>(tooltip,"panel"),Field<RectTransform>(view,"root"),"Buff tooltip");
        buffLabel.GetComponent<MatchHudTooltipTarget>().OnPointerExit(null);Check(!Field<RectTransform>(tooltip,"panel").gameObject.activeSelf,"Tooltip persisted after pointer exit");

        runtime.Disguised=true;game.IsBotGame=true;game.PlayerTeam=PieceTeam.White;game.CurrentTurn=PieceTeam.Black;
        Call(view,"Refresh");Check(runtime.LastViewer==PieceTeam.White,"Solo disguise viewer followed the bot's turn");
        Field<TextMeshProUGUI>(view,"blackBuff").GetComponent<MatchHudTooltipTarget>().OnPointerEnter(null);
        Check(!Field<TextMeshProUGUI>(tooltip,"body").text.Contains("Tickets"),"Disguised opponent leaked actual live resources");tooltip.Hide();
        game.IsBotGame=false;Call(view,"Refresh");Check(runtime.LastViewer==PieceTeam.Black,"Hotseat disguise viewer did not follow the active player");
        Check(game.StatisticsBuffProgress(PieceTeam.White,AramBuffId.GachaBanner)==string.Empty&&game.StatisticsBuffProgress(PieceTeam.Black,AramBuffId.GachaBanner).Contains("Tickets"),"Hotseat disguise progress has wrong owner scope");
        runtime.Disguised=false;game.CurrentTurn=PieceTeam.White;
        game.MarkStatisticsTimeUnavailable();Call(view,"Refresh");TextFits(Field<TextMeshProUGUI>(view,"elapsed"),"Unknown elapsed time");
        Field<Button>(view,"capturesTab").onClick.Invoke();Check(Field<TextMeshProUGUI>(view,"history").text.StartsWith("White captured:"),"Captures tab is not showing captures");
        TextFits(Field<TextMeshProUGUI>(view,"hint"),"Capture semantics hint");
        Field<Button>(view,"historyTab").onClick.Invoke();Check(Field<TextMeshProUGUI>(view,"history").text.Contains("a2-a4"),"History tab lost journal rows");
        view.SetVisible(false);Check(Mathf.Approximately(camera.fieldOfView,60)&&camera.rect==new Rect(0,0,1,1),"Camera not restored after hide");
        view.SetVisible(true);UnityEngine.Object.DestroyImmediate(camera.gameObject);
        var replacement=new GameObject("Replacement fixture camera",typeof(Camera)).GetComponent<Camera>();replacement.tag="MainCamera";replacement.fieldOfView=48;replacement.rect=new Rect(.1f,.1f,.8f,.8f);
        Call(view,"Layout");view.SetVisible(false);
        Check(Mathf.Approximately(replacement.fieldOfView,48)&&replacement.rect==new Rect(.1f,.1f,.8f,.8f),"Replacement camera restored another camera's settings");
        AuditSnapshots(game);
        AuditDraft(buffs);
    }
    private static void AuditDraft(AramBuffDraftView view)
    {
        var definitions=AramBuffLibrary.CreateBuiltInDefinitions();
        view.ShowDraft(definitions,(_,__)=>{});
        CanvasSize(view.gameObject,new Vector2(1280,720));
        var cards=Field<RectTransform>(view,"cardRoot");var scroll=Field<ScrollRect>(view,"cardScroll");
        Check(scroll.vertical,"Long buff library cannot scroll");
        Check(cards.childCount==definitions.Count,"Draft is missing buff choices");
        foreach(RectTransform card in cards)
        {
            foreach(var text in card.GetComponentsInChildren<TextMeshProUGUI>())
            {
                Check(text.font==ChessFontCatalog.TmpFont,"Draft uses a different font");
                TextFits(text,"Draft "+card.name+" / "+text.name);
            }
            var description=card.GetComponentInChildren<ScrollRect>();
            Check(description.content.rect.height>=description.viewport.rect.height,"Description loses long content");
        }
        view.ShowPracticeRoll(definitions.GetRange(0,3),definitions.GetRange(3,3),(_,__)=>{});
        Canvas.ForceUpdateCanvases();
        Check(!scroll.vertical,"Three-card roll unnecessarily scrolls");
        foreach(RectTransform card in cards)if(card.gameObject.activeSelf)Within(card,scroll.viewport,"Draft choice");
        TextFits(Field<TextMeshProUGUI>(view,"titleLabel"),"Draft title");
        TextFits(Field<TextMeshProUGUI>(view,"subtitleLabel"),"Draft subtitle");
        view.HideAll();
        foreach(var d in definitions)UnityEngine.Object.Destroy(d);
        report.Add("PASS themed draft: complete library, readable scrollable descriptions, Schoolbell font and three-card bounds");
    }
    private static float scrollHeight(AnalysisBoardView view) => Field<ScrollRect>(view,"scroll").viewport.rect.height;
    private static void AuditSnapshots(ChessGame game)
    {
        Call(game,"ResetStatistics");
        game.MarkStatisticsRecoveryIncomplete(4);
        Check(!game.StatisticsTimeKnown&&!game.Statistics.HistoryComplete&&!game.Statistics.CapturesComplete,
            "Recovery fabricated complete history/time");
        report.Add("PASS production statistics adapter: incomplete recovery");
    }
}
