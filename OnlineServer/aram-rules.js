"use strict";

const BOARD_SIZE = 8;
const BUFFS = [
  "CommandantPawn",
  "StrongFortress",
  "FreestyleLeap",
  "Doppelganger",
  "SuicideBomber",
  "FlyingThunderGod",
];

function stableHash(value) {
  let hash = 2166136261 >>> 0;
  const text = String(value || "");
  for (let i = 0; i < text.length; i += 1) {
    hash ^= text.charCodeAt(i);
    hash = Math.imul(hash, 16777619) >>> 0;
  }
  return hash >>> 0;
}

function squareToPosition(square) {
  if (!/^[a-h][1-8]$/i.test(String(square || ""))) return null;
  return { x: square.toLowerCase().charCodeAt(0) - 97, y: Number(square[1]) - 1 };
}

function positionToSquare(position) {
  if (!inside(position)) return "";
  return `${String.fromCharCode(97 + position.x)}${position.y + 1}`;
}

function inside(position) {
  return position && position.x >= 0 && position.x < BOARD_SIZE && position.y >= 0 && position.y < BOARD_SIZE;
}

function parseFenBoard(fen) {
  const board = Array.from({ length: BOARD_SIZE }, () => Array(BOARD_SIZE).fill(null));
  const ranks = String(fen || "").trim().split(/\s+/)[0].split("/");
  if (ranks.length !== BOARD_SIZE) throw new Error("Invalid FEN board");

  for (let rankIndex = 0; rankIndex < BOARD_SIZE; rankIndex += 1) {
    let file = 0;
    const y = 7 - rankIndex;
    for (const symbol of ranks[rankIndex]) {
      if (/\d/.test(symbol)) {
        file += Number(symbol);
      } else {
        if (file >= BOARD_SIZE) throw new Error("Invalid FEN rank");
        board[file][y] = {
          symbol,
          team: symbol === symbol.toUpperCase() ? "WHITE" : "BLACK",
          type: fenType(symbol),
        };
        file += 1;
      }
    }
    if (file !== BOARD_SIZE) throw new Error("Invalid FEN rank width");
  }
  return board;
}

function fenType(symbol) {
  return ({ p: "PAWN", n: "KNIGHT", b: "BISHOP", r: "ROOK", q: "QUEEN", k: "KING" })[symbol.toLowerCase()];
}

function createDeterministicState(seed) {
  return {
    version: 1,
    seed: String(seed || ""),
    white: createTeamState("WHITE", seed),
    black: createTeamState("BLACK", seed),
  };
}

function createTeamState(team, seed) {
  const hash = stableHash(`${seed || ""}|${team}|ARAM-V1`);
  const buff = BUFFS[hash % BUFFS.length];
  const homeRank = team === "WHITE" ? 1 : 8;
  const pawnRank = team === "WHITE" ? 2 : 7;
  const state = {
    team,
    buff,
    commandantPawns: [],
    swappedKnight: "",
    swappedBishop: "",
    originalQueen: `d${homeRank}`,
    suicideBomberUsed: false,
    queenTeleportUses: 0,
    queenTeleportCooldown: 0,
  };

  if (buff === "CommandantPawn") state.commandantPawns = [`d${pawnRank}`, `e${pawnRank}`, `c${pawnRank}`];
  if (buff === "Doppelganger") {
    const knights = [`b${homeRank}`, `g${homeRank}`];
    const bishops = [`c${homeRank}`, `f${homeRank}`];
    state.swappedKnight = knights[hash % knights.length];
    state.swappedBishop = bishops[(hash >>> 8) % bishops.length];
  }
  return state;
}

function validateAramMove({ gameMode, fen, turn, from, to, aramState, baseMoveLegal = false }) {
  if (String(gameMode || "").toUpperCase() !== "ARAM") return reject("WRONG_GAME_MODE");
  if (!aramState || !aramState.white || !aramState.black) return reject("ARAM_STATE_REQUIRED");

  const source = squareToPosition(from);
  const destination = squareToPosition(to);
  if (!source || !destination || (source.x === destination.x && source.y === destination.y)) return reject("INVALID_SQUARE");

  let board;
  try {
    board = parseFenBoard(fen);
  } catch (error) {
    return reject("INVALID_FEN", error.message);
  }

  const piece = board[source.x][source.y];
  const target = board[destination.x][destination.y];
  const expectedTeam = String(turn || "").toUpperCase();
  if (!piece || piece.team !== expectedTeam) return reject("NOT_YOUR_PIECE");
  if (target && target.team === piece.team) return reject("FRIENDLY_DESTINATION");
  if (target && target.type === "KING") return reject("KING_CAPTURE_FORBIDDEN");

  const teamState = piece.team === "WHITE" ? aramState.white : aramState.black;
  const delta = { x: destination.x - source.x, y: destination.y - source.y };
  const sourceSquare = positionToSquare(source);
  let aramLegal = false;
  let moveKind = "STANDARD";

  if (teamState.buff === "CommandantPawn" && piece.type === "PAWN" && teamState.commandantPawns.includes(sourceSquare)) {
    const direction = piece.team === "WHITE" ? 1 : -1;
    if (delta.x === 0 && delta.y === direction * 2 && !target && !board[source.x][source.y + direction]) {
      aramLegal = true;
      moveKind = "COMMANDANT_DOUBLE_STEP";
    }
  }

  if (teamState.buff === "FreestyleLeap" && piece.type === "KNIGHT") {
    if (Math.abs(delta.x) === 2 && Math.abs(delta.y) === 2) {
      aramLegal = true;
      moveKind = "FREESTYLE_LEAP";
    }
  }

  if (teamState.buff === "Doppelganger") {
    if (piece.type === "KNIGHT" && teamState.swappedKnight === sourceSquare) {
      aramLegal = bishopPattern(board, source, destination);
      moveKind = "DOPPELGANGER_BISHOP_MOVE";
      baseMoveLegal = false;
    } else if (piece.type === "BISHOP" && teamState.swappedBishop === sourceSquare) {
      aramLegal = knightPattern(delta);
      moveKind = "DOPPELGANGER_KNIGHT_MOVE";
      baseMoveLegal = false;
    }
  }

  if (teamState.buff === "FlyingThunderGod" && piece.type === "QUEEN" && teamState.originalQueen === sourceSquare && !target) {
    const normalQueenMove = queenPattern(board, source, destination);
    if (!normalQueenMove && teamState.queenTeleportUses < 5 && teamState.queenTeleportCooldown <= 0) {
      aramLegal = true;
      moveKind = "QUEEN_TELEPORT";
    }
  }

  if (teamState.buff === "StrongFortress" && piece.type === "KING" && delta.y === 0 && Math.abs(delta.x) === 2) {
    aramLegal = fortressStructureLegal(board, piece.team, source, destination, castlingRights(fen));
    moveKind = "STRONG_FORTRESS_CASTLE";
  }

  if (!aramLegal && !baseMoveLegal) return reject("ILLEGAL_ARAM_MOVE");
  return { accepted: true, moveKind, source, destination };
}

function applyAramPostMove({ aramState, team, from, to, capturedPiece, nextTurn }) {
  const next = JSON.parse(JSON.stringify(aramState));
  const state = String(team).toUpperCase() === "WHITE" ? next.white : next.black;
  const opponent = String(team).toUpperCase() === "WHITE" ? next.black : next.white;

  moveTrackedSquare(state.commandantPawns, from, to);
  if (state.swappedKnight === from) state.swappedKnight = to;
  if (state.swappedBishop === from) state.swappedBishop = to;
  if (state.originalQueen === from) state.originalQueen = to;

  if (state.buff === "FlyingThunderGod" && state.originalQueen === to) {
    const source = squareToPosition(from);
    const destination = squareToPosition(to);
    const delta = { x: destination.x - source.x, y: destination.y - source.y };
    const queenShape = delta.x === 0 || delta.y === 0 || Math.abs(delta.x) === Math.abs(delta.y);
    if (!queenShape) {
      state.queenTeleportUses += 1;
      state.queenTeleportCooldown = 5;
    }
  }

  const nextTurnState = String(nextTurn || "").toUpperCase() === "WHITE" ? next.white : next.black;
  if (nextTurnState && String(nextTurn || "").length > 0)
    nextTurnState.queenTeleportCooldown = Math.max(0, nextTurnState.queenTeleportCooldown - 1);
  if (capturedPiece && capturedPiece.type === "QUEEN" && opponent.buff === "SuicideBomber" && opponent.originalQueen === to && !opponent.suicideBomberUsed) {
    opponent.suicideBomberUsed = true;
    opponent.originalQueen = "";
    return { aramState: next, explosionCenter: to };
  }
  if (capturedPiece && capturedPiece.type === "QUEEN" && opponent.originalQueen === to) opponent.originalQueen = "";
  return { aramState: next, explosionCenter: null };
}

function moveTrackedSquare(values, from, to) {
  if (!Array.isArray(values)) return;
  const index = values.indexOf(from);
  if (index >= 0) values[index] = to;
}

function knightPattern(delta) {
  const x = Math.abs(delta.x);
  const y = Math.abs(delta.y);
  return (x === 1 && y === 2) || (x === 2 && y === 1);
}

function bishopPattern(board, from, to) {
  return Math.abs(to.x - from.x) === Math.abs(to.y - from.y) && pathClear(board, from, to);
}

function queenPattern(board, from, to) {
  const delta = { x: to.x - from.x, y: to.y - from.y };
  return (delta.x === 0 || delta.y === 0 || Math.abs(delta.x) === Math.abs(delta.y)) && pathClear(board, from, to);
}

function pathClear(board, from, to) {
  const step = { x: Math.sign(to.x - from.x), y: Math.sign(to.y - from.y) };
  let current = { x: from.x + step.x, y: from.y + step.y };
  while (current.x !== to.x || current.y !== to.y) {
    if (board[current.x][current.y]) return false;
    current = { x: current.x + step.x, y: current.y + step.y };
  }
  return true;
}

function castlingRights(fen) {
  const fields = String(fen || "").trim().split(/\s+/);
  return fields.length > 2 ? fields[2] : "-";
}

function fortressStructureLegal(board, team, from, to, rights) {
  const homeRank = team === "WHITE" ? 0 : 7;
  if (from.x !== 4 || from.y !== homeRank || to.y !== homeRank) return false;
  const kingSide = to.x === 6;
  const queenSide = to.x === 2;
  if (!kingSide && !queenSide) return false;
  const requiredRight = team === "WHITE" ? (kingSide ? "K" : "Q") : (kingSide ? "k" : "q");
  if (!String(rights || "").includes(requiredRight)) return false;
  const rookFile = kingSide ? 7 : 0;
  const rook = board[rookFile][homeRank];
  if (!rook || rook.team !== team || rook.type !== "ROOK") return false;
  const step = kingSide ? 1 : -1;
  for (let x = from.x + step; x !== rookFile; x += step) if (board[x][homeRank]) return false;
  return true;
}

function reject(code, detail = "") {
  return { accepted: false, code, detail };
}

module.exports = {
  BUFFS,
  applyAramPostMove,
  createDeterministicState,
  parseFenBoard,
  stableHash,
  validateAramMove,
};
