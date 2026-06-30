# Spring WebSocket pause contract

The authoritative `/ws/chess` backend should add the following client events to the existing envelope format.

```json
{"type":"PAUSE","requestId":"pause-uuid","payload":{"paused":true}}
{"type":"RESUME","requestId":"resume-uuid","payload":{"paused":false}}
```

After validating that the authenticated user belongs to an active match, the server broadcasts one of:

```json
{"type":"PLAYER_PAUSED","payload":{"userId":"uuid","username":"player","paused":true}}
{"type":"PLAYER_RESUMED","payload":{"userId":"uuid","username":"player","paused":false}}
```

Server requirements:

1. Keep a thread-safe set of paused user IDs per active match/room.
2. Make PAUSE and RESUME idempotent.
3. Reject MOVE with an error such as `MATCH_PAUSED` while the set is non-empty.
4. Clear the set on GAME_OVER and room cleanup.
5. On reconnect or SYNC_REQUEST, send each current pause state as `PAUSE_STATE` so the UI recovers correctly.
6. Do not stop the WebSocket heartbeat or server clock by changing Unity `Time.timeScale`; pause is an authoritative match state.

The Unity client already sends `PAUSE`/`RESUME` and accepts `PLAYER_PAUSED`, `PLAYER_RESUMED`, `PAUSE_STATE`, and `PAUSE_STATE_CHANGED`.
