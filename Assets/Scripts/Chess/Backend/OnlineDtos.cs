using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

namespace ChessButWeird.Online
{
    // Field names mirror the backend's camelCase REST and SignalR JSON.
    [Serializable, Preserve] public sealed class GameSettings
    {
        public string mode = "Classic", region = "VN";
        public int initialSeconds = 600, incrementSeconds = 5, protocolVersion = 1;
    }
    [Serializable, Preserve] public sealed class LobbyRequest
    { public string requestId; public GameSettings settings; }
    [Serializable, Preserve] public sealed class Ticket
    { public string ticketId, status, matchId; public GameSettings settings; public DateTime createdAt, expiresAt; }
    [Serializable, Preserve] public sealed class Room
    {
        public string roomId, code, ownerId, status, matchId;
        public List<string> members = new List<string>();
        public GameSettings settings;
        public DateTime expiresAt;
    }
    [Serializable, Preserve] public sealed class Loadout
    { public string itemId, code, unityAssetKey, type; }
    [Serializable, Preserve] public sealed class Player
    {
        public string userId, username, displayName, color;
        public int rating; public bool ready, connected; public DateTime? reconnectDeadline;
        public List<Loadout> loadout = new List<Loadout>();
    }
    [Serializable, Preserve] public sealed class Piece
    { public int id, forward; public string kind, team, square; public bool hasMoved; }
    [Serializable, Preserve] public sealed class Clocks
    { public double whiteMilliseconds, blackMilliseconds; public string runningColor; public DateTime serverTime; }
    [Serializable, Preserve] public sealed class DrawOffer
    { public string id, userId; public DateTime expiresAt; }
    [Serializable, Preserve] public sealed class PlayerResult
    { public string userId; public int ratingBefore, ratingAfter, ratingChange; public long golds, diamonds, tickets; }
    [Serializable, Preserve] public sealed class Result
    { public string outcome, winnerId, reason; public DateTime finishedAt; public List<PlayerResult> players = new List<PlayerResult>(); }
    [Serializable, Preserve] public sealed class MatchState
    {
        public string matchId, status, turn, fen, castlingRights, enPassantTarget, rematchId;
        public long stateVersion, eventSequence;
        public int halfMoveClock, fullMoveNumber;
        public DateTime serverTime, readyDeadline;
        public GameSettings settings;
        public List<Piece> board = new List<Piece>();
        public List<Player> players = new List<Player>();
        public Clocks clocks; public DrawOffer drawOffer; public JObject aram; public Result result;
        public bool IsActive => status == "AwaitingReady" || status == "InProgress";
    }
    [Serializable, Preserve] public sealed class MatchSummary
    {
        public string matchId, status; public GameSettings settings;
        public List<Player> players = new List<Player>();
        public DateTime createdAt; public DateTime? startedAt, finishedAt; public Result result;
    }
    [Serializable, Preserve] public sealed class Page<T>
    { public int pageNumber, pageSize; public long total; public List<T> items = new List<T>(); }
    [Serializable, Preserve] public sealed class MoveRecord
    { public string id, matchId, userId, commandId, kind, payloadJson; public long sequence, stateVersion; public DateTime playedAt; }
    [Serializable, Preserve] public sealed class MoveCommand
    { public string from, to, promotion; }
    [Serializable, Preserve] public sealed class FormationPlacement
    { public int pieceId; public string square; }
    [Serializable, Preserve] public sealed class AbilityCommand
    {
        public string kind, target; public int? buffId, pieceId, targetPieceId; public double? yaw, pitch;
        public List<int> pieceIds = new List<int>();
        public List<FormationPlacement> formation = new List<FormationPlacement>();
    }
    [Serializable, Preserve] public sealed class CommandAck
    { public string commandId, errorCode; public bool accepted, replayed; public long stateVersion, eventSequence; public DateTime serverTime; }
    [Serializable, Preserve] public sealed class Subscription
    { public MatchState state; public long resumeAfterSequence; }
    [Serializable, Preserve] public sealed class ServerEvent
    { public string eventId, type, matchId; public long sequence, stateVersion; public DateTime serverTime; public JToken payload; }
    [Serializable, Preserve] public sealed class EventReplay
    { public long latestSequence; public List<ServerEvent> events = new List<ServerEvent>(); }
}
