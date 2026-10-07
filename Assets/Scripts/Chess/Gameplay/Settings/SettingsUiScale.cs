using UnityEngine;
using UnityEngine.UI;

/// <summary>Scale selected gameplay information canvases without touching Inventory artwork/layout.</summary>
[RequireComponent(typeof(CanvasScaler))]
public sealed class SettingsUiScale : MonoBehaviour
{
    private CanvasScaler scaler;
    private Vector2 baseline;
    private float applied=-1;
    private void Start(){scaler=GetComponent<CanvasScaler>();baseline=scaler.referenceResolution;Apply();}
    private void LateUpdate()=>Apply();
    private void Apply()
    {
        if(!scaler)return;
        float value=UserSettings.Get("ui_scale")/100;
        if(Mathf.Approximately(applied,value))return;
        applied=value;scaler.referenceResolution=baseline/value;
    }
}
