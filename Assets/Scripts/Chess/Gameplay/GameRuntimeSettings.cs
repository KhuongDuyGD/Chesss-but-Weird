using UnityEngine;

public static class GameRuntimeSettings
{
    private const string MusicVolumeKey = "cbw_settings_music_volume";
    private const string SoundVolumeKey = "cbw_settings_sound_volume";
    private const string QualityKey = "cbw_settings_quality";
    private const string FpsKey = "cbw_settings_fps";

    private const int DefaultMusicVolume = 80;
    private const int DefaultSoundVolume = 100;
    private const int DefaultFps = 60;
    private const int MinimumFps = 30;
    private const int MaximumFps = 240;

    public static int MusicVolumePercent
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(MusicVolumeKey, DefaultMusicVolume), 0, 100);
        set
        {
            PlayerPrefs.SetInt(MusicVolumeKey, Mathf.Clamp(value, 0, 100));
            PlayerPrefs.Save();
            GameMusicManager.RefreshVolumeFromSettings();
        }
    }

    public static int SoundVolumePercent
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(SoundVolumeKey, DefaultSoundVolume), 0, 100);
        set
        {
            PlayerPrefs.SetInt(SoundVolumeKey, Mathf.Clamp(value, 0, 100));
            PlayerPrefs.Save();
        }
    }

    public static int QualityIndex
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, RecommendedQualityIndex), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        set
        {
            int index = Mathf.Clamp(value, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            PlayerPrefs.SetInt(QualityKey, index);
            PlayerPrefs.Save();
            ApplyQualityLevel(index);
        }
    }

    public static int TargetFps
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(FpsKey, RecommendedTargetFps), MinimumFps, MaximumFps);
        set
        {
            int fps = Mathf.Clamp(value, MinimumFps, MaximumFps);
            PlayerPrefs.SetInt(FpsKey, fps);
            PlayerPrefs.Save();
            ApplyFrameRate(fps);
        }
    }

    public static float MusicVolume01 => MusicVolumePercent / 100f;
    public static float SoundVolume01 => SoundVolumePercent / 100f;
    public static int RecommendedQualityIndex => GetRecommendedQualityIndex();
    public static int RecommendedTargetFps => GetRecommendedTargetFps();

    public static void ApplySaved()
    {
        EnsureRecommendedPerformanceDefaults();
        ApplyQualityLevel(QualityIndex);
        ApplyFrameRate(TargetFps);
        Application.backgroundLoadingPriority = ThreadPriority.Low;
        Time.maximumDeltaTime = 1f / 20f;
        GameMusicManager.RefreshVolumeFromSettings();
    }

    public static void ResetPerformanceToRecommended()
    {
        QualityIndex = RecommendedQualityIndex;
        TargetFps = RecommendedTargetFps;
    }

    public static string QualityLabel(int index)
    {
        string[] names = QualitySettings.names;
        if (names == null || names.Length == 0)
            return "Default";
        return names[Mathf.Clamp(index, 0, names.Length - 1)];
    }

    private static void EnsureRecommendedPerformanceDefaults()
    {
        bool changed = false;
        if (!PlayerPrefs.HasKey(QualityKey))
        {
            PlayerPrefs.SetInt(QualityKey, RecommendedQualityIndex);
            changed = true;
        }

        if (!PlayerPrefs.HasKey(FpsKey))
        {
            PlayerPrefs.SetInt(FpsKey, RecommendedTargetFps);
            changed = true;
        }

        if (changed)
            PlayerPrefs.Save();
    }

    private static void ApplyQualityLevel(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex, true);
        ApplyQualityGuards(qualityIndex);
    }

    private static void ApplyFrameRate(int fps)
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Mathf.Clamp(fps, MinimumFps, MaximumFps);
    }

    private static void ApplyQualityGuards(int qualityIndex)
    {
        int maxQualityIndex = Mathf.Max(0, QualitySettings.names.Length - 1);
        float normalizedQuality = maxQualityIndex == 0 ? 1f : qualityIndex / (float)maxQualityIndex;

        if (normalizedQuality < 0.34f)
        {
            QualitySettings.antiAliasing = 0;
            QualitySettings.pixelLightCount = Mathf.Min(QualitySettings.pixelLightCount, 1);
            QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, 30f);
            QualitySettings.lodBias = Mathf.Min(QualitySettings.lodBias, 0.8f);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            return;
        }

        if (normalizedQuality < 0.67f)
        {
            QualitySettings.pixelLightCount = Mathf.Min(QualitySettings.pixelLightCount, 2);
            QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, 55f);
            QualitySettings.lodBias = Mathf.Min(QualitySettings.lodBias, 1.1f);
        }
    }

    private static int GetRecommendedQualityIndex()
    {
        int maxQualityIndex = Mathf.Max(0, QualitySettings.names.Length - 1);
        switch (GetHardwareTier())
        {
            case HardwareTier.Low:
                return 0;
            case HardwareTier.Medium:
                return Mathf.RoundToInt(maxQualityIndex * 0.5f);
            default:
                return maxQualityIndex;
        }
    }

    private static int GetRecommendedTargetFps()
    {
        return GetHardwareTier() == HardwareTier.Low ? MinimumFps : DefaultFps;
    }

    private static HardwareTier GetHardwareTier()
    {
        int graphicsMemory = SystemInfo.graphicsMemorySize;
        bool limitedGpu = graphicsMemory > 0 && graphicsMemory <= 1024;
        bool modestGpu = graphicsMemory > 0 && graphicsMemory <= 2048;

        if (SystemInfo.systemMemorySize <= 4096 ||
            SystemInfo.processorCount <= 4 ||
            limitedGpu ||
            SystemInfo.graphicsShaderLevel < 35)
        {
            return HardwareTier.Low;
        }

        if (SystemInfo.systemMemorySize <= 8192 ||
            SystemInfo.processorCount <= 6 ||
            modestGpu)
        {
            return HardwareTier.Medium;
        }

        return HardwareTier.High;
    }

    private enum HardwareTier
    {
        Low,
        Medium,
        High
    }
}
