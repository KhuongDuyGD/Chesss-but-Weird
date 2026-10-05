using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Read-only buff information; never steals board clicks or reveals hidden data.</summary>
public sealed class MatchHudTooltip : MonoBehaviour
{
    private RectTransform root,panel;
    private TextMeshProUGUI title,body;
    private Transform owner;
    public void Initialize(RectTransform canvas)
    {
        root=canvas;
        panel=MatchHudStyle.Rect(canvas,"Buff information",Vector2.one*.5f,Vector2.one*.5f,new Vector2(430,200),Vector2.zero);
        MatchHudStyle.Surface(panel,false);
        title=MatchHudStyle.Text(panel,"Buff title","",24);
        body=MatchHudStyle.Text(panel,"Buff description","",20);
        title.alignment=TextAlignmentOptions.TopLeft;body.alignment=TextAlignmentOptions.TopLeft;
        title.overflowMode=TextOverflowModes.Overflow;body.overflowMode=TextOverflowModes.Overflow;
        title.rectTransform.anchorMin=title.rectTransform.anchorMax=new Vector2(0,1);
        body.rectTransform.anchorMin=body.rectTransform.anchorMax=new Vector2(0,1);
        panel.gameObject.SetActive(false);
    }
    public void Show(RectTransform target,string heading,string description)
    {
        if(!root||!target||string.IsNullOrEmpty(description))return;
        owner=target;title.text=heading;body.text=description;
        float width=Mathf.Min(440,root.rect.width-32),inner=width-32;
        float titleHeight=Mathf.Max(34,title.GetPreferredValues(heading,inner,0).y+4);
        float bodyHeight=Mathf.Max(28,body.GetPreferredValues(description,inner,0).y+4);
        float height=titleHeight+bodyHeight+40;
        // Source descriptions are bounded catalog content. Fit long descriptions
        // inside the screen by widening before ever reducing readable font size.
        if(height>root.rect.height-32)
        {
            width=Mathf.Min(600,root.rect.width-32);inner=width-32;
            titleHeight=Mathf.Max(34,title.GetPreferredValues(heading,inner,0).y+4);
            bodyHeight=Mathf.Max(28,body.GetPreferredValues(description,inner,0).y+4);
            height=titleHeight+bodyHeight+40;
        }
        MatchHudStyle.Place(title.rectTransform,new Vector2(0,1),new Vector2(inner,titleHeight),new Vector2(16+inner/2,-18-titleHeight/2));
        MatchHudStyle.Place(body.rectTransform,new Vector2(0,1),new Vector2(inner,bodyHeight),new Vector2(16+inner/2,-24-titleHeight-bodyHeight/2));
        Vector3 point=root.InverseTransformPoint(target.position);
        Vector2 position=new Vector2(point.x-target.rect.width/2-width/2-12,point.y-height/2+20);
        position.x=Mathf.Clamp(position.x,root.rect.xMin+16+width/2,root.rect.xMax-16-width/2);
        position.y=Mathf.Clamp(position.y,root.rect.yMin+16+height/2,root.rect.yMax-16-height/2);
        MatchHudStyle.Place(panel,Vector2.one*.5f,new Vector2(width,height),position);
        panel.SetAsLastSibling();panel.gameObject.SetActive(true);
    }
    public void Hide(Transform source=null) {if(source&&source!=owner)return;owner=null;if(panel)panel.gameObject.SetActive(false);}
}

public sealed class MatchHudTooltipTarget : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
{
    private MatchHudTooltip tooltip;
    private string heading,description;
    private bool hovered;
    public void Configure(MatchHudTooltip view,string title,string detail)
    {bool changed=heading!=title||description!=detail;tooltip=view;heading=title;description=detail;if(hovered&&changed&&tooltip)tooltip.Show((RectTransform)transform,heading,description);}
    public void OnPointerEnter(PointerEventData data){hovered=true;if(tooltip)tooltip.Show((RectTransform)transform,heading,description);}
    public void OnPointerExit(PointerEventData data){hovered=false;if(tooltip)tooltip.Hide(transform);}
    private void OnDisable(){hovered=false;if(tooltip)tooltip.Hide(transform);}
}
