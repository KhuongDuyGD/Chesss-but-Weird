# Isolated HUD assertions

Run `tools/verify-loading-compile.ps1` first, then `tools/verify-match-hud-headless.ps1`.

The second script creates a new Unity project under `Logs/hud-headless/<timestamp>` and launches it with `-batchmode -nographics`. It copies the current production UI sources and records their SHA256 hashes. It does not launch, capture, change Play Mode, or install test hooks in the user's game project. MCP and gameplay/account/server code are absent. A five-minute timeout only stops the process created by this script.

Fixture data sources are deliberately separate from gameplay. Actual Unity RectTransform, Canvas, TMP font metrics and buttons exercise initialization, text clipping, bounds at five aspect ratios, ARAM panels with 0–10 actions, scroll reading offsets, camera restoration/toggle invariance/replacement, and stale action identity/context handling. WorldSpace canvases model the dimensions produced by the production Expand scaler, without using a graphics device.

The copied production `ChessGame.Statistics.cs` adapter exercises incomplete recovery. Its narrow gameplay dependencies are fixtures; online .NET snapshots are outside this isolated HUD check.

These checks cannot confirm raster appearance, 3D board composition, real server integration or ARAM gameplay. A manual cold playthrough still needs verification. Do not put the fixture runner under the real game's `Assets` directory.
