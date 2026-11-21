using System.Collections.Generic;
using ChessButWeird.Settings;
using UnityEngine;

// Only approved painted magic is emissive; no character surface or collider is deformed.
public sealed class KingdomRushPieceMagic : MonoBehaviour
{
    public Renderer surface;
    public Renderer staffFlame;
    private MaterialPropertyBlock block,flameBlock;
    private SettingsManager settings;
    private Color emission;
    private float phase;
    private bool pulse;
    private static readonly int Emission=Shader.PropertyToID("_EmissionColor");
    public float PulsePeriod => 6.2f;
    public float EmissionMultiplier(float seconds) => pulse?.91f+.09f*Mathf.Sin(seconds*(2*Mathf.PI/PulsePeriod)+phase):1f;
    private void OnEnable()
    {
        if(!Application.isPlaying||!surface)return;
        block=new MaterialPropertyBlock();flameBlock=new MaterialPropertyBlock();
        emission=surface.sharedMaterial.GetColor(Emission);
        phase=Mathf.Repeat(GetInstanceID()*.618034f,2*Mathf.PI);
        settings=UserSettings.Manager;settings.Changed+=Changed;Changed(null);
    }
    private void Changed(IReadOnlyList<string> ids)
    {
        bool reduced=UserSettings.Presentation.ReducedMotion;
        pulse=!reduced&&!UserSettings.Enabled("reduce_flashing")&&UserSettings.Presentation.Effects>0;
        if(staffFlame)
        {
            staffFlame.gameObject.SetActive(UserSettings.Presentation.Effects>0&&!UserSettings.Enabled("visual_chaos"));
            staffFlame.GetPropertyBlock(flameBlock);flameBlock.SetFloat("_MotionAmount",reduced?0:1);
            staffFlame.SetPropertyBlock(flameBlock);
        }
        Apply(Time.timeSinceLevelLoad);
    }
    private void LateUpdate(){if(pulse)Apply(Time.timeSinceLevelLoad);}
    private void Apply(float seconds)
    {
        if(!surface||block==null)return;
        surface.GetPropertyBlock(block);block.SetColor(Emission,emission*EmissionMultiplier(seconds));surface.SetPropertyBlock(block);
    }
    private void OnDisable()
    {
        if(settings!=null)settings.Changed-=Changed;
        settings=null;
        if(surface&&Application.isPlaying&&block!=null){surface.GetPropertyBlock(block);block.SetColor(Emission,emission);surface.SetPropertyBlock(block);}
    }
}
