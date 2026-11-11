# Multiplayer fixes

Changes cover the Unity project and `D:/Game_Project/ChessButWeirdBE/ChessGame.Api`.

- Return to menu resets terminal match flags even after the additive board scene is unloaded. A finished online session is disposed and its cached presentation/request state is cleared. The result-screen button returns to the play hub (main menu 2).
- Server snapshots include `checkedTeam` for Classic and Chaos. The client uses that state for the king pulse and check SFX. Classic keeps compatibility with older FEN snapshots.
- Committed board changes select move/capture/castling/promotion SFX. Repeated snapshots do not replay move audio.
- Queue requests and tickets carry `rated`, defaulting to false. Normal/ranked use separate pools. Normal games do not update rating, tier, or placement counts. Results/recent matches label the queue type.
- Normal matchmaking skips ranked rating/opponent restrictions. A pool with only two candidates relaxes rating/latency/repeat restrictions after five seconds; settlement retains its anti-farming rules. Settings and queue type must still match.
- Durable event delivery wakes after commit and runs independently of match sweeps. Initial lobby refresh avoids duplicate recovery requests; command acknowledgements avoid redundant snapshots when their push already arrived.
- Joining reuses a matching room reservation, safely clears confirmed terminal reservations, and remains idempotent for simultaneous retries. Known MongoDB reservation conflicts/transient failures return structured 409/503 responses and are logged.

Validation completed:

- Unity editor/player/editor-tools C# compilation passed.
- Isolated Unity audit passed menu reset after win/loss/draw, return to main menu 2, session disposal, SFX selection, unchanged-snapshot suppression, the ARAM check-pulse coroutine, and queue flags. Evidence: `Logs/multiplayer-headless/20261112-000432/audit-result.txt`.
- Existing isolated HUD audit passed 9,913 assertions. Evidence: `Logs/hud-headless/20261111-235950-90258d/audit-result.txt`.
- Backend suite: 477 passed, 7 existing skips. After the final legacy check-projection change, the targeted multiplayer suite passed all 12 cases, including snapshot purity.
- Real isolated MongoDB probe passed Classic/Chaos room lifecycle, concurrent joins, full rooms, four stale-reservation cases, normal/ranked settlement, subsequent queue entry, and durable event delivery. The delivery recorder observed RoomUpdated after 15 ms locally; this is not an Internet latency measurement.

The tester's original production HTTP 500 was not reproduced and no server stack trace was available. Reservation handling and error reporting have been strengthened, but that exact failure still needs confirmation on the deployed backend. These checks do not verify two physical clients, graphics appearance, or audible output on a device.

Changes are source-only. Rebuild the client and deploy the backend together. No existing ratings were rewritten and no production database was accessed.
