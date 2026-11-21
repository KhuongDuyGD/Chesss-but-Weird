using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>A settings-owned confirmation sheet; used by the existing quit action.</summary>
public sealed class SettingsPrompt : MonoBehaviour
{
    private Action cancel;
    private GameObject previousSelection;
    private bool dismissed;
    public static SettingsPrompt Show(RectTransform parent,string message,Action accepted,string acceptLabel="Leave Match")
    {
        var existing=parent.GetComponentInChildren<SettingsPrompt>();
        if(existing)return existing;
        var root=new GameObject("Quit confirmation",typeof(RectTransform)).GetComponent<RectTransform>();root.SetParent(parent,false);
        root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
        var canvas=root.gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=1000;
        root.gameObject.AddComponent<GraphicRaycaster>();
        var image=root.gameObject.AddComponent<AntialiasedMenuImage>();image.color=new Color(0,0,0,.55f);image.raycastTarget=true;
        var prompt=root.gameObject.AddComponent<SettingsPrompt>();
        prompt.previousSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
        prompt.cancel=()=>prompt.Dismiss(null);
        var frame=MenuDesignFrame.Create(root,"Quit",new Vector2(1440,900));
        var card=SketchbookUI.Card(frame,"Confirmation",new Rect(360,280,720,320),SketchbookUI.White,shadow:false).rectTransform;
        SketchbookUI.Text(card,"Question",message,new Rect(36,28,648,176),28,alignment:TextAlignmentOptions.Center,wrap:true);
        var cancel=SketchbookUI.Button(card,"Cancel","Cancel",new Rect(40,232,282,54),SketchbookUI.Paper,()=>prompt.cancel(),25);
        var quit=SketchbookUI.Button(card,"Quit",acceptLabel,new Rect(386,232,282,54),SketchbookUI.Pink,()=>prompt.Dismiss(accepted),25);
        quit.GetComponentInChildren<TextMeshProUGUI>().color=new Color(.76f,.12f,.17f);
        var navigation=cancel.navigation;navigation.mode=Navigation.Mode.Explicit;navigation.selectOnRight=quit;navigation.selectOnLeft=quit;cancel.navigation=navigation;
        navigation=quit.navigation;navigation.mode=Navigation.Mode.Explicit;navigation.selectOnRight=cancel;navigation.selectOnLeft=cancel;quit.navigation=navigation;
        cancel.Select();return prompt;
    }
    private void Dismiss(Action accepted)
    {
        if(dismissed)return;dismissed=true;
        gameObject.SetActive(false);Destroy(gameObject);
        if(EventSystem.current&&previousSelection&&previousSelection.activeInHierarchy)EventSystem.current.SetSelectedGameObject(previousSelection);
        accepted?.Invoke();
    }
    private void Update(){if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)cancel?.Invoke();}
}
