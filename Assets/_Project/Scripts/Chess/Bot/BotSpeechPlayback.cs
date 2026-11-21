using System;

/// <summary>Dialogue timing without a Unity dependency. Silence is an explicit state.</summary>
public sealed class BotSpeechPlayback
{
    private int characters;
    private double elapsed, revealTime, readTime;
    public bool IsVisible { get; private set; }
    public bool IsTyping => IsVisible && elapsed < revealTime;
    public int VisibleCharacters => !IsVisible ? 0 : Math.Min(characters, (int)(elapsed * 32));

    public void Start(int count)
    {
        characters=Math.Max(0,count);elapsed=0;revealTime=characters/32d;
        readTime=Math.Max(3.5,Math.Min(8,characters/19d));IsVisible=characters>0;
    }
    public void Advance(double seconds, bool paused = false, bool persistent = false)
    {
        if(!IsVisible||paused)return;
        elapsed+=Math.Max(0,seconds);
        if(!persistent&&elapsed>=revealTime+readTime)Stop();
    }
    public void RevealAll(){if(IsVisible)elapsed=Math.Max(elapsed,revealTime);}
    public void Stop(){IsVisible=false;elapsed=0;characters=0;}
}
