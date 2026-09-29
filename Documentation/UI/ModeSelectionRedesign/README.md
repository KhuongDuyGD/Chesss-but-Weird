# Chess But Weird — mode selection redesign

Implemented in the second runtime menu, after START. The original mode-menu reference guided the redesign; its superseded sheet and bitmap controls were subsequently removed during the [asset cleanup](../MenuAssetCleanup/README.md). Main Menu 1's original logo, Schoolbell handwriting, soft ink edges, paper grain and restrained shadows continue into this screen.

## Easy-to-find files

- `Assets/Scripts/Chess/Gameplay/UI/ModeSelectionPresentation.cs` — layout, editable copy, mode cards, utility column, keyboard/controller navigation and modal focus handling.
- `Assets/Scripts/Chess/Gameplay/UI/ModeMenuDoodleGraphic.cs` — all eight illustrated UI meshes and their colors. The chest, gold diamond, blue tongue-out portrait and gray gear preserve the original utility icon identities.
- `Assets/Scripts/Chess/Gameplay/UI/HandDrawnMenuView.cs` — existing game destinations, account checks, profile/settings overlays and the hub integration.
- `Assets/Resources/MainMenu/MainMenuLogoOriginal.png` — the unchanged original logo shared with Main Menu 1.
- `Assets/Editor/ModeSelectionRedesignAudit.cs` — isolated production-builder validation and captures.
- `ModeSelection-1920x1080.png` — primary preview.
- `ModeSelectionLocal-Hover.png` / `ModeSelectionLocal-Pressed.png` and `ModeSelectionInventory-Hover.png` / `ModeSelectionInventory-Pressed.png` — interaction previews.

All new filenames and runtime icon object names are descriptive. Existing source artwork still used by other screens remains available; replaced mode-menu artwork is removed. No generated replacement logo was introduced.

## Art direction and behavior

LOCAL, ONLINE, ARAM and SHOP form a two-by-two grid. Yellow, sage, lavender and blue carry forward the reference palette. Each mode has a large label, a short explanation and a distinct chess illustration. The smaller right column groups Inventory, Gacha, Profile and Settings with visible labels and the upgraded versions of their familiar icons. The original logo is smaller in the header. Repeated battle doodles, background hearts and scattered stars are removed from this screen.

The mode and utility controls use the same soft hover lift, short spring, press compression and small selection wobble as Main Menu 1. Color transitions take 0.12 seconds. The hub has no hover pointer arrows. Only the tiny gacha gem animates continuously, on its own child canvas; its idle displacement is 0.35 design pixels with 0.32 degrees of rotation. The rest of the artwork stays static at idle.

Existing mode destinations and account requirements remain connected. BACK returns to Main Menu 1; LOG OUT keeps the existing authentication flow. The original SHOP action only logged a click. It now has a visible COMING SOON label and an English notification directing players to Inventory or Gacha. This change does not implement a shop or a purchase flow.

Arrow keys, Tab / Shift+Tab and controller navigation cover all ten controls. Profile and Settings block both pointer and keyboard activation of the underlying hub, then restore the selected hub control on close. Screen transitions block keyboard activation until the destination settles. Player-facing copy is English.

## Rendering and performance

The shared paper surface uses the existing static gradient mesh and small grain texture. After the shared antialiasing correction, the eight illustrations use 6,603 cached vertices in total and require no raster texture assets. Their geometry uses controlled fills, soft contact shadows and joined ink strokes. Card shadows use simple translucent UI meshes. The shared menu coverage shader smooths their silhouettes independently of camera anti-aliasing, without particles, blur, full-screen post-processing or tween dependencies. Target-device FPS has not been benchmarked. See `../MenuAntialiasing/README.md` for the shared graphics correction.

## Verification

- 467 mode-menu checks passed: callback forwarding, navigation, text overflow, viewport containment, illustration mesh bounds, hover/press/settling, modal input blocking, focus restoration and screen-exit selection cleanup.
- Rendered 1920 × 1080, 1280 × 720, 1024 × 768, 800 × 600, 2560 × 1080, 3840 × 2160 and 1080 × 1920, plus four interaction variants.
- Inspected full-size and 4:3 previews and the hover states.
- Main Menu 1 regression audit passed all 190 checks.
- Runtime editor/player sources and all editor tools compiled against Unity 6000.4.7f1.

Reproduce in the editor through **Chess → UI Audit → Capture Redesigned Mode Selection**, or run:

```powershell
./tools/verify-main-menu.ps1 -Menu Modes
./tools/verify-main-menu.ps1
./tools/verify-loading-compile.ps1 -UnityPath 'D:/Unity Editor/6000.4.7f1/Editor/Unity.exe'
```

The render script copies the actual builders, font and logo into an isolated preview project under Temp. It does not change the user's open scene, connect to a server, change their account or spend currency. These checks exercise the UI builder; full game startup and live multiplayer were not exercised.
