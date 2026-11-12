// Only settings/team labels are isolated. Presentation, shaders, props and online mapping are production files.
public enum PieceTeam { White, Black }
public static class PlayModeNames { public static string DisplayText(string value)=>value; }
public static class UserSettings
{
    public static string LanguageCode=>"en";
    public static class Presentation
    {
        public static bool ReducedMotion;
        public static int Effects=2;
        public static float AnimationSpeed=1,Flash=1,Shake=0;
    }
}
