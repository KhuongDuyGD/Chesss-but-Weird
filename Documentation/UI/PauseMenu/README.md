# Pause Menu Implementation

## Scope and Files

- `Assets/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs`: rebuilt Pause/Match Menu, confirmations, Settings integration, Match Info, keyboard navigation and guarded actions.
- `Assets/Scripts/Chess/Networking/ChessLanController.PauseExit.cs`: small, isolated exit adapter; preserves the originating lobby and disposes the old session after acknowledgement.
- `Assets/Scripts/Chess/Core/ChessGame.cs`: backward-compatible return-to-menu callback overload, routed through the existing content owner.
- `Assets/Scripts/Chess/Backend/OnlineSession.cs`: backward-compatible acknowledgement-required leave overload used only by Pause Menu.
- `Assets/Editor/PauseMenuAudit.cs`: repeatable isolated Play Mode audit.
- No Settings, Accessibility, Inventory, Board, Bot Selection, Main Menu 2 or Multiplayer Lobby redesign. No matchmaking, gameplay, bot or surrender rule changes.

## Existing and New Structure

Previously, an independent canvas showed cropped bitmap Resume, Restart, Settings and Quit Game buttons. Multiplayer displayed a disabled Restart button; Quit Game returned to the first main menu through the old optional confirmation path.

The independent canvas is retained at sorting order 2500. The new responsive paper sheet uses existing antialiased doodle graphics, TMP text and explicit keyboard navigation. Open/Resume are immediate, without entrance/exit animation or input delay. No Accessibility system changes are needed, and Reduce Motion cannot introduce menu motion.

| Singleplayer | Multiplayer |
| --- | --- |
| Resume | Return to Match |
| Restart Match | Settings |
| Settings | Match Info |
| Leave Match | Leave Match |
| Main Menu | Main Menu |
| Quit Desktop | Quit Desktop |

## Actions and Destinations

- Resume restores the captured singleplayer time scale and local interaction immediately. Multiplayer only locks local board interaction; transport and online clocks continue.
- ESC opens/closes the menu once per frame. The existing first-press piece deselection is preserved. ESC cancels a confirmation or returns from Match Info. Settings owns its own ESC, preventing a second simultaneous Resume.
- Restart is singleplayer-only, confirmed, and uses `RestartCurrentLocalGame`, preserving bot side, difficulty and options. It resumes immediately after reset.
- Restart, Leave Match, Main Menu and Quit Desktop always have Cancel/Confirm dialogs. Cancel keeps the current match/session. Confirm is consumed once; duplicate clicks cannot repeat commands or navigation.
- Settings uses the existing `SettingsMenuController` unchanged. Returning from Settings keeps Pause/Match Menu open.
- Match Info displays existing player names, mode, connection, clocks and last-move information.
- Singleplayer Leave Match clears the current match and opens existing Bot Selection using `ShowBotDifficultySelection`.
- Multiplayer Leave Match uses existing `OnlineSession.LeaveAsync` semantics: decline while awaiting readiness, resign an in-progress match, or clear a finished match without a new surrender. Rejected active-match exits remain recoverable in Match Menu.
- After acknowledged multiplayer leave, the old transport/session, match presenters and transient exit state are disposed. Existing `ShowLobby` reopens the preserved LAN/Online and Classic/ARAM destination, with its normal refresh behavior.
- Main Menu releases match content and opens Main Menu 2 through `ShowTurnSelection`, not the first main menu.
- Quit Desktop calls `Application.Quit` in a player, or stops Play Mode in the Editor. Multiplayer first attempts the existing leave path. After five seconds without acknowledgement, or a failed acknowledgement, local transport cleanup still occurs and desktop exit proceeds. Server-side disconnect/surrender policies are unchanged.
- Navigation waits for the existing `LoadingManager` success callback after asset release and additive scene unload. A busy content owner is reported without swallowing the exit or leaving controls permanently disabled.

## Verification

Run **Chess > UI Audit > Test Pause Menu** from a saved single-scene Edit Mode setup. The audit restores the original scene, account values, token preferences and accessibility preference values afterward. It does not contact the production game service or write match rewards.

`checks.txt` records every required singleplayer case (13) and multiplayer case (15), plus regressions for real board control after Resume, preserved bot settings, Reduce Motion, old time scales, live WebSocket snapshots, online clocks, four originating lobby variants, duplicate exits, rejected exits, finished matches, async content release and desktop timeout cleanup.

The loopback peer exercises the real `SignalRGameTransport` and `OnlineSession`; only the server responses are synthetic. The existing lobby is rendered, but its external REST refresh is suppressed in the fixture. Desktop quit is intercepted to avoid closing the Editor during the audit.

Twenty screenshots cover Singleplayer, Multiplayer, Restart Confirmation and Match Info at 1920x1080, 1280x720, 800x600, 2560x1080 and 1080x1920. Each capture checks text overflow, button bounds and nonblank rendering. Compilation and `git diff --check` are also verified.

## Remaining Debt and Limits

- `LoadingManager` has a success-only navigation callback. An exceptional content-release/unload failure still uses its shared loading error/recovery UI, which is outside this Pause Menu scope.
- No live two-player production match or packaged OS-process quit was exercised. These require a separate two-client/build smoke test; the audit validates real transport/session cleanup and the desktop quit invocation without closing the host Editor.
