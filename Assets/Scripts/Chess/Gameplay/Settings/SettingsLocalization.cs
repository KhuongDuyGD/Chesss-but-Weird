using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Optional Resources/Localization/Settings_CODE.json tables. Missing text uses readable English.</summary>
public static class SettingsLocalization
{
    [Serializable] private sealed class Entry { public string key, text; }
    [Serializable] private sealed class Table { public Entry[] entries; }
    private static readonly Dictionary<string,Dictionary<string,string>> tables=new Dictionary<string,Dictionary<string,string>>();
    public static string Text(string key,string english)
    {
        string code=UserSettings.LanguageCode;
        if(code=="en")return english;
        if(!tables.TryGetValue(code,out var strings))
        {
            strings=new Dictionary<string,string>();tables[code]=strings;
            var resource=Resources.Load<TextAsset>("Localization/Settings_"+code);
            if(resource)
            {
                try
                {
                    var table=JsonUtility.FromJson<Table>(resource.text);
                    if(table?.entries!=null)foreach(var entry in table.entries)
                        if(!string.IsNullOrEmpty(entry.key)&&!string.IsNullOrWhiteSpace(entry.text))strings[entry.key]=entry.text;
                }
                catch(ArgumentException){Debug.LogWarning("[Settings] Invalid translation table for "+code+"; English fallback is active.");}
            }
        }
        return strings.TryGetValue(key,out var value)?value:english;
    }
}
