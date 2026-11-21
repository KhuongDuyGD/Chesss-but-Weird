using ChessButWeird.Settings;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Formats catalog/public information; never evaluates or exposes hidden game state.</summary>
public static class TooltipPreferences
{
    public static float TextScale => UserSettings.Presentation.TooltipScale * (UserSettings.Enabled("large_text")?1.15f:1);
    public static string Description(string text)
    {
        if(string.IsNullOrEmpty(text)||UserSettings.Presentation.TooltipDetail!=TooltipDetailLevel.Simple)return text;
        int sentence=text.IndexOf(". ",System.StringComparison.Ordinal);
        return sentence<0?text:text.Substring(0,sentence+1);
    }
    public static string Buff(AramBuffDefinition definition,string publicProgress=null)
    {
        string text=definition.DisplayName+(UserSettings.Enabled("upgrade_rarity")?" · "+definition.Tier:"")+"\n"+Description(definition.Description);
        if(UserSettings.Presentation.TooltipDetail==TooltipDetailLevel.Nerd && UserSettings.Enabled("trigger_preview") && !string.IsNullOrEmpty(publicProgress))
            text+="\n\n"+publicProgress;
        return text;
    }
}
