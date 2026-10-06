# Unity online client for the .NET backend

The Classic and ARAM online menus now use the REST and SignalR contract in
`D:/Stuurdy/Chess_But_Weird_BE/ChessGame.Api/Online/ONLINE_CONTRACT.md`.
REST/SignalR protocol version is 1. Online ARAM rules version is 2 (26 buffs).
The newer local practice mode has 35 buffs and continues to use its own runtime.

## Legacy backend cleanup (2026-10-06)

The old Spring REST/raw WebSocket clients, auth token store, DTOs, statistics
snapshot adapter and lobby/match session classes have been removed. The Node
relay and ARAM-V1 reference under `OnlineServer` and the old backend analysis
and multiplayer proposal have also been removed.

`AramBuffRuntime` and `AramBuffDraftView` now handle local practice only. Their
old six-buff online branches, seeded fallback and wire snapshots were removed.
Online ARAM uses `OnlineAramPresenter` and backend snapshots. The shared local
and bot move value is now `ChessMove`; its old CSV protocol helpers are gone.

Existing scenes keep the `ChessLanController` component and script GUID. Its
network flow uses the .NET client. Removed script GUIDs were checked against
scene, prefab and asset references. Editor checks and standalone fixtures no
longer depend on the removed contracts. Historical architecture/statistics
notes are explicitly marked as historical.

After cleanup, Domain, Editor runtime, Windows player runtime and Editor tools
compiled with the installed Unity Roslyn compiler with no diagnostics. This
is a source compilation check; live integration and gameplay remain manual.

## Configuration and entry points

- Set the backend base URL in `Assets/Scripts/Network/ApiConfig.cs`. Both HTTP
  and the `/hubs/game` WebSocket derive from this one URL. Its existing default
  is `https://chessgame-api-fn6r.onrender.com`.
- Sign in using the existing .NET auth UI. Guest accounts cannot play online.
- **Online** opens two choices: **Rooms** and **Matchmaking**.
- **Rooms** opens **Create Room** or **Find Room**. Create Room immediately
  creates an eight-character code. Find Room lists open rooms with pagination,
  refresh and a code input; click a room or enter a code to join. Full rooms
  appear disabled. The owner can change clock presets and start with two players.
- **Matchmaking** opens loadout selection for owned chess skins and boards.
  Selecting an available item saves it using InventoryService before Play.
  Items without local art remain visible but cannot be selected here.
- **Play** queues a ticket and opens **Finding opponent**, with elapsed time
  and Cancel Search. MatchFound opens **Accept** with a server deadline of
  30 seconds. Each player must Accept; the first waits for the second.
- Expiry or Decline cancels the pending match on the backend, releases both
  seats and returns to loadout. It does not automatically requeue. A fresh Play
  uses a fresh requestId. Search expiry also returns to loadout.
- After both Accept (or a private room starts), the client loads the board
  and sends **SetReady** automatically. Ready/setup has a separate 180-second
  window. ARAM players still finish draft/setup before the clock starts.
- Legacy LAN entry points use this same backend room/menu flow.
- Online HUD actions: offer/respond to a draw, Classic draw claim, resign,
  rematch, and recovery of an uncertain previous command. The result screen's
  **New Game** button requests a rematch; both players can press it to agree.
  **Spectate** reveals the online HUD with accept/decline controls.

## Implementation map

- `OnlineDtos.cs`: camelCase REST/hub contract, including stable piece IDs,
  frozen loadouts, snapshots, command acknowledgments, rewards and pagination.
- `OnlineRestService.cs`: all 17 online REST endpoints. Buttons use services;
  they do not construct HTTP requests.
- `SignalRGameTransport.cs`: native `ClientWebSocket`, bearer authentication,
  SignalR JSON handshake/framing, invocation completions, pings and watchdog.
  It follows Microsoft's [Hub Protocol](https://github.com/dotnet/aspnetcore/blob/main/src/SignalR/docs/specs/HubProtocol.md)
  and [Transport Protocols](https://github.com/dotnet/aspnetcore/blob/main/src/SignalR/docs/specs/TransportProtocols.md).
- `OnlineSession.cs`: queue/room lifecycle, snapshot watermark, duplicate
  suppression, buffered events, full snapshot recovery on sequence gaps,
  serialized commands and uncertain-command retries with the original
  command ID, arguments and expected version.
- `ChessLanController.Online.cs`: lobby, readiness, connection recovery,
  result/profile refresh and server command history. The original component
  GUID remains in `ChessLanController.cs`.
- `ChessLanController.Menu.cs`: full paper-style screens for Online choices,
  room creation/list/code entry, room lobby, loadout, searching and Accept.
  It uses SketchbookUI and MenuDesignFrame for shared styling and safe-area fit.
- `ChessLanController.Flow.cs`: navigation, paginated room data, inventory
  selection, elapsed search time, acceptance countdown and automatic readiness.
- `ChessGame.Online.cs`: canonical board coordinates, server piece IDs,
  snapshot presentation, advisory move highlights, promotion commands and
  canonical ARAM hitboxes. Online moves do not mutate the board optimistically.
- `OnlineAramPresenter.cs`: all active v2 commands, draft/target selection,
  complete formation submission, reinforcements, escape, public effects,
  mines/crates/collapsed files, and rifle yaw/pitch camera controls.
- `link.xml`: preserve reflection-based online DTO serialization in IL2CPP.

## Authority and recovery

The backend owns legality, RNG, clocks, outcomes, rewards and Elo. The client
uses snapshots to create/remove/promote/change the side of pieces. It does not
start the local ARAM runtime for online play. Move highlights are advisory;
the server validates every move and ability.

REST and SignalR share `ApiClient`'s refresh gate. The hub reconnects with the
current JWT when disconnected or near expiry. Input remains locked during
recovery or while a command's outcome is uncertain. On timeout, use **Recover
previous command**; reconnect also retries the same command automatically.
Sequence gaps are resolved by another full `SubscribeMatch` snapshot.
The ID of an open room is saved per account so it can be recovered after a
client restart. Started/closed rooms are removed from that saved reference.

Skins are loaded from each player's server loadout and frozen for the match.
The fixed board camera faces from the local player's server-assigned color:
White views from the White edge, Black from the Black edge (180 degrees around
the board). Each player's pieces appear at the bottom; turns do not rotate the
camera. Mouse-wheel zoom and the fixed downward pitch remain available.
The local player's board skin is used. Unknown/unavailable local cosmetic
keys fall back to the default visual; this does not change backend equipment.
Rematches load their new loadouts.

The backend room browser is authenticated `GET /api/rooms`, with optional mode
and region filters and page/pageSize parameters. It returns a Page containing
roomId, code, settings, memberCount, capacity and expiresAt; expired, started and
closed rooms are excluded. Membership/capacity is revalidated when joining.

Rated matchmaking snapshots include nullable `acceptDeadline` and
`players[].accepted`. AcceptMatch persists acceptance and emits `MatchAccepted`
with a full versioned snapshot. Both must accept before the deadline to clear
the gate and begin loading/Ready. The worker enforces expiry even if clients
are offline. Private rooms/rematches have no acceptance gate. UI countdowns use
snapshot serverTime plus monotonic elapsed time; the server decides expiry.

The local backend source in `D:/Stuurdy/Chess_But_Weird_BE/ChessGame.Api` now
contains these additions. Deploy/restart the updated API at the configured
base URL before trying this menu against a hosted backend with the old contract.

Finishing an online match does not call the local reward/stat increment path.
The displayed reward is the backend result, and `/api/users/me` refreshes the
profile. Private rooms/rematches receive zero wallet rewards under the current
contract. Combat gacha tickets are separate from wallet tickets.

Local pause does not stop the server clock. Online history includes confirmed
move and ability records. ARAM captures are marked unavailable because the
contract does not provide capture records for every indirect effect. Recovered
duration is marked unavailable because state does not include `startedAt`.

Rifle aiming uses the backend's canonical axes, origin and piece hitboxes.
Hold the right mouse button to aim; left mouse or **Fire** sends only yaw/pitch.
The server determines hit/miss/occlusion/cooldown. **Exit rifle** keeps charge.
Army formation planning is shown as a full ID-to-square list: click a piece,
then a destination; an occupied planned destination swaps both positions.
Confirm submits every owned piece; the official board changes only on approval.

## Platform and verification boundaries

For the 2026-10-06 menu update, Unity Editor runtime, Windows player runtime,
Domain and Editor tools compiled successfully. The .NET API build also passed
with zero errors and warnings. Temporary compilation/patch outputs were removed.
No server, API, database or automated gameplay tests were run.

Manual two-client checks: list/row/code joins; empty/full/expired rooms; owner
start/leave; save loadout; cancel search versus pairing; elapsed timer; both Accept;
one or neither Accept; Decline; disconnect/reconnect during acceptance; repeat
Play after failure; ARAM draft/setup after accepting; correct equipment in-game.

The transport targets native Unity players and uses a direct WebSocket
connection (skip negotiation). WebGL, SSE/long polling and Azure SignalR
redirects are not implemented. No deployment or backend data was changed.

Editor and Standalone Windows player compiler checks passed with zero errors
and zero warnings. These checks use the installed Unity Roslyn compiler, the project's existing
Unity response-file references/defines, and a freshly compiled domain assembly.
Temporary compilation helpers and outputs in `Library/OnlineClientCompile`
were removed during project cleanup after compilation completed.
The configured Unity MCP endpoint points to an old project path, so an editor
recompile through that endpoint is unavailable. Compilation is not a live
two-client, MongoDB, authentication or deployment validation.

## Manual acceptance checks

1. Configure a running .NET API with its required MongoDB replica set. Open two
   native clients and sign in with separate accounts.
2. Classic matchmaking with matching settings: find, cancel, retry, pair,
   ready both clients. Verify opposite colors, legal/illegal moves, castling,
   en passant, promotion and running clocks.
3. Private room: share the eight-character code, join, change clock as owner,
   start, ready both clients; also leave/close before start and cancel search
   while pairing occurs.
4. Disconnect one client during a command; reconnect and recover it. Check
   that no move applies twice, the board matches the other client and input
   stays locked while disconnected. Repeat across token renewal/API restart.
5. Test each of the 26 online ARAM buffs from the backend contract. Exercise
   three-pawn targets, knight/bishop targets, full formation and fallback,
   Snipe/LoadPawn/Recruit/HideKing, combat gacha, Deploy and Escape.
6. Rifle: verify both colors' camera axes, hit/miss, blockers, board occlusion,
   king/queen immunity, expiry, charge/cooldown and reconnect while aiming.
7. End by resignation, timeout and draw. Verify rewards/Elo/profile against
   the backend, private/rematch zero rewards, recent matches and history.
8. Request/accept/decline a rematch. Verify new Ready/draft, new loadouts and
   main-menu return without reopening the previous finished match.

These checks are for manual execution; the agent has not run server/API,
MongoDB or automated gameplay tests.
