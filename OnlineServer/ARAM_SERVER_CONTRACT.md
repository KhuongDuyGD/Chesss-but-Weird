# ARAM authoritative server contract

The Unity client uses the existing room and WebSocket flow. ARAM rooms carry `gameMode: "ARAM"`; classic rooms carry `gameMode: "CLASSIC"`.

The deployed Spring server is the authority. It must never accept `aramState` supplied by a client. Generate and store the state on the server, update it transactionally with the accepted move, and return it with the canonical FEN.

## REST room fields

- `POST /api/rooms/create`: optional body `{ "gameMode": "ARAM" }`
- `POST /api/rooms/join`: `{ "roomCode": "ABC123", "gameMode": "ARAM" }`
- Room DTO: add `gameMode`
- A join must fail when the requested mode differs from the room mode.

## WebSocket fields

`READY`, `START`, `SYNC_REQUEST`, and `MOVE` include `gameMode`. Reject a message whose mode differs from the stored room/match mode.

`GAME_START` and `GAME_STATE` add:

```json
{
  "gameMode": "ARAM",
  "aramSeed": "server-owned-match-seed",
  "aramState": {
    "version": 1,
    "seed": "server-owned-match-seed",
    "white": { "team": "WHITE", "buff": "CommandantPawn" },
    "black": { "team": "BLACK", "buff": "FlyingThunderGod" }
  }
}
```

`MOVE_RESULT` adds `gameMode` and the updated `aramState`. Send the complete state to both game clients for local move prediction, but the Unity UI deliberately renders only the local player's buff/markers. If stronger secrecy is required, send a private local state plus a server-only opponent state and accept that opponent check prediction remains server-only.

## Validation order

1. Load the match, FEN, ARAM state, turn, and optimistic-lock version in one transaction.
2. Reject room/match mode mismatch, stale version, wrong player, wrong turn, malformed squares, friendly capture, or king capture.
3. Ask the normal chess engine whether the move is legal.
4. Ask `aram-rules.js` whether an ARAM override makes it legal. Doppelganger replaces the selected piece's normal movement; it is not an additional movement set.
5. Simulate the move and reject it if the moving side's king remains in check. Attack detection must include Freestyle Leap and Doppelganger movement. Strong Fortress waives only the in-check/through-check restrictions; castling rights, rook presence, and clear path still apply.
6. Apply promotion/castling/en-passant, ARAM cooldowns, tracked-piece square changes, and Suicide Bomber explosion. Kings and the capturing piece are immune to the explosion, matching Unity.
7. Re-evaluate checkmate/draw using ARAM legal moves, persist FEN + ARAM state + move atomically, then broadcast the canonical result.

The executable reference validator and deterministic `ARAM-V1` setup are in `aram-rules.js`; run `node OnlineServer/aram-rules.test.js` before deploying server changes.
