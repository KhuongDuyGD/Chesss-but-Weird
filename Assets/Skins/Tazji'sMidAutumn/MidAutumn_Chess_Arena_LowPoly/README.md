# Trăng Rằm — Mid-Autumn Chess Arena

Low-poly Vietnamese Mid-Autumn festival courtyard, designed to accompany the MidAutumn_Chess_Set. Red lacquer, jade, bamboo gold and ivory paving; moon gate, rabbit medallion, star lanterns, perimeter lantern strings, banyan tree, mooncake stall, festival drums, offering tray and lotus basin. The board is intentionally empty so game pieces can be placed by the consuming project.

## Exact compatibility
Measured directly from Chess_Arena_Source.blend, and checked against the retained Halloween_Chess_Arena/reference_geometry.json.

- Playable board: 8 x 8 cells; each cell exactly 1 x 1 original Blender unit.
- Playable coordinates: X and Y from -4 to +4; surface Z = 0.
- Board outer mesh: 8.68 x 8.68.
- Six right slots: centers X = 4.93; Y = -3.15, -1.89, -0.63, 0.63, 1.89, 3.15.
- Right slot pitch: 1.26. Outer footprint: 0.70 x 0.90. Inner surface: 0.59 x 0.79, top Z approximately 0.036.
- The original six left slots are also retained.
- Chess_Board, Arena_Walkable_Floor, Arena_Floor_Inlays and Arena_Floating_Island retain the original vertex coordinates and face indices exactly. Only their materials change.
- New scenery expands the rear perimeter. The playable board and slots are not rescaled or moved.

## Deliverables
- MidAutumn_Chess_Arena_Source.blend — source scene, game collection and separate presentation collection, two cameras, packed plaque font.
- MidAutumn_Chess_Arena.fbx — 13 game mesh objects, 44,686 triangles. Lights, cameras and studio floor excluded.
- MidAutumn_Chess_Arena_Preview.png — hero render.
- MidAutumn_Chess_Arena_Top.png — orthographic layout render.
- layout.json — board and right-slot coordinates.
- validation.json — geometry, source comparison, raw FBX comparison, axes/unit metadata and reimport evidence.

## Validation
All export meshes have unit scale, no modifiers, zero non-manifold edges and zero degenerate triangles. Protected geometry is checked in both the Blender scene and binary FBX. Original FBX axes and UnitScaleFactor=100 metadata are matched using FBX_SCALE_UNITS. An FBX reimport confirms 13 meshes and matching triangle totals. Conservative per-triangle XY bounds above the playing surface find no scenery overlapping the board or six right slots.

The new ornamental geometry uses closed overlapping islands rather than fused printable solids. Materials are constant-color Principled shaders; lantern emission and scene lighting may need equivalent setup in the target engine. No engine-specific import/runtime test was performed. Do not rescale the board to fit the showcase scale of the separate piece collection; choose the piece instance scale in the target project.

## Provenance
Protected base geometry comes from the user's local Chess_Arena_Source.blend. New decorations are original procedural Blender geometry. No downloaded models, texture libraries or paid generation services were used. Prior board files remain unchanged.

## Scripts
build_midautumn_arena.py contains construction helpers and thematic groups; presentation.py prepares lights/cameras; validate_export.py performs validation and FBX export; render.py renders both previews. The saved .blend is the verified, authoritative editable scene.
