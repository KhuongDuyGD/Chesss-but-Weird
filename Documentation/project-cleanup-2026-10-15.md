# Project cleanup - 2026-10-15

Initial project size: 10,247,319,442 bytes (9.54 GiB), including Git and generated files.

Already removed regenerable build caches and the old CosmeticsVerification player: 3,415,487,984 bytes.

Build-cache retention threshold: 1 GiB in ProjectSettings/ScriptableBuildPipeline.json (the package default was 20 GiB). It is pruned after builds and can temporarily exceed the threshold while building.

## Reviewed source deletions

135 reviewed source/capture files plus their existing metadata: 192 files total. All 192 are unchanged from HEAD and verified recoverable locally from Git or the local Git LFS object store. No remaining GUID references were found in retained Assets/ProjectSettings/domain-package text assets.

Runtime filename/path references were checked before including asset candidates. Active resource catalogs protect Bot, Pause, Analysis and skin thumbnails. Gacha demo is not in Build Settings; the obsolete Inventory fixture still expects the removed EquipItem method.

| File | MiB |
| --- | ---: |
| Assets/Materials/Gacha_menu/BackButton.png | 0.025 |
| Assets/Materials/Gacha_menu/BackGacha.png | 0.140 |
| Assets/Materials/Gacha_menu/DiamondGacha.png | 0.832 |
| Assets/Materials/Gacha_menu/GachaMenuDesign.png | 2.341 |
| Assets/Materials/Gacha_menu/GachaMenuDesignBlank.png | 1.009 |
| Assets/Materials/Gacha_menu/GachaResultBlank.png | 1.777 |
| Assets/Materials/Gacha_menu/GachaResultDesign.png | 2.036 |
| Assets/Materials/Gacha_menu/GachaTitle.png | 0.273 |
| Assets/Materials/Gacha_menu/GoldGacha.png | 0.483 |
| Assets/Materials/Gacha_menu/HistoryButton.png | 0.460 |
| Assets/Materials/Gacha_menu/StandardGacha.png | 1.248 |
| Assets/Materials/Gacha_menu/SummonTicket.png | 0.923 |
| Assets/Materials/Gacha_menu/SummonX1.png | 1.268 |
| Assets/Materials/Gacha_menu/SummonX10.png | 1.097 |
| Assets/Materials/GameplayUI/AnalysisBoard.png | 0.876 |
| Assets/Materials/LANUI/BackButton.png | 1.024 |
| Assets/Materials/LANUI/CopyIcon.png | 0.226 |
| Assets/Materials/LANUI/HostButton.png | 0.879 |
| Assets/Materials/LANUI/JoinButton.png | 0.666 |
| Assets/Materials/LANUI/LANUIDesign.png | 2.882 |
| Assets/Materials/LANUI/LeaveButton.png | 0.908 |
| Assets/Materials/LANUI/ReadyButton.png | 1.135 |
| Assets/Materials/LANUI/RefreshButton.png | 0.685 |
| Assets/Materials/LANUI/StartButton.png | 1.159 |
| Assets/Materials/MultiplayerUI/BackButton.png | 1.325 |
| Assets/Materials/MultiplayerUI/CopyIcon.png | 0.226 |
| Assets/Materials/MultiplayerUI/CreateRoomButton.png | 1.658 |
| Assets/Materials/MultiplayerUI/JoinRoomButton.png | 1.121 |
| Assets/Materials/MultiplayerUI/LeaveRoomButton.png | 1.580 |
| Assets/Materials/MultiplayerUI/MultiplayerUIBlank.png | 2.346 |
| Assets/Materials/MultiplayerUI/MultiplayerUIDesign.png | 2.991 |
| Assets/Materials/MultiplayerUI/ReadyButton.png | 1.596 |
| Assets/Materials/MultiplayerUI/RefreshButton.png | 1.324 |
| Assets/Materials/MultiplayerUI/StartGameButton.png | 1.659 |
| Assets/Materials/PlayerProfile/AchievementTitle.png | 0.396 |
| Assets/Materials/PlayerProfile/Avatar1.png | 1.449 |
| Assets/Materials/PlayerProfile/Avatar2.png | 1.869 |
| Assets/Materials/PlayerProfile/Avatar3.png | 1.505 |
| Assets/Materials/PlayerProfile/Avatar4.png | 1.559 |
| Assets/Materials/PlayerProfile/Avatar5.png | 1.590 |
| Assets/Materials/PlayerProfile/BasicProfileData.png | 1.047 |
| Assets/Materials/PlayerProfile/ChangeDataProfileButton.png | 0.243 |
| Assets/Materials/PlayerProfile/MatchHistory.png | 1.298 |
| Assets/Materials/PlayerProfile/PlayerProfileMain.png | 1.700 |
| Assets/Materials/PlayerProfile/PlayerProfileMainBlank.png | 0.327 |
| Assets/Materials/PlayerProfile/PlayerProfileSketch.png | 0.026 |
| Assets/Materials/PlayerProfile/Streaks.png | 0.716 |
| Assets/Materials/Result_Menu/DrawUI.png | 1.617 |
| Assets/Materials/Result_Menu/LoseUI.png | 1.629 |
| Assets/Materials/Result_Menu/StalemateUI.png | 1.832 |
| Assets/Materials/Result_Menu/WinUI.png | 1.667 |
| Assets/Materials/Rook_skin/73ad768b60f1ddd498245a5c3c4deb76.fbx | 36.090 |
| Assets/Materials/SettingsMenu/SettingsMenu.png | 1.493 |
| Assets/Editor/InventoryMusicRedesignAudit.cs | 0.015 |
| Assets/Scripts/Chess/Gameplay/UI/GachaSummonRevealDemo.cs | 0.003 |
| Assets/Scenes/GachaRevealDemo.unity | 8.632 |
| Documentation/UI/menu-layout-overview.jpg | 0.713 |
| Documentation/UI/AuthMenuRendering/Login-After-Logout-1.png | 0.103 |
| Documentation/UI/AuthMenuRendering/Login-After-Logout-2.png | 0.103 |
| Documentation/UI/AuthMenuRendering/Login-After-Logout-3.png | 0.103 |
| Documentation/UI/AuthMenuRendering/Login-After-Switch.png | 0.103 |
| Documentation/UI/AuthMenuRendering/Login-Startup.png | 0.103 |
| Documentation/UI/AuthMenuRendering/Login-Untouched.png | 0.103 |
| Documentation/UI/AuthMenuRendering/Signup.png | 0.123 |
| Documentation/UI/InventoryMusicRedesign/Inventory-1080x1920.png | 0.175 |
| Documentation/UI/InventoryMusicRedesign/Inventory-1280x720.png | 0.191 |
| Documentation/UI/InventoryMusicRedesign/Inventory-1920x1080.png | 0.326 |
| Documentation/UI/InventoryMusicRedesign/Inventory-2560x1080.png | 0.343 |
| Documentation/UI/InventoryMusicRedesign/Inventory-800x600.png | 0.101 |
| Documentation/UI/InventoryMusicRedesign/Inventory-Filters-1080x1920.png | 0.185 |
| Documentation/UI/InventoryMusicRedesign/Inventory-Filters-1280x720.png | 0.203 |
| Documentation/UI/InventoryMusicRedesign/Inventory-Filters-1920x1080.png | 0.349 |
| Documentation/UI/InventoryMusicRedesign/Inventory-Filters-2560x1080.png | 0.367 |
| Documentation/UI/InventoryMusicRedesign/Inventory-Filters-800x600.png | 0.108 |
| Documentation/UI/InventoryMusicRedesign/MainMenu2-Music-1080x1920.png | 1.983 |
| Documentation/UI/InventoryMusicRedesign/MainMenu2-Music-1280x720.png | 0.826 |
| Documentation/UI/InventoryMusicRedesign/MainMenu2-Music-1920x1080.png | 1.628 |
| Documentation/UI/InventoryMusicRedesign/MainMenu2-Music-2560x1080.png | 2.335 |
| Documentation/UI/InventoryMusicRedesign/MainMenu2-Music-800x600.png | 0.411 |
| Documentation/UI/InventoryMusicRedesign/Music-Picker-1080x1920.png | 1.959 |
| Documentation/UI/InventoryMusicRedesign/Music-Picker-1280x720.png | 0.802 |
| Documentation/UI/InventoryMusicRedesign/Music-Picker-1920x1080.png | 1.509 |
| Documentation/UI/InventoryMusicRedesign/Music-Picker-2560x1080.png | 2.234 |
| Documentation/UI/InventoryMusicRedesign/Music-Picker-800x600.png | 0.392 |
| Documentation/UI/MainMenuRedesign/MainMenu-1024x768.png | 0.957 |
| Documentation/UI/MainMenuRedesign/MainMenu-1080x1920.png | 2.158 |
| Documentation/UI/MainMenuRedesign/MainMenu-1280x720.png | 1.074 |
| Documentation/UI/MainMenuRedesign/MainMenu-1920x1080.png | 2.233 |
| Documentation/UI/MainMenuRedesign/MainMenu-2560x1080.png | 2.907 |
| Documentation/UI/MainMenuRedesign/MainMenu-3840x2160.png | 7.620 |
| Documentation/UI/MainMenuRedesign/MainMenu-800x600.png | 0.491 |
| Documentation/UI/MainMenuRedesign/MainMenuCredits-1024x768.png | 0.789 |
| Documentation/UI/MainMenuRedesign/MainMenuCredits-1920x1080.png | 1.710 |
| Documentation/UI/MainMenuRedesign/MainMenuStart-Hover.png | 2.232 |
| Documentation/UI/MainMenuRedesign/MainMenuStart-Pressed.png | 2.235 |
| Documentation/UI/MenuAntialiasing/MainMenu-Before.png | 2.217 |
| Documentation/UI/MenuAntialiasing/MenuEdges-AAOff.png | 0.089 |
| Documentation/UI/MenuAntialiasing/MenuEdges-MSAA4x.png | 0.089 |
| Documentation/UI/MenuAntialiasing/ModeSelection-Before.png | 1.535 |
| Documentation/UI/ModeSelectionRedesign/ModeSelection-1024x768.png | 0.785 |
| Documentation/UI/ModeSelectionRedesign/ModeSelection-1080x1920.png | 1.983 |
| Documentation/UI/ModeSelectionRedesign/ModeSelection-1280x720.png | 0.827 |
| Documentation/UI/ModeSelectionRedesign/ModeSelection-1920x1080.png | 1.628 |
| Documentation/UI/ModeSelectionRedesign/ModeSelection-2560x1080.png | 2.336 |
| Documentation/UI/ModeSelectionRedesign/ModeSelection-3840x2160.png | 5.303 |
| Documentation/UI/ModeSelectionRedesign/ModeSelection-800x600.png | 0.411 |
| Documentation/UI/ModeSelectionRedesign/ModeSelectionInventory-Hover.png | 1.628 |
| Documentation/UI/ModeSelectionRedesign/ModeSelectionInventory-Pressed.png | 1.629 |
| Documentation/UI/ModeSelectionRedesign/ModeSelectionLocal-Hover.png | 1.624 |
| Documentation/UI/ModeSelectionRedesign/ModeSelectionLocal-Pressed.png | 1.634 |
| Documentation/UI/PauseMenu/Match-Info-1080x1920.png | 0.075 |
| Documentation/UI/PauseMenu/Match-Info-1280x720.png | 0.068 |
| Documentation/UI/PauseMenu/Match-Info-1920x1080.png | 0.123 |
| Documentation/UI/PauseMenu/Match-Info-2560x1080.png | 0.133 |
| Documentation/UI/PauseMenu/Match-Info-800x600.png | 0.036 |
| Documentation/UI/PauseMenu/Multiplayer-1080x1920.png | 0.083 |
| Documentation/UI/PauseMenu/Multiplayer-1280x720.png | 0.077 |
| Documentation/UI/PauseMenu/Multiplayer-1920x1080.png | 0.133 |
| Documentation/UI/PauseMenu/Multiplayer-2560x1080.png | 0.144 |
| Documentation/UI/PauseMenu/Multiplayer-800x600.png | 0.042 |
| Documentation/UI/PauseMenu/Restart-Confirmation-1080x1920.png | 0.073 |
| Documentation/UI/PauseMenu/Restart-Confirmation-1280x720.png | 0.066 |
| Documentation/UI/PauseMenu/Restart-Confirmation-1920x1080.png | 0.115 |
| Documentation/UI/PauseMenu/Restart-Confirmation-2560x1080.png | 0.125 |
| Documentation/UI/PauseMenu/Restart-Confirmation-800x600.png | 0.036 |
| Documentation/UI/PauseMenu/Singleplayer-1080x1920.png | 0.078 |
| Documentation/UI/PauseMenu/Singleplayer-1280x720.png | 0.070 |
| Documentation/UI/PauseMenu/Singleplayer-1920x1080.png | 0.121 |
| Documentation/UI/PauseMenu/Singleplayer-2560x1080.png | 0.131 |
| Documentation/UI/PauseMenu/Singleplayer-800x600.png | 0.039 |
| Documentation/UI/SettingsRebuild/Accessibility-1920x1080.png | 0.305 |
| Documentation/UI/SettingsRebuild/General-1280x720-scale150-large.png | 0.163 |
| Documentation/UI/SettingsRebuild/General-1920x1080.png | 0.280 |
| Documentation/UI/SettingsRebuild/Graphics-1920x1080.png | 0.323 |
| Documentation/UI/MainMenuRedesign/DiscardedLogoConcept/MainMenuLogoDiscarded.png | 1.566 |

Status: reviewed; deletion pending.

The Git database, current skin models/thumbnails, Halloween assets, Stockfish, installed packages and reusable runtime tests are retained.
