# Main menu asset cleanup

Removed 33 superseded PNGs and their 33 `.meta` files: 18 from `Assets/Materials/Main_Menu` and 15 from `Assets/Resources/Main_Menu`. Removed their 18 CoreUI Addressables entries, so startup no longer preloads them. The PNGs accounted for 38,078,953 bytes (36.3 MiB); this is source-file storage, not a measured runtime memory saving.

The original logo is preserved unchanged at **`Assets/Resources/MainMenu/MainMenuLogoOriginal.png`**. Its SHA-256 is `199E6CEA377322CD80F015F06CDF38E008E8BC92B071048DFA24BF7C6B4E8801`, matching the original `GameLogo.png` before its redundant copy was removed. Main Menu 1 and 2 now use this single logo source directly.

## Removed artwork

Material filenames below were under `Assets/Materials/Main_Menu`; resource filenames were under `Assets/Resources/Main_Menu`.

| Superseded element | Removed material PNG | Removed resource PNG | Current implementation |
| --- | --- | --- | --- |
| START | `StartButton.png` | `start_button.png` | `MainMenuPresentation` button |
| SETTINGS | `SettingsButton.png` | `settings_button.png` | `MainMenuPresentation` button |
| CREDITS | `CreditsButton.png` | `credits_button.png` | `MainMenuPresentation` button and credits panel |
| Old mascot decoration | `DoodleFaceDecorateBackground.png` | `mascot.png` | Intentional ink accents in the presentations |
| Old slogan image | `GameSlogan.png` | `slogan.png` | Readable live text |
| LOCAL | `LocalGameplayButton.png` | `local_button.png` | `ModeSelectionPresentation` card |
| ONLINE | `OnlinePlayButton.png` | `online_button.png` | `ModeSelectionPresentation` card |
| ARAM | `ARAMModeButton.png` | `aram_button.png` | `ModeSelectionPresentation` card |
| SHOP | `ShopButton.png` | `shop_button.png` | `ModeSelectionPresentation` card |
| Inventory icon | `InventoryButton.png` | `inventory_icon.png` | `ModeMenuDoodleGraphic` inventory drawing |
| Gacha icon | `GachaButton.png` | `gacha_icon.png` | `ModeMenuDoodleGraphic` gacha drawing |
| Profile icon | `PlayerProfile.png` | `player_profile_icon.png` | `ModeMenuDoodleGraphic` profile drawing |
| Settings icon | `SettingsIcon.png` | `settings_icon.png` | `ModeMenuDoodleGraphic` settings drawing |
| Old logout image | `LogoutButton.png` | `logout_button.png` | Both presentations' live logout controls |
| Duplicate logo sources | `GameLogo.png` | `logo.png` | Unchanged `MainMenuLogoOriginal.png` |
| Old main-menu reference sheet | `MainMenuDesign.png` | — | Production Main Menu 1 and its saved previews |
| Old mode-menu reference sheet | `MainMenuDesgin2.png` | — | Production Main Menu 2 and its saved previews |
| Unused bot-difficulty reference sheet | `ChooseBotDifficultUI.png` | — | Existing blank background and five difficulty buttons remain |

Removed unused sprite slots, load steps, obsolete main-menu decoration helpers, old bitmap logout/back helpers, and the duplicate-logo fallback. `HasRequiredSprites` now checks the actual preserved logo and shared submenu navigation/side artwork instead of retired first/second menu sprites. Obsolete gacha load slots pointed at nonexistent old filenames; the current gacha controller and all its artwork remain.

## Artwork retained because it is still used

All 30 retained PNGs in the audited menu folders have their original bytes and GUIDs. `MenuAssetCleanupManifest.json` records the exact filenames, hashes and GUIDs for both removed and protected textures.

| Retained artwork | Active consumer |
| --- | --- |
| `MainMenu/MainMenuLogoOriginal.png` | Main Menu 1 and 2 |
| `Main_Menu/Back.png` | Local, ARAM, multiplayer, difficulty/side/skin navigation and LAN UI |
| `BackgroundDecoration1.png` and `punch_doodle.png` fallback | Local/ARAM/skin selection doodles |
| `BackgroundDecoration2.png`, `BackgroundDecoration3.png`, `BackgroundDecoration4.png` and their hearts/stars/crown fallbacks | Submenu decorations and transition veil |
| `GameVersionFake.png` and `early_access.png` fallback | Side-selection badge |
| `ChooseYour MultiplayerMode.png`, `LANButton.png`, `MultiplayerButton.png` and their resource fallbacks | Multiplayer-mode selection |
| `ChooseYourSide.png`, `WhiteSideButton.png`, `BlackSideButton.png` and their resource fallbacks | Side selection |
| `ChooseBotDifficultUIBlank.png`, `BeginnerButton.png`, `EasyButton.png`, `MediumButton.png`, `HardButton.png`, `ExpertButton.png` | Six serialized references in `Resources/Chess/BotDifficultyAssets.asset` |

The remaining `Main_Menu` folders contain shared submenu artwork and active fallbacks. Their older naming does not mean they are safe to delete. Gacha, inventory, profile, settings, lobbies, pause, results, fonts, gameplay assets and recovery files were preserved.

## Validation

- Traced runtime field consumers and load steps, resource paths, Addressables entries, and asset GUID references across Assets, ProjectSettings and Packages before deletion.
- 341 reference/preservation checks pass: retired PNGs/metas absent, protected bytes/GUIDs unchanged, no references to deleted GUIDs, and every remaining CoreUI entry resolves to the correct asset.
- 27 Unity play-mode checks pass using the real `HandDrawnMenuAssets` loader and retained artwork. The isolated fixture adapts only the Addressables cache, exercising the file/Resources path without changing a player account or game scene.
- Main Menu 1 passes 190 rendering/interaction/layout checks; Main Menu 2 passes 467. The login rendering regression passes 50 checks on Direct3D12 after cleanup. Runtime editor/player and full editor C# compilation pass.

Recheck the cleanup and menu loaders:

```powershell
.\tools\verify-menu-asset-cleanup.ps1
.\tools\verify-main-menu.ps1 -Menu Cleanup
.\tools\verify-main-menu.ps1 -Menu Main
.\tools\verify-main-menu.ps1 -Menu Modes
```
