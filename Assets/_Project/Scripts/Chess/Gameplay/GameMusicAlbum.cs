using System;
using System.IO;
using UnityEngine;

/// <summary>Build-safe references to streamed music clips, without copying source audio.</summary>
public sealed class GameMusicAlbum : ScriptableObject
{
    [SerializeField] private AudioClip[] clips;

    public AudioClip FindClip(string fileName)
    {
        string clipName = Path.GetFileNameWithoutExtension(fileName);
        if (clips != null)
            foreach (AudioClip clip in clips)
                if (clip && string.Equals(clip.name, clipName, StringComparison.Ordinal))
                    return clip;
        return null;
    }
}
