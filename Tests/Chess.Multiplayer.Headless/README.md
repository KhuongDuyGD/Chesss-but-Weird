# Multiplayer regression checks

Run `tools/verify-loading-compile.ps1` with the installed Unity path, then
`tools/verify-multiplayer-headless.ps1`. The second command starts a separate batch-mode,
edit-mode Unity project under `Logs/multiplayer-headless/<timestamp>` and records its PID.
Read `audit-result.txt` for completion and `Editor.log` for errors.

The audit loads freshly compiled production assemblies and checks return-to-menu after
win/loss/draw with an unloaded board, return to main menu 2, online-session disposal, SFX clip selection, duplicate
snapshot suppression, the check-pulse coroutine, and normal/ranked/custom request flags.
It does not load user scenes, authenticate an account, contact the backend, render the game,
or verify audible playback on a physical device.
