using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Persistent left-corner buff controls with optional details.</summary>
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
    private AramBuffRuntime runtime;
    private GameObject root;
    private TextMeshProUGUI status, summary, details, crosshair, rifleHint;
    private readonly List<Button> buttons=new List<Button>();
    private readonly List<AbilityAction> actions=new List<AbilityAction>(), freshActions=new List<AbilityAction>();
    private RectTransform actionPanel, frame, actionContent;
    private ScrollRect actionScroll;
    private Button expandButton;
    private string displayedContext, displayedPresentation;
    private bool requestedVisible=true, expanded;
    private float nextRefresh;
    public float PanelHeight => root&&root.activeSelf?actionPanel.rect.height:0;
    public void SetVisible(bool visible)
    {
        requestedVisible=visible;
        nextRefresh=0;
        if(!visible){expanded=false;if(root)root.SetActive(false);}
    }
    public void Initialize(AramBuffRuntime source)
    {
        runtime=source;
        root=new GameObject("ARAM Abilities",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root.transform.SetParent(transform,false);
        root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder=90;
        ResponsiveUi.ConfigureCanvasScaler(root.GetComponent<CanvasScaler>(),new Vector2(1280,720));
        frame=MatchHudStyle.Rect(root.transform,"ARAM Actions",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        frame.gameObject.AddComponent<ResponsiveSafeArea>();
        // Aim follows the camera viewport centre even with asymmetric safe areas.
        crosshair=Label(root.transform,"Rifle Crosshair",38);crosshair.text="+";
        MatchHudStyle.Place(crosshair.rectTransform,Vector2.one*.5f,new Vector2(60,60),Vector2.zero);
        crosshair.alignment=TextAlignmentOptions.Center;
        rifleHint=Label(frame,"Rifle Mouse Hint",20);
        MatchHudStyle.Place(rifleHint.rectTransform,new Vector2(0,1),new Vector2(350,36),new Vector2(195,-112));
        actionPanel=MatchHudStyle.Rect(frame,"Action Panel",Vector2.zero,Vector2.zero,new Vector2(300,180),Vector2.zero);
        MatchHudStyle.Surface(actionPanel);
        var title=Label(actionPanel,"Title",22);title.text="BUFFS";
        MatchHudStyle.Place(title.rectTransform,new Vector2(0,1),new Vector2(130,34),new Vector2(81,-25));
        expandButton=MatchHudStyle.Button(actionPanel,"Expand","Expand",()=>{expanded=!expanded;nextRefresh=0;});
        MatchHudStyle.Place((RectTransform)expandButton.transform,Vector2.one,new Vector2(108,34),new Vector2(-68,-25));
        expandButton.GetComponentInChildren<TextMeshProUGUI>().rectTransform.sizeDelta=new Vector2(-12,-4);
        summary=Label(actionPanel,"Buff summary",20);
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
        if(!root||!runtime)return;
        bool visible=requestedVisible&&runtime.AbilityHudVisible;
        if(!visible){if(root.activeSelf)root.SetActive(false);return;}
        // Only this view owns visibility. Turn changes never pulse an empty panel.
        if(!root.activeSelf)root.SetActive(true);
        if(Time.unscaledTime<nextRefresh)return;
        nextRefresh=Time.unscaledTime+.1f;
        string context=runtime.AbilityContext;
        bool contextChanged=displayedPresentation!=runtime.AbilityPresentationContext;
        displayedPresentation=runtime.AbilityPresentationContext;
        displayedContext=context;
        runtime.GetAbilityActions(actions);
        summary.text=runtime.AbilityBuffSummary;
        status.text=runtime.AbilityStatus;
        details.text=expanded?runtime.AbilityBuffDetails:"";
        crosshair.gameObject.SetActive(runtime.RifleActive);
        rifleHint.gameObject.SetActive(runtime.RifleActive);
        rifleHint.text=runtime.RifleMouseHint;
        expandButton.GetComponentInChildren<TextMeshProUGUI>().text=expanded?"Collapse":"Expand";
        while(buttons.Count<actions.Count)
        {
            int index=buttons.Count;
            var button=MatchHudStyle.Button(actionContent,"Ability "+index,"",()=>
            {
                if(!requestedVisible||!runtime.AbilityHudVisible||displayedContext!=runtime.AbilityContext||index>=actions.Count)return;
                string id=actions[index].Id;
                runtime.GetAbilityActions(freshActions);
                foreach(var current in freshActions)
                    if(current.Id==id){if(current.Enabled)current.Invoke?.Invoke();break;}
            });
            button.GetComponentInChildren<TextMeshProUGUI>().rectTransform.sizeDelta=new Vector2(-16,-4);
            buttons.Add(button);
        }
        float width=Mathf.Min(expanded?360:300,frame.rect.width-40);
        float summaryHeight=Mathf.Clamp(summary.GetPreferredValues(summary.text,width-32,0).y+6,34,94);
        float statusHeight=string.IsNullOrEmpty(status.text)?0:Mathf.Clamp(status.GetPreferredValues(status.text,width-32,0).y+6,32,runtime.AbilityInstructionRequired?150:62);
        float header=52+summaryHeight+statusHeight+12;
        // Long summaries are available without clipping inside the expanded scroll.
        if(expanded)details.text=runtime.AbilityBuffSummary+"\n\n"+runtime.AbilityBuffDetails;
        float detailHeight=expanded?details.GetPreferredValues(details.text,width-48,0).y+16:0;
        float cellHeight=expanded?64:38, gap=6;
        float contentHeight=detailHeight+actions.Count*(cellHeight+gap);
        float height=Mathf.Min(Mathf.Max(header+Mathf.Min(contentHeight,expanded?400:200)+16,150),Mathf.Max(180,frame.rect.height-244));
        MatchHudStyle.Place(actionPanel,Vector2.zero,new Vector2(width,height),new Vector2(20+width/2,96+height/2));
        MatchHudStyle.Place(summary.rectTransform,new Vector2(.5f,1),new Vector2(width-32,summaryHeight),new Vector2(0,-52-summaryHeight/2));
        MatchHudStyle.Place(status.rectTransform,new Vector2(.5f,1),new Vector2(width-32,statusHeight),new Vector2(0,-52-summaryHeight-statusHeight/2));
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
            buttons[i].GetComponentInChildren<TextMeshProUGUI>().text=actions[i].Label+
                (expanded&&!string.IsNullOrEmpty(actions[i].Detail)?"\n"+actions[i].Detail:"");
        }
        if(contextChanged)actionScroll.verticalNormalizedPosition=1;
    }
    private void OnDestroy(){if(root)Destroy(root);}
    private static TextMeshProUGUI Label(Transform parent,string name,int size)
    {var text=MatchHudStyle.Text(parent,name,"",size);text.alignment=TextAlignmentOptions.MidlineLeft;return text;}
}
