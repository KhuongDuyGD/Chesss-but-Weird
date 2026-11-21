using System.Collections.Generic;
using UnityEngine;

/// <summary>Changes active one-shots as well as future playback without scaling the same channel twice.</summary>
[RequireComponent(typeof(AudioSource))]
public sealed class SettingsSfxVolume : MonoBehaviour
{
    private AudioSource source;
    private float baseline;
    private void Awake(){source=GetComponent<AudioSource>();baseline=source.volume;Apply();}
    private void OnEnable(){UserSettings.Manager.Changed+=Changed;Apply();}
    private void Changed(IReadOnlyList<string> ids){foreach(var id in ids)if(id=="sfx"){Apply();break;}}
    private void Apply(){if(source)source.volume=baseline*GameRuntimeSettings.SoundVolume01;}
    private void OnDisable(){UserSettings.Manager.Changed-=Changed;if(source)source.volume=baseline;}
}
