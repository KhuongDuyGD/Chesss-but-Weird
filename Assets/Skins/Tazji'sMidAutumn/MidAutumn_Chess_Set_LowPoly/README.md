# Trăng Rằm — Low-poly Mid-Autumn Chess Set

12 reusable models: 6 roles for each of two sides, Lantern (red) and Moon (jade).

| Chess role | Festival design | Triangles each |
|---|---|---:|
| Pawn / Tốt | Five-point bamboo star lantern | 3356 |
| Knight / Mã | Vietnamese lion dance toy | 5696 |
| Rook / Xe | Festival drum and beaters | 4800 |
| Bishop / Tượng | Jade rabbit holding a mooncake | 3880 |
| Queen / Hậu | Chị Hằng with crescent, hair ribbons and lantern | 5208 |
| King / Vua | Chú Cuội with wrapped headcloth, moon halo and banyan leaf fan | 4876 |

## Files
- MidAutumn_Chess_Set.blend: source and presentation scene.
- MidAutumn_Chess_Set_12.fbx: all 12 pieces in two rows at floor height.
- FBX/MA_[Lantern|Moon]_[Role].fbx: each piece at world origin.
- MidAutumn_Chess_Set_Preview.png: rendered overview.
- validation.json: geometry and FBX reimport evidence.

## Geometry and use
55,632 triangles total. Each piece is one mesh object made from closed overlapping islands; these are game render meshes, not fused 3D-print solids. Flat shaded triangles, UVs, constant-color Principled materials, no external textures and no live mesh modifiers. Bottom-center pivots, scale 1; authored facing Blender -Y, exported FBX -Z forward / Y up. Bases are about 1.168 Blender units wide; choose uniform scale for the target board. Presentation lights, camera, labels and platforms are excluded from FBX. Six source roles per side can be duplicated to populate a standard 32-piece board.

Geometry and binary FBX headers passed validation. Full FBX was imported back into Blender with 12 meshes, matching triangle totals, materials and UVs. Target engine import/runtime was not tested.

## Provenance
Original procedural Blender geometry created for this request, without downloaded models, paid providers or external texture assets. Display typography uses local Arial; text is not part of the exported pieces. No third-party model license is asserted. Draft v1 and earlier scripts are retained separately for revision history.

## Rebuild
build_midautumn_lowpoly.py defines creation helpers; call build_v2('Lantern') and build_v2('Moon') in a fresh scene. presentation_lowpoly.py lowers the pawn and prepares the two display rows. The saved .blend and FBX files are the verified deliverables.
