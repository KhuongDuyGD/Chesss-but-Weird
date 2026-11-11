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
    private void OnEnable()
    {
        if (!Application.isPlaying) return;
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
        if (profile) { if (Application.isPlaying) Destroy(profile); else DestroyImmediate(profile); }
    }
}
