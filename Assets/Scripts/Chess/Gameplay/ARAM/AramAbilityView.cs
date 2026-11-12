using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Buff actions docked below the single right-side CHAOS summary.</summary>
public sealed class AramAbilityView : MonoBehaviour
{
    public readonly struct AbilityAction
    {
        public readonly string Id, Label, Detail;
        public readonly Action Invoke;
        public readonly bool Enabled;
        public AbilityAction(string label, Action invoke, bool enabled=true, string detail=null, string id=null)
        { Id=id??label; Label=label; Invoke=invoke; Enabled=enabled; Detail=detail; }
    }
    public sealed class Presentation
    {
        public bool Visible, RifleActive, InstructionRequired;
        public string Context, PresentationContext, Summary, Status, Details, RifleHint;
        public readonly List<AbilityAction> Actions = new List<AbilityAction>();
    }
    private Func<Presentation> onlinePresentation;
    private Func<bool> onlineVisible;
    private AramBuffRuntime runtime;
    public void InitializeOnline(Func<Presentation> source, Func<bool> visibility)
    {
        onlinePresentation = source;
        onlineVisible = visibility;
        Initialize(null);
    }
    public bool ContainsPointer(Vector2 screen) => root && root.activeInHierarchy &&
        RectTransformUtility.RectangleContainsScreenPoint(actionPanel, screen);

    private Presentation ReadPresentation()
    {
        if (onlinePresentation != null) return onlinePresentation();
        if (!runtime) return null;
        var value = new Presentation {
            Visible = runtime.AbilityHudVisible, RifleActive = runtime.RifleActive,
            InstructionRequired = runtime.AbilityInstructionRequired,
            Context = runtime.AbilityContext, Summary = runtime.AbilityBuffSummary,
            Status = runtime.AbilityStatus, Details = runtime.AbilityBuffDetails,
            RifleHint = runtime.RifleMouseHint
        };
        runtime.GetAbilityActions(value.Actions);
        return value;
    }
    private GameObject root;
    private TextMeshProUGUI status, summary, details, crosshair, rifleHint;
    private readonly List<Button> buttons=new List<Button>();
    private readonly List<AbilityAction> actions=new List<AbilityAction>();
    private RectTransform actionPanel, frame, actionContent;
    private ScrollRect actionScroll;
    private Button expandButton;
    private string displayedContext, displayedPresentation;
    private bool requestedVisible=true, expanded;
    private float nextRefresh;
    public float PanelHeight => root&&root.activeSelf?actionPanel.rect.height:0;
    public void SetVisible(bool visible)
    {
        if (requestedVisible == visible) return;
        requestedVisible=visible;
        nextRefresh=0;
        if(!visible){expanded=false;if(root)root.SetActive(false);}
    }
    public void Initialize(AramBuffRuntime source)
    {
        if (root) return;
        runtime=source;
        root=new GameObject("CHAOS Abilities",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root.transform.SetParent(transform,false);
        root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder=90;
        ResponsiveUi.ConfigureCanvasScaler(root.GetComponent<CanvasScaler>(),new Vector2(1280,720));
        root.AddComponent<SettingsUiScale>();
        frame=MatchHudStyle.Rect(root.transform,"CHAOS Actions",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        frame.gameObject.AddComponent<ResponsiveSafeArea>();
        // Aim follows the camera viewport centre even with asymmetric safe areas.
        crosshair=Label(root.transform,"Rifle Crosshair",38);crosshair.text="+";
        MatchHudStyle.Place(crosshair.rectTransform,Vector2.one*.5f,new Vector2(60,60),Vector2.zero);
        crosshair.alignment=TextAlignmentOptions.Center;
        rifleHint=Label(frame,"Rifle Mouse Hint",20);
        MatchHudStyle.Place(rifleHint.rectTransform,new Vector2(0,1),new Vector2(350,36),new Vector2(195,-112));
        actionPanel=MatchHudStyle.Rect(frame,"Action Panel",Vector2.zero,Vector2.zero,new Vector2(300,180),Vector2.zero);
        MatchHudStyle.Surface(actionPanel);
        var title=Label(actionPanel,"Title",22);title.text="USE BUFF";
        MatchHudStyle.Place(title.rectTransform,new Vector2(0,1),new Vector2(130,34),new Vector2(81,-25));
        expandButton=MatchHudStyle.Button(actionPanel,"Expand","Expand",()=>{expanded=!expanded;nextRefresh=0;});
        MatchHudStyle.Place((RectTransform)expandButton.transform,Vector2.one,new Vector2(108,34),new Vector2(-68,-25));
        expandButton.GetComponentInChildren<TextMeshProUGUI>().rectTransform.sizeDelta=new Vector2(-12,-4);
        summary=Label(actionPanel,"Buff summary",20);
        summary.gameObject.SetActive(false);
        status=Label(actionPanel,"Status",19);
        var viewport=MatchHudStyle.Rect(actionPanel,"Actions viewport",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        viewport.gameObject.AddComponent<Image>().color=Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        actionScroll=viewport.gameObject.AddComponent<ScrollRect>();
        actionScroll.viewport=viewport;actionScroll.horizontal=false;actionScroll.vertical=true;
        actionScroll.movementType=ScrollRect.MovementType.Clamped;actionScroll.scrollSensitivity=30;
        actionContent=MatchHudStyle.Rect(viewport,"Actions content",Vector2.up,Vector2.one,Vector2.zero,Vector2.zero);
        actionContent.pivot=new Vector2(.5f,1);actionScroll.content=actionContent;
        details=Label(actionContent,"Buff details",19);details.overflowMode=TextOverflowModes.Overflow;
        crosshair.gameObject.SetActive(false);rifleHint.gameObject.SetActive(false);
        root.SetActive(false);
    }
    private void Update()
    {
        if(!root)return;
        bool visible=requestedVisible&&(runtime?runtime.AbilityHudVisible:onlineVisible?.Invoke()==true);
        if(!visible){if(root.activeSelf)root.SetActive(false);return;}
        // Only this view owns visibility. Turn changes never pulse an empty panel.
        if(!root.activeSelf)root.SetActive(true);
        if(Time.unscaledTime<nextRefresh)return;
        nextRefresh=Time.unscaledTime+.1f;
        var presentation=ReadPresentation();
        if (presentation == null) return;
        string context=presentation.Context;
        string presentationContext=runtime?runtime.AbilityPresentationContext:presentation.PresentationContext??context;
        bool contextChanged=displayedPresentation!=presentationContext;
        displayedPresentation=presentationContext;
        displayedContext=context;
        actions.Clear(); actions.AddRange(presentation.Actions);
        summary.text=presentation.Summary;
        status.text=presentation.InstructionRequired?presentation.Status:actions.Count==0?"Passive buff":"";
        details.text=expanded?TooltipPreferences.Description(presentation.Details):"";
        crosshair.gameObject.SetActive(presentation.RifleActive);
        rifleHint.gameObject.SetActive(presentation.RifleActive);
        rifleHint.text=presentation.RifleHint;
        expandButton.GetComponentInChildren<TextMeshProUGUI>().text=expanded?"Collapse":"Expand";
        while(buttons.Count<actions.Count)
        {
            int index=buttons.Count;
            var button=MatchHudStyle.Button(actionContent,"Ability "+index,"",()=>
            {
                var fresh=ReadPresentation();
                if(!requestedVisible||fresh==null||!fresh.Visible||displayedContext!=fresh.Context||index>=actions.Count)return;
                string id=actions[index].Id;
                foreach(var current in fresh.Actions)
                    if(current.Id==id){if(current.Enabled)current.Invoke?.Invoke();break;}
            });
            button.GetComponentInChildren<TextMeshProUGUI>().rectTransform.sizeDelta=new Vector2(-16,-4);
            buttons.Add(button);
        }
        float width=Mathf.Min(AnalysisBoardView.SidebarWidth,frame.rect.width-24);
        float statusHeight=string.IsNullOrEmpty(status.text)?0:Mathf.Clamp(status.GetPreferredValues(status.text,width-32,0).y+6,28,presentation.InstructionRequired?110:36);
        float header=48+statusHeight+8;
        // Long summaries are available without clipping inside the expanded scroll.
        if(expanded)details.text=presentation.Summary+"\n\n"+TooltipPreferences.Description(presentation.Details);
        float detailHeight=expanded?details.GetPreferredValues(details.text,width-48,0).y+16:0;
        float cellHeight=54, gap=6;
        float contentHeight=detailHeight+actions.Count*(cellHeight+gap);
        var game=GetComponentInParent<ChessGame>();
        float bottom=game&&game.IsBotGame?Mathf.Max(12,BotCompanionView.BottomInset(game)+12):game&&game.UsesDotNetOnline?120:12;
        float start=AnalysisBoardView.BuffSummaryBottom+6;
        float height=Mathf.Min(header+Mathf.Min(contentHeight,expanded?360:180)+12,Mathf.Max(header+76,frame.rect.height-start-bottom));
        MatchHudStyle.Place(actionPanel,Vector2.one,new Vector2(width,height),new Vector2(-AnalysisBoardView.SidebarMargin-width/2,-start-height/2));
        MatchHudStyle.Place(status.rectTransform,new Vector2(.5f,1),new Vector2(width-32,statusHeight),new Vector2(0,-48-statusHeight/2));
        status.gameObject.SetActive(statusHeight>0);
        var viewportRect=actionScroll.viewport;
        viewportRect.gameObject.SetActive(contentHeight>0);
        viewportRect.offsetMin=new Vector2(12,12);viewportRect.offsetMax=new Vector2(-12,-header);
        actionContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,contentHeight);
        details.gameObject.SetActive(expanded);
        MatchHudStyle.Place(details.rectTransform,new Vector2(.5f,1),new Vector2(width-48,detailHeight),new Vector2(0,-detailHeight/2));
        for(int i=0;i<buttons.Count;i++)
        {
            buttons[i].gameObject.SetActive(i<actions.Count);
            if(i>=actions.Count)continue;
            MatchHudStyle.Place((RectTransform)buttons[i].transform,new Vector2(.5f,1),new Vector2(width-32,cellHeight),
                new Vector2(0,-detailHeight-cellHeight/2-i*(cellHeight+gap)));
            buttons[i].interactable=actions[i].Enabled;
            var label=buttons[i].GetComponentInChildren<TextMeshProUGUI>();
            label.fontSize=17;
            label.text=actions[i].Label+(!string.IsNullOrEmpty(actions[i].Detail)?"\n"+actions[i].Detail:"");
        }
        if(contextChanged)actionScroll.verticalNormalizedPosition=1;
    }
    private void OnDestroy(){if(root)Destroy(root);}
    private static TextMeshProUGUI Label(Transform parent,string name,int size)
    {var text=MatchHudStyle.Text(parent,name,"",size);text.alignment=TextAlignmentOptions.MidlineLeft;return text;}
}
