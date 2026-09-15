# Halloween Chess Set — Ember / Specter

12 original low-poly chess masters: 6 roles per faction, no duplicate pieces.

- Ember: burnt orange, plum cloak, amber accents.
- Specter: spectral jade, midnight blue cloak, mint accents.
- Pawn: jack-o-lantern. Rook: haunted tower. Knight: skeletal horse.
- Bishop: witch. Queen: bat queen. King: crowned pumpkin monarch.

## Files
- Halloween_Chess_Set.blend: editable meshes, materials and presentation scene.
- Halloween_Chess_Set_12.fbx: 12 meshes in display arrangement; excludes stage/cameras/lights.
- FBX/: 12 individual meshes, each at world origin with bottom-center pivot.
- Halloween_Chess_Set_Preview.png: rendered presentation.
- validation.json: source geometry checks and FBX reimport checks.

## Technical notes
Blender Z-up; individual pieces face -Y, FBX exported Y-up / -Z forward.
Base diameter 0.724 Blender units; heights about 0.98–1.91 units. Scale to your board.
900–2292 triangles per piece. Flat-shaded meshes, baked transforms, no modifiers.
Procedural mesh construction, solid material colors, no external textures or assets.
The pieces contain intersecting closed decorative components; they are not single fused solids for printing.
Emission appearance may need material setup in the destination game engine. No game-engine import test performed.

## Provenance
Created locally in Blender for the user; no paid generation or third-party source assets.
User prompt: Tạo 1 chess set mới theo style halloween
Scope: mỗi bên chỉ cần 6 quân thôi còn các quân trùng lặp thì không cần làm
Negative prompt: none; no image/model generation provider used.
No third-party license selected or required for source assets.
