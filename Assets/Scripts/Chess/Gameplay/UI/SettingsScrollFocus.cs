using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class SettingsScrollFocus : MonoBehaviour,ISelectHandler
{
    private ScrollRect scroll;private RectTransform target;
    public void Configure(ScrollRect owner,RectTransform item){scroll=owner;target=item;}
    public void OnSelect(BaseEventData data)
    {
        if(!scroll||!target)return;Canvas.ForceUpdateCanvases();
        var corners=new Vector3[4];target.GetWorldCorners(corners);
        float bottom=scroll.viewport.InverseTransformPoint(corners[0]).y,top=scroll.viewport.InverseTransformPoint(corners[2]).y;
        var position=scroll.content.anchoredPosition;
        if(bottom<scroll.viewport.rect.yMin)position.y+=scroll.viewport.rect.yMin-bottom;
        else if(top>scroll.viewport.rect.yMax)position.y-=top-scroll.viewport.rect.yMax;
        position.y=Mathf.Clamp(position.y,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height));
        scroll.content.anchoredPosition=position;
    }
}
