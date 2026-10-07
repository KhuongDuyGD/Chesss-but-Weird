using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ChessButWeird.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI = SketchbookUI;

/// <summary>One registry-generated menu, used in the main menu and while locally paused.</summary>
public sealed class SettingsMenuController : MonoBehaviour
{
    private const float Width=1440, Height=900;
    private RectTransform root, frame, content, popupLayer, popup;
    private ScrollRect scroll;
    private TextMeshProUGUI categoryTitle, status, confirmationText;
    private UnityAction closeAction;
    private SettingsCategory category;
    private readonly Dictionary<string,Action> refreshers=new Dictionary<string,Action>();
    private readonly List<Button> tabs=new List<Button>();
    private readonly List<Selectable> focusOrder=new List<Selectable>();
    private DisplayConfirmation display;
    private GameObject previousSelection;
    private string rebindId;
    private float rebuildAt=-1;
    private bool subscribed, opened;
    private Action confirmAction;
    private Vector3Int observedDisplay;
    public static int EscapeConsumedFrame { get; private set; }=-1;
    public bool IsOpen => opened && root && root.gameObject.activeInHierarchy;
    public SettingsCategory CurrentCategory => category;
    public RectTransform Content => content;
    private float TextScale => UserSettings.Get("ui_scale")/100 * (UserSettings.Enabled("large_text")?1.15f:1);

    public void Initialize(RectTransform newRoot,UnityAction close)
    {
        root=newRoot;closeAction=close;
        display=new DisplayConfirmation(SettingsDisplay.Apply,SettingsDisplay.Save);
        var group=root.GetComponent<CanvasGroup>();
        if(!group)group=root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts=true;group.interactable=true;
        UI.Background(root);
        frame=MenuDesignFrame.Create(root,"Settings",new Vector2(Width,Height));
        BuildShell(); Subscribe(); SelectCategory(SettingsCategory.General);
    }
    public void Open()
    {
        opened=true; Subscribe(); GameRuntimeSettings.ApplySaved(); Refresh();
        observedDisplay=new Vector3Int(Screen.width,Screen.height,(int)Screen.fullScreenMode);
        if(tabs.Count>0)tabs[(int)category].Select();
    }
    private void Subscribe()
    { if(subscribed)return;UserSettings.Manager.Changed+=OnSettingsChanged;subscribed=true; }
    private void BuildShell()
    {
        UI.Card(frame,"Settings notebook",new Rect(10,12,1420,872),UI.White,shadow:false);
        UI.Tape(frame,76,8,145,-3);
        UI.Text(frame,"Settings title",SettingsLocalization.Text("settings.title","SETTINGS"),new Rect(44,42,1100,64),46);
        UI.Text(frame,"Settings subtitle",SettingsLocalization.Text("settings.subtitle","Your game, your comfort. Changes save automatically."),new Rect(46,106,1150,32),23,UI.Muted);
        UI.Card(frame,"Tab rail",new Rect(36,164,238,586),UI.Paper,shadow:false,radius:16);
        for(int i=0;i<6;i++)
        {
            var tab=(SettingsCategory)i;
            var button=UI.Button(frame,tab+" tab",tab.ToString(),new Rect(48,178+i*78,214,60),UI.Paper,()=>SelectCategory(tab),26);
            tabs.Add(button);
        }
        categoryTitle=UI.Text(frame,"Category title","",new Rect(308,160,1020,44),32);
        var viewport=UI.Node(frame,"Settings viewport",new Rect(300,220,1098,510));
        var hit=viewport.gameObject.AddComponent<AntialiasedMenuImage>();hit.color=Color.clear;hit.raycastTarget=true;
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;
        scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity=42;scroll.inertia=false;
        content=UI.Node(viewport,"Settings rows",new Rect(0,0,1064,510));
        content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);
        content.anchoredPosition=new Vector2(-17,0);content.sizeDelta=new Vector2(-34,510);scroll.content=content;
        var scrollbarRect=UI.Node(viewport,"Scroll bar",new Rect(1080,0,12,510));
        var track=scrollbarRect.gameObject.AddComponent<AntialiasedMenuImage>();track.color=UI.Paper;
        var bar=scrollbarRect.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
        var handle=UI.Node(scrollbarRect,"Scroll thumb",new Rect(0,0,12,42));
        var handleImage=handle.gameObject.AddComponent<AntialiasedMenuImage>();handleImage.color=UI.Muted;
        bar.handleRect=handle;bar.targetGraphic=handleImage;scroll.verticalScrollbar=bar;
        status=UI.Text(frame,"Settings status","Changes save automatically",new Rect(310,748,1070,40),21,UI.Muted);
        UI.Button(frame,"Reset tab","Reset This Tab",new Rect(40,804,240,52),UI.Paper,ResetTab,24);
        UI.Button(frame,"Reset all","Reset All Settings",new Rect(300,804,265,52),UI.Paper,()=>Confirm("Reset all settings to default?\nControls and accessibility preferences will also reset.",()=>{UserSettings.Manager.ResetAll();BuildRows();BeginDisplayReset();},"Reset All"),24);
        UI.Button(frame,"Close settings","Close  ·  ESC",new Rect(1158,804,235,52),UI.Green,Close,24);
        popupLayer=UI.Node(frame,"Settings popups",new Rect(0,0,Width,Height));
        var shade=popupLayer.gameObject.AddComponent<AntialiasedMenuImage>();shade.color=new Color(0,0,0,.38f);shade.raycastTarget=true;
        var dismiss=popupLayer.gameObject.AddComponent<Button>();dismiss.transition=Selectable.Transition.None;
        dismiss.onClick.AddListener(()=>{if(display.Pending)RevertDisplay();else HidePopup();});
        popupLayer.gameObject.SetActive(false);
    }
    public void SelectCategory(SettingsCategory selected)
    {
        if(display!=null&&display.Pending)RevertDisplay();else HidePopup();
        category=selected;categoryTitle.text=SettingsLocalization.Text("tab."+selected,selected.ToString()).ToUpperInvariant();
        for(int i=0;i<tabs.Count;i++)
        {
            var graphic=tabs[i].GetComponent<HandDrawnRoundedGraphic>();
            graphic.Configure(i==(int)selected?UI.Blue:UI.Paper,UI.Ink,15, i==(int)selected?4:2,1.2f,i);
            tabs[i].GetComponentInChildren<TextMeshProUGUI>().text=(i==(int)selected?">  ":"")+SettingsLocalization.Text("tab."+(SettingsCategory)i,((SettingsCategory)i).ToString());
        }
        BuildRows();scroll.verticalNormalizedPosition=1;
    }
    private void OnSettingsChanged(IReadOnlyList<string> ids)
    {
        Refresh();
        if(ids.Any(id=>id=="ui_scale"||id=="large_text"||id=="contrast_ui"||id=="language"))rebuildAt=Time.unscaledTime+.12f;
    }
    private void Refresh(){foreach(var refresh in refreshers.Values)refresh();}
    private void BuildRows()
    {
        RefreshShellText();
        refreshers.Clear();
        foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        float y=4;
        if(category==SettingsCategory.Controls)
            AddNote(ref y,"Mouse & keyboard","Left click selects and moves pieces. ESC cancels selection / goes back. Rifle: right mouse aims, left mouse fires, Alt releases the cursor. Camera and Settings shortcuts below can be rebound. Controller support is not available.");
        if(category==SettingsCategory.Accessibility)
            AddNote(ref y,"Text & information","UI Scale and Tooltip Size are in General. Buff detail is in Gameplay. These tabs share one set of preferences.");
        foreach(var definition in UserSettings.Manager.Definitions)
            if(definition.Category==category)AddRow(definition,ref y);
        if(category==SettingsCategory.Graphics)AddDisplayRows(ref y);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(y+12,scroll.viewport.rect.height));
        Canvas.ForceUpdateCanvases();Refresh();RefreshFocusOrder();
    }
    private void RefreshShellText()
    {
        frame.Find("Settings title").GetComponent<TextMeshProUGUI>().text=SettingsLocalization.Text("settings.title","SETTINGS");
        frame.Find("Settings subtitle").GetComponent<TextMeshProUGUI>().text=SettingsLocalization.Text("settings.subtitle","Your game, your comfort. Changes save automatically.");
        categoryTitle.text=SettingsLocalization.Text("tab."+category,category.ToString()).ToUpperInvariant();
        for(int i=0;i<tabs.Count;i++)tabs[i].GetComponentInChildren<TextMeshProUGUI>().text=(i==(int)category?">  ":"")+SettingsLocalization.Text("tab."+(SettingsCategory)i,((SettingsCategory)i).ToString());
        frame.Find("Reset tab").GetComponentInChildren<TextMeshProUGUI>().text=SettingsLocalization.Text("settings.reset_tab","Reset This Tab");
        frame.Find("Reset all").GetComponentInChildren<TextMeshProUGUI>().text=SettingsLocalization.Text("settings.reset_all","Reset All Settings");
        frame.Find("Close settings").GetComponentInChildren<TextMeshProUGUI>().text=SettingsLocalization.Text("settings.close","Close  ·  ESC");
    }
    private void AddNote(ref float y,string title,string text)
    {
        float size=22*TextScale;
        var label=UI.Text(content,title,title+"\n"+text,new Rect(12,y,1032,120),size,UI.Muted,wrap:true);
        float height=label.GetPreferredValues(label.text,1032,0).y+24;
        label.rectTransform.sizeDelta=new Vector2(1032,height);label.rectTransform.anchoredPosition=new Vector2(528,-y-height/2);y+=height+14;
    }
    private void AddRow(SettingDefinition definition,ref float y)
    {
        float scale=TextScale;
        float rowHeight=Mathf.Max(110,108*scale);
        var row=UI.Node(content,definition.Id+" row",new Rect(0,y,1064,rowHeight));
        var label=UI.Text(row,"Setting name",SettingsLocalization.Text(definition.Id+".name",definition.Name),new Rect(16,8,550,36*scale),27*scale,UserSettings.Enabled("contrast_ui")?Color.black:UI.Ink,wrap:true);
        var description=UI.Text(row,"Description",SettingsLocalization.Text(definition.Id+".description",definition.Description),new Rect(16,12+36*scale,550,50*scale),21*scale,UserSettings.Enabled("contrast_ui")?UI.Ink:UI.Muted,wrap:true);
        float nameHeight=Mathf.Max(36*scale,label.GetPreferredValues(label.text,550,0).y+4);
        float descHeight=Mathf.Max(40*scale,description.GetPreferredValues(description.text,550,0).y+4);
        label.rectTransform.sizeDelta=new Vector2(550,nameHeight);label.rectTransform.anchoredPosition=new Vector2(291,-8-nameHeight/2);
        description.rectTransform.sizeDelta=new Vector2(550,descHeight);description.rectTransform.anchoredPosition=new Vector2(291,-12-nameHeight-descHeight/2);
        rowHeight=Mathf.Max(rowHeight,20+nameHeight+descHeight);
        row.sizeDelta=new Vector2(1064,rowHeight);row.anchoredPosition=new Vector2(532,-y-rowHeight/2);
        float controlY=(rowHeight-48)/2;
        Action refresh;
        if(definition.Type==SettingType.Slider)
        {
            var value=UI.Text(row,"Value","",new Rect(770,controlY-34*scale,210,32*scale),22*scale,UI.Ink,TextAlignmentOptions.Right);
            var slider=CreateSlider(row,new Rect(596,controlY+20*scale,360,28),definition);
            slider.onValueChanged.AddListener(v=>UserSettings.Manager.Set(definition.Id,v));
            refresh=()=>{slider.SetValueWithoutNotify(UserSettings.Get(definition.Id));value.text=FormatValue(definition,UserSettings.Get(definition.Id));};
        }
        else
        {
            Button button=null;
            button=UI.Button(row,definition.Id+" control","",new Rect(590,controlY,368,48),UI.White,()=>
            {
                if(definition.Type==SettingType.Toggle)UserSettings.Manager.Set(definition.Id,UserSettings.Enabled(definition.Id)?0:1);
                else if(definition.Type==SettingType.Keybind)BeginRebind(definition.Id);
                else ShowChoices((RectTransform)button.transform,OptionLabels(definition),definition.OptionIndex(UserSettings.Get(definition.Id)),index=>UserSettings.Manager.Set(definition.Id,definition.Values[index]));
            },24*Mathf.Min(scale,1.25f));
            refresh=()=>button.GetComponentInChildren<TextMeshProUGUI>().text=definition.Type==SettingType.Toggle?(UserSettings.Enabled(definition.Id)?SettingsLocalization.Text("common.on","ON"):SettingsLocalization.Text("common.off","OFF")):SettingsLocalization.Text(definition.Id+".option."+definition.OptionIndex(UserSettings.Get(definition.Id)),definition.Labels[definition.OptionIndex(UserSettings.Get(definition.Id))])+(definition.Type==SettingType.Dropdown?"  v":"");
        }
        var reset=UI.Button(row,definition.Id+" reset","Reset",new Rect(976,controlY,78,48),UI.Paper,()=>
        {
            if(definition.Type==SettingType.Keybind)
            {
                if(!UserSettings.Manager.TryRebind(definition.Id,definition.Default,out var conflict))
                    status.text=definition.Labels[definition.OptionIndex(definition.Default)]+" is already assigned to "+conflict+". Rebind that action first.";
            }
            else UserSettings.Manager.Reset(definition.Id);
        },19);
        var dot=UI.Text(row,"Modified","",new Rect(566,controlY+7,22,28),24,UI.Muted,TextAlignmentOptions.Center);
        refreshers[definition.Id]=()=>{refresh();bool changed=UserSettings.Manager.IsModified(definition.Id);dot.text=changed?"*":"";reset.interactable=changed;};
        y+=rowHeight+12;
    }
    private static string[] OptionLabels(SettingDefinition definition) => definition.Labels.Select((label,index)=>SettingsLocalization.Text(definition.Id+".option."+index,label)).ToArray();
    private Slider CreateSlider(Transform parent,Rect area,SettingDefinition definition)
    {
        var rect=UI.Node(parent,definition.Id+" slider",area);
        var hit=rect.gameObject.AddComponent<AntialiasedMenuImage>();hit.color=Color.clear;hit.raycastTarget=true;
        var slider=rect.gameObject.AddComponent<Slider>();slider.minValue=definition.Minimum;slider.maxValue=definition.Maximum;
        var track=UI.Image(rect,"Slider track",null,new Rect(0,10,area.width,8));track.color=UI.Muted;
        var handleArea=UI.Node(rect,"Handle area",new Rect(10,0,area.width-20,area.height));
        handleArea.anchorMin=Vector2.zero;handleArea.anchorMax=Vector2.one;handleArea.offsetMin=new Vector2(10,0);handleArea.offsetMax=new Vector2(-10,0);
        var thumb=UI.Image(handleArea,"Slider handle",null,new Rect(0,0,20,28));thumb.color=UI.Ink;thumb.raycastTarget=true;
        slider.handleRect=thumb.rectTransform;slider.targetGraphic=thumb;
        return slider;
    }
    private static string FormatValue(SettingDefinition definition,float value) => definition.Id=="tooltip_delay"?value.ToString("0.00",CultureInfo.InvariantCulture)+" s":Mathf.RoundToInt(value)+"%";
    private void ResetTab()
    {
        UserSettings.Manager.ResetCategory(category);
        if(category==SettingsCategory.Graphics)
            Confirm("Reset display mode and resolution too?",BeginDisplayReset,"Reset Display");
        status.text="Reset "+category+" to defaults.";
    }
    private void AddDisplayRows(ref float y)
    {
#if UNITY_EDITOR
        AddNote(ref y,"Display","Display mode and resolution confirmation are available in the Windows player. The Editor game view is not resized.");
#else
        AddNote(ref y,"Display","Display changes require confirmation and revert after 15 seconds. VSync and frame rate changes above are immediate.");
        var modes=new[]{FullScreenMode.Windowed,FullScreenMode.FullScreenWindow,FullScreenMode.ExclusiveFullScreen};
        AddDisplayChoice("Display Mode",new[]{"Windowed","Borderless","Exclusive Fullscreen"},Array.IndexOf(modes,Screen.fullScreenMode),i=>BeginDisplay(new DisplayConfiguration(Screen.width,Screen.height,(int)modes[i])),ref y);
        var resolutions=Screen.resolutions.Select(r=>new Vector2Int(r.width,r.height)).Where(r=>r.x>=800&&r.y>=600).Distinct().ToList();
        var current=new Vector2Int(Screen.width,Screen.height);if(!resolutions.Contains(current))resolutions.Add(current);
        resolutions.Sort((a,b)=>a.x!=b.x?a.x.CompareTo(b.x):a.y.CompareTo(b.y));
        AddDisplayChoice("Resolution",resolutions.Select(r=>r.x+" x "+r.y).ToArray(),resolutions.IndexOf(current),i=>BeginDisplay(new DisplayConfiguration(resolutions[i].x,resolutions[i].y,(int)Screen.fullScreenMode)),ref y);
#endif
    }
    private void AddDisplayChoice(string name,string[] options,int selected,Action<int> changed,ref float y)
    {
        var row=UI.Node(content,name,new Rect(0,y,1064,100));
        var label=UI.Text(row,"Name",name,new Rect(16,16,550,44*TextScale),27*TextScale,wrap:true);
        float nameHeight=Mathf.Max(44*TextScale,label.GetPreferredValues(name,550,0).y+4),height=Mathf.Max(100,nameHeight+32);
        row.sizeDelta=new Vector2(1064,height);row.anchoredPosition=new Vector2(532,-y-height/2);
        label.rectTransform.sizeDelta=new Vector2(550,nameHeight);label.rectTransform.anchoredPosition=new Vector2(291,-16-nameHeight/2);
        Button button=null;button=UI.Button(row,"Display control",options[Mathf.Clamp(selected,0,options.Length-1)],new Rect(590,(height-48)/2,368,48),UI.White,()=>ShowChoices((RectTransform)button.transform,options,Mathf.Max(0,selected),changed),24*Mathf.Min(TextScale,1.25f));
        y+=height+12;
    }
    private void BeginDisplay(DisplayConfiguration value)
    {
        display.Begin(SettingsDisplay.Current,value,Time.realtimeSinceStartupAsDouble);
        Confirm("Display settings changed.\nKeep these settings?",()=>{display.Keep();BuildRows();},"Keep",RevertDisplay);
    }
    private void BeginDisplayReset()
    {
#if UNITY_EDITOR
        SettingsDisplay.ClearSaved();
#else
        var recommended=new DisplayConfiguration(Screen.currentResolution.width,Screen.currentResolution.height,(int)FullScreenMode.FullScreenWindow);
        BeginDisplay(recommended);
#endif
    }
    private void RevertDisplay(){display?.Revert();HidePopup();BuildRows();}
    private void ShowChoices(RectTransform anchor,string[] options,int selected,Action<int> chosen)
    {
        HidePopup();previousSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
        popupLayer.gameObject.SetActive(true);popupLayer.SetAsLastSibling();
        var corners=new Vector3[4];anchor.GetWorldCorners(corners);
        var bottom=frame.InverseTransformPoint(corners[0]);var top=frame.InverseTransformPoint(corners[2]);
        float height=Mathf.Min(480,options.Length*52+16),width=390;
        // Coordinates are local to the centered design frame, then converted to its top-left artwork system.
        float left=Mathf.Clamp(bottom.x+Width/2,24,Width-width-24);
        float topY=Height/2-bottom.y+8;
        if(topY+height>Height-24)topY=Height/2-top.y-height-8;
        topY=Mathf.Clamp(topY,24,Height-height-24);
        popup=UI.Card(popupLayer,"Choices",new Rect(left,topY,width,height),UI.White,shadow:false).rectTransform;
        popup.GetComponent<HandDrawnRoundedGraphic>().raycastTarget=true;
        var viewport=UI.Node(popup,"Choices viewport",new Rect(8,8,width-16,height-16));viewport.gameObject.AddComponent<RectMask2D>();
        var hit=viewport.gameObject.AddComponent<AntialiasedMenuImage>();hit.color=Color.clear;hit.raycastTarget=true;
        var list=UI.Node(viewport,"Choices content",new Rect(0,0,width-16,options.Length*52));list.pivot=new Vector2(.5f,1);list.anchoredPosition=new Vector2((width-16)/2,0);
        var dropdownScroll=viewport.gameObject.AddComponent<ScrollRect>();dropdownScroll.viewport=viewport;dropdownScroll.content=list;dropdownScroll.horizontal=false;dropdownScroll.inertia=false;dropdownScroll.scrollSensitivity=40;dropdownScroll.movementType=ScrollRect.MovementType.Clamped;
        var buttons=new List<Button>();
        for(int i=0;i<options.Length;i++)
        {
            int index=i;
            var button=UI.Button(list,"Choice "+i,options[i],new Rect(4,i*52+2,width-24,44),i==selected?UI.Blue:UI.White,()=>{HidePopup();chosen(index);},23);
            buttons.Add(button);button.gameObject.AddComponent<SettingsScrollFocus>().Configure(dropdownScroll,(RectTransform)button.transform);
        }
        for(int i=0;i<buttons.Count;i++)buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=buttons[Mathf.Max(0,i-1)],selectOnDown=buttons[Mathf.Min(buttons.Count-1,i+1)]};
        buttons[Mathf.Clamp(selected,0,buttons.Count-1)].Select();
    }
    public void Confirm(string text,Action confirmed,string label="Confirm",Action cancelled=null)
    {
        HidePopup();popupLayer.gameObject.SetActive(true);popupLayer.SetAsLastSibling();confirmAction=confirmed;
        previousSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
        popup=UI.Card(popupLayer,"Confirmation",new Rect(360,260,720,340),UI.White,shadow:false).rectTransform;
        popup.GetComponent<HandDrawnRoundedGraphic>().raycastTarget=true;
        confirmationText=UI.Text(popup,"Question",text,new Rect(32,24,656,190),27,UI.Ink,TextAlignmentOptions.Center,true);
        UI.Button(popup,"Confirm action",label,new Rect(386,256,282,54),UI.Green,()=>{var action=confirmAction;HidePopup();action?.Invoke();},25);
        var cancel=UI.Button(popup,"Cancel action",display.Pending?"Revert":"Cancel",new Rect(40,256,282,54),UI.Paper,()=>{HidePopup();cancelled?.Invoke();},25);cancel.Select();
    }
    private void BeginRebind(string id)
    {
        Confirm("Press a letter, F1–F12, Space, Tab or Backquote.\nESC cancels. Duplicate bindings are rejected.",()=>{},"Waiting…");rebindId=id;
        var waiting=popup.Find("Confirm action");if(waiting)waiting.GetComponent<Button>().interactable=false;
    }
    private void HidePopup()
    {
        rebindId=null;confirmAction=null;
        if(popup){popup.gameObject.SetActive(false);Destroy(popup.gameObject);}popup=null;
        if(popupLayer)popupLayer.gameObject.SetActive(false);
        if(previousSelection&&previousSelection.activeInHierarchy&&EventSystem.current)EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection=null;
    }
    private void RefreshFocusOrder()
    {
        focusOrder.Clear();focusOrder.AddRange(frame.GetComponentsInChildren<Selectable>().Where(s=>s.gameObject.activeInHierarchy&&s.interactable&&!(s is Scrollbar)));
        foreach(var selectable in content.GetComponentsInChildren<Selectable>())
            if(!selectable.GetComponent<SettingsScrollFocus>())selectable.gameObject.AddComponent<SettingsScrollFocus>().Configure(scroll,(RectTransform)selectable.transform);
    }
    private void Update()
    {
        if(!IsOpen)return;
        var actualDisplay=new Vector3Int(Screen.width,Screen.height,(int)Screen.fullScreenMode);
        if(actualDisplay!=observedDisplay)
        {observedDisplay=actualDisplay;if(category==SettingsCategory.Graphics&&!popup)rebuildAt=Time.unscaledTime+.12f;}
        if(rebuildAt>=0&&Time.unscaledTime>=rebuildAt&&(Mouse.current==null||!Mouse.current.leftButton.isPressed))
        {rebuildAt=-1;float position=scroll.verticalNormalizedPosition;BuildRows();scroll.verticalNormalizedPosition=position;}
        if(display.Pending)
        {
            display.Tick(Time.realtimeSinceStartupAsDouble);
            if(!display.Pending){HidePopup();BuildRows();}
            else if(confirmationText)confirmationText.text="Display settings changed.\nKeep these settings?\nReverting in "+display.SecondsRemaining(Time.realtimeSinceStartupAsDouble)+" seconds.";
        }
        var keyboard=Keyboard.current;if(keyboard==null)return;
        if(keyboard.escapeKey.wasPressedThisFrame)
        {
            EscapeConsumedFrame=Time.frameCount;
            if(display.Pending)RevertDisplay();else if(popup)HidePopup();else Close();return;
        }
        if(rebindId!=null)
        {
            foreach(var key in keyboard.allKeys)
                if(key.wasPressedThisFrame)
                {
                    int index=Array.IndexOf(SettingsRegistry.BindingKeys,key.keyCode.ToString());if(index<0)continue;
                    if(UserSettings.Manager.TryRebind(rebindId,index,out string conflict)){HidePopup();status.text="Shortcut saved.";}
                    else if(confirmationText)confirmationText.text=key.displayName+" is already assigned to "+conflict+".\nChoose another key, or press ESC to cancel.";
                    break;
                }
            return;
        }
        if(keyboard.tabKey.wasPressedThisFrame&&!popup)
        {
            RefreshFocusOrder();var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            int index=focusOrder.FindIndex(s=>s.gameObject==selected);int step=keyboard.leftShiftKey.isPressed||keyboard.rightShiftKey.isPressed?-1:1;
            if(focusOrder.Count>0)focusOrder[(index+step+focusOrder.Count)%focusOrder.Count].Select();
        }
    }
    private void Close()
    {EscapeConsumedFrame=Time.frameCount;display?.Revert();HidePopup();opened=false;UserSettings.Manager.Flush();closeAction?.Invoke();}
    private void OnApplicationFocus(bool focused){if(!focused&&display!=null&&display.Pending)RevertDisplay();}
    private void OnDisable()
    {display?.Revert();HidePopup();opened=false;if(subscribed){UserSettings.Manager.Changed-=OnSettingsChanged;subscribed=false;}UserSettings.Manager.Flush();}
    private void OnDestroy(){if(subscribed)UserSettings.Manager.Changed-=OnSettingsChanged;display?.Revert();}
}
