"use strict";

const assert = require("assert");
const { applyAramPostMove, createDeterministicState, validateAramMove } = require("./aram-rules");

const initialFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

function stateWith(team, buff) {
  const state = createDeterministicState("test");
  const target = team === "WHITE" ? state.white : state.black;
  target.buff = buff;
  return state;
}

{
  const state = stateWith("WHITE", "CommandantPawn");
  state.white.commandantPawns = ["e2"];
  const result = validateAramMove({ gameMode: "ARAM", fen: initialFen, turn: "WHITE", from: "e2", to: "e4", aramState: state });
  assert.equal(result.accepted, true);
  assert.equal(result.moveKind, "COMMANDANT_DOUBLE_STEP");
}

{
  const state = stateWith("WHITE", "FreestyleLeap");
  const result = validateAramMove({ gameMode: "ARAM", fen: initialFen, turn: "WHITE", from: "b1", to: "d3", aramState: state });
  assert.equal(result.accepted, true);
  assert.equal(result.moveKind, "FREESTYLE_LEAP");
}

{
  const state = stateWith("WHITE", "Doppelganger");
  state.white.swappedBishop = "c1";
  const result = validateAramMove({ gameMode: "ARAM", fen: initialFen, turn: "WHITE", from: "c1", to: "b3", aramState: state });
  assert.equal(result.accepted, true);
  assert.equal(result.moveKind, "DOPPELGANGER_KNIGHT_MOVE");
}

{
  const emptyQueenFen = "4k3/8/8/8/8/8/8/3QK3 w - - 0 1";
  const state = stateWith("WHITE", "FlyingThunderGod");
  state.white.originalQueen = "d1";
  const result = validateAramMove({ gameMode: "ARAM", fen: emptyQueenFen, turn: "WHITE", from: "d1", to: "f4", aramState: state });
  assert.equal(result.accepted, true);
  assert.equal(result.moveKind, "QUEEN_TELEPORT");
  const applied = applyAramPostMove({ aramState: state, team: "WHITE", from: "d1", to: "f4" });
  assert.equal(applied.aramState.white.queenTeleportUses, 1);
  assert.equal(applied.aramState.white.queenTeleportCooldown, 5);

  state.white.queenTeleportCooldown = 2;
  const blockedByCooldown = validateAramMove({ gameMode: "ARAM", fen: emptyQueenFen, turn: "WHITE", from: "d1", to: "f4", aramState: state });
  assert.equal(blockedByCooldown.accepted, false);
}

{
  const castleFen = "4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1";
  const state = stateWith("WHITE", "StrongFortress");
  const result = validateAramMove({ gameMode: "ARAM", fen: castleFen, turn: "WHITE", from: "e1", to: "g1", aramState: state });
  assert.equal(result.accepted, true);
  assert.equal(result.moveKind, "STRONG_FORTRESS_CASTLE");

  const noRights = validateAramMove({ gameMode: "ARAM", fen: castleFen.replace(" KQ ", " - "), turn: "WHITE", from: "e1", to: "g1", aramState: state });
  assert.equal(noRights.accepted, false);
}

{
  const state = stateWith("BLACK", "SuicideBomber");
  state.black.originalQueen = "d8";
  const applied = applyAramPostMove({ aramState: state, team: "WHITE", from: "d1", to: "d8", capturedPiece: { type: "QUEEN" }, nextTurn: "BLACK" });
  assert.equal(applied.explosionCenter, "d8");
  assert.equal(applied.aramState.black.suicideBomberUsed, true);
  assert.equal(applied.aramState.black.originalQueen, "");
}

{
  const state = stateWith("WHITE", "CommandantPawn");
  const result = validateAramMove({ gameMode: "CLASSIC", fen: initialFen, turn: "WHITE", from: "e2", to: "e4", aramState: state });
  assert.equal(result.accepted, false);
  assert.equal(result.code, "WRONG_GAME_MODE");
}

console.log("ARAM server rule tests passed");
