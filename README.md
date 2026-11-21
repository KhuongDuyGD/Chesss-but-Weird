# Chess but Weird

A Unity chess game with variant rules, online modes, and a shared C# domain package.

## Open the project

Open this folder in Unity Hub with **Unity 6000.4.7f1**. Let Unity resolve the dependencies in `Packages/manifest.json` before opening scenes or scripts. `Packages/packages-lock.json` records the resolved package versions.

## Repository layout

- `Assets/_Project/` — project-owned scripts, game data/scenes, art, audio, settings, and cinematic work.
- `Assets/` — Unity-managed folders, runtime `Resources`, tests, plugins, Addressables data, and third-party assets.
- `Packages/` — Unity package manifest and the embedded `com.chessbutweird.domain` package.
- `Tests/` — domain tests and isolated headless fixtures.
- `Assets/Tests/` — Unity Editor verification scripts and Play Mode tests.
- `tools/` — repeatable PowerShell and Python verification helpers.
- `Documentation/` — project plans, audits, guides, and cosmetics documentation; start at its index.
- `legacy/` — unused or historical items quarantined for later review; not imported by Unity.
- `Refactor.md` — details of the asset-folder reorganization and path updates.

## Local Unity data

`Library/`, `Temp/`, `Logs/`, and `UserSettings/` are generated or machine-specific. They are ignored by Git and should not be committed. Unity recreates generated data as needed.

Start with [the documentation index](Documentation/README.md) and [the architecture plan](Documentation/Architecture/ARCHITECTURE_PLAN.md) for project structure, implementation details, and validation limits.
