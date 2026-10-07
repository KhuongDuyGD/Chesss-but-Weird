using System;
using System.Collections.Generic;
using System.Globalization;
using ChessButWeird.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class UserSettings
{
    private static SettingsManager manager;
    private static PresentationPreferences preferences;
    public static SettingsManager Manager
    {
        get
        {
            if(manager!=null)return manager;
            var store=new PlayerPrefsSettingsStore();
            float fps;
            var registry=SettingsRegistry.Create(float.TryParse(store.Read("fps"),NumberStyles.Float,CultureInfo.InvariantCulture,out fps)?(float?)fps:null);
            return manager=new SettingsManager(registry,store);
        }
    }
    public static PresentationPreferences Presentation => preferences ?? (preferences = new PresentationPreferences(Manager));
    public static bool Enabled(string id) => Manager.Enabled(id);
    public static float Get(string id) => Manager.Get(id);
    public static string LanguageCode => SettingsLanguages.Code((int)Get("language"));
    public static bool KeyPressed(string id)
    {
        if (Keyboard.current == null) return false;
        var name = SettingsRegistry.BindingKeys[(int)Get(id)];
        return Enum.TryParse(name, out Key key) && Keyboard.current[key].wasPressedThisFrame;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { manager = null; preferences = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void StartRuntime()
    {
        var owner = new GameObject("User Settings Runtime");
        UnityEngine.Object.DontDestroyOnLoad(owner);
        owner.AddComponent<SettingsRuntime>();
    }
}
