const net = require("net");

const DEFAULT_PORT = 19848;
const port = Number(process.env.PORT || process.argv[2] || DEFAULT_PORT);
const rooms = new Map();
const clients = new Set();

function sanitize(value, fallback = "") {
  return String(value || fallback)
    .replace(/[\r\n|]/g, " ")
    .trim()
    .slice(0, 64);
}

function makeRoomCode() {
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  for (let attempt = 0; attempt < 128; attempt += 1) {
    let code = "";
    for (let i = 0; i < 5; i += 1) {
      code += alphabet[Math.floor(Math.random() * alphabet.length)];
    }

    if (!rooms.has(code)) {
      return code;
    }
  }

  throw new Error("Unable to allocate a room code");
}

function send(client, line) {
  if (!client.socket.destroyed) {
    client.socket.write(`${line}\n`);
  }
}

function getPeer(client) {
  if (!client.roomCode) {
    return null;
  }

  const room = rooms.get(client.roomCode);
  if (!room) {
    return null;
  }

  return room.host === client ? room.guest : room.host;
}

function leaveRoom(client, reason = "Online peer left the room.") {
  if (!client.roomCode) {
    return;
  }

  const room = rooms.get(client.roomCode);
  client.roomCode = "";
  client.role = "None";

  if (!room) {
    return;
  }

  const peer = room.host === client ? room.guest : room.host;
  room.pausedPlayers.delete(client.id);
  rooms.delete(room.code);

  if (peer) {
    peer.roomCode = "";
    peer.role = "None";
    send(peer, `PEERLEFT|${sanitize(reason, "Online peer left the room.")}`);
  }

  console.log(`[room ${room.code}] closed`);
}

function createRoom(client) {
  leaveRoom(client);

  const code = makeRoomCode();
  rooms.set(code, {
    code,
    host: client,
    guest: null,
    pausedPlayers: new Set(),
    createdAt: Date.now(),
  });

  client.roomCode = code;
  client.role = "Host";
  send(client, `ROOM|${code}|Host`);
  console.log(`[room ${code}] created by ${client.name}`);
}

function joinRoom(client, requestedCode) {
  leaveRoom(client);

  const code = sanitize(requestedCode).toUpperCase();
  const room = rooms.get(code);
  if (!room) {
    send(client, `ERROR|Room ${code || "(empty)"} does not exist.`);
    return;
  }

  if (room.guest && room.guest !== client) {
    send(client, `ERROR|Room ${code} is already full.`);
    return;
  }

  if (room.host === client) {
    send(client, `ERROR|You are already hosting room ${code}.`);
    return;
  }

  room.guest = client;
  client.roomCode = code;
  client.role = "Client";

  send(client, `ROOM|${code}|Client`);
  send(client, `PEER|${sanitize(room.host.name, "Host")}`);
  send(room.host, `PEER|${sanitize(client.name, "Client")}`);
  console.log(`[room ${code}] ${client.name} joined ${room.host.name}`);
}

function relayToPeer(client, command, payload) {
  const peer = getPeer(client);
  if (!peer) {
    send(client, "ERROR|No peer is connected to this room.");
    return;
  }

  send(peer, `${command}|${payload || ""}`);
}

function setPaused(client, paused) {
  const room = rooms.get(client.roomCode);
  const peer = getPeer(client);
  if (!room || !peer) {
    send(client, "ERROR|No peer is connected to this room.");
    return;
  }

  if (paused) {
    room.pausedPlayers.add(client.id);
    send(peer, `PLAYER_PAUSED|${sanitize(client.name, "Opponent")}`);
  } else {
    room.pausedPlayers.delete(client.id);
    send(peer, `PLAYER_RESUMED|${sanitize(client.name, "Opponent")}`);
  }

  send(client, `PAUSE_STATE|${paused ? "PAUSED" : "RESUMED"}`);
}

function resign(client) {
  const room = rooms.get(client.roomCode);
  const peer = getPeer(client);
  if (!room || !peer) {
    send(client, "ERROR|No active opponent to surrender to.");
    return;
  }

  send(client, "SURRENDERED|You surrendered the match.");
  send(peer, `OPPONENT_SURRENDERED|${sanitize(client.name, "Opponent")}`);
  client.roomCode = "";
  client.role = "None";
  peer.roomCode = "";
  peer.role = "None";
  room.pausedPlayers.clear();
  rooms.delete(room.code);
  console.log(`[room ${room.code}] ${client.name} surrendered`);
}

function handleLine(client, line) {
  const trimmed = line.trim();
  if (!trimmed) {
    return;
  }

  const separatorIndex = trimmed.indexOf("|");
  const command = (separatorIndex >= 0 ? trimmed.slice(0, separatorIndex) : trimmed).toUpperCase();
  const payload = separatorIndex >= 0 ? trimmed.slice(separatorIndex + 1) : "";

  switch (command) {
    case "HELLO":
      client.name = sanitize(payload, `Player-${client.id}`);
      send(client, `INFO|Hello ${client.name}.`);
      break;

    case "PING":
      send(client, `PONG|${payload}`);
      break;

    case "CREATE":
      createRoom(client);
      break;

    case "JOIN":
      joinRoom(client, payload);
      break;

    case "START":
      if (client.role !== "Host") {
        send(client, "ERROR|Only the room host can start the match.");
        break;
      }

      relayToPeer(client, "START", sanitize(payload, "White"));
      break;

    case "MOVE":
      if (client.roomCode && rooms.get(client.roomCode)?.pausedPlayers.size > 0) {
        send(client, "ERROR|The match is paused.");
        break;
      }
      relayToPeer(client, "MOVE", payload);
      break;

    case "PAUSE":
      setPaused(client, true);
      break;

    case "RESUME":
      setPaused(client, false);
      break;

    case "RESIGN":
      resign(client);
      break;

    case "LEAVE":
      leaveRoom(client);
      send(client, "INFO|Left room.");
      break;

    default:
      send(client, `ERROR|Unknown command: ${command}`);
      break;
  }
}

let nextClientId = 1;
const server = net.createServer((socket) => {
  const client = {
    id: nextClientId,
    socket,
    name: `Player-${nextClientId}`,
    buffer: "",
    roomCode: "",
    role: "None",
  };
  nextClientId += 1;

  clients.add(client);
  socket.setEncoding("utf8");
  socket.setNoDelay(true);
  send(client, "INFO|Connected to Chess but Weird online relay.");
  console.log(`[client ${client.id}] connected from ${socket.remoteAddress}:${socket.remotePort}`);

  socket.on("data", (chunk) => {
    client.buffer += chunk;
    let newlineIndex = client.buffer.indexOf("\n");
    while (newlineIndex >= 0) {
      const line = client.buffer.slice(0, newlineIndex);
      client.buffer = client.buffer.slice(newlineIndex + 1);
      handleLine(client, line);
      newlineIndex = client.buffer.indexOf("\n");
    }
  });

  socket.on("close", () => {
    leaveRoom(client, "Online peer disconnected.");
    clients.delete(client);
    console.log(`[client ${client.id}] disconnected`);
  });

  socket.on("error", (error) => {
    console.warn(`[client ${client.id}] socket error: ${error.message}`);
  });
});

server.listen(port, "0.0.0.0", () => {
  console.log(`Chess but Weird online relay listening on 0.0.0.0:${port}`);
});

process.on("SIGINT", () => {
  console.log("Shutting down online relay...");
  for (const client of clients) {
    send(client, "PEERLEFT|Server shutting down.");
    client.socket.destroy();
  }

  server.close(() => process.exit(0));
});
