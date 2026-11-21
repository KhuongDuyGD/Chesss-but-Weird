using System;
using System.Collections.Generic;
using System.Globalization;
using ChessButWeird.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SettingsDisplay
{
    public static DisplayConfiguration Current => new DisplayConfiguration(Screen.width,Screen.height,(int)Screen.fullScreenMode);
    public static void Apply(DisplayConfiguration value)
    {
#if !UNITY_EDITOR
        Screen.SetResolution(value.Width,value.Height,(FullScreenMode)value.Mode);
#endif
    }
    public static void Save(DisplayConfiguration value)
    {
        PlayerPrefs.SetInt(PlayerPrefsSettingsStore.Prefix+"display_width",value.Width);
        PlayerPrefs.SetInt(PlayerPrefsSettingsStore.Prefix+"display_height",value.Height);
        PlayerPrefs.SetInt(PlayerPrefsSettingsStore.Prefix+"display_mode",value.Mode);PlayerPrefs.Save();
    }
    public static void ApplySaved()
    {
        string key=PlayerPrefsSettingsStore.Prefix+"display_width";
        if(!PlayerPrefs.HasKey(key))return;
        int width=PlayerPrefs.GetInt(key),height=PlayerPrefs.GetInt(PlayerPrefsSettingsStore.Prefix+"display_height");
        int mode=PlayerPrefs.GetInt(PlayerPrefsSettingsStore.Prefix+"display_mode",(int)FullScreenMode.FullScreenWindow);
        // A removed monitor or corrupt config must never request an unusable fullscreen size.
        bool supported=mode==(int)FullScreenMode.Windowed;
        foreach(var resolution in Screen.resolutions) supported|=resolution.width==width&&resolution.height==height;
        if(supported&&width>=800&&height>=600&&width<=16384&&height<=16384&&
            (mode==(int)FullScreenMode.Windowed||mode==(int)FullScreenMode.FullScreenWindow||mode==(int)FullScreenMode.ExclusiveFullScreen))
            Apply(new DisplayConfiguration(width,height,mode));
    }
    public static void ClearSaved()
    {
        foreach(var key in new[]{"display_width","display_height","display_mode"})PlayerPrefs.DeleteKey(PlayerPrefsSettingsStore.Prefix+key);
        PlayerPrefs.Save();
    }
}
