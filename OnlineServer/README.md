# Chess but Weird Online Relay

Simple TCP relay server for prototype online multiplayer. The server creates room codes, accepts one host and one client per room, then relays gameplay and pause state between them.

This server does not validate chess rules, users, or anti-cheat. It is intended for internal dev testing.

## How It Differs From LAN

LAN multiplayer connects the two game clients directly by IP address on the same local network.

Online multiplayer connects both game clients to this Node.js relay server. The host creates a room code, the guest joins that room code, and the server forwards `START` and `MOVE` messages between the two clients. The server does not run chess logic.

## Capacity

Each room supports exactly 2 clients: 1 host and 1 guest.

One server process can hold many rooms at the same time. There is no fixed room limit in code; practical capacity depends on the machine, OS socket limits, and network bandwidth. For internal playtesting, dozens of idle or lightly active rooms should be fine on a normal dev machine, but this prototype has no load testing, rate limiting, reconnect, or abuse protection.

## Run

```powershell
node OnlineServer/online-server.js
```

Default port: `19848`

Run on a custom port:

```powershell
node OnlineServer/online-server.js 25000
```

## Unity Flow

1. Start the server on a machine reachable by both players.
2. In game, open `Start > Online > Multiplayer Online`.
3. Host enters the server IP/domain and port, then clicks `Create Room`.
4. Host shares the displayed room code.
5. Client enters the same server IP/domain, port, and room code, then clicks `Join Room`.
6. Host starts the match with `Start As White` or `Start As Black`.

## Pause and surrender protocol

- `PAUSE` marks the sender paused and emits `PLAYER_PAUSED|<name>` to the opponent.
- `RESUME` clears that state and emits `PLAYER_RESUMED|<name>`.
- `MOVE` is rejected while either room member is paused.
- `RESIGN` emits `SURRENDERED` to the loser and `OPPONENT_SURRENDERED|<name>` to the winner, then closes the room.

The current Unity client uses the newer Spring WebSocket backend rather than this legacy TCP protocol. Its equivalent JSON contract is documented in `SPRING_PAUSE_PROTOCOL.md`.

For Internet testing, the server machine must expose the TCP port through firewall/cloud rules. If running on a home network, forward TCP port `19848` from the router to the server machine.
