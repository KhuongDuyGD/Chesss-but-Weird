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
- `Mirror/` and `TextMesh Pro/` — third-party package assets were not reorganized.
- `ScriptTemplates/` and `_Recovery/` — Unity/editor-specific content.

`legacy/` at the repository root remains a quarantine for historical or unused material. Unity does not import it.

## Git ignore updates

The root `.gitignore` excludes Unity-generated caches and local outputs such as `Library/`, `Temp/`, `Logs/`, `UserSettings/`, build output, Addressables build data, crash reports, and `ExportedObj/`. These folders can be recreated locally and should not be committed. Project assets under `Assets/_Project/` remain tracked.

## Contributor notes

- Open the repository root as the Unity project; do not open `Assets/_Project/` as a separate project.
- When adding project-owned runtime content, place it under the matching `Assets/_Project/` category. Keep assets that rely on `Resources.Load` under `Assets/Resources/`.
- Preserve `.meta` files when moving Unity assets. Never regenerate a moved asset's `.meta` unless intentionally changing its GUID.
- `Assets/_Project/Cinematics/` contains demo, integration, and proof content. Do not add demo scenes to the normal build settings unless the game explicitly requires them.
- This refactor was a path/organization change. It did not delete the assets moved into `Assets/_Project/`.
