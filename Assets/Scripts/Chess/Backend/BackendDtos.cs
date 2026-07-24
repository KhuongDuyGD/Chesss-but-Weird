using System;
using System.Collections.Generic;

[Serializable]
public class BackendAuthRequest
{
    public string username;
    public string password;
}

[Serializable]
public class BackendJoinRoomRequest
{
    public string roomCode;
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public string gameMode;
}

[Serializable]
public class BackendCreateRoomRequest
{
    public string gameMode;
}

[Serializable]
public class BackendUserProfileDto
{
    public string id;
    public string username;
    public int elo;
    public string createdAt;
}

[Serializable]
public class BackendAuthResponseDto
{
    public string token;
    public long expiresInSeconds;
    public BackendUserProfileDto user;
}

[Serializable]
public class BackendRoomDto
{
    public string id;
    public string roomCode;
    public string hostId;
    public string hostUsername;
    public string guestId;
    public string guestUsername;
    public string status;
    public string gameMode;
}

[Serializable]
public class BackendMatchMoveDto
{
    public string id;
    public int moveNumber;
    public string playerId;
    public string playerUsername;
    public string from;
    public string to;
    public string promotion;
    public string notation;
    public string fenAfter;
    public string playedAt;
}

[Serializable]
public class BackendMatchDto
{
    public string id;
    public string roomCode;
    public string whitePlayerId;
    public string whiteUsername;
    public string blackPlayerId;
    public string blackUsername;
    public string winnerId;
    public string winnerUsername;
    public string status;
    public string terminationReason;
    public string currentFen;
    public int moveCount;
    public int whiteEloBefore;
    public int? whiteEloAfter;
    public int blackEloBefore;
    public int? blackEloAfter;
    public string startedAt;
    public string finishedAt;
    public string gameMode;
    public string aramSeed;
    public BackendAramStatePayload aramState;
    public List<BackendMatchMoveDto> moves;
}

[Serializable]
public class BackendSocketEnvelope
{
    public string type;
    public string requestId;
    public string roomCode;
    public string sentAt;
    public Newtonsoft.Json.Linq.JToken payload;
}

[Serializable]
public class BackendSocketErrorPayload
{
    public string code;
    public string message;
}

[Serializable]
public class BackendPlayerPresencePayload
{
    public string userId;
    public string username;
}

[Serializable]
public class BackendPlayerReadyPayload
{
    public string userId;
    public string username;
    public int readyCount;
}

[Serializable]
public class BackendGameStartPayload
{
    public string matchId;
    public string whitePlayerId;
    public string whiteUsername;
    public string blackPlayerId;
    public string blackUsername;
    public string fen;
    public string turn;
    public string gameMode;
    public string aramSeed;
    public BackendAramStatePayload aramState;
}

[Serializable]
public class BackendMovePayload
{
    public string from;
    public string to;
    public string promotion;
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public string gameMode;
}

[Serializable]
public class BackendMoveResultPayload
{
    public bool accepted;
    public bool newlyProcessed;
    public string matchId;
    public int moveNumber;
    public string from;
    public string to;
    public string promotion;
    public string notation;
    public string fen;
    public string turn;
    public bool check;
    public string status;
    public string gameMode;
    public BackendAramStatePayload aramState;
    public BackendGameOverPayload gameOver;
}

[Serializable]
public class BackendGameStatePayload
{
    public string matchId;
    public string status;
    public string fen;
    public string turn;
    public int moveCount;
    public string whitePlayerId;
    public string blackPlayerId;
    public string result;
    public string reason;
    public string winnerId;
    public int? whiteEloBefore;
    public int? whiteEloAfter;
    public int? blackEloBefore;
    public int? blackEloAfter;
    public string gameMode;
    public string aramSeed;
    public BackendAramStatePayload aramState;
}

[Serializable]
public class BackendGameModePayload
{
    public string gameMode;
}

[Serializable]
public class BackendAramStatePayload
{
    public int version;
    public string seed;
    public BackendAramTeamStatePayload white;
    public BackendAramTeamStatePayload black;
}

[Serializable]
public class BackendAramTeamStatePayload
{
    public string team;
    public string buff;
    public List<string> commandantPawns;
    public string swappedKnight;
    public string swappedBishop;
    public string originalQueen;
    public bool suicideBomberUsed;
    public int queenTeleportUses;
    public int queenTeleportCooldown;
}

[Serializable]
public class BackendGameOverPayload
{
    public string matchId;
    public string result;
    public string reason;
    public string winnerId;
    public int whiteEloBefore;
    public int? whiteEloAfter;
    public int blackEloBefore;
    public int? blackEloAfter;
}

[Serializable]
public class BackendDrawOfferedPayload
{
    public string offeredBy;
}

[Serializable]
public class BackendPauseStatePayload
{
    public string userId;
    public string username;
    public bool paused;
}
