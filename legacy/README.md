# Legacy archive

This folder holds items moved out of active project paths after a reference check. It is outside Unity's `Assets/` tree, so Unity does not import these files. Their original `.meta` files are kept beside archived Unity assets for possible restoration.

## Archived items

- `Unity/Assets/Scenes/GachaRevealDemo.unity` — not listed in Build Settings and had no project GUID or path references.
- `Unity/Assets/_Project/Audio/Music/` — six old tracks had no serialized GUID or source path references. The empty `Assets/_Project/Audio/Music/` folder remains because the audio fallback code still checks that path for other optional sound names.
- `Unity/Assets/Scenes.meta` and `Unity/Assets/Resources/MainMenu.meta` — metadata for old folders after active scenes/resources were consolidated under `Assets/_Project/Game/Scenes/` and `Assets/Resources/Main_Menu/`.
- `References/Main_Menu_contact_sheet.png` — design contact sheet with no runtime or documentation link found.
- `Notes/MirrorExamplesPipelineConverted.txt` — one-line conversion marker, not referenced by project files.

Review this archive before deleting it. Restore a Unity asset by moving both the asset and its `.meta` file back to their original `Assets/` path.
