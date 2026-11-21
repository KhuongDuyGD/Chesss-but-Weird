using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChessButWeird.BuffVfx
{
    internal sealed class BuffTacticalHud
    {
        private const int RowHeight=60;
        private readonly GameObject root;
        private readonly TextMeshProUGUI[] labels=new TextMeshProUGUI[5];
        private readonly Image[] segments=new Image[6];
        private readonly TextMeshProUGUI[] milestoneNames=new TextMeshProUGUI[6];
        private readonly TextMeshProUGUI pulse;
        private readonly BuffHourglassGraphic hourglass;
        private readonly RectTransform panelRect;
        private float pulseAge=2,age;
        private bool urgent,wasPeaceReady,initialized;
        internal int Rows {get;private set;}
        internal BuffTacticalHud(Transform owner)
        {
            root=new GameObject("CHAOS objective presentation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(owner,false);
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=89;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var panel=new GameObject("Objectives",typeof(RectTransform),typeof(Image));panel.transform.SetParent(root.transform,false);panelRect=(RectTransform)panel.transform;panelRect.anchorMin=panelRect.anchorMax=new Vector2(.5f,1);panelRect.pivot=new Vector2(.5f,1);panelRect.anchoredPosition=new Vector2(0,-70);panelRect.sizeDelta=new Vector2(260,240);panel.GetComponent<Image>().color=new Color(.1f,.12f,.15f,.88f);panel.GetComponent<Image>().raycastTarget=false;
            for(int i=0;i<5;i++){labels[i]=Label(panel.transform,"Live objective "+i,19);var r=labels[i].rectTransform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(16,-12-i*RowHeight);r.sizeDelta=new Vector2(228,RowHeight);}
            pulse=Label(root.transform,"Confirmed extra move",25);pulse.alignment=TextAlignmentOptions.Center;pulse.rectTransform.anchorMin=pulse.rectTransform.anchorMax=new Vector2(.5f,1);pulse.rectTransform.sizeDelta=new Vector2(330,45);pulse.gameObject.SetActive(false);
            string[] names={"P","N","B","R","Q","+"};
            for(int i=0;i<6;i++){var go=new GameObject("Quest milestone "+i,typeof(RectTransform),typeof(Image));go.transform.SetParent(panel.transform,false);segments[i]=go.GetComponent<Image>();segments[i].raycastTarget=false;var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.sizeDelta=new Vector2(29,5);milestoneNames[i]=Label(panel.transform,"Milestone "+names[i],13);milestoneNames[i].text=names[i];milestoneNames[i].alignment=TextAlignmentOptions.Center;var mr=milestoneNames[i].rectTransform;mr.anchorMin=mr.anchorMax=new Vector2(0,1);mr.sizeDelta=new Vector2(29,18);}
            var icon=new GameObject("Kingless hourglass",typeof(RectTransform),typeof(CanvasRenderer),typeof(BuffHourglassGraphic));icon.transform.SetParent(panel.transform,false);hourglass=icon.GetComponent<BuffHourglassGraphic>();hourglass.raycastTarget=false;hourglass.color=new Color(.96f,.75f,.39f);var ir=(RectTransform)icon.transform;ir.anchorMin=ir.anchorMax=new Vector2(1,1);ir.anchoredPosition=new Vector2(-18,-23);ir.sizeDelta=new Vector2(14,22);
            root.SetActive(false);
        }
        private static TextMeshProUGUI Label(Transform parent,string name,float size)
        {var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var t=go.GetComponent<TextMeshProUGUI>();t.font=ChessFontCatalog.TmpFont;t.fontSize=size;t.color=new Color(.94f,.91f,.81f);t.richText=false;t.raycastTarget=false;return t;}
        internal void Sync(IReadOnlyList<BuffHudState> states)
        {
            Rows=Mathf.Min(5,states.Count);urgent=false;bool quest=false,kingless=false,peaceReady=false;
            for(int i=0;i<5;i++)
            {
                labels[i].gameObject.SetActive(i<Rows);if(i>=Rows)continue;var s=states[i];
                labels[i].text=s.Text+"\n"+(s.Target>0?s.Current+" / "+s.Target:s.Current.ToString())+(string.IsNullOrEmpty(s.Unit)?"":" "+s.Unit);
                if(s.BuffId==32){urgent=s.Current<=3;kingless=true;}
                if(s.BuffId==10&&s.Text=="Recruitment ready")peaceReady=true;
                if(s.BuffId==26){quest=true;for(int j=0;j<6;j++)segments[j].color=(s.Mask&(1<<j))!=0?new Color(.9f,.73f,.38f):new Color(.3f,.34f,.37f);}
            }
            panelRect.gameObject.SetActive(Rows>0);panelRect.sizeDelta=new Vector2(260,Rows*RowHeight+(quest?48:24));
            pulse.rectTransform.anchoredPosition=new Vector2(0,Rows>0?-70-panelRect.sizeDelta.y-28:-110);
            for(int i=0;i<segments.Length;i++){segments[i].rectTransform.anchoredPosition=new Vector2(29+i*36,-Rows*RowHeight-30);segments[i].gameObject.SetActive(quest);milestoneNames[i].rectTransform.anchoredPosition=new Vector2(29+i*36,-Rows*RowHeight-14);milestoneNames[i].gameObject.SetActive(quest);}
            if(initialized&&peaceReady&&!wasPeaceReady){pulse.text="Recruitment ready";pulseAge=0;}
            initialized=true;wasPeaceReady=peaceReady;hourglass.gameObject.SetActive(kingless);root.SetActive(Rows>0||pulseAge<1.1f);
        }
        internal void Cue(BuffVisualEvent cue){if(cue.BuffId==8&&cue.Outcome=="ExtraMove"&&cue.Amount==1){pulse.text="+1 extra move";pulseAge=0;pulse.gameObject.SetActive(true);root.SetActive(true);}}
        internal void FinishKingless(){Rows=0;panelRect.gameObject.SetActive(false);pulse.rectTransform.anchoredPosition=new Vector2(0,-110);pulse.text="Kingless deadline reached";pulseAge=0;pulse.gameObject.SetActive(true);root.SetActive(true);}
        internal void Tick(float dt)
        {
            age+=dt;pulseAge+=dt;pulse.gameObject.SetActive(pulseAge<1.1f);pulse.color=new Color(.96f,.84f,.49f,Mathf.Clamp01((1.1f-pulseAge)*3));
            hourglass.color=new Color(.96f,.75f,.39f,urgent&&!UserSettings.Presentation.ReducedMotion&&UserSettings.Presentation.Flash>0?.75f+.15f*Mathf.Sin(age*3):.9f);
            if(Rows==0&&pulseAge>=1.1f)root.SetActive(false);
        }
        internal void Visible(bool visible){root.GetComponent<Canvas>().enabled=visible;}
        internal void Clear(){Rows=0;pulseAge=2;wasPeaceReady=initialized=false;panelRect.gameObject.SetActive(false);pulse.gameObject.SetActive(false);root.SetActive(false);}
    }

}
