using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class ChessLanController : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float ReconnectDelaySeconds = 3f;
    private const float HeartbeatIntervalSeconds = 10f;

    private ChessGame chessGame;
    private ChessTurnSelectionUI turnSelectionUI;
    private BackendWebSocketClient webSocketClient;
    private readonly HashSet<string> pendingLocalMoveRequestIds = new HashSet<string>();
    private readonly List<BackendMatchDto> recentMatches = new List<BackendMatchDto>();

    private bool showLanPanel;
    private bool lanGameActive;
    private bool requestInFlight;
    private bool reconnectPending;
    private string roomCodeInput = string.Empty;
    private string statusMessage = "Connect to the Spring Boot backend, create a room, and wait for another player.";
    private string currentRoomCode = string.Empty;
    private string currentMatchId = string.Empty;
    private string lastConfirmedFen = string.Empty;
    private float reconnectAt;
    private float lastHeartbeatAt;
    private BackendRoomDto currentRoom;
    private BackendGameStartPayload currentGameStart;
    private bool localReady;
    private int readyCount;
    private bool opponentDrawOfferPending;
    private bool intentionalDisconnect;
    private Texture2D backButtonTexture;

    public bool IsNetworkGameActive => lanGameActive;
    public event Action<bool, string> OpponentPauseChanged;

    public void Initialize(ChessGame newChessGame, ChessTurnSelectionUI newTurnSelectionUI)
    {
        chessGame = newChessGame;
        turnSelectionUI = newTurnSelectionUI;
        chessGame.MoveCommitted += HandleMoveCommitted;
        chessGame.ReturnedToMainMenu += HandleReturnedToMainMenu;
        backButtonTexture = LoadProjectTexture("Assets/Materials/Main_Menu/Back.png");
        EnsureWebSocketClient();
    }

    private void Update()
    {
        if (reconnectPending && Time.unscaledTime >= reconnectAt && !requestInFlight)
        {
            reconnectPending = false;
            StartCoroutine(ConnectWebSocketCoroutine(true));
        }

        if (webSocketClient != null && webSocketClient.IsConnected && lanGameActive && Time.unscaledTime - lastHeartbeatAt >= HeartbeatIntervalSeconds)
        {
            lastHeartbeatAt = Time.unscaledTime;
            _ = webSocketClient.SendAsync("SYNC_REQUEST", CreateRequestId("sync"), new { });
        }
    }

    private void OnDestroy()
    {
        if (chessGame != null)
        {
            chessGame.MoveCommitted -= HandleMoveCommitted;
            chessGame.ReturnedToMainMenu -= HandleReturnedToMainMenu;
        }

        ReleaseWebSocketClient();
    }

    public void SendPauseState(bool paused)
    {
        if (!lanGameActive || webSocketClient == null || !webSocketClient.IsConnected)
            return;

        string eventType = paused ? "PAUSE" : "RESUME";
        _ = webSocketClient.SendAsync(eventType, CreateRequestId(paused ? "pause" : "resume"), new { paused });
        statusMessage = paused ? "Pause sent to opponent." : "Resume sent to opponent.";
    }

    public void QuitActiveMatch()
    {
        if (!lanGameActive)
        {
            chessGame?.RestartToMainMenu();
            return;
        }

        if (webSocketClient == null || !webSocketClient.IsConnected)
        {
            statusMessage = "Connection lost. Returning to the main menu.";
            chessGame?.RestartToMainMenu();
            return;
        }

        SendResign();
    }

    private void OnGUI()
    {
        Matrix4x4 previousMatrix = GUI.matrix;
        float guiScale = GetGuiScale();
        float guiWidth = Screen.width / guiScale;
        float guiHeight = Screen.height / guiScale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1f));

        try
        {
            if (showLanPanel)
                DrawLanPanel(guiWidth, guiHeight);

            if (lanGameActive)
                DrawInGameHud(guiWidth, guiHeight);
        }
        finally
        {
            GUI.matrix = previousMatrix;
        }
    }

    public void ShowLanSetup()
    {
        EnsureWebSocketClient();
        showLanPanel = true;
        statusMessage = "Connect to the backend, share your room code, and play online.";
        roomCodeInput = currentRoomCode;
        StartCoroutine(LoadMatchHistoryCoroutine());
        StartCoroutine(LoadActiveMatchCoroutine());
    }

    public void HideLanSetup()
    {
        showLanPanel = false;
    }

    private void DrawLanPanel(float guiWidth, float guiHeight)
    {
        float panelWidth = Mathf.Clamp(guiWidth * 0.54f, 860f, 1120f);
        float panelHeight = Mathf.Clamp(guiHeight * 0.72f, 720f, 900f);
        Rect panelRect = new Rect(
            (guiWidth - panelWidth) * 0.5f,
            (guiHeight - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.30f);
        GUI.DrawTexture(new Rect(0f, 0f, guiWidth, guiHeight), Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUI.Box(panelRect, string.Empty);
        GUI.Label(new Rect(panelRect.x + 28f, panelRect.y + 20f, panelRect.width - 56f, 36f), "Online Multiplayer", GetTitleStyle());
        GUI.Label(new Rect(panelRect.x + 28f, panelRect.y + 58f, panelRect.width - 56f, 62f), statusMessage, GetStatusStyle());

        DrawServerInfo(panelRect);
        DrawRoomSection(panelRect);
        DrawHistory(panelRect);
        DrawButtons(panelRect);
        DrawBackButton(panelRect);
    }

    private void DrawServerInfo(Rect panelRect)
    {
        GUI.Label(new Rect(panelRect.x + 28f, panelRect.y + 126f, 180f, 28f), $"Server: {BackendConfig.BaseUrl}", GetBodyStyle());
        GUI.Label(new Rect(panelRect.x + 28f, panelRect.y + 154f, 360f, 28f), $"User: {PlayerAuthService.CurrentDisplayName}", GetBodyStyle());
        GUI.Label(new Rect(panelRect.x + 28f, panelRect.y + 182f, 360f, 28f), $"Room: {GetRoomSummary()}", GetBodyStyle());
        GUI.Label(new Rect(panelRect.x + 28f, panelRect.y + 210f, 360f, 28f), $"Connection: {GetConnectionSummary()}", GetBodyStyle());
        GUI.Label(new Rect(panelRect.x + 420f, panelRect.y + 154f, panelRect.width - 600f, 56f), "Players on any network can join this room code if they use the same backend URL.", GetBodyStyle());

        if (GUI.Button(new Rect(panelRect.x + panelRect.width - 152f, panelRect.y + 20f, 124f, 32f), "Refresh"))
        {
            StartCoroutine(LoadMatchHistoryCoroutine());
            StartCoroutine(LoadActiveMatchCoroutine());
        }
    }

    private void DrawRoomSection(Rect panelRect)
    {
        Rect roomRect = new Rect(panelRect.x + 28f, panelRect.y + 252f, panelRect.width - 56f, 170f);
        GUI.Box(roomRect, string.Empty);
        GUI.Label(new Rect(roomRect.x + 16f, roomRect.y + 12f, roomRect.width - 32f, 28f), "Room", GetSectionStyle());

        GUI.Label(new Rect(roomRect.x + 16f, roomRect.y + 50f, 120f, 28f), "Room Code", GetBodyStyle());
        roomCodeInput = GUI.TextField(new Rect(roomRect.x + 144f, roomRect.y + 46f, 180f, 34f), roomCodeInput, 6).Trim().ToUpperInvariant();
        if (GUI.Button(new Rect(roomRect.x + 338f, roomRect.y + 46f, 120f, 34f), "Copy Code"))
            CopyRoomCodeToClipboard();

        if (currentRoom != null)
        {
            GUI.Label(new Rect(roomRect.x + 16f, roomRect.y + 92f, roomRect.width - 32f, 24f), $"Host: {currentRoom.hostUsername}", GetBodyStyle());
            GUI.Label(new Rect(roomRect.x + 16f, roomRect.y + 118f, roomRect.width - 32f, 24f), $"Guest: {(string.IsNullOrWhiteSpace(currentRoom.guestUsername) ? "Waiting..." : currentRoom.guestUsername)}", GetBodyStyle());
            GUI.Label(new Rect(roomRect.x + 16f, roomRect.y + 144f, roomRect.width - 32f, 24f), $"Status: {currentRoom.status} | Ready: {readyCount}/2", GetBodyStyle());
        }
    }

    private void DrawHistory(Rect panelRect)
    {
        Rect historyRect = new Rect(panelRect.x + 28f, panelRect.y + 438f, panelRect.width - 56f, 220f);
        GUI.Box(historyRect, string.Empty);
        GUI.Label(new Rect(historyRect.x + 16f, historyRect.y + 12f, historyRect.width - 32f, 28f), "Recent Matches", GetSectionStyle());

        if (recentMatches.Count == 0)
        {
            GUI.Label(new Rect(historyRect.x + 16f, historyRect.y + 52f, historyRect.width - 32f, 28f), "No match history loaded yet.", GetBodyStyle());
            return;
        }

        float rowY = historyRect.y + 48f;
        int maxRows = Mathf.Min(5, recentMatches.Count);
        for (int i = 0; i < maxRows; i++)
        {
            BackendMatchDto match = recentMatches[i];
            string label = $"{match.whiteUsername} vs {match.blackUsername} | {match.status} | {match.terminationReason} | Moves: {match.moveCount}";
            GUI.Label(new Rect(historyRect.x + 16f, rowY, historyRect.width - 32f, 28f), label, GetBodyStyle());
            rowY += 32f;
        }
    }

    private void DrawButtons(Rect panelRect)
    {
        bool previousEnabled = GUI.enabled;
        GUI.enabled = !requestInFlight;

        Rect firstButton = new Rect(panelRect.x + 28f, panelRect.y + panelRect.height - 72f, 180f, 40f);
        Rect secondButton = new Rect(panelRect.x + 220f, panelRect.y + panelRect.height - 72f, 180f, 40f);
        Rect thirdButton = new Rect(panelRect.x + 412f, panelRect.y + panelRect.height - 72f, 180f, 40f);
        Rect fourthButton = new Rect(panelRect.x + panelRect.width - 208f, panelRect.y + panelRect.height - 72f, 180f, 40f);

        if (GUI.Button(firstButton, "Create Room"))
            StartCoroutine(CreateRoomCoroutine());

        if (GUI.Button(secondButton, "Join Room"))
            StartCoroutine(JoinRoomCoroutine());

        if (GUI.Button(thirdButton, localReady ? "Ready Sent" : "Ready"))
            SendReady();

        if (GUI.Button(fourthButton, currentRoom != null ? "Leave Room" : "Back"))
        {
            if (currentRoom != null)
                LeaveRoom();
            else
                turnSelectionUI.ShowMultiplayerModeSelection();
        }

        GUI.enabled = previousEnabled;
    }

    private void DrawBackButton(Rect panelRect)
    {
        if (currentRoom != null)
            return;

        Rect backRect = new Rect(panelRect.x + 18f, panelRect.y + 18f, 132f, 56f);
        if (backButtonTexture != null)
        {
            if (GUI.Button(backRect, backButtonTexture, GUIStyle.none))
                turnSelectionUI.ShowMultiplayerModeSelection();

            return;
        }

        if (GUI.Button(backRect, "Back"))
            turnSelectionUI.ShowMultiplayerModeSelection();
    }

    private void DrawInGameHud(float guiWidth, float guiHeight)
    {
        float width = Mathf.Clamp(guiWidth * 0.22f, 320f, 410f);
        Rect panelRect = new Rect(18f, guiHeight - 188f, width, 170f);
        GUI.Box(panelRect, string.Empty);

        GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 10f, panelRect.width - 28f, 24f), "Online Match", GetSectionStyle());
        GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 38f, panelRect.width - 28f, 20f), $"Room: {currentRoomCode}", GetHudStyle());
        GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 58f, panelRect.width - 28f, 20f), $"Match: {currentMatchId}", GetHudStyle());
        GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 78f, panelRect.width - 28f, 20f), GetConnectionSummary(), GetHudStyle());
        GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 98f, panelRect.width - 28f, 20f), opponentDrawOfferPending ? "Opponent offered a draw." : string.Empty, GetHudStyle());

        if (GUI.Button(new Rect(panelRect.x + 14f, panelRect.y + 122f, panelRect.width - 28f, 20f), "Offer Draw"))
            SendDrawOffer();

        if (GUI.Button(new Rect(panelRect.x + 14f, panelRect.y + 144f, (panelRect.width - 38f) * 0.5f, 20f), opponentDrawOfferPending ? "Accept Draw" : "Resign"))
        {
            if (opponentDrawOfferPending)
                SendDrawAccept();
            else
                SendResign();
        }

        if (GUI.Button(new Rect(panelRect.x + 20f + (panelRect.width - 38f) * 0.5f, panelRect.y + 144f, (panelRect.width - 38f) * 0.5f, 20f), "Leave"))
            LeaveRoom();
    }

    private IEnumerator CreateRoomCoroutine()
    {
        requestInFlight = true;
        statusMessage = "Creating room...";
        yield return BackendRestClient.Send<BackendRoomDto>(
            "POST",
            "/api/rooms/create",
            null,
            true,
            response =>
            {
                requestInFlight = false;
                currentRoom = response.result;
                currentRoomCode = currentRoom.roomCode ?? string.Empty;
                roomCodeInput = currentRoomCode;
                readyCount = 0;
                localReady = false;
                statusMessage = $"Room {currentRoomCode} created. Connecting to waiting room...";
                StartCoroutine(ConnectWebSocketCoroutine(false));
            },
            (message, _) =>
            {
                requestInFlight = false;
                statusMessage = message;
            });
    }

    private IEnumerator JoinRoomCoroutine()
    {
        if (string.IsNullOrWhiteSpace(roomCodeInput))
        {
            statusMessage = "Enter the 6-character room code first.";
            yield break;
        }

        requestInFlight = true;
        statusMessage = $"Joining room {roomCodeInput}...";
        yield return BackendRestClient.Send<BackendRoomDto>(
            "POST",
            "/api/rooms/join",
            new BackendJoinRoomRequest { roomCode = roomCodeInput },
            true,
            response =>
            {
                requestInFlight = false;
                currentRoom = response.result;
                currentRoomCode = currentRoom.roomCode ?? roomCodeInput;
                roomCodeInput = currentRoomCode;
                readyCount = 0;
                localReady = false;
                statusMessage = $"Joined room {currentRoomCode}. Connecting to waiting room...";
                StartCoroutine(ConnectWebSocketCoroutine(false));
            },
            (message, _) =>
            {
                requestInFlight = false;
                statusMessage = message;
            });
    }

    private IEnumerator ConnectWebSocketCoroutine(bool reconnecting)
    {
        if (string.IsNullOrWhiteSpace(currentRoomCode))
        {
            statusMessage = "Room code is missing.";
            yield break;
        }

        if (!PlayerAuthService.IsAuthenticated || string.IsNullOrWhiteSpace(PlayerAuthService.Token))
        {
            statusMessage = "Login expired. Please sign in again.";
            yield break;
        }

        EnsureWebSocketClient();
        intentionalDisconnect = false;
        requestInFlight = true;
        statusMessage = reconnecting ? "Reconnecting to room..." : "Connecting to room socket...";
        var task = webSocketClient.ConnectAsync(currentRoomCode, PlayerAuthService.Token);
        while (!task.IsCompleted)
            yield return null;

        requestInFlight = false;
        if (task.IsFaulted)
            statusMessage = "Unable to connect to room socket.";
    }

    private IEnumerator LoadMatchHistoryCoroutine()
    {
        if (!PlayerAuthService.IsAuthenticated)
            yield break;

        yield return BackendRestClient.Send<List<BackendMatchDto>>(
            "GET",
            "/api/matches/history",
            null,
            true,
            response =>
            {
                recentMatches.Clear();
                if (response.result != null)
                    recentMatches.AddRange(response.result);
            },
            (_, __) => { });
    }

    private IEnumerator LoadActiveMatchCoroutine()
    {
        if (!PlayerAuthService.IsAuthenticated)
            yield break;

        yield return BackendRestClient.Send<BackendMatchDto>(
            "GET",
            "/api/matches/active",
            null,
            true,
            response =>
            {
                if (response.result == null)
                    return;

                BackendMatchDto match = response.result;
                currentRoomCode = match.roomCode ?? currentRoomCode;
                currentMatchId = match.id ?? currentMatchId;
                lastConfirmedFen = match.currentFen ?? lastConfirmedFen;
                turnSelectionUI.SetMatchPlayers(match.whiteUsername, match.blackUsername);
                PieceTeam localTeam = string.Equals(match.whitePlayerId, PlayerAuthService.UserId, StringComparison.OrdinalIgnoreCase)
                    ? PieceTeam.White
                    : PieceTeam.Black;
                chessGame.BeginLanGame(PieceTeam.White, localTeam);
                chessGame.ApplyFenState(match.currentFen);
                lanGameActive = string.Equals(match.status, "ACTIVE", StringComparison.OrdinalIgnoreCase);
                statusMessage = $"Recovered active match in room {currentRoomCode}.";
                StartCoroutine(RefreshRoomCoroutine());

                if (!string.IsNullOrWhiteSpace(currentRoomCode) && (webSocketClient == null || !webSocketClient.IsConnected) && !requestInFlight)
                    StartCoroutine(ConnectWebSocketCoroutine(true));
            },
            (_, error) =>
            {
                if (error != null && error.code != 4004)
                    statusMessage = "Unable to load active match.";
            });
    }

    private void SendReady()
    {
        if (string.IsNullOrWhiteSpace(currentRoomCode) || webSocketClient == null || !webSocketClient.IsConnected)
        {
            statusMessage = "Connect to the room before sending ready.";
            return;
        }

        if (localReady)
        {
            statusMessage = "Ready already sent.";
            return;
        }

        localReady = true;
        string requestId = CreateRequestId("ready");
        _ = webSocketClient.SendAsync("READY", requestId, new { });
        statusMessage = "Ready sent. Waiting for the other player...";
    }

    private void SendResign()
    {
        if (!CanSendGameCommand())
            return;

        _ = webSocketClient.SendAsync("RESIGN", CreateRequestId("resign"), new { });
        statusMessage = "Resign sent.";
    }

    private void SendDrawOffer()
    {
        if (!CanSendGameCommand())
            return;

        _ = webSocketClient.SendAsync("DRAW_OFFER", CreateRequestId("draw-offer"), new { });
        statusMessage = "Draw offer sent.";
    }

    private void SendDrawAccept()
    {
        if (!CanSendGameCommand())
            return;

        _ = webSocketClient.SendAsync("DRAW_ACCEPT", CreateRequestId("draw-accept"), new { });
        opponentDrawOfferPending = false;
        statusMessage = "Draw accepted.";
    }

    private void HandleMoveCommitted(ChessLanMove move)
    {
        if (!lanGameActive || webSocketClient == null || !webSocketClient.IsConnected)
            return;

        string requestId = CreateRequestId("move");
        pendingLocalMoveRequestIds.Add(requestId);
        _ = webSocketClient.SendAsync("MOVE", requestId, new BackendMovePayload
        {
            from = ToSquare(move.from),
            to = ToSquare(move.to),
            promotion = move.hasPromotion ? ToPromotion(move.promotionType) : null
        });
    }

    private void HandleReturnedToMainMenu()
    {
        ResetRoomState(clearRoomIdentity: false);
        DisconnectSocketIntentional();
    }

    private void HandleWebSocketConnected()
    {
        requestInFlight = false;
        reconnectPending = false;
        statusMessage = $"Connected to room {currentRoomCode}.";
        lastHeartbeatAt = Time.unscaledTime;
        _ = webSocketClient.SendAsync("SYNC_REQUEST", CreateRequestId("sync"), new { });
        StartCoroutine(LoadActiveMatchCoroutine());
    }

    private void HandleWebSocketClosed(string message)
    {
        requestInFlight = false;

        if (intentionalDisconnect)
        {
            intentionalDisconnect = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(currentRoomCode) || !PlayerAuthService.IsAuthenticated)
            return;

        statusMessage = message;
        reconnectPending = true;
        reconnectAt = Time.unscaledTime + ReconnectDelaySeconds;
    }

    private void HandleWebSocketError(string message)
    {
        statusMessage = message;
    }

    private void HandleWebSocketMessage(BackendSocketEnvelope envelope)
    {
        if (envelope == null)
            return;

        switch (envelope.type)
        {
            case "PLAYER_JOINED":
                statusMessage = "A player connected to the room.";
                StartCoroutine(RefreshRoomCoroutine());
                break;
            case "PLAYER_DISCONNECTED":
                statusMessage = "A player disconnected from the room.";
                StartCoroutine(RefreshRoomCoroutine());
                break;
            case "PLAYER_READY":
                HandlePlayerReady(envelope.payload);
                break;
            case "GAME_START":
                HandleGameStart(envelope.payload);
                break;
            case "MOVE_RESULT":
                HandleMoveResult(envelope.requestId, envelope.payload);
                break;
            case "DRAW_OFFERED":
                HandleDrawOffered(envelope.payload);
                break;
            case "PLAYER_PAUSED":
                HandlePauseState(envelope.payload, true);
                break;
            case "PLAYER_RESUMED":
                HandlePauseState(envelope.payload, false);
                break;
            case "PAUSE_STATE":
            case "PAUSE_STATE_CHANGED":
                HandlePauseState(envelope.payload, null);
                break;
            case "GAME_STATE":
                HandleGameState(envelope.payload);
                break;
            case "GAME_OVER":
                HandleGameOver(envelope.payload);
                break;
            case "ERROR":
                HandleSocketErrorPayload(envelope.requestId, envelope.payload);
                break;
        }
    }

    private void HandlePlayerReady(JToken payloadToken)
    {
        BackendPlayerReadyPayload payload = payloadToken.ToObject<BackendPlayerReadyPayload>();
        readyCount = payload != null ? Mathf.Max(readyCount, payload.readyCount) : readyCount;
        statusMessage = payload == null
            ? "A player is ready."
            : $"{payload.username} is ready ({payload.readyCount}/2).";
    }

    private void HandleDrawOffered(JToken payloadToken)
    {
        BackendDrawOfferedPayload payload = payloadToken.ToObject<BackendDrawOfferedPayload>();
        opponentDrawOfferPending = payload != null &&
            !string.Equals(payload.offeredBy, PlayerAuthService.Username, StringComparison.OrdinalIgnoreCase);
        statusMessage = opponentDrawOfferPending
            ? $"{payload.offeredBy} offered a draw."
            : "Draw offer broadcast to room.";
    }

    private void HandlePauseState(JToken payloadToken, bool? forcedState)
    {
        BackendPauseStatePayload payload = payloadToken?.ToObject<BackendPauseStatePayload>();
        if (payload == null ||
            string.Equals(payload.userId, PlayerAuthService.UserId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(payload.username, PlayerAuthService.Username, StringComparison.OrdinalIgnoreCase))
            return;

        bool paused = forcedState ?? payload.paused;
        string opponentName = string.IsNullOrWhiteSpace(payload.username) ? "Opponent" : payload.username;
        statusMessage = paused ? $"{opponentName} paused the match." : $"{opponentName} resumed the match.";
        OpponentPauseChanged?.Invoke(paused, opponentName);
    }

    private void HandleGameStart(JToken payloadToken)
    {
        BackendGameStartPayload payload = payloadToken.ToObject<BackendGameStartPayload>();
        if (payload == null)
            return;

        currentGameStart = payload;
        currentMatchId = payload.matchId ?? string.Empty;
        lastConfirmedFen = payload.fen ?? string.Empty;
        opponentDrawOfferPending = false;
        turnSelectionUI.SetMatchPlayers(payload.whiteUsername, payload.blackUsername);
        PieceTeam localTeam = string.Equals(payload.whitePlayerId, PlayerAuthService.UserId, StringComparison.OrdinalIgnoreCase)
            ? PieceTeam.White
            : PieceTeam.Black;
        chessGame.BeginLanGame(PieceTeam.White, localTeam);
        chessGame.ApplyFenState(payload.fen);
        lanGameActive = true;
        showLanPanel = false;
        readyCount = 2;
        statusMessage = "Match started.";
        StartCoroutine(RefreshRoomCoroutine());
    }

    private void HandleMoveResult(string requestId, JToken payloadToken)
    {
        BackendMoveResultPayload payload = payloadToken.ToObject<BackendMoveResultPayload>();
        if (payload == null)
            return;

        bool isLocalMove = !string.IsNullOrWhiteSpace(requestId) && pendingLocalMoveRequestIds.Remove(requestId);
        lastConfirmedFen = payload.fen ?? lastConfirmedFen;

        chessGame.ApplyFenState(payload.fen);
        turnSelectionUI.SetLatestMoveText(
            string.IsNullOrWhiteSpace(payload.notation) ? $"{payload.from}-{payload.to}" : payload.notation,
            isLocalMove);
        statusMessage = string.IsNullOrWhiteSpace(payload.notation)
            ? $"Move {payload.moveNumber} accepted."
            : $"{(isLocalMove ? "Your" : "Opponent")} move {payload.moveNumber}: {payload.notation}";

        if (!string.Equals(payload.status, "ACTIVE", StringComparison.OrdinalIgnoreCase) && payload.gameOver != null)
            HandleGameOver(JToken.FromObject(payload.gameOver));
    }

    private void HandleGameState(JToken payloadToken)
    {
        BackendGameStatePayload payload = payloadToken.ToObject<BackendGameStatePayload>();
        if (payload == null)
            return;

        currentMatchId = payload.matchId ?? currentMatchId;
        if (!string.IsNullOrWhiteSpace(payload.fen))
        {
            lastConfirmedFen = payload.fen;
            if (!chessGame.GameStarted)
            {
                PieceTeam localTeam = string.Equals(payload.whitePlayerId, PlayerAuthService.UserId, StringComparison.OrdinalIgnoreCase)
                    ? PieceTeam.White
                    : PieceTeam.Black;
                chessGame.BeginLanGame(PieceTeam.White, localTeam);
                lanGameActive = string.Equals(payload.status, "ACTIVE", StringComparison.OrdinalIgnoreCase);
            }

            chessGame.ApplyFenState(payload.fen);
        }

        if (!string.Equals(payload.status, "ACTIVE", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(payload.result))
            chessGame.ApplyServerGameOver(payload.result, payload.reason);
    }

    private void HandleGameOver(JToken payloadToken)
    {
        BackendGameOverPayload payload = payloadToken.ToObject<BackendGameOverPayload>();
        if (payload == null)
            return;

        lanGameActive = false;
        opponentDrawOfferPending = false;
        OpponentPauseChanged?.Invoke(false, string.Empty);
        statusMessage = $"Game over: {payload.result} ({payload.reason})";
        chessGame.ApplyServerGameOver(payload.result, payload.reason);
        StartCoroutine(LoadMatchHistoryCoroutine());
        if (string.Equals(payload.reason, "RESIGNATION", StringComparison.OrdinalIgnoreCase))
            StartCoroutine(ReturnToMainMenuAfterResignation());
    }

    private IEnumerator ReturnToMainMenuAfterResignation()
    {
        yield return new WaitForSecondsRealtime(2.25f);
        DisconnectSocketIntentional();
        chessGame.RestartToMainMenu();
    }

    private void HandleSocketErrorPayload(string requestId, JToken payloadToken)
    {
        BackendSocketErrorPayload payload = payloadToken.ToObject<BackendSocketErrorPayload>();
        string message = payload != null && !string.IsNullOrWhiteSpace(payload.message)
            ? payload.message
            : "Socket request failed.";
        statusMessage = message;

        if (!string.IsNullOrWhiteSpace(requestId) && pendingLocalMoveRequestIds.Remove(requestId) && !string.IsNullOrWhiteSpace(lastConfirmedFen))
            chessGame.ApplyFenState(lastConfirmedFen);
    }

    private IEnumerator RefreshRoomCoroutine()
    {
        if (string.IsNullOrWhiteSpace(currentRoomCode))
            yield break;

        yield return BackendRestClient.Send<BackendRoomDto>(
            "GET",
            $"/api/rooms/{currentRoomCode}",
            null,
            true,
            response => currentRoom = response.result,
            (_, __) => { });
    }

    private void LeaveRoom()
    {
        ResetRoomState(clearRoomIdentity: true);
        statusMessage = "Left online room.";
        DisconnectSocketIntentional();
        showLanPanel = true;
        chessGame.RestartToMainMenu();
    }

    private void CopyRoomCodeToClipboard()
    {
        string codeToCopy = !string.IsNullOrWhiteSpace(currentRoomCode) ? currentRoomCode : roomCodeInput;
        if (string.IsNullOrWhiteSpace(codeToCopy))
        {
            statusMessage = "No room code to copy yet.";
            return;
        }

        GUIUtility.systemCopyBuffer = codeToCopy;
        statusMessage = $"Copied room code {codeToCopy}.";
    }

    private void EnsureWebSocketClient()
    {
        if (webSocketClient != null)
            return;

        webSocketClient = new BackendWebSocketClient();
        webSocketClient.Connected += HandleWebSocketConnected;
        webSocketClient.MessageReceived += HandleWebSocketMessage;
        webSocketClient.Closed += HandleWebSocketClosed;
        webSocketClient.Error += HandleWebSocketError;
    }

    private void ReleaseWebSocketClient()
    {
        if (webSocketClient == null)
            return;

        webSocketClient.Connected -= HandleWebSocketConnected;
        webSocketClient.MessageReceived -= HandleWebSocketMessage;
        webSocketClient.Closed -= HandleWebSocketClosed;
        webSocketClient.Error -= HandleWebSocketError;
        webSocketClient.Dispose();
        webSocketClient = null;
    }

    private void DisconnectSocketIntentional()
    {
        reconnectPending = false;
        requestInFlight = false;
        intentionalDisconnect = true;
        ReleaseWebSocketClient();
    }

    private void ResetRoomState(bool clearRoomIdentity)
    {
        lanGameActive = false;
        localReady = false;
        readyCount = 0;
        opponentDrawOfferPending = false;
        OpponentPauseChanged?.Invoke(false, string.Empty);
        pendingLocalMoveRequestIds.Clear();

        if (!clearRoomIdentity)
            return;

        currentRoom = null;
        currentRoomCode = string.Empty;
        currentMatchId = string.Empty;
        lastConfirmedFen = string.Empty;
    }

    private string GetRoomSummary()
    {
        if (currentRoom == null)
            return "Not in a room";

        return $"{currentRoom.roomCode} ({currentRoom.status})";
    }

    private string GetConnectionSummary()
    {
        if (webSocketClient == null)
            return "No socket";
        if (webSocketClient.IsConnected)
            return "Connected";
        if (reconnectPending)
            return "Reconnecting";
        return "Disconnected";
    }

    private bool CanSendGameCommand()
    {
        if (!lanGameActive || webSocketClient == null || !webSocketClient.IsConnected)
        {
            statusMessage = "You need an active connected match first.";
            return false;
        }

        return true;
    }

    private static string CreateRequestId(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    private static string ToSquare(Vector2Int boardPosition)
    {
        return $"{(char)('a' + boardPosition.x)}{(char)('1' + boardPosition.y)}";
    }

    private static bool TryParseSquare(string square, out Vector2Int boardPosition)
    {
        boardPosition = -Vector2Int.one;
        if (string.IsNullOrWhiteSpace(square) || square.Length != 2)
            return false;

        char file = char.ToLowerInvariant(square[0]);
        char rank = square[1];
        if (file < 'a' || file > 'h' || rank < '1' || rank > '8')
            return false;

        boardPosition = new Vector2Int(file - 'a', rank - '1');
        return true;
    }

    private static string ToPromotion(PieceType pieceType)
    {
        switch (pieceType)
        {
            case PieceType.Rook:
                return "ROOK";
            case PieceType.Bishop:
                return "BISHOP";
            case PieceType.Knight:
                return "KNIGHT";
            case PieceType.Queen:
            default:
                return "QUEEN";
        }
    }

    private static bool TryCreateLanMove(BackendMoveResultPayload payload, out ChessLanMove move)
    {
        move = default;
        if (payload == null || !TryParseSquare(payload.from, out Vector2Int from) || !TryParseSquare(payload.to, out Vector2Int to))
            return false;

        if (string.IsNullOrWhiteSpace(payload.promotion))
        {
            move = new ChessLanMove(from, to);
            return true;
        }

        PieceType promotionType = PieceType.Queen;
        switch (payload.promotion.ToUpperInvariant())
        {
            case "ROOK":
                promotionType = PieceType.Rook;
                break;
            case "BISHOP":
                promotionType = PieceType.Bishop;
                break;
            case "KNIGHT":
                promotionType = PieceType.Knight;
                break;
        }

        move = new ChessLanMove(from, to, promotionType);
        return true;
    }

    private static GUIStyle GetTitleStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold
        };
    }

    private static GUIStyle GetSectionStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
    }

    private static GUIStyle GetBodyStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            wordWrap = true
        };
    }

    private static GUIStyle GetHudStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
    }

    private static GUIStyle GetStatusStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Italic,
            wordWrap = true
        };
    }

    private static float GetGuiScale()
    {
        float widthScale = Screen.width / ReferenceWidth;
        float heightScale = Screen.height / ReferenceHeight;
        return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 1f, 2f);
    }

    private static Texture2D LoadProjectTexture(string projectRelativePath)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath);
        if (!File.Exists(fullPath))
            return null;

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            UnityEngine.Object.Destroy(texture);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(projectRelativePath);
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }
}
