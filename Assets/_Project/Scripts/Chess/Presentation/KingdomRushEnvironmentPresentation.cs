using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ChessButWeird.Settings;

// Environment-owned art direction. Nothing is persisted to scenes, settings or the account.
// The existing loader destroys this instance before releasing its Addressables handles.
public sealed class KingdomRushEnvironmentPresentation : MonoBehaviour
{
    public bool darkDominion;
    private bool applied;
    private Light key, fill;
    private readonly List<Light> disabledSuns = new List<Light>();
    private AmbientMode ambientMode;
    private Color sky, equator, ground, fogColor;
    private float ambientIntensity, fogDensity, fogStart, fogEnd;
    private bool fog, cameraPost, cameraHdr;
    private FogMode fogMode;
    private Light previousSun;
    private Camera cameraView;
    private VolumeProfile profile;
    private SettingsManager settings;
    private readonly List<FlowSurface> flow = new List<FlowSurface>();
    private sealed class FlowSurface
    {
        public Renderer renderer; public int index; public bool water;
        public Color emission; public MaterialPropertyBlock block = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        ambientMode=RenderSettings.ambientMode; sky=RenderSettings.ambientSkyColor;
        equator=RenderSettings.ambientEquatorColor; ground=RenderSettings.ambientGroundColor;
        ambientIntensity=RenderSettings.ambientIntensity; previousSun=RenderSettings.sun;
        fog=RenderSettings.fog; fogMode=RenderSettings.fogMode; fogColor=RenderSettings.fogColor;
        fogDensity=RenderSettings.fogDensity; fogStart=RenderSettings.fogStartDistance; fogEnd=RenderSettings.fogEndDistance;
        applied=true;
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.enabled && light.type==LightType.Directional && !light.transform.IsChildOf(transform))
            { disabledSuns.Add(light); light.enabled=false; }
        var source=new GameObject("Kingdom Rush soft daylight");source.transform.SetParent(transform,false);
        source.transform.rotation=Quaternion.Euler(darkDominion?49:47,darkDominion?-38:-32,0);
        key=source.AddComponent<Light>();key.type=LightType.Directional;
        key.color=darkDominion?new Color(.78f,.82f,1):new Color(1,.93f,.82f);
        key.intensity=darkDominion?1.75f:1.72f;
        key.shadowStrength=.72f;key.shadowBias=.045f;key.shadowNormalBias=.08f;
        key.shadows=GameRuntimeSettings.ShadowsEnabled?LightShadows.Soft:LightShadows.None;
        settings=UserSettings.Manager;settings.Changed+=SettingsChanged;
        RenderSettings.sun=key;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientIntensity=1;
        // Broad shadowless bounce reveals faces and cloth when the board is orbited.
        var bounce=new GameObject("Kingdom Rush broad bounce");bounce.transform.SetParent(transform,false);
        bounce.transform.rotation=Quaternion.Euler(28,145,0);fill=bounce.AddComponent<Light>();
        fill.type=LightType.Directional;fill.shadows=LightShadows.None;
        fill.color=darkDominion?new Color(.69f,.79f,1):new Color(.86f,.92f,1);
        fill.intensity=darkDominion?.48f:.34f;
        RenderSettings.ambientSkyColor=darkDominion?new Color(.56f,.55f,.66f):new Color(.67f,.74f,.80f);
        RenderSettings.ambientEquatorColor=darkDominion?new Color(.40f,.36f,.46f):new Color(.42f,.46f,.38f);
        RenderSettings.ambientGroundColor=darkDominion?new Color(.22f,.19f,.25f):new Color(.23f,.21f,.17f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
        RenderSettings.fogColor=darkDominion?new Color(.115f,.105f,.15f):new Color(.38f,.47f,.48f);
        // Atmosphere starts beyond the arena and never softens the playable squares.
        float unit=Mathf.Max(.01f,transform.lossyScale.x);
        RenderSettings.fogStartDistance=35*unit;RenderSettings.fogEndDistance=110*unit;
        var volume=gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=30;
        profile=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=profile;
        var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(darkDominion?.20f:.11f);
        bloom.threshold.Override(1.25f);bloom.scatter.Override(.52f);bloom.clamp.Override(3);
        bloom.highQualityFiltering.Override(true);
        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
        var color=profile.Add<ColorAdjustments>(true);color.contrast.Override(3);color.saturation.Override(darkDominion?-2:0);
        color.postExposure.Override(darkDominion?.22f:0);
        var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(.06f);vignette.smoothness.Override(.45f);
        cameraView=Camera.main;
        if(cameraView)
        {
            var data=cameraView.GetUniversalAdditionalCameraData();cameraPost=data.renderPostProcessing;
            cameraHdr=cameraView.allowHDR;data.renderPostProcessing=true;cameraView.allowHDR=true;
        }
        foreach(var renderer in GetComponentsInChildren<MeshRenderer>())
        {
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++)
            {
                var material=materials[i];if(!material)continue;
                bool water=material.name.Contains("River");
                if(!water&&!material.name.Contains("Sickly_soul")&&!material.name.Contains("Cinder"))continue;
                // Native flame renderers own their animation and do not expose Lit emission.
                if(water?!material.HasProperty("_BaseMap"):!material.HasProperty("_EmissionColor"))continue;
                flow.Add(new FlowSurface{renderer=renderer,index=i,water=water,emission=water?Color.black:material.GetColor("_EmissionColor")});
            }
        }
        SettingsChanged(null);
    }

    private void LateUpdate()
    {
        if(!applied)return;
        // Camera dolly keeps the complete orthographic near rectangle above terrain.
        // Keep fog beyond the arena even at the lowest pitch / widest zoom.
        if(cameraView)
        {
            float unit=Mathf.Max(.01f,transform.lossyScale.x);
            float arenaDepth=Vector3.Dot(transform.position-cameraView.transform.position,cameraView.transform.forward);
            RenderSettings.fogStartDistance=Mathf.Max(35*unit,arenaDepth+25*unit);
            RenderSettings.fogEndDistance=Mathf.Max(110*unit,arenaDepth+100*unit);
        }
        bool still=UserSettings.Presentation.ReducedMotion;
        float t=still?0:Time.timeSinceLevelLoad;
        foreach(var surface in flow)
        {
            if(!surface.renderer)continue;
            surface.renderer.GetPropertyBlock(surface.block,surface.index);
            if(surface.water)surface.block.SetVector("_BaseMap_ST",new Vector4(1,1,t*.009f,t*.017f));
            else surface.block.SetColor("_EmissionColor",surface.emission*(still||UserSettings.Enabled("reduce_flashing")?1:.96f+.04f*Mathf.Sin(t*1.05f+surface.index)));
            surface.renderer.SetPropertyBlock(surface.block,surface.index);
        }
    }

    private void SettingsChanged(IReadOnlyList<string> ids)
    {
        if(key)key.shadows=GameRuntimeSettings.ShadowsEnabled?LightShadows.Soft:LightShadows.None;
        if(profile&&profile.TryGet<Bloom>(out var bloom))
        {
            int quality=UserSettings.Presentation.Effects;
            float amount=quality==0?0:quality==1?.06f:darkDominion?.16f:.10f;
            if(UserSettings.Enabled("reduce_flashing"))amount=Mathf.Min(amount,.04f);
            bloom.intensity.Override(amount);bloom.highQualityFiltering.Override(quality>=2);
        }
    }

    private void OnDisable()
    {
        if(!applied)return;applied=false;
        if(settings!=null)settings.Changed-=SettingsChanged;settings=null;
        RenderSettings.ambientMode=ambientMode;RenderSettings.ambientSkyColor=sky;
        RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;
        RenderSettings.ambientIntensity=ambientIntensity;RenderSettings.sun=previousSun;
        RenderSettings.fog=fog;RenderSettings.fogMode=fogMode;RenderSettings.fogColor=fogColor;
        RenderSettings.fogDensity=fogDensity;RenderSettings.fogStartDistance=fogStart;RenderSettings.fogEndDistance=fogEnd;
        foreach(var light in disabledSuns)if(light)light.enabled=true;
        disabledSuns.Clear();flow.Clear();
        if(cameraView)
        {
            var aa=GameRuntimeSettings.AppliedAntiAliasing;
            cameraView.GetUniversalAdditionalCameraData().renderPostProcessing=cameraPost||aa==GameAntiAliasingMode.FXAA||aa==GameAntiAliasingMode.SMAA;
            cameraView.allowHDR=cameraHdr;
        }
        if(key)Destroy(key.gameObject);
        if(fill)Destroy(fill.gameObject);
        foreach(var volume in GetComponents<Volume>())if(volume.sharedProfile==profile)Destroy(volume);
        if(profile){foreach(var component in profile.components)Destroy(component);Destroy(profile);}
    }
}
