using System;
using System.Collections.Generic;

namespace ChessButWeird.Settings
{
    public readonly struct SettingsLanguage
    {
        public readonly int Id;
        public readonly string Code, Label;
        public SettingsLanguage(int id,string code,string label){Id=id;Code=code;Label=label;}
    }
    public static class SettingsLanguages
    {
        // Stable IDs survive catalog ordering changes. Translation tables are optional.
        public static readonly IReadOnlyList<SettingsLanguage> All=Array.AsReadOnly(new[]{
            new SettingsLanguage(0,"en","English"),new SettingsLanguage(1,"vi","Vietnamese (in development)")});
        public static string Code(int id){foreach(var language in All)if(language.Id==id)return language.Code;return "en";}
    }
}
