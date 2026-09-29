# Chess But Weird — main menu redesign

Implemented in the existing Unity runtime menu. The pawn-versus-rook joke, rough ink edges, and yellow / green / purple action palette carry forward from the original main-menu reference. Its superseded reference sheet and unused bitmap controls were removed during the [asset cleanup](../MenuAssetCleanup/README.md).

## Easy-to-find files

- `Assets/Resources/MainMenu/MainMenuLogoOriginal.png` — pixel-for-pixel copy of the original GameLogo.png, included in player builds.
- `Assets/Resources/MainMenu/MainMenuCredits.txt` — editable credits copy.
- `Assets/Scripts/Chess/Gameplay/UI/MainMenuPresentation.cs` — first-screen composition, copy, buttons, navigation and credits panel.
- `MainMenuButtonMotion.cs` — hover, focus, press and spring timing.
- `MainMenuPaperGraphic.cs` — paper grain and ambient lighting.
- `MainMenuInkGraphic.cs` — reusable geometric ink accents.

Unity requires GUIDs inside .meta files; every new asset and source filename is descriptive.

## Visual and interaction choices

The central title and large yellow START button form one clear reading path. SETTINGS and CREDITS use smaller sage and lavender cards. Four restrained side accents replace the repeated background illustration. Native UI text uses the existing Schoolbell font.

The original lettering, pawn, rook, expressions, blue scribbles and impact marks are retained. Transparent export padding is trimmed using the original sprite bounds. A 0.65-pixel translucent ink outline slightly improves edge clarity, and a faint shadow sits 2 pixels right and 4 pixels down. The title drifts by 0.7 design pixels and 0.24 degrees, on its own small canvas. Focus raises a button by 5 pixels and scales it to 1.025, with a bounded spring and 0.32-degree wobble. Press scale is 0.978. Color transitions take 0.12 seconds and credits fade over 0.18 seconds, all on unscaled time. Shadows travel with the buttons. Arrow keys, Tab / Shift+Tab, and controller navigation are supported; Escape / controller Back closes credits.

The backdrop uses one static 187-vertex mesh and a 64 × 64 grain texture created once. Lighting is baked into mesh colors. The illustrated logo now has alpha transparency, mipmaps, Trilinear filtering and high-quality compression. The shared menu coverage shader smooths vector silhouettes independently of camera anti-aliasing. This menu adds no particles, blur, post-processing volumes, shader packages or animation dependencies. FPS on target hardware has not been benchmarked. See `../MenuAntialiasing/README.md` for the shared graphics correction.

Hover and keyboard selection use the lift, scale and wobble feedback without an arrow pointing at the button. The small forward icon inside START is part of the button artwork.

## Player notifications

All player-facing error notifications use English copy through `Assets/Scripts/Network/PlayerNotificationText.cs`. Known server codes and common English/Vietnamese error messages receive specific English messages. Unrecognized server prose and localized OS exceptions receive an English fallback. Account, profile, inventory, gacha and room UI use this shared handling; server match-result reasons are formatted in English as well. API and room connections request English with `Accept-Language: en`. Original API exception messages and response bodies remain available for diagnostics. Player names and item names are retained.

Verify the language mapping without touching a live account or Unity session:

```powershell
dotnet run --project Tests/Chess.Notifications.Tests/Chess.Notifications.Tests.csproj
```

## Verification

- Runtime editor, runtime player, and editor-tool C# compilation passed against the installed Unity 6000.4.7f1 references.
- 190 isolated production-builder checks passed: callback forwarding, explicit navigation, absence of hover pointer arrows, hover and press feedback, settling, disabled states, credits input blocking, text overflow, and viewport containment.
- 40 English notification checks passed: common localized errors, structured-code precedence, HTTP fallbacks, unknown-language fallbacks, localized OS exceptions, diagnostic preservation and match-result copy.
- Rendered 1920 × 1080, 1280 × 720, 1024 × 768, 800 × 600, 2560 × 1080, 3840 × 2160 and 1080 × 1920.
- Inspected normal, hover and credits captures. Full game startup and gameplay were not exercised by this UI fixture.
- Primary preview: `MainMenu-1920x1080.png`. Interaction variants: `MainMenuStart-Hover.png` and `MainMenuStart-Pressed.png`.

Reproduce through **Chess → UI Audit → Capture Redesigned Main Menu**, or run:

```powershell
./tools/verify-main-menu.ps1
./tools/verify-loading-compile.ps1 -UnityPath 'D:/Unity Editor/6000.4.7f1/Editor/Unity.exe'
```

The render script copies the menu builders, loader, fonts and resources into an isolated project under `Logs/MainMenuPreviewProject`; it does not modify the open scene. Older artwork still used by downstream menus is retained; superseded Main Menu 1/2 artwork is removed.

## Original logo provenance

`MainMenuLogoOriginal.png` preserves the original `GameLogo.png` byte for byte. Matching SHA-256 hashes were verified before the redundant material copy was removed, and the hash is recorded in the cleanup manifest. Both menus now use the single resource copy. This revision introduced no generated replacement. `MainMenuLogoOriginal.cs` uses the same 1294 × 715 crop as the original menu, scaled to the imported texture dimensions. Crop, placement, subtle edge clarity and shadow are rendering changes; the source drawing is untouched.

The rejected generated concept and its prompt are archived under `DiscardedLogoConcept/`, outside the Unity Assets folder and outside player resources.
