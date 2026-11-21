# Cosmetic photography

Run from the repository root:

```powershell
./tools/capture-cosmetic-cinematics.ps1 -Install
```

The script creates a disposable Unity project under `Logs/cinematic-capture`, copies the render dependencies, and launches its own hidden batch Editor. It does not open or change the gameplay scene in the user's Editor. Omitting `-Install` keeps the results in the photography project for review.

Piece portraits show all six actual piece types, with examples of both team appearances, on a dedicated dark slate chess plinth. Their face direction, warm key light, colored rim light, contact shadows and depth of field are chosen for small menu cards. Board photographs use different close perspective angles, with the playable surface and characteristic decorations in frame. Environment photographs show the populated arena and its immediate surroundings. The terrain perimeter is outside the shot.

Each photograph produces a native **4096 x 3072** PNG master and a supersampled **2048 x 1536** menu preview. Rendering uses the project's URP materials, high quality shadows, ambient occlusion, HDR reflections, SMAA, restrained bloom and ACES. The photographic plinth and lighting exist only inside the capture project. Gameplay models and their materials are not edited.

Masters and `captures.json` are in the capture project's `Photography` folder. The report records the actual source size, camera position, target, field of view and preview destination. Installation replaces only the existing preview PNG files; their Sprite GUIDs, importer settings, Addressables bindings and catalog references stay intact. Previous menu art is backed up beside the render project.

`CosmeticThumbnailCapture.Verify` can be run in the photography project with `-executeMethod` to check the imported Sprites against the catalog and data assets. Rendering fails on missing visible materials, and the runner refuses installation after a rendering exception or an incomplete capture.
