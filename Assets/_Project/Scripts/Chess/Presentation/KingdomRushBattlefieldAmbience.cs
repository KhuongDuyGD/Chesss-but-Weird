using System.Collections.Generic;
using ChessButWeird.Settings;
using UnityEngine;

// One controller per environment/board, with no light proliferation or per-frame allocations.
public sealed class KingdomRushBattlefieldAmbience : MonoBehaviour
{
    public bool darkDominion, drivesWind;
    public ParticleSystem[] motes=new ParticleSystem[0], accents=new ParticleSystem[0], mist=new ParticleSystem[0];
    public Renderer[] flames=new Renderer[0];
    public Transform[] birds=new Transform[0], leftWings=new Transform[0], rightWings=new Transform[0];
    private SettingsManager settings;
    private float previousWind;
    private int quality;
    private bool reduced;
    private MaterialPropertyBlock block;
    private static readonly int Wind=Shader.PropertyToID("_KRWindStrength");
    public bool DecorativeMotionEnabled => !reduced&&quality>0;
    public int EffectsTier => quality;
    private void OnEnable()
    {
        if(!Application.isPlaying)return;
        block=new MaterialPropertyBlock();
        previousWind=Shader.GetGlobalFloat(Wind);
        settings=UserSettings.Manager;settings.Changed+=Changed;Changed(null);
    }
    private void Changed(IReadOnlyList<string> ids)
    {
        quality=UserSettings.Presentation.Effects;reduced=UserSettings.Presentation.ReducedMotion;
        float factor=reduced||quality==0?0:quality==1?.45f:1;
        SetParticles(motes,factor);SetParticles(accents,factor);SetParticles(mist,quality>=2?factor:0);
        if(drivesWind)Shader.SetGlobalFloat(Wind,reduced||quality==0?0:quality==1?.55f:1);
        foreach(var flame in flames)
        {
            if(!flame)continue;flame.enabled=quality>0&&!UserSettings.Enabled("visual_chaos");
            flame.GetPropertyBlock(block);block.SetFloat("_MotionAmount",reduced?0:1);flame.SetPropertyBlock(block);
        }
        foreach(var bird in birds)if(bird)bird.gameObject.SetActive(false);
    }
    private static void SetParticles(ParticleSystem[] systems,float factor)
    {
        foreach(var particles in systems)
        {
            if(!particles)continue;
            // Store the authored rate in a small component, never repeatedly multiply a modified rate.
            var budget=particles.GetComponent<KingdomRushParticleBudget>();
            var emission=particles.emission;emission.rateOverTime=budget.authoredRate*factor;
            if(factor>0){if(!particles.isPlaying)particles.Play();}
            else particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
    private void LateUpdate()
    { AnimateBirds(Time.timeSinceLevelLoad); }
    private void AnimateBirds(float time)
    {
        if(reduced||quality==0||birds.Length==0)return;
        for(int i=0;i<birds.Length;i++)
        {
            float cycle=Mathf.Repeat(time+i*17,46);bool visible=cycle<9&&(quality>=2||i==0);
            var bird=birds[i];if(!bird)continue;
            if(bird.gameObject.activeSelf!=visible)bird.gameObject.SetActive(visible);
            if(!visible)continue;
            float u=cycle/9,side=i==0?-1:1;
            bird.localPosition=new Vector3(side*(9+2*Mathf.Sin(u*Mathf.PI)),3.8f+.8f*Mathf.Sin(u*Mathf.PI),Mathf.Lerp(-14,14,u));
            var tangent=new Vector3(side*2*Mathf.PI*Mathf.Cos(u*Mathf.PI),.8f*Mathf.PI*Mathf.Cos(u*Mathf.PI),28);
            bird.localRotation=Quaternion.LookRotation(tangent)*Quaternion.Euler(0,0,side*12*Mathf.Sin(u*2*Mathf.PI));
            float flap=Mathf.Sin(time*7.5f+i)*20;
            leftWings[i].localRotation=Quaternion.Euler(0,0,flap);
            rightWings[i].localRotation=Quaternion.Euler(0,0,-flap);
        }
    }
    private void OnDisable()
    {
        if(settings!=null)settings.Changed-=Changed;
        if(settings!=null&&drivesWind)Shader.SetGlobalFloat(Wind,previousWind);
        settings=null;
    }
}
