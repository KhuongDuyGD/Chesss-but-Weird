public enum BotGameMode { Practice, Challenge }

/// <summary>Challenge never accepts assists, even when invoked outside the roster.</summary>
public sealed class BotGameOptions
{
    public static BotGameOptions Challenge { get; } = new BotGameOptions(BotGameMode.Challenge);
    public BotGameMode Mode { get; }
    public bool AllowHints { get; }
    public bool AllowUndo { get; }
    public bool IsPractice => Mode == BotGameMode.Practice;

    public BotGameOptions(BotGameMode mode, bool allowHints = true, bool allowUndo = true)
    {
        Mode = mode;
        AllowHints = IsPractice && allowHints;
        AllowUndo = IsPractice && allowUndo;
    }
}
