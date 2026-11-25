# Chess but Weird

A Unity chess game with Classic, Chaos, bot and online modes, and a shared C# domain package.

Open this folder in **Unity 6000.4.7f1** and let Unity resolve `Packages/manifest.json`.
The enabled build scenes, in order, are `Boot`, `MainMenu`, `ChessMatch` and `ChessClassic`
under `Assets/_Project/Game/Scenes/`.

- `Assets/_Project/` contains the current game scripts, data, scenes, art, audio and settings.
- `Assets/Resources/`, `Assets/StreamingAssets/`, `Assets/Plugins/` and `Assets/AddressableAssetsData/` contain runtime resources, Stockfish, plugins and content registrations.
- `Packages/com.chessbutweird.domain/` contains the shared chess rules.
- `Tests/`, `Assets/Tests/` and `tools/` contain regression checks and maintenance tools for current features.
- [Documentation](Documentation/README.md) covers the current online integration, cosmetics and localization.

Historical plans, retired LAN/Mirror assets, unregistered art and cinematic demo work
are archived outside this repository. They are not required to open or run the game.

`Library/`, `Temp/`, `Logs/` and `UserSettings/` are generated or machine-specific and
ignored by Git. Do not clear active Editor caches while Unity is open.
