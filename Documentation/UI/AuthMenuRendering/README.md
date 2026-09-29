# Login menu rendering repair

The account card and buttons could render as solid ink at startup and after logout. Opening another auth mode restored their fills.

The menu creates each Graphic while it is active, then parents it under the canvas. `OnEnable` runs before the Graphic has a canvas. Unity's `Graphic.OnTransformParentChanged` caches the new canvas, but the smoothing classes previously configured their shader channels only in `OnEnable` and `OnCanvasHierarchyChanged`. Parenting did not reliably invoke the latter callback. The first login canvas therefore had TexCoord1, Normal and Tangent (the text channels), but lacked TexCoord2, which carries the panel fill colour. Disabling and enabling another menu panel subsequently added the missing channel.

`AntialiasedUIGraphic` and `AntialiasedMenuImage` now configure channels after the base parenting callback and before a PreRender rebuild. The shared helper preserves existing channels and writes the mask only when a required bit is missing. This uses the normal UI rebuild lifecycle, with no per-frame polling, forced canvas refresh, delayed visibility, or extra effects.

## Verification

Unity 6000.4.7f1, using the production `MainMenuAuthUI`, font, rounded graphics, smoothing shader, responsive safe area and actual inventory layout fitter in an isolated play-mode project:

- Direct3D12: 50 checks passed.
- Direct3D11: 50 checks passed.
- The test checks channel configuration before the first rebuild, then actual ScreenSpaceOverlay pixels without interacting with the login screen. It verifies the light card fill, selected button colour, readable heading, signup and login switching, and three destroyed/recreated auth UIs.
- Removing the parenting and PreRender fix from the isolated fixture causes the regression to fail at its first missing-channel check. The earlier diagnostic reproduced the black panels and their recovery after a menu switch.
- Main menu: 190 existing checks passed. Mode selection: 467 passed. Antialiasing: 30 passed, including AA disabled, clipping, tint, fading and bitmap sampling.
- Runtime editor/player and full editor source compilation passed.

Logout UI recreation follows `SessionLoadingController.ReturnToLogin` → `AuthController.Create` → `MainMenuAuthUI.Initialize`. The fixture exercises that UI lifecycle without calling the account API or modifying a real player's session. GameView buffer capture allows its initial window resize to settle; the separate channel assertions run before the first canvas rebuild.

Run the overlay regression from PowerShell:

```powershell
.\tools\verify-main-menu.ps1 -Menu Auth -GraphicsApi Direct3D12
.\tools\verify-main-menu.ps1 -Menu Auth -GraphicsApi Direct3D11
```

The isolated project lives under `Logs/MainMenuPreviewProject`; it leaves the open project scene and play session untouched. PNGs and individual checks are saved here. The named API check files preserve results for both backends; the unsuffixed captures are replaced by each run.

![Login at startup](Login-Startup.png)

![Login after the third UI recreation](Login-After-Logout-3.png)
