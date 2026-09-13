using UnityEngine;

internal static class MatchAudioPresenter
{
    public static AudioSource EnsureSource(GameObject owner, AudioSource source)
    {
        if (!source) source = owner.GetComponent<AudioSource>();
        if (!source) source = owner.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    public static AudioSource Play(GameObject owner, AudioSource source, AudioClip clip, float volume)
    {
        if (!clip) return source;
        source = EnsureSource(owner, source);
        source.PlayOneShot(clip, volume);
        return source;
    }
}
