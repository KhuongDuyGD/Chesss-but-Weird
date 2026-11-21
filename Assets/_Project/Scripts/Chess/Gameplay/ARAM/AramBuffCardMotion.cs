using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Unscaled staggered reveal and focus lift, with immediate reduced-motion fallback.</summary>
public sealed class AramBuffCardMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private RectTransform rect;
    private CanvasGroup group;
    private Vector2 rest;
    private float started, delay, hover, velocity;
    private bool pointer, focused, pending, selected;
    public void Initialize(int index)
    {
        rect=(RectTransform)transform;rest=rect.anchoredPosition;
        group=gameObject.AddComponent<CanvasGroup>();
        started=Time.unscaledTime;delay=Mathf.Min(index,3)*.055f;
        Tick(Time.unscaledTime,0);
    }
    public void SetPending(bool waiting,bool chosen) { pending=waiting;selected=chosen; }
    public void OnPointerEnter(PointerEventData data)=>pointer=true;
    public void OnPointerExit(PointerEventData data)=>pointer=false;
    public void OnSelect(BaseEventData data)=>focused=true;
    public void OnDeselect(BaseEventData data)=>focused=false;
    private void OnDisable(){pointer=focused=false;hover=velocity=0;}
    private void Update()=>Tick(Time.unscaledTime,Time.unscaledDeltaTime);
    public void Tick(float now,float delta)
    {
        if(!rect)return;
        bool reduced=UserSettings.Presentation.ReducedMotion||UserSettings.Presentation.AnimationSpeed==0;
        float duration=UserSettings.Presentation.AnimationDuration(.38f);
        float progress=reduced?1:Mathf.Clamp01((now-started-delay)/Mathf.Max(.01f,duration));
        float eased=1-Mathf.Pow(1-progress,3);
        float target=!pending&&(pointer||focused)?1:0;
        hover=reduced?0:Mathf.SmoothDamp(hover,target,ref velocity,.10f,Mathf.Infinity,Mathf.Max(0,delta));
        rect.anchoredPosition=rest+new Vector2(0,(1-eased)*-12+hover*7);
        rect.localScale=Vector3.one*(1+hover*.012f);
        group.alpha=Mathf.Lerp(.25f,1,eased)*(pending && !selected ? .62f : 1);
        // Input is never delayed by presentation or an accessibility preference.
    }
}
