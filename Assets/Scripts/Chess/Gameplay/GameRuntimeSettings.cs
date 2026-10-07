using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public enum GameAntiAliasingMode
{
    Auto,
    Off,
    FXAA,
    SMAA,
    MSAA2x,
    MSAA4x
}

public static class GameRuntimeSettings
{
    private static readonly Dictionary<int, RenderPipelineAsset> pipelineOverrides = new Dictionary<int, RenderPipelineAsset>();
    private static readonly Dictionary<int, UniversalRenderPipelineAsset> pipelineCopies = new Dictionary<int, UniversalRenderPipelineAsset>();
    private const int MinimumFps = 30;
    private const int MaximumFps = 240;
    public static int MusicVolumePercent { get => (int)UserSettings.Get("music"); set => UserSettings.Manager.Set("music", value); }
    public static int SoundVolumePercent { get => (int)UserSettings.Get("sfx"); set => UserSettings.Manager.Set("sfx", value); }
    public static bool AutomaticGraphics => UserSettings.Get("graphics") == 0;
    public static bool AutomaticFrameRate => UserSettings.Get("fps") == 0;
    public static bool ShadowsEnabled { get => UserSettings.Get("shadows") > 0; set => UserSettings.Manager.Set("shadows", value ? 2 : 0); }
    public static int GraphicsPreset => AutomaticGraphics ? RecommendedGraphicsPreset : (int)UserSettings.Get("graphics") - 1;
    public static int TargetFps => AutomaticFrameRate ? RecommendedTargetFps : (int)UserSettings.Get("fps");
    public static GameAntiAliasingMode AntiAliasing { get => (GameAntiAliasingMode)(int)UserSettings.Get("aa"); set => UserSettings.Manager.Set("aa", (int)value); }

    public static float MusicVolume01 => MusicVolumePercent / 100f;
    public static float SoundVolume01 => SoundVolumePercent / 100f;
    public static int RecommendedGraphicsPreset => TierToGraphicsPreset(GetHardwareTier());
    public static int RecommendedTargetFps => GetRecommendedTargetFps(GetHardwareTier());
    public static GameAntiAliasingMode RecommendedAntiAliasing => GetRecommendedAntiAliasing(RecommendedGraphicsPreset);
    public static GameAntiAliasingMode AppliedAntiAliasing => ResolveAntiAliasing(AntiAliasing, GraphicsPreset);
    public static string DeviceTierLabel => GetHardwareTier().ToString();
    public static string RecommendedSettingsLabel =>
        $"{DeviceTierLabel} device: {GraphicsPresetLabel(RecommendedGraphicsPreset)}, " +
        $"{RecommendedTargetFps} FPS, {AntiAliasingLabel(RecommendedAntiAliasing)}";

    // Compatibility for callers that still reason in Unity quality-level indexes.
    public static int QualityIndex => PresetToQualityIndex(GraphicsPreset);
    public static int RecommendedQualityIndex => PresetToQualityIndex(RecommendedGraphicsPreset);

    public static void ApplySaved()
    {
        ApplyQualityProfile(GraphicsPreset);
        EnsureRuntimePipeline();
        ApplyFrameRate(TargetFps);
        ApplyAntiAliasing(AntiAliasing);
        ApplyShadows();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Application.backgroundLoadingPriority = ThreadPriority.Low;
        Time.maximumDeltaTime = 1f / 20f;
        GameMusicManager.RefreshVolumeFromSettings();
    }

    public static void UseAutomaticGraphics() => UserSettings.Manager.Set("graphics", 0);
    public static void SetGraphicsPreset(int preset) => UserSettings.Manager.Set("graphics", Mathf.Clamp(preset, 0, 2) + 1);
    public static void UseAutomaticFrameRate() => UserSettings.Manager.Set("fps", 0);
    public static void SetTargetFps(int fps) => UserSettings.Manager.Set("fps", fps);
    public static void ResetPerformanceToRecommended() => UserSettings.Manager.ResetCategory(ChessButWeird.Settings.SettingsCategory.Graphics);

    public static string GraphicsPresetLabel(int preset)
    {
        switch (Mathf.Clamp(preset, 0, 2))
        {
            case 0: return "Low";
            case 1: return "Medium";
            default: return "High";
        }
    }

    public static string AntiAliasingLabel(GameAntiAliasingMode mode)
    {
        switch (mode)
        {
            case GameAntiAliasingMode.Off: return "Off";
            case GameAntiAliasingMode.FXAA: return "FXAA";
            case GameAntiAliasingMode.SMAA: return "SMAA";
            case GameAntiAliasingMode.MSAA2x: return "MSAA 2x";
            case GameAntiAliasingMode.MSAA4x: return "MSAA 4x";
            default: return "Auto";
        }
    }

    public static string QualityLabel(int index)
    {
        string[] names = QualitySettings.names;
        if (names == null || names.Length == 0)
            return "Default";
        return names[Mathf.Clamp(index, 0, names.Length - 1)];
    }

    private static void ApplyQualityProfile(int preset)
    {
        preset = Mathf.Clamp(preset, 0, 2);
        QualitySettings.SetQualityLevel(PresetToQualityIndex(preset), true);

        switch (preset)
        {
            case 0:
                QualitySettings.pixelLightCount = 1;
                QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, 30f);
                QualitySettings.lodBias = 0.8f;
                QualitySettings.globalTextureMipmapLimit = 1;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                QualitySettings.softParticles = false;
                QualitySettings.realtimeReflectionProbes = false;
                break;
            case 1:
                QualitySettings.pixelLightCount = 2;
                QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, 55f);
                QualitySettings.lodBias = 1.25f;
                QualitySettings.globalTextureMipmapLimit = 0;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                QualitySettings.softParticles = true;
                break;
            default:
                QualitySettings.pixelLightCount = Mathf.Max(2, QualitySettings.pixelLightCount);
                QualitySettings.shadowDistance = Mathf.Max(75f, QualitySettings.shadowDistance);
                QualitySettings.lodBias = Mathf.Max(2f, QualitySettings.lodBias);
                QualitySettings.globalTextureMipmapLimit = 0;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                QualitySettings.softParticles = true;
                QualitySettings.realtimeReflectionProbes = true;
                break;
        }
        ApplyShadows();
    }

    private static void EnsureRuntimePipeline()
    {
        if(!Application.isPlaying || !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset))return;
        int index=QualitySettings.GetQualityLevel();
        if(pipelineCopies.TryGetValue(index,out var existing) && existing==asset)return;
        pipelineOverrides[index]=QualitySettings.renderPipeline;
        var copy=Object.Instantiate(asset);copy.name=asset.name+" (User settings runtime)";copy.hideFlags=HideFlags.HideAndDontSave;
        pipelineCopies[index]=copy;QualitySettings.renderPipeline=copy;
    }
    public static void ReleaseRuntimePipelines()
    {
        int selected=QualitySettings.GetQualityLevel();
        foreach(var entry in pipelineOverrides){QualitySettings.SetQualityLevel(entry.Key,false);QualitySettings.renderPipeline=entry.Value;}
        QualitySettings.SetQualityLevel(selected,false);
        foreach(var copy in pipelineCopies.Values)if(copy)Object.Destroy(copy);
        pipelineOverrides.Clear();pipelineCopies.Clear();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private static void ApplyShadows()
    {
        int tier = (int)UserSettings.Get("shadows");
        EnsureRuntimePipeline();
        bool enabled = tier > 0;
        float distance = tier == 1 ? 25 : tier == 2 ? 50 : 80;
        int atlas = tier == 1 ? 512 : tier == 2 ? 1024 : 2048;
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
        {
            pipeline.shadowDistance = enabled ? distance : 0;
            pipeline.mainLightShadowmapResolution = atlas;
            pipeline.additionalLightsShadowmapResolution = atlas;
            pipeline.shadowCascadeCount = tier <= 1 ? 1 : tier == 2 ? 2 : 4;
        }
        QualitySettings.shadowDistance = enabled ? distance : 0;
        QualitySettings.shadowResolution = tier == 1 ? UnityEngine.ShadowResolution.Low : tier == 2 ? UnityEngine.ShadowResolution.Medium : UnityEngine.ShadowResolution.High;
        QualitySettings.shadowCascades = tier <= 1 ? 0 : tier == 2 ? 2 : 4;
        QualitySettings.shadows = enabled ? UnityEngine.ShadowQuality.All : UnityEngine.ShadowQuality.Disable;
        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (!camera || !camera.gameObject.scene.IsValid() ||
                (camera.hideFlags & (HideFlags.DontSave | HideFlags.HideAndDontSave)) != 0) continue;
            // URP uses per-camera shadow rendering; QualitySettings alone is insufficient.
            camera.GetUniversalAdditionalCameraData().renderShadows = enabled;
        }
        ChessModelRendering.RefreshShadows(enabled);
    }

    private static void ApplyFrameRate(int fps)
    {
        QualitySettings.vSyncCount = UserSettings.Enabled("vsync") ? 1 : 0;
        Application.targetFrameRate = fps == -1 ? -1 : Mathf.Clamp(fps, MinimumFps, MaximumFps);
    }

    private static void ApplyAntiAliasing(GameAntiAliasingMode selectedMode)
    {
        GameAntiAliasingMode mode = ResolveAntiAliasing(selectedMode, GraphicsPreset);
        int msaaSamples = mode == GameAntiAliasingMode.MSAA4x ? 4 :
            mode == GameAntiAliasingMode.MSAA2x ? 2 : 1;

        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipelineAsset)
            pipelineAsset.msaaSampleCount = msaaSamples;

        QualitySettings.antiAliasing = msaaSamples > 1 ? msaaSamples : 0;

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        for (int i = 0; i < cameras.Length; i++)
            ApplyCameraAntiAliasing(cameras[i], mode, msaaSamples);
    }

    private static void ApplyCameraAntiAliasing(Camera camera, GameAntiAliasingMode mode, int msaaSamples)
    {
        if (!camera || !camera.gameObject.scene.IsValid() ||
            (camera.hideFlags & (HideFlags.DontSave | HideFlags.HideAndDontSave)) != 0)
            return;

        camera.allowMSAA = msaaSamples > 1;
        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderShadows = ShadowsEnabled;
        cameraData.antialiasingQuality = AntialiasingQuality.High;

        switch (mode)
        {
            case GameAntiAliasingMode.FXAA:
                cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                cameraData.renderPostProcessing = true;
                break;
            case GameAntiAliasingMode.SMAA:
                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cameraData.renderPostProcessing = true;
                break;
            default:
                cameraData.antialiasing = AntialiasingMode.None;
                break;
        }
    }

    private static void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        ApplyAntiAliasing(AntiAliasing);
        ApplyShadows();
    }

    private static GameAntiAliasingMode ResolveAntiAliasing(GameAntiAliasingMode mode, int graphicsPreset)
    {
        if (mode == GameAntiAliasingMode.Auto)
            mode = GetRecommendedAntiAliasing(graphicsPreset);

        if ((mode == GameAntiAliasingMode.MSAA2x || mode == GameAntiAliasingMode.MSAA4x) &&
            SystemInfo.supportsMultisampledTextures == 0)
        {
            return GameAntiAliasingMode.SMAA;
        }

        return mode;
    }

    private static int PresetToQualityIndex(int preset)
    {
        int max = Mathf.Max(0, QualitySettings.names.Length - 1);
        return preset <= 0 ? 0 : max;
    }

    private static int TierToGraphicsPreset(HardwareTier tier)
    {
        switch (tier)
        {
            case HardwareTier.Low: return 0;
            case HardwareTier.Medium: return 1;
            default: return 2;
        }
    }

    private static int GetRecommendedTargetFps(HardwareTier tier)
    {
        if (tier == HardwareTier.Low)
            return 30;

        float refreshRate = (float)Screen.currentResolution.refreshRateRatio.value;
        if (tier == HardwareTier.High && refreshRate >= 115f)
        {
            if (refreshRate >= 160f)
                return 165;
            if (refreshRate >= 143f)
                return 144;
            return 120;
        }

        return 60;
    }

    private static GameAntiAliasingMode GetRecommendedAntiAliasing(int graphicsPreset)
    {
        if (graphicsPreset <= 0)
            return GameAntiAliasingMode.FXAA;
        if (graphicsPreset == 1)
            return GameAntiAliasingMode.SMAA;
        return SystemInfo.supportsMultisampledTextures != 0
            ? GameAntiAliasingMode.MSAA4x
            : GameAntiAliasingMode.SMAA;
    }

    private static HardwareTier GetHardwareTier()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            return HardwareTier.Low;

        int score = 0;
        int processorCount = Mathf.Max(1, SystemInfo.processorCount);
        int processorFrequency = Mathf.Max(0, SystemInfo.processorFrequency);
        int systemMemory = Mathf.Max(0, SystemInfo.systemMemorySize);
        int graphicsMemory = Mathf.Max(0, SystemInfo.graphicsMemorySize);

        score += processorCount >= 8 ? 2 : processorCount >= 6 ? 1 : processorCount <= 4 ? -1 : 0;
        score += processorFrequency >= 3000 ? 1 : processorFrequency > 0 && processorFrequency < 1800 ? -1 : 0;
        score += systemMemory >= 16000 ? 2 : systemMemory >= 8000 ? 1 : systemMemory > 0 && systemMemory <= 4000 ? -2 : 0;
        score += graphicsMemory >= 8000 ? 3 : graphicsMemory >= 4000 ? 2 : graphicsMemory >= 2000 ? 1 :
            graphicsMemory > 0 && graphicsMemory <= 1000 ? -2 : 0;
        score += SystemInfo.graphicsShaderLevel >= 50 ? 1 : SystemInfo.graphicsShaderLevel < 45 ? -1 : 0;
        score += SystemInfo.supportsComputeShaders ? 1 : -1;

        long screenPixels = (long)Screen.currentResolution.width * Screen.currentResolution.height;
        if (screenPixels > 3000000L)
            score--;
        if (Application.isMobilePlatform)
            score -= 2;

        if (score >= 7)
            return HardwareTier.High;
        if (score >= 3)
            return HardwareTier.Medium;
        return HardwareTier.Low;
    }

    private enum HardwareTier
    {
        Low,
        Medium,
        High
    }
}
