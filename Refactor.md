# Project Folder Refactor

This note records the Unity asset-folder cleanup so contributors can find the current locations and understand the Git changes.

## New layout

Project-owned Unity content is grouped under `Assets/_Project/`:

```text
Assets/_Project/
├── Art/
│   ├── Fonts/
│   ├── Shaders/
│   ├── Skins/
│   ├── Skyboxes/
│   └── UI/                 # Former Assets/Materials
├── Audio/                  # Music, SFX, and packaged audio
├── Cinematics/
│   ├── Demo/               # Former Assets/CinematicDemo
│   ├── Integration/        # Former Assets/CinematicIntegration
│   └── Proof/              # Former Assets/CinematicProof
├── Config/
├── Game/                   # Data, materials, and scenes
├── Scripts/
└── Settings/
```

The moves preserve the existing assets and their `.meta` files. Folder GUIDs therefore remain stable, so Unity scene, prefab, material, and Addressables references continue to identify the same assets. Text-based path references in runtime/editor code, tools, project files, and documentation were updated to the new locations.

## Folders intentionally kept at `Assets/` root

Unity-recognized or runtime-sensitive folders remain where Unity and the project expect them:

- `AddressableAssetsData/` — Addressables settings and groups.
- `Editor/`, `Tests/`, and `Plugins/` — Unity special-purpose folders.
- `Resources/` — only assets loaded by `Resources.Load` must remain under a Resources folder.
- `StreamingAssets/` — includes the Stockfish engine files that must be copied without Unity asset processing.
- `TextMesh Pro/` — third-party assets remain in place.
- `_Recovery/` — Unity/editor-specific local content.

`legacy/` at the repository root remains a quarantine for historical or unused material. Unity does not import it.

`Assets/Mirror/` and `Assets/ScriptTemplates/` were moved to `legacy/Unity/Assets/` after checking the project scripts, package manifest, scenes, prefabs, and serialized asset references. No Mirror API references or serialized Mirror component GUIDs were found. The templates in `ScriptTemplates/` were Mirror-specific custom templates, not Unity's built-in templates. The archived copies and `.meta` files are retained for restoration.

### Resources assets intentionally retained

These image folders contain UI artwork, but their current runtime loaders depend on their Resources paths, so they were not moved during this folder-only refactor:

- `Assets/Resources/Main_Menu/` — `MainMenuLogoOriginal` and the cropped menu artwork are loaded through `Resources.Load` by the menu presentation/asset loader; `MainMenuCredits.txt` is loaded by `MainMenuPresentation`.
- `Assets/Resources/Chess/Bots/Avatars/` — bot profiles build paths such as `Chess/Bots/Avatars/pip`, and `BotAvatarCatalog` loads the matching texture with `Resources.Load` for bot roster/companion UI.

Moving these files into `Assets/_Project/Art/` requires migrating those loaders to Addressables or explicit asset references first. Moving the PNGs alone would break the corresponding UI at runtime.

## Git ignore updates

The root `.gitignore` excludes Unity-generated caches and local outputs such as `Library/`, `Temp/`, `Logs/`, `UserSettings/`, build output, Addressables build data, crash reports, and `ExportedObj/`. These folders can be recreated locally and should not be committed. Project assets under `Assets/_Project/` remain tracked.

## Contributor notes

- Open the repository root as the Unity project; do not open `Assets/_Project/` as a separate project.
- When adding project-owned runtime content, place it under the matching `Assets/_Project/` category. Keep assets that rely on `Resources.Load` under `Assets/Resources/`.
- Preserve `.meta` files when moving Unity assets. Never regenerate a moved asset's `.meta` unless intentionally changing its GUID.
- `Assets/_Project/Cinematics/` contains demo, integration, and proof content. Do not add demo scenes to the normal build settings unless the game explicitly requires them.
- This refactor did not delete moved assets. Mirror files were archived outside `Assets/` so Unity no longer imports the unused networking library and its templates.
