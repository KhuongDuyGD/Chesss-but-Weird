# Kingdom Rush cosmetics

Map author: **Tofu**. The in-game Credits screen includes the Kingdom Rush map credit.

## Runtime content

Unity 6000.4.7f1, URP 17.4, Addressables 4.0.1. PC uses Forward+; mobile uses Forward. Catalog registration does not grant ownership or change saved loadouts. Backend remains authoritative for ownership, prices and purchases.

Two independently equipped collections use IDs `kingdom_rush_denas` and `kingdom_rush_veznan`. Each contains six approved character sculpts with white/black identity rims, a board with six prisoner positions on each rank end, and a separately equipped environment. All are Legendary and sold directly, outside gacha.

| Product per faction | Gold | Diamonds |
| --- | ---: | ---: |
| Piece set | 60,000 | 3,000 |
| Board | 36,000 | 1,800 |
| Background | 6,000 | 300 |
| Three-item bundle, 25% off | 76,500 | 3,825 |

The archived mixed `kingdom_rush` set/board remain addressable for a future mode and stay outside the public catalog. Its old Linirea background remains separately sold at 6,000 Gold / 300 Diamonds. Classic is the fixed free default background. No real account, purchase, ownership grant or production migration is exercised by client verification.

Linirea uses fresh green vegetation, clean royal stone, blue water and four inner courtyard towers. River channels are cut out of the terrain; shoreline vertices match the water edges. Outer towers, connecting arches and distant citadels are removed. Vez'Nan uses scorched ash patches, four inner ruined columns and six skeletal ruin clusters; outer architecture and trench-overlapping trees are removed. Approved character paint, board/sculpt geometry, 64 cells and twelve prisoner anchors are preserved.

## Source and regeneration

- `Pieces/`: twelve editable approved Blender sculpts. Opposing teams face inward through the existing presenter. Maximum footprint is approximately 0.82 of a one-unit cell.
- `Factions/KR_{Denas,Veznan}_{Board,Environment}.blend`: current editable scenes, with packed material images.
- `AuthoringBase/`: required immutable inputs for repeatable environment refinement, including four base scenes and the material palette. These are source dependencies, not disposable review captures.
- `Polish3D/Geometry/` and `model_manifest.json`: character surface/UV inputs for optional regeneration of clarity and magic-mask derivatives.
- `asset_protection_hashes.json`: relative paths and approved SHA-256 hashes for sixteen protected models and thirteen source albedos.
- Runtime products live under `Assets/Skins/KingdomRushCollab/`; catalog data is under `Assets/Game/Data/`.

Current rebuild order, from the project root:

1. Run `Tools/prepare_style_direction.py` with the existing paint Python environment (NumPy/Pillow).
2. Run Blender in background with `--python-exit-code 1 --python ArtSource/KingdomRush/Tools/refine_faction_direction.py`. It starts from `AuthoringBase`, applies style and release cleanup, exports only the two environment FBX files, and refreshes packed materials in the four current scenes.
3. Run Unity `KingdomRushGameSetup.BuildContent`. This imports maps, refreshes material/prefab wrappers and builds local Addressables. The existing menu `Chess > Kingdom Rush > Register Shop Cosmetics` registers content without a content build.

Optional `Tools/prepare_advanced_surfaces.py` regenerates character clarity/magic maps and the local URP foliage shader using installed URP sources. It is separate from an ordinary environment rebuild. Do not run the older HQ/scaffold generators alone over the current delivery: they describe historical geometry rather than the current refinement.

Authoring diagnostics and render captures go under ignored `Logs/KingdomRush/`. Historical reports, obsolete test fixtures and old review captures are not required by the runtime or the current rebuild path.

## Presentation ownership

`KingdomRushEnvironmentPresentation` owns faction lighting, restrained post-processing, fog and water movement; it restores previous scene state on unload. Fog follows camera depth so the arena stays clear. `KingdomRushBattlefieldAmbience`, `KingdomRushPieceMagic` and the authored particle budgets honor existing quality, reduced-motion, flashing and visual-chaos settings. XYZ particle velocity curves use matching modes.

`ChessOrbitCamera` retains orthographic projection and existing orbit/zoom controls. Both new boards use optional framing padding 1.04; other boards keep their normal padding. Camera distance clears the complete near rectangle at low pitch/wide zoom. `CosmeticEnvironmentBackdrop` belongs to each selected custom environment, supplies an opaque horizon instead of the original scene sky artwork, and restores camera clear flags/color on unload. Classic has no custom environment instance and keeps its original background.

## Maintained verification

- `Tests/Chess.Domain.Tests`, `Chess.Notifications.Tests`, `Chess.Settings.Tests`, `Chess.Statistics.Tests`: independent .NET regression executables. Run with `dotnet run --project Tests/<name>/<name>.csproj --configuration Release`.
- `Assets/Tests/Editor/CosmeticReleaseVerification.cs`: read-only import, catalog-category, GUID, thumbnail, prefab, animation, particle-mode, environment and faction-layout checks. Run Unity in `-batchmode -nographics -quit` with `-executeMethod CBWRelease.CosmeticReleaseVerification.Run` and `-cbwEvidence=<absolute external JSON path>`.
- `Tools/check_release_cleanup.py`: offline protected-model/paint hashes, retained architecture, shoreline/bank/UV, skeletal geometry and camera regression checks. Run Blender with `--python-exit-code 1 --python <script> -- --no-render`; optional `--output=<absolute directory>` selects evidence location. Without `--no-render`, it also makes clearly labelled Blender previews.
- `Assets/Tests/Editor/KingdomRushFactionVerification.cs`: opt-in packed-content gameplay/render fixture covering both factions, moves, capture, promotion, camera, quality/accessibility, unload and synthetic shop prices. It requires a separate batch Editor with native graphics; `BuildAndStart` builds first, `Start` reuses packed content. It is never launched automatically. D3D11 uses `-force-d3d11 -kr-verify-d3d11`; D3D12 uses `-force-d3d12`. Do not pass `-quit` to this asynchronous fixture; it exits itself.

Final static/offline checks and content packaging do not certify actual in-match visuals, standalone/mobile performance or online two-client behavior. The user owns the current E2E acceptance pass: equip each background, orbit through low angles at both zoom limits, capture a piece, switch skins and return to Classic. Existing storefront thumbnails still show the preceding art and can be refreshed separately.
