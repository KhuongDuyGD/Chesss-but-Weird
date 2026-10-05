using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

internal static class MatchHudStyle
{
    internal static readonly Color Paper = new Color(.95f,.925f,.865f,1), Ink = new Color(.16f,.14f,.20f,1),
        Muted = new Color(.36f,.32f,.40f,1), Accent = new Color(.34f,.25f,.51f,1), Line = new Color(.65f,.59f,.60f,1);
    private static TMP_FontAsset font;
    internal static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max,Vector2 size,Vector2 position)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
        r.anchorMin=min;r.anchorMax=max;r.pivot=new Vector2(.5f,.5f);r.sizeDelta=size;r.anchoredPosition=position;return r;
    }
    internal static HandDrawnRoundedGraphic Surface(RectTransform rect,bool raycast=true)
    {
        var image=rect.gameObject.AddComponent<HandDrawnRoundedGraphic>();
        image.Configure(Paper,Line,10,1.5f,.25f,0);image.raycastTarget=raycast;return image;
    }
    internal static void Highlight(RectTransform rect,bool active)
    {rect.GetComponent<HandDrawnRoundedGraphic>().Configure(Paper,active?Accent:Line,10,active?2.5f:1.5f,.25f,0);}
    internal static TextMeshProUGUI Text(Transform parent,string name,string value,float size=22)
    {
        var r=Rect(parent,name,Vector2.zero,Vector2.one,new Vector2(-24,-12),Vector2.zero);
        var text=r.gameObject.AddComponent<TextMeshProUGUI>();
        if(!font)font=ChessFontCatalog.TmpFont;
        text.font=font?font:TMP_Settings.defaultFontAsset;text.text=value;text.fontSize=size;text.color=Ink;
        text.raycastTarget=false;text.richText=false;text.alignment=TextAlignmentOptions.MidlineLeft;
        text.overflowMode=TextOverflowModes.Ellipsis;return text;
    }
    internal static Button Button(Transform parent,string name,string value,UnityAction action)
    {
        var r=Rect(parent,name,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        var image=Surface(r);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;
        var colors=b.colors;colors.highlightedColor=new Color(.86f,.81f,.94f);colors.pressedColor=new Color(.72f,.65f,.83f);b.colors=colors;
        Text(r,"Label",value,20).alignment=TextAlignmentOptions.Center;b.onClick.AddListener(action);return b;
    }
    internal static void Place(RectTransform r,Vector2 anchor,Vector2 size,Vector2 position)
    {r.anchorMin=r.anchorMax=anchor;r.sizeDelta=size;r.anchoredPosition=position;}
    internal static string MoveCountLabel(int number) => number > 99 ? "99+" : Mathf.Max(0,number).ToString();
}
