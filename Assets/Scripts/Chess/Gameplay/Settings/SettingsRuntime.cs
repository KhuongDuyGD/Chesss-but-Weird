using System;
using System.Collections.Generic;
using System.Globalization;
using ChessButWeird.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Applies preferences outside match rules. Sliders save to memory immediately, disk after a quiet interval.</summary>
public sealed class SettingsRuntime : MonoBehaviour
{
    private float flushAt = -1;
    private float baselineListenerVolume;
    private bool focused = true;
    private void Awake()
    {
        baselineListenerVolume = AudioListener.volume;
        UserSettings.Manager.Changed += OnChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameRuntimeSettings.ApplySaved();
        SettingsDisplay.ApplySaved();
        ApplyAudio();
    }
    private void OnChanged(IReadOnlyList<string> ids)
    {
        flushAt = Time.unscaledTime + .5f;
        bool graphics=false, audio=false;
        foreach(var id in ids)
        {
            var category=UserSettings.Manager.Definition(id).Category;
            graphics |= category==SettingsCategory.Graphics && (id=="graphics"||id=="aa"||id=="shadows"||id=="fps"||id=="vsync");
            audio |= category==SettingsCategory.Audio;
        }
        if(graphics) GameRuntimeSettings.ApplySaved();
        if(audio) { ApplyAudio(); GameMusicManager.RefreshVolumeFromSettings(); }
    }
    private void ApplyAudio() => AudioListener.volume = baselineListenerVolume * UserSettings.Get("master") / 100 *
        (UserSettings.Enabled("mute_unfocused")&&!focused?0:1);
    private void OnSceneLoaded(Scene scene,LoadSceneMode mode) => ApplyAudio();
    private void Update()
    { if(flushAt>=0 && Time.unscaledTime>=flushAt){flushAt=-1;UserSettings.Manager.Flush();} }
    private void OnApplicationFocus(bool hasFocus) { focused=hasFocus;ApplyAudio(); }
    private void OnApplicationQuit() => UserSettings.Manager.Flush();
    private void OnDestroy()
    {
        UserSettings.Manager.Changed -= OnChanged;SceneManager.sceneLoaded -= OnSceneLoaded;
        UserSettings.Manager.Flush(); AudioListener.volume=baselineListenerVolume;
        GameRuntimeSettings.ReleaseRuntimePipelines();
    }
}
