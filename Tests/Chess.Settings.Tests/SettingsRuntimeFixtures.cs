// Only used by tools/verify-settings-ui.ps1 in a new isolated Unity project.
// These two dependencies are presentation adapters, not the settings manager or UI under test.
public static class GameMusicManager { public static int RefreshCount; public static void RefreshVolumeFromSettings(){RefreshCount++;} }
public static class ChessModelRendering { public static bool Shadows; public static void RefreshShadows(bool value){Shadows=value;} }
