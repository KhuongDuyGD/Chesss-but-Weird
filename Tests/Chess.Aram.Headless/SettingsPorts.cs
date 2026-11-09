// Real preference model with an inert store; no changes to the player's settings.
public static class UserSettings
{
    private sealed class Store:ChessButWeird.Settings.ISettingsStore
    {public string Read(string id)=>null;public void Write(string id,string value){}public void Flush(){}}
    public static readonly ChessButWeird.Settings.SettingsManager Manager=new ChessButWeird.Settings.SettingsManager(ChessButWeird.Settings.SettingsRegistry.Create(),new Store());
    public static readonly ChessButWeird.Settings.PresentationPreferences Presentation=new ChessButWeird.Settings.PresentationPreferences(Manager);
    public static float Get(string id)=>Manager.Get(id);
    public static bool Enabled(string id)=>Manager.Enabled(id);
}
public static class PlayModeNames {public static string DisplayText(string value)=>value;}
