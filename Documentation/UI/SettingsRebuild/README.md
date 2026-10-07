# Settings rebuild — 2026-10-07

## Scope and previous implementation

The previous `SettingsMenuController` manually authored one page for music, SFX,
graphics preset, FPS, anti-aliasing and a shadows On/Off dropdown. `GameRuntimeSettings`
read separate PlayerPrefs keys and flushed disk on each slider change. AA already
supported Auto, Off, FXAA, SMAA, MSAA 2x and MSAA 4x, including hardware recommendations
and the MSAA-to-SMAA fallback. These rendering paths are retained.

The pause menu's Settings action previously displayed an unavailable message and
blocked guests. Settings now work from the menu and local pause, including guests;
account and Inventory access policies remain separate.

No Board tab or cosmetic controls are present. No Domain/rules, buff definitions,
bot decisions, rewards, account/progression storage, Inventory, scene or renderer
asset configuration was changed. Shared gameplay files only gate presentation and
audio, or mark an existing highlight as an ability hint. Existing wait durations
and match/network clock logic are unchanged.

## Architecture

`SettingDefinition` → `SettingsRegistry` → generated Settings rows →
`SettingsManager` → `SettingsRuntime`/presentation adapters → settings-only PlayerPrefs.

- The definition/registry/manager and display confirmation model are Unity-independent.
- Six categories: General, Gameplay, Graphics, Audio, Controls, Accessibility.
- Definitions own stable ID, category, name, explanation, type, default, bounds,
  step and allowed choices. Typed runtime consumers use `PresentationPreferences`.
- `SettingsMenuController` generates controls from definitions. The separate display
  section uses a confirmation transaction because resolution choices depend on the monitor.
- `GameRuntimeSettings` is the existing compatibility API and graphics implementation,
  backed by the new manager rather than an independent configuration writer.
- `SettingsLanguages` uses stable numeric IDs and language codes. Optional
  `Resources/Localization/Settings_CODE.json` tables translate setting names/descriptions,
  options, tab names and the main menu shell, with English fallback per missing key.
  Table format: `{"entries":[{"key":"music.name","text":"..."}]}`.
- Add preferences in `SettingsRegistry`, then connect the runtime consumer. Unsupported
  preferences are not added as inert controls.

## Preferences and runtime effects

| Tab | Implemented preferences |
|---|---|
| General | Language preference, UI Scale 80–150%, Tooltip Size, delay 0–1s, Advanced Tooltips, quit confirmation, pause when unfocused |
| Gameplay | Legal move hints, selected model lift, local ability targets, Simple/Detailed/Nerd buff details, owning-side source label, public trigger progress, buff rarity |
| Graphics | Existing Auto/Low/Medium/High preset, all existing AA modes, four shadow tiers, effects quality, Flat/Smooth move arcs, all requested piece animation speeds including Instant, summon shake and flash, VSync, existing FPS limits plus Unlimited; Windows player display mode/resolution |
| Audio | Master, Music, SFX, move/castling sounds, capture sounds, mute when unfocused |
| Controls | Existing mouse/rifle controls explained, orbit sensitivity, right/middle orbit toggles, wheel zoom, camera-lock and Settings key rebinding |
| Accessibility | Large Text, stronger Settings/tooltip text contrast, three color-vision palettes for move hints, Reduce Motion, Reduce Flashing, Disable Screen Shake, Reduce Visual Chaos |

UI Scale applies to Settings content and selected gameplay-information canvases, leaving
Inventory's authored layout alone. The Settings shell fits the safe area; enlarged rows
scroll. Tooltips respect size/delay and available screen bounds. `Nerd` adds only supplied
public progress; hidden opponent information stays hidden. Simple descriptions use the
first catalog sentence; Detailed retains the complete supplied text. Required ability
instructions remain visible independently of optional tooltip detail.

Volume previews are immediate, including the volume of active SFX sources. No UI/ambient
channels were invented. The master listener multiplier handles all existing audio.
Focus loss opens the existing pause flow for local games only and requires explicit
Resume; online clocks continue. ESC cancels a popup/key capture before closing Settings,
and the pause menu avoids handling that same ESC twice.

## Graphics implementation

AA still configures URP MSAA samples, per-camera FXAA/SMAA and the original unsupported
MSAA fallback. Preset changes and scene loads reapply explicit AA/shadow preferences.
Runtime URP copies protect authoring assets; their previous overrides are restored on cleanup.

| Shadow tier | Distance | Main/additional atlas | URP cascades | Rendering |
|---|---:|---:|---:|---|
| Off | 0 | 2048 (inactive) | 4 (inactive) | Per-camera and registered model shadows disabled |
| Low | 25 | 512 | 1 | Enabled |
| Medium | 50 | 1024 | 2 | Enabled |
| High | 80 | 2048 | 4 | Enabled |

The shader/light pipeline retains its authored soft-shadow support. These tiers change
actual shadow rendering work rather than just labels. No FPS improvement was benchmarked.

Effects Quality reuses the existing summon reveal: Low/Medium/High enable 6/15/30 sparks,
with rays/glyphs from Medium and halo from High. Disabled decorations are inactive and
their animation calculations are skipped. Particle Quality is combined with this option
because the relevant effects use UI graphics rather than an independent ParticleSystem channel.

Animation Speed scales only `PieceAnimator` presentation; match coroutines retain their
original waits. Slow cosmetic motion can outlast the original input lock; a new move
cancels the earlier piece motion normally. Reduced motion/Flat removes arcs. Summon
shake affects the reward card, not board/rifle picking or the gameplay camera.

Display mode and resolution are native-player controls, with Keep/Revert, a 15-second
unscaled deadline, and rollback on close, disabled UI or focus loss. Only confirmed sizes
are saved. Startup rejects invalid/unsupported fullscreen sizes. Monitor selection and
refresh-rate switching are intentionally absent. The Unity Editor shows a clear player-only
display note; it does not pretend to resize the Editor game view.

## Persistence, reset and overrides

`cbw_settings_v3_` is a separate settings namespace. Missing values use registry defaults;
invalid values are normalized. Legacy music/SFX, AA, graphics-auto/preset, FPS-auto/value
and shadows keys migrate only when their corresponding new key is absent. Existing
On shadows maps to Medium; Off stays Off. A valid custom saved FPS remains selectable.
Old keys and player progress are not deleted.

Setters update memory immediately. The runtime flushes after a 0.5s quiet interval;
close, quit and reset flush explicitly. Reset Setting, Reset This Tab and confirmed
Reset All use the same defaults. Display resets also go through the preview transaction.
Modified preferences display `*`; individual key reset rejects a default key occupied
by another action. Corrupt duplicate stored shortcuts get separate valid bindings.

Reduced motion/flashing/chaos and Disable Screen Shake change *effective* presentation
values. Turning an override off recovers the underlying individual preferences.

## Files

- Replaced: `Assets/Scripts/Chess/Gameplay/UI/SettingsMenuController.cs`.
- Refactored: `Assets/Scripts/Chess/Gameplay/GameRuntimeSettings.cs`.
- Added: `Gameplay/Settings/{SettingsModel,SettingsRegistry,UserSettings,SettingsRuntime,
  PlayerPrefsSettingsStore,SettingsDisplay,SettingsLanguages,SettingsLocalization,
  TooltipPreferences,SettingsUiScale,SettingsSfxVolume}.cs`.
- Added UI helpers: `SettingsScrollFocus.cs`, `SettingsPrompt.cs`.
- Integrated: `ChessPauseMenu`, `HandDrawnMenuView`, `ChessOrbitCamera`, `PieceAnimator`,
  `MatchAudioPresenter`, `ChessGame` (audio/lift), `ChessBoard` (hint visibility/palette),
  `ChessGame.Aram` (hint kind), `AnalysisBoardView`, `AramAbilityView`, `MatchHudTooltip`,
  `PieceHoverTooltip`, `HandDrawnPressable`, `HandDrawnIdleWiggle`, `GachaSummonRevealController`.
- Tests/tools: `Tests/Chess.Settings.Tests`, `tools/verify-settings-ui.ps1`, updated HUD
  fixtures/runner and `verify-match-hud-headless.ps1`; `.gitignore` includes the new test project.
- Existing unrelated font asset edits were left alone.

## Verification and limits

- 164 standalone settings assertions: defaults, invalid values, culture-independent
  persistence, category/reset behavior, reversible overrides, key conflicts and corrupt
  bindings, retained custom FPS, display timeout/keep/nested rollback.
- 8,202 isolated Unity assertions: real PlayerPrefs migration/reload/progress isolation,
  real AudioListener/SFX preview, actual URP tier values without authoring asset mutation,
  all six tabs at 1280×720, 1920×1080, 2560×1440, 1920×1200; UI Scale 80/100/150 with
  Large Text on/off; text/bounds/scroll-focus, long translated strings, popup bounds, actual Input System keyboard
  rebinding/conflict warning and ESC order. See exported PNG previews here.
- 9,739 existing headless HUD assertions, including delayed/instant tooltip behavior,
  public Nerd progress and protection of hidden information.
- 91 Domain/application checks passed, including perft, special moves and ARAM rules.
- Unity Roslyn compilation passes for Editor runtime, Windows player branch and Editor tools.

The Unity MCP connection was revoked, so the user's open Editor was not controlled.
UI validation used new projects under `Logs/settings-audit` and `Logs/hud-headless`, with
isolated PlayerPrefs identity and source hashes. Raster previews were visually inspected.
No user match, account, Inventory or server data was operated on.

Still needed: a native Windows game playthrough for main-menu/pause lifecycle, physical
display switching/restore, actual scene shadows and subjective VFX/audio feel. The automated
display transaction is tested independently; fullscreen monitor behavior is not an end-to-end
claim. GPU timings are not benchmarked. Restart persistence is validated by reloading the
production store/manager rather than launching the complete game twice.

Intentionally omitted: Board customization; match rules; invented UI/ambient/buff/timer audio
channels; drag-to-move/controller support; threat/attack maps and move previews lacking a
ready read-only presentation path; high-contrast piece material replacement; independent
particle control; synergy/tag/secondary-buff metadata that the current catalog does not
provide; monitor/refresh selection. Vietnamese translation content remains in development.
Future tooltip providers can use the centralized detail preference and add structured public
mechanics metadata without duplicating game rules.

## Run again

```powershell
dotnet run --project Tests/Chess.Settings.Tests/Chess.Settings.Tests.csproj
dotnet run --project Tests/Chess.Domain.Tests/Chess.Domain.Tests.csproj
./tools/verify-loading-compile.ps1 -UnityPath 'D:/Unity Editor/6000.4.7f1/Editor/Unity.exe'
./tools/verify-settings-ui.ps1
./tools/verify-match-hud-headless.ps1
```

The Settings audit requires a graphics device for its PNG renders. The HUD audit is
headless. Both tools guard their own fixture project paths and supervise only their own
new process. `verify-settings-ui.ps1 -AuditDirectory <existing Logs/settings-audit/subdirectory>`
can reuse a fixture's imported packages without touching the main project.
