using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>A settings-owned confirmation sheet; used by the existing quit action.</summary>
public sealed class SettingsPrompt : MonoBehaviour
{
    private Action cancel;
    public static SettingsPrompt Show(RectTransform parent,string message,Action accepted)
    {
        var root=new GameObject("Quit confirmation",typeof(RectTransform)).GetComponent<RectTransform>();root.SetParent(parent,false);
        root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
        var image=root.gameObject.AddComponent<AntialiasedMenuImage>();image.color=new Color(0,0,0,.55f);image.raycastTarget=true;
        var prompt=root.gameObject.AddComponent<SettingsPrompt>();prompt.cancel=()=>Destroy(root.gameObject);
        var frame=MenuDesignFrame.Create(root,"Quit",new Vector2(1440,900));
        var card=SketchbookUI.Card(frame,"Confirmation",new Rect(360,280,720,320),SketchbookUI.White,shadow:false).rectTransform;
        SketchbookUI.Text(card,"Question",message,new Rect(36,28,648,176),28,alignment:TextAlignmentOptions.Center,wrap:true);
        var cancel=SketchbookUI.Button(card,"Cancel","Cancel",new Rect(40,232,282,54),SketchbookUI.Paper,()=>prompt.cancel(),25);
        SketchbookUI.Button(card,"Quit","Leave Match",new Rect(386,232,282,54),SketchbookUI.Pink,()=>{Destroy(root.gameObject);accepted();},25);
        cancel.Select();return prompt;
    }
    private void Update(){if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)cancel?.Invoke();}
}
