# Match Sharing & Chess Data Export

Implemented 16 November 2026. No scene or Inspector wiring is required.

## Player experience

The existing post-match result screen now offers **Share / Export**. The finished-board Share control opens the same modal. Existing rematch, lobby, reward and matchmaking ownership is unchanged.

- Preview a 1200 × 720 branded result card with player names, outcome, mode, available termination reason and a final-board representation.
- Save the card as PNG with **Save Image**, or copy actual image data with **Copy Image**. Copying does not create a file in Exports or replace the image with a text summary.
- **Refresh** rebuilds the preview and revalidates chess data, including recovery from a missing image or stale validation failure.
- Copy a plain-text result summary, with clipboard confirmation.
- Copy or save validated PGN/FEN in compatible Classic sessions.
- Open the export folder on desktop. Files have unique UTC timestamps and suffixes and never overwrite existing files.
- Keyboard navigation stays within the modal. Escape closes it without opening the pause menu. Busy actions reject repeated clicks.
- Copy/save completion and failures also use the existing notification banner, with scrollable details for long file paths.

The card uses the existing SketchbookUI palette, Schoolbell font, paper surfaces and drawn borders. A scoped fallback generated from the already bundled Liberation Sans preserves Vietnamese player names when Schoolbell lacks a glyph; the primary font assets and in-progress typography work are untouched. Board pieces use labeled light/dark tokens, avoiding new textures or font-dependent chess symbols. Long names wrap/shrink/ellipsize. Missing boards receive an explicit fallback; unavailable statistics are omitted. The card camera renders only its own distant, isolated canvas and stops after preview generation. Closing releases its render texture, scoped fallback font and objects. Disk writes and notation generation run off the Unity main thread.

Desktop output: `Documents/Chess but Weird/Exports`. Other filesystem targets use `Application.persistentDataPath/Exports`. No new runtime packages are required.

## Mode availability

| Actual mode / internal identifier | Result card and summary | PGN / FEN |
|---|---|---|
| Local Classic, including Black-first orientation | Available | Available with valid authoritative data |
| Bot Practice / Challenge (`PveBotRush`) | Available | Available; Practice uses its existing accepted branch after undo |
| Classic Normal / Ranked (`ClassicNormal`, `ClassicRanked`; backend `Classic`) | Available | Available after data validation; PGN requires complete recovered moves |
| Classic custom rooms (`CustomCreateRoom`, `CustomBrowserRoom`; backend `Classic`) | Available | Same Classic data requirements |
| Chaos local / Normal / Ranked / custom (`ChaosNormal`, `ChaosRanked`; backend `Chaos` or legacy `Aram`) | Available | Hidden and rejected at service level |
| Unknown or unsupported rules / legacy state without an authoritative owner | Available when completed | Rejected |

`MatchExportPolicy` is the shared policy. A Chaos flag, Chaos payload or backend Chaos/Aram identifier blocks notation even when every visible move looks legal. Chaos never instantiates notation buttons or their shadows. The legacy public `ChessGame.ExportFen()` also rejects Chaos.

## Data and validation

Local notation uses the existing ClassicMatchCoordinator's initial snapshot, accepted canonical commands and final snapshot. Pending promotion choices do not enter its journal. Board orientation is mapped to canonical coordinates. Practice reuses its existing accepted command history; no new engine or replay functionality was added.

Online FEN comes from the server's six-field FEN, checked against its authoritative piece list and turn. PGN uses the existing paginated move-record recovery. Duplicate/wrong-match records, unsupported abilities, invalid commands, incomplete history and final-state divergence are rejected. A middle-game FEN import cannot masquerade as a complete original match.

PGN generates the seven required tags, escapes names, uses unknown markers for unavailable metadata, supports custom roots through SetUp/FEN tags, and writes SAN with legal-move disambiguation, castling, captures, checks, mate, en passant and all four promotions. Reapplying every recorded command must reproduce all six final FEN fields exactly. FEN validates grammar, kings, pawn ranks, material/promotion feasibility, side-to-move legality, castling rights, en passant evidence and counters. A valid FEN can remain available when PGN history is incomplete.

Implementation follows the [PGN/FEN specification](https://www.saremba.de/chessgml/standards/pgn/pgn-complete.htm), sections 16.1.3–16.1.4. Generated FEN has six space-separated fields: piece placement, active color, castling rights, en passant target, halfmove clock and fullmove number. Ranks run from 8 to 1, files from a to h; piece symbols are ASCII, White uppercase and Black lowercase. Castling rights use `KQkq` order or `-`. The halfmove clock resets after a pawn move or capture; the fullmove number starts at 1 and increments after Black.

The en passant field records the passed square after every two-square pawn advance, even when no capture is available. For example, after `1. e4` the final four fields are `KQkq e3 0 1`. This is distinct from the legal-capture-only identity used for repetition. The domain FEN writer already implements the specified structure; the export validator now additionally rejects Unicode lookalikes that could otherwise normalize into a piece symbol. Validation detects the supported structural and legality errors described above; it is not an exhaustive proof that an arbitrary imported position has a reachable game history.

On Windows, Copy Image publishes the original PNG plus a standard `CF_DIB` bitmap, following [Windows clipboard formats](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-formats). It allocates both payloads before replacing the clipboard, retries briefly when another application holds it, transfers native memory ownership correctly, and reports success only after publication. On Android it uses a read-only content URI in `ClipData.newUri`, following [Android clipboard guidance](https://developer.android.com/develop/ui/views/touch-and-input/copy-paste). The provider retains its private cached image for receiving applications; no storage permission is needed.

## Generated examples

These are actual test outputs, with fixture names and date:

```pgn
[Event "Chess but Weird"]
[Site "?"]
[Date "2026.11.16"]
[Round "?"]
[White "Alice"]
[Black "Bob"]
[Result "0-1"]

1. f3 e5 2. g4 Qh4# 0-1
```

Corresponding final FEN:

```text
rnb1kbnr/pppp1ppp/8/4p3/6Pq/5P2/PPPPP2P/RNBQKBNR w KQkq - 1 3
```

## Files

Modified existing integration points:

- `Assets/_Project/Scripts/Chess/Application/ClassicMatchCoordinator.cs`: retain accepted export commands and initial state.
- `Assets/_Project/Scripts/Chess/Core/ChessGame.cs`: initialize export metadata and guard the existing public FEN entry point.
- `Assets/_Project/Scripts/Chess/Core/ChessGame.Online.cs`: connect existing move-record recovery to export validation.
- `Assets/_Project/Scripts/Chess/Core/ChessGame.Statistics.cs`: tolerate missing online mode metadata; resolve bot participants from the current match instead of cached online-menu labels.
- `Assets/_Project/Scripts/Chess/Gameplay/UI/MatchResultSharing.cs`: extend the pre-existing text-sharing helper to every completed mode and reliable statistics.
- `Assets/_Project/Scripts/Chess/Gameplay/UI/ResultMenuView.cs`: open Share / Export from existing result and finished-board controls.
- `Assets/_Project/Scripts/Chess/Gameplay/UI/ChessPauseMenu.cs`: keep Escape handling scoped to the open sharing modal.
- `Assets/_Project/Scripts/Chess/UI/ChessFontCatalog.cs` and `Assets/Resources/Chess/ChessFontCatalog.asset`: reference the existing bundled font for scoped result-card fallback.
- `Tests/Chess.Multiplayer.Headless/PostMatchAudit.cs`: update the previous Chaos-excluded sharing expectations.

New runtime files, with Unity metadata:

- `Assets/_Project/Scripts/Chess/Application/MatchExportService.cs`
- `Assets/_Project/Scripts/Chess/Application/StandardChessNotation.cs`
- `Assets/_Project/Scripts/Chess/Core/ChessGame.Exports.cs`
- `Assets/_Project/Scripts/Chess/Gameplay/UI/MatchResultCardRenderer.cs`
- `Assets/_Project/Scripts/Chess/Gameplay/UI/MatchShareExportView.cs`
- `Assets/_Project/Scripts/Chess/Gameplay/UI/MatchExportFiles.cs`
- `Assets/_Project/Scripts/Chess/Gameplay/UI/MatchImageClipboard.cs`
- `Assets/Plugins/Android/ChessResultShare.androidlib/`: manifest, Java PNG provider, library properties and JNI keep rule.

New verification files:

- `Tests/Chess.Export.Tests/Chess.Export.Tests.csproj`, `Program.cs`, `MatchSharingUnityAudit.cs`, `MatchSharingPlayModeAudit.cs`, `MatchClipboardAudit.cs`
- `tools/verify-chess-exports.py`
- `tools/verify-match-sharing.ps1`

Pre-existing unrelated uncommitted work was preserved.

## Verification performed

- Unity Editor, standalone player and Editor-tools C# compilation passed against Unity 6000.4.7f1.
- Existing domain suite: **91 checks passed**, including rule, promotion, session and result-recorder regression checks.
- Export suite: **143 checks passed**, including exact standard FEN examples, move counters, castling-right changes, malformed ASCII/Unicode input, ordinary games, both castles, promotion/underpromotion, en passant, mate, draw, resignation/timeout scores, disambiguation, escaped/unknown metadata, invalid/incomplete records, custom starts, Black-first orientation and Chaos bypass attempts through both backend and menu mode identifiers.
- Independent python-chess 1.11.2 verification: **57 generated games / 1,998 plies / 2,055 six-field FEN positions** matched exactly, including every intermediate position. Written SAN matched independently generated canonical SAN and commands matched the recorded history. This dependency is confined to ignored test output and is not a game dependency.
- Isolated Unity graphics audit: **865 assertions passed** for actual card PNGs, PNG persistence, summary/PGN/FEN clipboard actions, PGN/FEN saving, actual Windows PNG/bitmap clipboard readback, renamed actions, busy-action guards, recovered and truncated online records, contradictory snapshots, Chaos UI/service restrictions, missing metadata, long and Vietnamese names, four aspect ratios, both built-in and URP rendering, hidden-menu canvas ownership and stale bot labels. Generated images were visually inspected.
- Isolated component-level Play Mode audit: **95 assertions passed** using production result-button callbacks, preview coroutines, EventSystem raycasts, clipboard operations, file writes and notification banners. Covered Pip, switching sides/bots/results, stale clipboard replacement, paused time, duplicate clicks, Chaos rejection, clipboard contention, Refresh recovery and closing during an asynchronous action. Clipboard readback compares the PNG bytes and every bitmap pixel, including row orientation and color channels. This is code-driven integration coverage; it does not load the normal game scene, start Stockfish, authenticate or contact a server.
- Existing isolated multiplayer/post-match audit passed its 10 grouped checks: completion/menu reset, result navigation, sharing visibility, finished-board exit, next-match reset, retry states, request classification and presentation regression.
- Android clipboard C# branch and Java provider compiled successfully against the installed Unity references and Android 35 SDK with Java 8 target compatibility. No Android device or APK test was performed.
- `git diff --check` passed.

Reproduce from the project root in PowerShell:

```powershell
dotnet run --project Tests/Chess.Export.Tests/Chess.Export.Tests.csproj
dotnet run --project Tests/Chess.Domain.Tests/Chess.Domain.Tests.csproj
./tools/verify-loading-compile.ps1 -UnityPath 'D:/Unity Editor/6000.4.7f1/Editor/Unity.exe'
./tools/verify-multiplayer-headless.ps1
# Once audit-result.txt exists in the newly printed isolated fixture:
./tools/verify-match-sharing.ps1
./tools/verify-match-sharing.ps1 -PlayMode

# Optional independent parser (test-only installation):
py -m pip install --target Logs/match-export-python python-chess
$env:PYTHONPATH = (Resolve-Path Logs/match-export-python).Path
py tools/verify-chess-exports.py
```

Outputs: `Logs/match-export-tests/`, `Logs/match-export-tests.log`, `Logs/match-export-compile.log`, and the printed isolated fixture's `sharing-audit-result.txt`, `sharing-editor.log`, `sharing-playmode-result.txt`, `sharing-playmode-editor.log`, `captures/` and `audit-result.txt`.

## Follow-up fixes after the Pip report

- Reproduced a failed canvas-ownership assertion against the original implementation. `MatchShareExportView` lived on the same object as the main-menu Canvas; parenting the modal under that object inherited the hidden menu's canvas hierarchy. Its nominal sorting order did not give it an independent root above the result screen. The modal now attaches directly to the ChessGame hierarchy, like the existing result canvas. Tests assert root ownership and confirm that its buttons win EventSystem raycasts over the result screen.
- Bot names now take precedence over cached menu player labels. The human display name is captured when `BeginBotGame` starts; the opponent comes from the selected difficulty's actual bot profile. Summary, card, result UI and PGN all use that source. Tests seed stale Alice/BBBB labels and verify Pip for both player colors.
- Completed copy/save operations and failures show the existing notification banner as well as inline feedback. Modal-opening and background notation-validation failures are handled explicitly.
- The verification harness now restores the Windows clipboard **before** requesting Editor exit, including failed runs. Alice/BBBB are test fixtures only; no runtime sample match is included. A stale clipboard must be replaced by a real successful action, not treated as proof that an export happened.
- E2E verification in the normal game remains the user's responsibility, as requested. No further computer-use interaction was performed after the user stopped it.

## Manual verification and limitations

- No manual Unity scene/Inspector configuration is needed. Let Unity import the added scripts and Android library.
- Live authenticated matchmaking, server reward delivery and a real rematch handshake were not exercised. Their code paths were not rewritten; focused local regression checks passed. Play a completed Classic and Chaos session in the normal project to verify the complete production flow.
- Native Windows image copying is verified through real clipboard readback. Android image copying is implemented and compiled, but requires Unity Android Build Support and a device build to verify the merged provider manifest, receiving app and JNI call. Copy Image is disabled on other platforms; Save Image remains available. WebGL downloads are not implemented.
- Current Android builds have minification disabled. If enabling R8 later, include the packaged `proguard.txt` JNI keep rule in the build's consumer/user rules.
- The current online DTO does not provide custom initial-position metadata. Nonstandard online starts reject PGN when reconstruction from the known Classic root fails; a valid authoritative final FEN can still be exported. Local custom roots with complete commands are supported.
- Old/local matches whose original accepted commands were never retained cannot acquire a reconstructed history retroactively. Missing/inconsistent data produces an explanation and disables the affected notation action.
- Clipboard success means the operating system accepted the image. Pasting into a particular external application is part of manual E2E verification.

No analyzer, evaluation, new engine feature, game-review/replay system, Chaos notation/JSON export, buff mechanic or backend service was added.
