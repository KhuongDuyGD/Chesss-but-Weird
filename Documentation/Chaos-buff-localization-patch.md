# Chaos buff localization patch

Applied on 2026-11-12. This patch changes localization only.

## Coverage and checkout limitation

The approved list contains **18 names**: 17 expansion buffs plus the board-collapse rename. All 18 have English display names, compact HUD names, descriptions and summaries in the existing Settings localization table format. All original Vietnamese names are preserved in the Vietnamese table.

This checkout has 35 registered buffs, but only four entries from the requested list: Absolute Sniper (7), Every Man for Himself (26), High-Tech Era Cannon (16), and The Chessboard Has Fallen (18). The other 14 expansion buffs are absent from both the registered definitions and gameplay code. Their strings are prepared in the catalog; they are not added to the draft pool or presented as playable buffs. Localization key stems for these entries are not new serialized buff IDs. When their actual implementations arrive, map their established IDs to these keys rather than inventing IDs as part of a translation patch.

The four existing descriptions retain the mechanics implemented in this checkout. In particular, the current long-distance Sniper condition, capture/promotion quest, and original cannon behavior remain described accurately. The unimplemented v2 mission and cannon enhancement are not advertised as active.

| Localization key stem | Approved English display name | Integration |
| --- | --- | --- |
| `buff.AbsoluteSniper` | Absolute Sniper | Integrated; existing ID 7 |
| `buff.EveryManForHimself` | Every Man for Himself | Integrated; existing ID 26 |
| `buff.BrokenLegKnight` | Knightmare on the Chessboard | Catalog only; no registered buff in this checkout |
| `buff.WeaponSupplyPackage` | Tactical Supply Drop | Catalog only; no registered buff in this checkout |
| `buff.NewYearFireworks` | New Year, New Boom! | Catalog only; no registered buff in this checkout |
| `buff.AssassinPawns` | Pawn Identity: Assassin | Catalog only; no registered buff in this checkout |
| `buff.MedicalStation` | Emergency Respawn Center | Catalog only; no registered buff in this checkout |
| `buff.WeddingNight` | Checkmate & Consummate | Catalog only; no registered buff in this checkout |
| `buff.ProtectedPawn` | You Wouldn't Hurt a Cute Girl, Would You? | Catalog only; no registered buff in this checkout |
| `buff.Lullaby` | Deadly Lullaby | Catalog only; no registered buff in this checkout |
| `buff.GoldenExperienceRequiem` | Golden Experience Requiem | Catalog only; no registered buff in this checkout |
| `buff.QueenCastling` | Royal Family Drama | Catalog only; no registered buff in this checkout |
| `buff.QueenSubordinates` | Death by Queen | Catalog only; no registered buff in this checkout |
| `buff.Snowman` | Do You Want to Build a Snowman? | Catalog only; no registered buff in this checkout |
| `buff.DrunkDriving` | Don't Drink and Rook! | Catalog only; no registered buff in this checkout |
| `buff.MagicCaster` | Magic Caster | Catalog only; no registered buff in this checkout |
| `buff.HighTechEra` | High-Tech Era Cannon | Integrated; existing ID 16 |
| `buff.DefinitionOfAram` | The Chessboard Has Fallen | Integrated; existing ID 18 |

## Localization and presentation files

- `Assets/Resources/Localization/Settings_en.json`: 72 entries, covering name, shortName, description and summary for all 18 approved entries. Existing buff descriptions/summaries include their exact source text so translations cannot replace a different server ruleset.
- `Assets/Resources/Localization/Settings_vi.json`: 36 entries preserving the 18 original names and using those names for Vietnamese HUD labels. Other Vietnamese text retains the existing English fallback; this is not a full Vietnamese UI translation.
- `Assets/Resources/Localization.meta` and both JSON `.meta` files: Unity resource/import metadata.
- `Assets/Scripts/Chess/Gameplay/Settings/SettingsLocalization.cs`: load English tables as well as other languages; optional source-text matching for rule descriptions; retain missing/invalid-table and missing-key fallback.
- `Assets/Scripts/Chess/Gameplay/ARAM/AramBuffDefinition.cs`: resolve display name, HUD short name, description and summary through the existing localization system by stable ID name. Update the cannon and collapse English fallback names. Shared getters cover draft cards, ability details, piece tooltips and buff-name notifications. No separate inventory localization was found in this checkout.
- `Assets/Scripts/Chess/Networking/OnlineAramPresenter.cs`: use the approved names in legacy online descriptions and localize their name prefixes. Server-provided descriptions and summaries remain intact when their rules differ from the local source text.

No IDs, buff registrations, tiers, balances, activation conditions, timers, rules, animations, VFX or UI layout values were changed. Existing unrelated workspace edits were left untouched.

## Tests and test-fixture changes

- `Tests/Chess.Settings.Tests/LocalizationAudit.cs`: approved English/Vietnamese pairs, all 18 catalog entries, Unicode normalization and decoding, key uniqueness, missing/malformed/blank table behavior, live language switching, description/summary source guards, server metadata fallback, original ID values and rarity-pool counts.
- `Tests/Chess.Settings.Tests/LocalizationPorts.cs`: inert Unity resource/serialization ports for running the production localization and definition classes without starting Unity or accessing player preferences.
- `Tests/Chess.Settings.Tests/Chess.Settings.Tests.csproj` and `Program.cs`: include production localization code/resources and run the localization checks with the existing settings suite.
- `Tests/Chess.Aram.Headless/SettingsPorts.cs`, `Tests/Chess.Hud.Headless/HudFixture.cs`, `Tests/Chess.BuffVfx/FixturePorts.cs`: expose the test language preference needed by production definitions.
- `Tests/Chess.Hud.Headless/Chaos35OnlineAudit.cs`: expect localized names while retaining server metadata checks.
- `tools/verify-aram-headless.ps1`, `tools/verify-match-hud-headless.ps1`, `tools/verify-buff-vfx.ps1`: copy the production localization dependency and resources into their isolated test projects.

Executed:

```powershell
dotnet run --project Tests/Chess.Settings.Tests/Chess.Settings.Tests.csproj --no-restore
& tools/verify-loading-compile.ps1 -UnityPath 'D:/Unity Editor/6000.4.7f1/Editor/Unity.exe'
git diff --check
```

Results: **345 localization assertions and 167 existing settings assertions passed (512 total)**. Production runtime-editor and runtime-player branches compiled (260 source files each); editor tools compiled (29 source files). Compiler warnings concern existing obsolete Unity APIs and serializer-populated fields. No compilation errors. Whitespace checks passed.

The existing Unity HUD/VFX/ARAM audit fixtures were updated for the new localization dependency but not launched. No Unity editor, graphical test window, mouse/keyboard automation or player settings were used. Rendering, font glyph coverage and a full player build were not tested.
