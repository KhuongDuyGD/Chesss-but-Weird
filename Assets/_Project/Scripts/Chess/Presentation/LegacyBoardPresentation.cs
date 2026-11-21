using System.Collections.Generic;
using ChessButWeird.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The original boards share scene lighting and the same quality controls as all piece sets.
// Faction boards retain their own presentation. No imported material or model is modified.
public sealed class LegacyBoardPresentation : MonoBehaviour
{
    private VolumeProfile profile;
    private Volume volume;
    private SettingsManager settings;
    private bool celestial;
    private Light boardSun;
    private Quaternion originalSunRotation, appliedSunRotation;
    private readonly Dictionary<Renderer,Material[]> surfaceMaterials=new Dictionary<Renderer,Material[]>();
    private readonly List<Material> surfaceCopies=new List<Material>();
    public void Configure(bool isCelestial)
    {celestial=isCelestial;if(celestial)PrepareCelestialSurface();if(settings!=null)Refresh(null);}
    private void PrepareCelestialSurface()
    {
        if(surfaceMaterials.Count>0)return;
        foreach(var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if(renderer.name!="CL_Chess_Board")continue;
            var original=renderer.sharedMaterials;surfaceMaterials.Add(renderer,original);
            var copies=new Material[original.Length];
            for(int i=0;i<copies.Length;i++)
            {
                if(!original[i])continue;
                var copy=new Material(original[i]){name=original[i].name+" (Celestial surface)",hideFlags=HideFlags.HideAndDontSave};
                // The imported enamel emits white across the playable squares, washing out shadows.
                copy.DisableKeyword("_EMISSION");if(copy.HasProperty("_EmissionColor"))copy.SetColor("_EmissionColor",Color.black);
                copies[i]=copy;surfaceCopies.Add(copy);
            }
            renderer.sharedMaterials=copies;
        }
    }
    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if(celestial)PrepareCelestialSurface();
        if (!volume) volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true; volume.priority = 15;
        profile = ScriptableObject.CreateInstance<VolumeProfile>(); volume.sharedProfile = profile;
        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.2f); bloom.scatter.Override(.5f); bloom.clamp.Override(3);
        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
        settings = UserSettings.Manager; settings.Changed += Refresh;
        Refresh(null);
    }
    private void Refresh(IReadOnlyList<string> _)
    {
        GameRuntimeSettings.RefreshMatchCameras();
        if(celestial)
        {
            var sun=RenderSettings.sun;
            if(!sun&&Camera.main)foreach(var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if(light.type==LightType.Directional&&light.gameObject.scene==Camera.main.gameObject.scene&&(!sun||light.intensity>sun.intensity))sun=light;
            if(sun)
            {
                if(boardSun!=sun){boardSun=sun;originalSunRotation=sun.transform.rotation;}
                // Shorter shadows stay close to the pieces and away from the animated rim.
                appliedSunRotation=Quaternion.Euler(68,-30,0);sun.transform.rotation=appliedSunRotation;
            }
        }
        int effects = UserSettings.Presentation.Effects;
        if (profile && profile.TryGet<Bloom>(out var bloom))
        {
            bloom.intensity.Override(UserSettings.Enabled("reduce_flashing") || effects == 0 ? 0 : effects == 1 ? .08f : .2f);
            bloom.highQualityFiltering.Override(effects >= 2);
        }
        if (Camera.main)
        {
            Camera.main.allowHDR = true;
            var data = Camera.main.GetUniversalAdditionalCameraData();
            data.renderPostProcessing |= effects > 0;
        }
    }
    private void OnDisable()
    {
        if (settings != null) settings.Changed -= Refresh;
        settings = null;
        if(boardSun&&Quaternion.Angle(boardSun.transform.rotation,appliedSunRotation)<.01f)boardSun.transform.rotation=originalSunRotation;
        boardSun=null;
        foreach(var pair in surfaceMaterials)if(pair.Key)pair.Key.sharedMaterials=pair.Value;
        surfaceMaterials.Clear();
        foreach(var material in surfaceCopies)if(material){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
        surfaceCopies.Clear();
        if (profile) { if (Application.isPlaying) Destroy(profile); else DestroyImmediate(profile); }
    }
}
