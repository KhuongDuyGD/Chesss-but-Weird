# Menu layout calibration

Updated 2026-09-16. See `menu-layout-overview.jpg` for Unity-rendered previews.

## Reference artwork

| Screen | Reference | Runtime composition |
| --- | --- | --- |
| Main Menu 1 | `Main_Menu/MainMenuDesign.png` | Separate original components, positions measured in 1672 × 941 coordinates |
| Main Menu 2 | `Main_Menu/MainMenuDesgin2.png` | Separate original components, normalized from the 640 × 363 reference |
| Gacha | `Gacha_menu/GachaMenuDesign.png` | `GachaMenuDesignBlank.png` plus individual buttons, panels and live values |
| LAN lobby | `LANUI/LANUIDesign.png` | `LANUIBlank.png` plus buttons and live text in the existing slots |
| Multiplayer lobby | `MultiplayerUI/MultiplayerUIDesign.png` | `MultiplayerUIBlank.png` plus buttons and live text in the existing slots |
| Profile | `PlayerProfile/PlayerProfileMain.png` | `PlayerProfileMainBlank.png` plus profile panels and live values |
| Choose Side / ARAM | No complete reference supplied; manual layout confirmed by user | Existing artwork, balanced options, clear title/caption areas |

All paths in this table are relative to `Assets/Materials`. Original PNGs and their import settings were not edited. No reference Design image is used as the Gacha runtime background. The shared paper sample and ARAM option border come from small empty regions of existing Blank images.

## Layout behavior

- `MenuDesignFrame` fits a fixed design canvas uniformly inside `ResponsiveSafeArea`. Extra space remains paper-colored; artwork and interactive controls share the same coordinates. Transition animations run on a child, separate from the sizing transform.
- Main menus use 1920 × 1080 design units; Gacha, Profile and lobby artwork uses 1672 × 941 units. Native artwork positions and sizes stay together in each controller.
- Choose Side Back is centered at (-720, -435), sized 270 × 100 in the 1920 × 1080 frame: its left edge is 105 design units inside the frame before hover animation.
- ARAM has three equal 470 × 180 option frames at x = -540, 0, 540. Title, captions and decorations occupy separate areas. ARAM lobby status is prefixed with ARAM instead of overlaying the illustrated heading.
- Gacha Summon, History and reward controls use their visible images as the click targets. Live counters no longer overlap baked example values. Profile history text stays inside its panel, and the opaque profile backdrop prevents hub artwork showing through.
- `CoreArtworkCache.GetSprite(path, trimTransparent)` distinguishes full images from trimmed sprites. `MenuArtworkBounds` uses normalized source alpha bounds for both imported GPU-only textures and file-loaded textures, preventing loading-path-dependent sizes. Existing callers without the optional argument retain full-image behavior.

## Validation and reproduction

- Unity script compilation succeeded; four pre-existing obsolete-API warnings remain in `CosmeticsRuntimeVerification.cs`.
- 45 rendered layouts and control-containment checks passed: 9 screens at 1920 × 1080, 1024 × 768, 2560 × 1080, 2340 × 1080 and 1080 × 1920. The phone landscape case includes an asymmetric left/right inset and bottom gesture inset. See `layout-checks.txt`.
- Run **Chess > UI Audit > Capture Menus** in Play mode. The editor utility renders actual runtime canvases to textures at the listed sizes and checks active selectable bounds. Output goes to `Temp/MenuLayoutAudit`. Its lobby fixture does not create/join rooms or spend currency.
- Run `python tools/generate_menu_artwork_bounds.py --check` (Pillow required) to verify the baked geometry against all 64 source PNGs. Run without `--check` after replacing component artwork.

The validation covers Unity Editor rendering and simulated safe areas, not native device builds or OS-level input. Portrait keeps the full landscape composition visible, so controls are smaller; the preferred game presentation remains landscape. Screenshot capture does not constitute a multiplayer transport or gacha transaction test.
