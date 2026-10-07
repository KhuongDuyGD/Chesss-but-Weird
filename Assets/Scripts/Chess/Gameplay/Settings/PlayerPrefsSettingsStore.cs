using System;
using System.Collections.Generic;
using System.Globalization;
using ChessButWeird.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Settings-only namespace. Old preference values are copied once per missing new key.</summary>
public sealed class PlayerPrefsSettingsStore : ISettingsStore
{
    public const string Prefix = "cbw_settings_v3_";
    public PlayerPrefsSettingsStore()
    {
        Migrate("music","cbw_settings_music_volume"); Migrate("sfx","cbw_settings_sound_volume");
        Migrate("aa","cbw_settings_antialiasing_v1");
        if (!PlayerPrefs.HasKey(Prefix+"shadows") && PlayerPrefs.HasKey("cbw_settings_shadows_v1"))
            Write("shadows",PlayerPrefs.GetInt("cbw_settings_shadows_v1")==0?"0":"2");
        if (!PlayerPrefs.HasKey(Prefix+"graphics") && PlayerPrefs.GetInt("cbw_settings_graphics_auto_v2",1)==0)
            Write("graphics",(PlayerPrefs.GetInt("cbw_settings_graphics_preset_v2",1)+1).ToString(CultureInfo.InvariantCulture));
        if (!PlayerPrefs.HasKey(Prefix+"fps") && PlayerPrefs.GetInt("cbw_settings_fps_auto_v2",1)==0)
            Migrate("fps","cbw_settings_fps");
    }
    private void Migrate(string id,string oldKey)
    { if (!PlayerPrefs.HasKey(Prefix+id) && PlayerPrefs.HasKey(oldKey)) Write(id,PlayerPrefs.GetInt(oldKey).ToString(CultureInfo.InvariantCulture)); }
    public string Read(string id) => PlayerPrefs.HasKey(Prefix+id)?PlayerPrefs.GetString(Prefix+id):null;
    public void Write(string id,string value) => PlayerPrefs.SetString(Prefix+id,value);
    public void Flush() => PlayerPrefs.Save();
}
