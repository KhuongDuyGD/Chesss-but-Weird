# Halloween Chess Arena — Dread Cemetery

A dark cemetery arena with a skull-crowned Gothic gate, dead trees, leaning tombstones, iron spear fences, cobwebs, rotten jack-o-lanterns and spectral green fire.

## Compatibility with Chess_Arena
All coordinates below use the original Blender units:
- Playing grid: 8 x 8. Cell: 1 x 1. Board outer mesh: 8.68 x 8.68.
- Arena floor footprint: 13.4 x 11.7.
- 6 side slots on each side, 12 total.
- Slot outer size: 0.70 x 0.90. Inner surface: 0.59 x 0.79.
- Slot X centers: -4.93 / +4.93.
- Slot Y centers: -3.15, -1.89, -0.63, 0.63, 1.89, 3.15. Pitch: 1.26.
- Board, floor, floor inlays including all side slots, and floating island retain the exact original source vertices and faces.
- Exported FBX protected meshes also match reference vertices within 0.000001 and exactly match face indices.
- FBX version, axes and unit scale metadata match the original FBX.
- Perimeter artwork and maximum height were redesigned as requested.

## Deliverables
- Halloween_Chess_Arena_Source.blend: editable source and lit preview camera.
- Halloween_Chess_Arena.fbx: 8 mesh objects; excludes camera and lights.
- Halloween_Chess_Arena_Preview.png: rendered preview.
- validation.json: geometry and export comparison.

No third-party assets or paid generators used. Base arena is the user's existing Chess_Arena.
No game-engine import test performed. Emission/bloom may require engine material configuration.
Decorative elements contain intersecting closed components and are not intended as a fused print-ready solid.
