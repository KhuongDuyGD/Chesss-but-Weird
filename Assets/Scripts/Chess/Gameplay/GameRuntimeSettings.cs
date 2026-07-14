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
        get => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        set
        {
            int index = Mathf.Clamp(value, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            PlayerPrefs.SetInt(QualityKey, index);
            PlayerPrefs.Save();
            QualitySettings.SetQualityLevel(index, true);
        }
    }

    public static int TargetFps
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(FpsKey, DefaultFps), 30, 240);
        set
        {
            int fps = Mathf.Clamp(value, 30, 240);
            PlayerPrefs.SetInt(FpsKey, fps);
            PlayerPrefs.Save();
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fps;
        }
    }

    public static float MusicVolume01 => MusicVolumePercent / 100f;
    public static float SoundVolume01 => SoundVolumePercent / 100f;

    public static void ApplySaved()
    {
        QualitySettings.SetQualityLevel(QualityIndex, true);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFps;
        GameMusicManager.RefreshVolumeFromSettings();
    }

    public static string QualityLabel(int index)
    {
        string[] names = QualitySettings.names;
        if (names == null || names.Length == 0)
            return "Default";
        return names[Mathf.Clamp(index, 0, names.Length - 1)];
    }
}
