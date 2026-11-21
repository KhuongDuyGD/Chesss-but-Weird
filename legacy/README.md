# Legacy archive

This folder holds items moved out of active project paths after a reference check. It is outside Unity's `Assets/` tree, so Unity does not import these files. Their original `.meta` files are kept beside archived Unity assets for possible restoration.

## Archived items

- `Unity/Assets/Scenes/GachaRevealDemo.unity` — not listed in Build Settings and had no project GUID or path references.
- `Unity/Assets/_Project/Audio/Music/` — six old tracks had no serialized GUID or source path references. The empty `Assets/_Project/Audio/Music/` folder remains because the audio fallback code still checks that path for other optional sound names.
- `Unity/Assets/Scenes.meta` and `Unity/Assets/Resources/MainMenu.meta` — metadata for old folders after active scenes/resources were consolidated under `Assets/_Project/Game/Scenes/` and `Assets/Resources/Main_Menu/`.
- `References/Main_Menu_contact_sheet.png` — design contact sheet with no runtime or documentation link found.
- `Notes/MirrorExamplesPipelineConverted.txt` — one-line conversion marker, not referenced by project files.
- `Unity/Assets/Mirror/` — vendored Mirror networking library; no project API references or serialized component GUID references were found, and it is not a manifest dependency.
- `Unity/Assets/ScriptTemplates/` — Mirror-specific custom script templates and their Editor folder mover; not Unity's built-in templates and not used by project code.
- `Unity/Assets/Resources/Main_Menu/black_card.png`, `choose_side_title.png`, `early_access.png`, `white_card.png` — fallback art for menu fields that the current views no longer render; the obsolete loader fields were removed.
- `Unity/Assets/_Project/Art/UI/Main_Menu/` — `GameVersionFake`, `ChooseYourSide`, `WhiteSideButton`, and `BlackSideButton` were only loaded into unused menu fields. `ChooseBotDifficultUIBlank` and the five difficulty buttons belonged to an uncalled bot-difficulty art catalog; its resource asset and catalog script are archived with them.
- `Unity/Assets/_Project/Art/UI/GameplayUI/` — six old Analysis Board UI images, their Resources catalog, catalog script, Editor rebuild tool, shader, and folder metadata. The current `AnalysisBoardView` builds its UI in code and has no runtime caller for that catalog.

Review this archive before deleting it. Restore a Unity asset by moving both the asset and its `.meta` file back to their original `Assets/` path.
