using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>English-only action HUD. Buttons issue explicit actions; no hidden hotkeys.</summary>
public sealed class AramAbilityView : MonoBehaviour
{
    public readonly struct AbilityAction
    {
        public readonly string Label; public readonly Action Invoke; public readonly bool Enabled;
        public AbilityAction(string label,Action invoke,bool enabled=true){Label=label;Invoke=invoke;Enabled=enabled;}
    }
    private AramBuffRuntime runtime;
    private GameObject root;
    private TextMeshProUGUI status;
    private TextMeshProUGUI crosshair;
    private readonly List<Button> buttons=new List<Button>();
    private readonly List<AbilityAction> actions=new List<AbilityAction>();
    public void SetVisible(bool visible) { if(root)root.SetActive(visible); }
    public void Initialize(AramBuffRuntime source)
    {
        runtime=source;
        root=new GameObject("ARAM Abilities",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root.transform.SetParent(transform,false);
        root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=90;
        ResponsiveUi.ConfigureCanvasScaler(root.GetComponent<CanvasScaler>(),new Vector2(1920,1080));
        var frame=MenuDesignFrame.Create(root.transform,"ARAM Actions",new Vector2(1920,1080));
        crosshair=Label(frame,"Rifle Crosshair",Vector2.zero,new Vector2(60,60),38);crosshair.text="+";
        var panel=Rect(frame,"Action Panel",new Vector2(0,-430),new Vector2(1550,180));
        panel.gameObject.AddComponent<Image>().color=new Color(.04f,.035f,.065f,.9f);
        status=Label(panel,"Status",new Vector2(0,42),new Vector2(1480,80),25);
        for(int i=0;i<6;i++)
        {
            int index=i;
            var rect=Rect(panel,"Ability "+i,new Vector2(-625+i*250,-48),new Vector2(238,58));
            var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.9f,.76f,.35f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            button.onClick.AddListener(()=>{if(index<actions.Count && actions[index].Enabled)actions[index].Invoke?.Invoke();});
            var label=Label(rect,"Label",Vector2.zero,new Vector2(218,48),24);label.color=Color.black;
            buttons.Add(button);
        }
    }
    private void Update()
    {
        if(!root||!runtime)return;
        bool visible=runtime.AbilityHudVisible;
        root.SetActive(visible);if(!visible)return;
        runtime.GetAbilityActions(actions);status.text=runtime.AbilityStatus;
        crosshair.gameObject.SetActive(runtime.RifleActive);
        for(int i=0;i<buttons.Count;i++)
        {
            buttons[i].gameObject.SetActive(i<actions.Count);
            if(i<actions.Count){buttons[i].interactable=actions[i].Enabled;buttons[i].GetComponentInChildren<TextMeshProUGUI>().text=actions[i].Label;}
        }
    }
    private void OnDestroy(){if(root)Destroy(root);}
    private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    private static TextMeshProUGUI Label(Transform parent,string name,Vector2 position,Vector2 size,int fontSize)
    {
        var text=Rect(parent,name,position,size).gameObject.AddComponent<TextMeshProUGUI>();text.fontSize=fontSize;text.enableAutoSizing=true;
        text.fontSizeMin=18;text.fontSizeMax=fontSize;text.alignment=TextAlignmentOptions.Center;text.color=Color.white;text.raycastTarget=false;return text;
    }
}
