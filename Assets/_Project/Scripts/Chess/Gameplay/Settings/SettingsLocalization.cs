using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Optional Resources/Localization/Settings_CODE.json tables, including English. Missing text uses readable English.</summary>
public static class SettingsLocalization
{
    // Optional source guards keep local rule translations from replacing different server rules.
    [Serializable] private sealed class Entry { public string key, text, source; }
    [Serializable] private sealed class Table { public Entry[] entries; }
    private static readonly Dictionary<string,Dictionary<string,Entry>> tables=new Dictionary<string,Dictionary<string,Entry>>();
    public static string Text(string key,string english)
    {
        string code=UserSettings.LanguageCode;
        if(!tables.TryGetValue(code,out var strings))
        {
            strings=new Dictionary<string,Entry>();tables[code]=strings;
            var resource=Resources.Load<TextAsset>("Localization/Settings_"+code);
            if(resource)
            {
                try
                {
                    var table=JsonUtility.FromJson<Table>(resource.text);
                    if(table?.entries!=null)foreach(var entry in table.entries)
                        if(entry!=null&&!string.IsNullOrEmpty(entry.key)&&!string.IsNullOrWhiteSpace(entry.text))strings[entry.key]=entry;
                }
                catch(ArgumentException){Debug.LogWarning("[Settings] Invalid translation table for "+code+"; English fallback is active.");}
            }
        }
        return strings.TryGetValue(key,out var value)&&
            (string.IsNullOrEmpty(value.source)||value.source==english)?value.text:english;
    }
}
