using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum NetworkLobbyUiMode
{
    Lan,
    Multiplayer
}

public class ChessLanController : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float ReconnectDelaySeconds = 3f;
    private const float HeartbeatIntervalSeconds = 10f;

    private ChessGame chessGame;
    private ChessTurnSelectionUI turnSelectionUI;
    private BackendWebSocketClient webSocketClient;
    private ChessOrbitCamera orbitCamera;
    private readonly HashSet<string> pendingLocalMoveRequestIds = new HashSet<string>();
    private readonly List<BackendMatchDto> recentMatches = new List<BackendMatchDto>();
    private readonly HashSet<string> readyPlayerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> readyPlayerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private bool showLanPanel;
    private bool lanGameActive;
    private bool requestInFlight;
    private bool refreshInFlight;
    private bool reconnectPending;
    private bool startRequestInFlight;
    private NetworkLobbyUiMode lobbyMode = NetworkLobbyUiMode.Lan;
    private string roomCodeInput = string.Empty;
    private string statusMessage = "Connect to the Spring Boot backend, create a room, and wait for another player.";
    private string serverHealthStatus = "Offline";
    private string currentRoomCode = string.Empty;
    private string currentMatchId = string.Empty;
    private string lastConfirmedFen = string.Empty;
    private float reconnectAt;
    private float lastHeartbeatAt;
    private float nextRefreshAllowedAt;
    private float nextStartAllowedAt;
    private BackendRoomDto currentRoom;
    private BackendGameStartPayload currentGameStart;
    private bool localReady;
    private int readyCount;
    private bool opponentDrawOfferPending;
    private bool intentionalDisconnect;
    private Texture2D backButtonTexture;
    private NetworkLobbyUiController lobbyUi;

    public bool IsNetworkGameActive => lanGameActive;
    public event Action<bool, string> OpponentPauseChanged;

    public void Initialize(ChessGame newChessGame, ChessTurnSelectionUI newTurnSelectionUI)
    {
        chessGame = newChessGame;
        turnSelectionUI = newTurnSelectionUI;
        chessGame.MoveCommitted += HandleMoveCommitted;
        chessGame.ReturnedToMainMenu += HandleReturnedToMainMenu;
        backButtonTexture = LoadProjectTexture("Assets/Materials/Main_Menu/Back.png");
        CacheOrbitCamera();
        ApplyLobbyCameraState(immediate: true);
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

        if (startRequestInFlight && Time.unscaledTime >= nextStartAllowedAt + 3f)
            startRequestInFlight = false;

        if (showLanPanel && lobbyUi != null)
            lobbyUi.Refresh();
    }

    private void OnDestroy()
    {
        if (chessGame != null)
        {
            chessGame.MoveCommitted -= HandleMoveCommitted;
            chessGame.ReturnedToMainMenu -= HandleReturnedToMainMenu;
        }

        ReleaseWebSocketClient();
        DestroyLobbyUi();
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
        ShowLobby(NetworkLobbyUiMode.Lan);
    }

    public void ShowMultiplayerSetup()
    {
        ShowLobby(NetworkLobbyUiMode.Multiplayer);
    }

    private void ShowLobby(NetworkLobbyUiMode mode)
    {
        CacheOrbitCamera();
        EnsureWebSocketClient();
        lobbyMode = mode;
        showLanPanel = true;
        statusMessage = mode == NetworkLobbyUiMode.Lan
            ? "Host a LAN lobby or join by room code."
            : "Create or join an online multiplayer room.";
        roomCodeInput = currentRoomCode;
        EnsureLobbyUi();
        lobbyUi.Show(mode);
        ApplyLobbyCameraState(immediate: false);
        RequestRefreshLobby();
    }

    public void HideLanSetup()
    {
        showLanPanel = false;
        if (lobbyUi != null)
            lobbyUi.Hide();
        if (!lanGameActive)
            ApplyLobbyCameraState(immediate: false);
    }

    public void ResetForAuthenticationChange(bool authenticated = false)
    {
        showLanPanel = false;
        if (lobbyUi != null)
            lobbyUi.Hide();
        ResetRoomState(clearRoomIdentity: true);
        DisconnectSocketIntentional();
        recentMatches.Clear();
        roomCodeInput = string.Empty;
        statusMessage = authenticated
            ? "Signed in. Online multiplayer is ready."
            : "Sign in to use online multiplayer.";
        ApplyLobbyCameraState(immediate: false);
    }

    public void RequestCreateRoom()
    {
        if (!CanUseLobbyCommand("create a room"))
            return;

        if (currentRoom != null)
        {
            statusMessage = "You are already in a room. Leave it before creating another.";
            return;
        }

        StartCoroutine(CreateRoomCoroutine());
    }

    public void RequestJoinRoom(string rawRoomCode)
    {
        if (!CanUseLobbyCommand("join a room"))
            return;

        if (currentRoom != null)
        {
            statusMessage = "Leave the current room before joining another.";
            return;
        }

        roomCodeInput = NormalizeRoomCode(rawRoomCode);
        if (!IsValidRoomCode(roomCodeInput))
        {
            statusMessage = "Room code must be 6 letters or numbers.";
            return;
        }

        StartCoroutine(JoinRoomCoroutine());
    }

    public void RequestReady()
    {
        if (!CanUseLobbyCommand("ready up"))
            return;

        if (currentRoom == null || string.IsNullOrWhiteSpace(currentRoomCode))
        {
            statusMessage = "Create or join a room before readying up.";
            return;
        }

        if (!HasTwoPlayers())
        {
            statusMessage = "Waiting for another player before readying up.";
            return;
        }

        if (webSocketClient == null || !webSocketClient.IsConnected)
        {
            statusMessage = "Room socket is not connected yet.";
            return;
        }

        if (localReady)
        {
            statusMessage = "You are already ready.";
            return;
        }

        SendReady();
    }

    public void RequestStartGame()
    {
        if (!CanUseLobbyCommand("start the game"))
            return;

        if (Time.unscaledTime < nextStartAllowedAt)
        {
            statusMessage = "Start request is cooling down.";
            return;
        }

        if (currentRoom == null || string.IsNullOrWhiteSpace(currentRoomCode))
        {
            statusMessage = "Create or join a room before starting.";
            return;
        }

        if (!HasTwoPlayers())
        {
            statusMessage = "Need two players before starting.";
            return;
        }

        if (readyCount < 2)
        {
            statusMessage = $"Both players must be ready first ({readyCount}/2).";
            return;
        }

        if (webSocketClient == null || !webSocketClient.IsConnected)
        {
            statusMessage = "Room socket is not connected yet.";
            return;
        }

        nextStartAllowedAt = Time.unscaledTime + 2.5f;
        startRequestInFlight = true;
        _ = webSocketClient.SendAsync("START", CreateRequestId("start"), new { roomCode = currentRoomCode });
        _ = webSocketClient.SendAsync("SYNC_REQUEST", CreateRequestId("sync"), new { });
        statusMessage = "Start requested. Waiting for server confirmation...";
    }

    public void RequestRefreshLobby()
    {
        if (startRequestInFlight)
        {
            statusMessage = "Cannot refresh while a start request is pending.";
            return;
        }

        if (refreshInFlight || requestInFlight)
        {
            statusMessage = "Please wait for the current server request to finish.";
            return;
        }

        if (Time.unscaledTime < nextRefreshAllowedAt)
        {
            statusMessage = "Refresh is cooling down for a moment.";
            return;
        }

        nextRefreshAllowedAt = Time.unscaledTime + 1.75f;
        StartCoroutine(RefreshLobbyCoroutine());
    }

    public void RequestCopyRoomCode()
    {
        CopyRoomCodeToClipboard();
    }

    public void RequestBackToMultiplayerChoice()
    {
        ResetRoomState(clearRoomIdentity: true);
        DisconnectSocketIntentional();
        HideLanSetup();
        turnSelectionUI.ShowMultiplayerModeSelection();
    }

    public void RequestLeaveToModeSelection()
    {
        ResetRoomState(clearRoomIdentity: true);
        DisconnectSocketIntentional();
        HideLanSetup();
        turnSelectionUI.ShowTurnSelection();
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
        if (requestInFlight)
            yield break;

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
                serverHealthStatus = "Online";
                currentRoom = response.result;
                if (currentRoom == null)
                {
                    statusMessage = "Server did not return a room. Please try again.";
                    return;
                }

                currentRoomCode = currentRoom.roomCode ?? string.Empty;
                roomCodeInput = currentRoomCode;
                readyCount = 0;
                localReady = false;
                readyPlayerIds.Clear();
                readyPlayerNames.Clear();
                statusMessage = $"Room {currentRoomCode} created. Connecting to waiting room...";
                StartCoroutine(ConnectWebSocketCoroutine(false));
            },
            (message, _) =>
            {
                requestInFlight = false;
                serverHealthStatus = GetServerHealthFromError(message);
                statusMessage = $"Could not create room: {message}";
            });
    }

    private IEnumerator JoinRoomCoroutine()
    {
        if (requestInFlight)
            yield break;

        roomCodeInput = NormalizeRoomCode(roomCodeInput);
        if (!IsValidRoomCode(roomCodeInput))
        {
            statusMessage = "Enter a valid 6-character room code first.";
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
                serverHealthStatus = "Online";
                currentRoom = response.result;
                if (currentRoom == null)
                {
                    statusMessage = "Server did not return the joined room. Please try again.";
                    return;
                }

                currentRoomCode = currentRoom.roomCode ?? roomCodeInput;
                roomCodeInput = currentRoomCode;
                readyCount = 0;
                localReady = false;
                readyPlayerIds.Clear();
                readyPlayerNames.Clear();
                statusMessage = $"Joined room {currentRoomCode}. Connecting to waiting room...";
                StartCoroutine(ConnectWebSocketCoroutine(false));
            },
            (message, _) =>
            {
                requestInFlight = false;
                serverHealthStatus = GetServerHealthFromError(message);
                statusMessage = $"Join failed. Check the code or connection: {message}";
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
        {
            serverHealthStatus = "Offline";
            statusMessage = "Unable to connect to room socket.";
        }
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
                serverHealthStatus = "Online";
                recentMatches.Clear();
                if (response.result != null)
                    recentMatches.AddRange(response.result);
            },
            (message, _) =>
            {
                serverHealthStatus = GetServerHealthFromError(message);
            });
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
                serverHealthStatus = "Online";
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
                if (lanGameActive)
                    ApplyActiveMatchCameraState(localTeam, immediate: true);
                else
                    ApplyLobbyCameraState(immediate: false);
                statusMessage = $"Recovered active match in room {currentRoomCode}.";
                StartCoroutine(RefreshRoomCoroutine());

                if (!string.IsNullOrWhiteSpace(currentRoomCode) && (webSocketClient == null || !webSocketClient.IsConnected) && !requestInFlight)
                    StartCoroutine(ConnectWebSocketCoroutine(true));
            },
            (_, error) =>
            {
                if (error != null && error.code != 4004)
                {
                    serverHealthStatus = "Offline";
                    statusMessage = "Unable to load active match.";
                }
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
        MarkPlayerReady(PlayerAuthService.UserId, PlayerAuthService.Username);
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
        ApplyLobbyCameraState(immediate: false);
    }

    private void HandleWebSocketConnected()
    {
        requestInFlight = false;
        serverHealthStatus = "Online";
        reconnectPending = false;
        statusMessage = $"Connected to room {currentRoomCode}.";
        lastHeartbeatAt = Time.unscaledTime;
        _ = webSocketClient.SendAsync("SYNC_REQUEST", CreateRequestId("sync"), new { });
        StartCoroutine(LoadActiveMatchCoroutine());
    }

    private void HandleWebSocketClosed(string message)
    {
        requestInFlight = false;
        startRequestInFlight = false;

        if (intentionalDisconnect)
        {
            intentionalDisconnect = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(currentRoomCode) || !PlayerAuthService.IsAuthenticated)
            return;

        statusMessage = message;
        serverHealthStatus = "Offline";
        reconnectPending = true;
        reconnectAt = Time.unscaledTime + ReconnectDelaySeconds;
    }

    private void HandleWebSocketError(string message)
    {
        startRequestInFlight = false;
        serverHealthStatus = GetServerHealthFromError(message);
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
        if (payload != null)
            MarkPlayerReady(payload.userId, payload.username);
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
        ApplyActiveMatchCameraState(localTeam, immediate: true);
        lanGameActive = true;
        showLanPanel = false;
        if (lobbyUi != null)
            lobbyUi.Hide();
        readyCount = 2;
        startRequestInFlight = false;
        serverHealthStatus = "Online";
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
            PieceTeam localTeam = string.Equals(payload.whitePlayerId, PlayerAuthService.UserId, StringComparison.OrdinalIgnoreCase)
                ? PieceTeam.White
                : PieceTeam.Black;
            if (!chessGame.GameStarted)
            {
                chessGame.BeginLanGame(PieceTeam.White, localTeam);
                lanGameActive = string.Equals(payload.status, "ACTIVE", StringComparison.OrdinalIgnoreCase);
            }

            chessGame.ApplyFenState(payload.fen);
            if (string.Equals(payload.status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                ApplyActiveMatchCameraState(localTeam, immediate: false);
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
        if (!string.IsNullOrWhiteSpace(requestId) && requestId.StartsWith("start-", StringComparison.OrdinalIgnoreCase))
            startRequestInFlight = false;

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
            response =>
            {
                serverHealthStatus = "Online";
                if (response.result != null)
                    currentRoom = response.result;
            },
            (message, _) =>
            {
                serverHealthStatus = GetServerHealthFromError(message);
                statusMessage = $"Refresh failed: {message}";
            });
    }

    private IEnumerator RefreshLobbyCoroutine()
    {
        refreshInFlight = true;
        statusMessage = string.IsNullOrWhiteSpace(currentRoomCode)
            ? "Checking server and match history..."
            : $"Refreshing room {currentRoomCode}...";

        if (!string.IsNullOrWhiteSpace(currentRoomCode))
            yield return RefreshRoomCoroutine();

        if (lobbyMode == NetworkLobbyUiMode.Multiplayer)
            yield return LoadMatchHistoryCoroutine();

        yield return LoadActiveMatchCoroutine();

        refreshInFlight = false;
        if (string.IsNullOrWhiteSpace(statusMessage) || statusMessage.StartsWith("Checking", StringComparison.OrdinalIgnoreCase) ||
            statusMessage.StartsWith("Refreshing", StringComparison.OrdinalIgnoreCase))
        {
            statusMessage = "Lobby refreshed.";
        }
    }

    private void LeaveRoom()
    {
        bool wasActiveMatch = lanGameActive || (chessGame != null && chessGame.GameStarted);
        ResetRoomState(clearRoomIdentity: true);
        statusMessage = "Left online room.";
        DisconnectSocketIntentional();
        showLanPanel = false;
        if (lobbyUi != null)
            lobbyUi.Hide();
        ApplyLobbyCameraState(immediate: false);
        if (wasActiveMatch)
            chessGame.RestartToMainMenu();
        else
            turnSelectionUI.ShowTurnSelection();
    }

    private void CopyRoomCodeToClipboard()
    {
        string codeToCopy = currentRoomCode;
        if (string.IsNullOrWhiteSpace(codeToCopy))
        {
            statusMessage = "No room code to copy yet.";
            return;
        }

        GUIUtility.systemCopyBuffer = codeToCopy;
        statusMessage = $"Copied room code {codeToCopy}.";
    }

    private bool CanUseLobbyCommand(string action)
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            serverHealthStatus = "Offline";
            statusMessage = $"Cannot {action}: network is offline.";
            return false;
        }

        if (!PlayerAuthService.CanUseOnlineFeatures)
        {
            statusMessage = $"Cannot {action}: please log in first.";
            return false;
        }

        if (requestInFlight || refreshInFlight)
        {
            statusMessage = $"Please wait before trying to {action}.";
            return false;
        }

        if (startRequestInFlight)
        {
            statusMessage = "Start request is already in progress.";
            return false;
        }

        return true;
    }

    private bool HasTwoPlayers()
    {
        return currentRoom != null &&
               !string.IsNullOrWhiteSpace(currentRoom.hostUsername) &&
               !string.IsNullOrWhiteSpace(currentRoom.guestUsername);
    }

    private void MarkPlayerReady(string userId, string username)
    {
        if (!string.IsNullOrWhiteSpace(userId))
            readyPlayerIds.Add(userId);
        if (!string.IsNullOrWhiteSpace(username))
            readyPlayerNames.Add(username);
    }

    private bool IsPlayerReady(string userId, string username, bool localPlayer)
    {
        if (localPlayer && localReady)
            return true;
        if (!string.IsNullOrWhiteSpace(userId) && readyPlayerIds.Contains(userId))
            return true;
        if (!string.IsNullOrWhiteSpace(username) && readyPlayerNames.Contains(username))
            return true;
        return readyCount >= 2;
    }

    private string GetServerHealth()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
            return "Offline";
        if (requestInFlight || refreshInFlight)
            return "Checking";
        if (webSocketClient != null && webSocketClient.IsConnected)
            return "Online";
        if (reconnectPending)
            return "Reconnecting";
        return string.IsNullOrWhiteSpace(serverHealthStatus) ? "Offline" : serverHealthStatus;
    }

    private string GetServerHealthFromError(string message)
    {
        if (!string.IsNullOrWhiteSpace(message) && message.IndexOf("maintenance", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Maintenance";
        return Application.internetReachability == NetworkReachability.NotReachable ? "Offline" : "Offline";
    }

    private string GetHostPlayerLine()
    {
        if (currentRoom == null || string.IsNullOrWhiteSpace(currentRoom.hostUsername))
            return "Waiting for host";

        bool localPlayer = IsLocalPlayer(currentRoom.hostId, currentRoom.hostUsername);
        string status = IsPlayerReady(currentRoom.hostId, currentRoom.hostUsername, localPlayer) ? "Ready" : "In room";
        return $"{LimitName(currentRoom.hostUsername, 20)} - {status}";
    }

    private string GetGuestPlayerLine()
    {
        if (currentRoom == null || string.IsNullOrWhiteSpace(currentRoom.guestUsername))
            return "Waiting for player";

        bool localPlayer = IsLocalPlayer(currentRoom.guestId, currentRoom.guestUsername);
        string status = IsPlayerReady(currentRoom.guestId, currentRoom.guestUsername, localPlayer) ? "Ready" : "In room";
        return $"{LimitName(currentRoom.guestUsername, 20)} - {status}";
    }

    private bool IsLocalPlayer(string userId, string username)
    {
        return (!string.IsNullOrWhiteSpace(userId) &&
                string.Equals(userId, PlayerAuthService.UserId, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrWhiteSpace(username) &&
                string.Equals(username, PlayerAuthService.Username, StringComparison.OrdinalIgnoreCase));
    }

    private string GetLobbyRoomCode()
    {
        return string.IsNullOrWhiteSpace(currentRoomCode) ? string.Empty : currentRoomCode;
    }

    private string GetRecentMatchLine(int index)
    {
        if (recentMatches.Count == 0)
            return index == 0 ? "No recent matches." : string.Empty;
        if (index < 0 || index >= recentMatches.Count || index >= 3)
            return string.Empty;

        BackendMatchDto match = recentMatches[index];
        string white = LimitName(match.whiteUsername, 10);
        string black = LimitName(match.blackUsername, 10);
        string result = string.IsNullOrWhiteSpace(match.winnerUsername)
            ? match.status
            : $"{LimitName(match.winnerUsername, 10)} won";
        return $"{white} vs {black} | {LimitName(result, 16)}";
    }

    private static string NormalizeRoomCode(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        return raw.Trim().ToUpperInvariant();
    }

    private static bool IsValidRoomCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            return false;
        for (int i = 0; i < code.Length; i++)
            if (!char.IsLetterOrDigit(code[i]))
                return false;
        return true;
    }

    private static string LimitName(string value, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        string trimmed = value.Trim();
        if (trimmed.Length <= maxCharacters)
            return trimmed;
        return trimmed.Substring(0, Mathf.Max(0, maxCharacters - 3)) + "...";
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

    private void CacheOrbitCamera()
    {
        if (orbitCamera == null)
            orbitCamera = FindAnyObjectByType<ChessOrbitCamera>();
    }

    private void ApplyLobbyCameraState(bool immediate)
    {
        CacheOrbitCamera();
        if (orbitCamera == null)
            return;

        // Trong menu va waiting room, camera chi orbit quanh tam ban de tranh khoa nham vao quan.
        orbitCamera.SetAllowPieceLock(false);
        orbitCamera.ResetToBoardView(immediate);
    }

    private void ApplyActiveMatchCameraState(PieceTeam localTeam, bool immediate)
    {
        CacheOrbitCamera();
        if (orbitCamera == null)
            return;

        orbitCamera.SetAllowPieceLock(true);
        if (!immediate)
            return;

        // Chi dua goc nhin ve phia nguoi choi khi vua vao/recover tran.
        // Cac lan sync GAME_STATE giua tran khong duoc keo camera ve goc mac dinh.
        orbitCamera.ConfigureForPlayerSide(localTeam, true);
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

    private void EnsureLobbyUi()
    {
        if (lobbyUi != null)
            return;

        lobbyUi = new NetworkLobbyUiController(this);
    }

    private void DestroyLobbyUi()
    {
        if (lobbyUi == null)
            return;

        lobbyUi.Destroy();
        lobbyUi = null;
    }

    private sealed class NetworkLobbyUiController
    {
        private const float DesignWidth = 1672f;
        private const float DesignHeight = 941f;
        private static readonly Vector2 DesignSize = new Vector2(DesignWidth, DesignHeight);

        private readonly ChessLanController owner;
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly List<UnityEngine.Object> runtimeAssets = new List<UnityEngine.Object>();
        private readonly TextMeshProUGUI[] playerLabels = new TextMeshProUGUI[2];
        private readonly TextMeshProUGUI[] recentLabels = new TextMeshProUGUI[3];

        private GameObject canvasRoot;
        private RectTransform contentRoot;
        private TMP_InputField joinInput;
        private TextMeshProUGUI roomCodeLabel;
        private TextMeshProUGUI statusLabel;
        private TextMeshProUGUI serverLabel;
        private NetworkLobbyUiMode currentMode;

        public NetworkLobbyUiController(ChessLanController newOwner)
        {
            owner = newOwner;
            BuildCanvas();
        }

        public void Show(NetworkLobbyUiMode mode)
        {
            currentMode = mode;
            canvasRoot.SetActive(true);
            Rebuild();
            Refresh();
        }

        public void Hide()
        {
            if (canvasRoot)
                canvasRoot.SetActive(false);
        }

        public void Refresh()
        {
            if (!canvasRoot || !canvasRoot.activeSelf)
                return;

            if (roomCodeLabel)
                roomCodeLabel.text = string.IsNullOrWhiteSpace(owner.GetLobbyRoomCode()) ? "------" : owner.GetLobbyRoomCode();

            if (joinInput && !joinInput.isFocused && string.IsNullOrWhiteSpace(joinInput.text) && !string.IsNullOrWhiteSpace(owner.roomCodeInput))
                joinInput.SetTextWithoutNotify(owner.roomCodeInput);

            if (playerLabels[0])
                playerLabels[0].text = owner.GetHostPlayerLine();
            if (playerLabels[1])
                playerLabels[1].text = owner.GetGuestPlayerLine();
            if (statusLabel)
                statusLabel.text = owner.statusMessage ?? string.Empty;

            if (serverLabel)
            {
                string health = owner.GetServerHealth();
                serverLabel.text = health;
                serverLabel.color = GetServerColor(health);
            }

            for (int i = 0; i < recentLabels.Length; i++)
                if (recentLabels[i])
                    recentLabels[i].text = owner.GetRecentMatchLine(i);
        }

        public void Destroy()
        {
            if (canvasRoot)
                UnityEngine.Object.Destroy(canvasRoot);

            for (int i = 0; i < runtimeAssets.Count; i++)
                if (runtimeAssets[i])
                    UnityEngine.Object.Destroy(runtimeAssets[i]);

            runtimeAssets.Clear();
            sprites.Clear();
        }

        private void BuildCanvas()
        {
            canvasRoot = new GameObject("Network Lobby UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 64;

            ResponsiveUi.ConfigureCanvasScaler(canvasRoot.GetComponent<CanvasScaler>(), DesignSize);
            contentRoot = CreateChild(canvasRoot.transform, "Network Lobby Content", Vector2.zero, DesignSize);
            canvasRoot.SetActive(false);
        }

        private void Rebuild()
        {
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(contentRoot.GetChild(i).gameObject);

            for (int i = 0; i < playerLabels.Length; i++)
                playerLabels[i] = null;
            for (int i = 0; i < recentLabels.Length; i++)
                recentLabels[i] = null;

            joinInput = null;
            roomCodeLabel = null;
            statusLabel = null;
            serverLabel = null;

            if (currentMode == NetworkLobbyUiMode.Lan)
                BuildLan();
            else
                BuildMultiplayer();
        }

        private void BuildLan()
        {
            AddImage(contentRoot, "LAN Background", LoadSprite("LANUIBlank.png"), Vector2.zero, DesignSize);
            AddButton("Host", "HostButton.png", D(410f, 390f), new Vector2(360f, 142f), owner.RequestCreateRoom);
            joinInput = AddInput("Join Code Input", D(815f, 354f), new Vector2(330f, 58f));
            AddButton("Join", "JoinButton.png", D(815f, 440f), new Vector2(360f, 112f), () => owner.RequestJoinRoom(joinInput != null ? joinInput.text : string.Empty));
            roomCodeLabel = AddText("Room Code Value", D(838f, 612f), new Vector2(310f, 66f), 34f, TextAlignmentOptions.Center, Color.black);
            AddButton("Copy", "CopyIcon.png", D(988f, 612f), new Vector2(92f, 92f), owner.RequestCopyRoomCode, 1.08f);
            playerLabels[0] = AddText("Host Player", D(1316f, 353f), new Vector2(300f, 44f), 25f, TextAlignmentOptions.MidlineLeft, Color.black);
            playerLabels[1] = AddText("Guest Player", D(1316f, 460f), new Vector2(300f, 44f), 25f, TextAlignmentOptions.MidlineLeft, Color.black);
            statusLabel = AddText("Lobby Status", D(842f, 760f), new Vector2(890f, 48f), 25f, TextAlignmentOptions.Center, new Color(0.1f, 0.08f, 0.06f, 0.9f));

            AddButton("Ready", "ReadyButton.png", D(325f, 866f), new Vector2(270f, 108f), owner.RequestReady);
            AddButton("Start", "StartButton.png", D(576f, 866f), new Vector2(270f, 108f), owner.RequestStartGame);
            AddButton("Refresh", "RefreshButton.png", D(835f, 866f), new Vector2(270f, 108f), owner.RequestRefreshLobby);
            AddButton("Leave", "LeaveButton.png", D(1096f, 866f), new Vector2(270f, 108f), owner.RequestLeaveToModeSelection);
            AddButton("Back", "BackButton.png", D(1356f, 866f), new Vector2(270f, 108f), owner.RequestBackToMultiplayerChoice);
        }

        private void BuildMultiplayer()
        {
            AddImage(contentRoot, "Multiplayer Background", LoadSprite("MultiplayerUIBlank.png"), Vector2.zero, DesignSize);
            AddButton("Create Room", "CreateRoomButton.png", D(382f, 337f), new Vector2(360f, 142f), owner.RequestCreateRoom);
            joinInput = AddInput("Join Code Input", D(807f, 310f), new Vector2(340f, 58f));
            AddButton("Join Room", "JoinRoomButton.png", D(807f, 392f), new Vector2(360f, 112f), () => owner.RequestJoinRoom(joinInput != null ? joinInput.text : string.Empty));
            roomCodeLabel = AddText("Room Code Value", D(702f, 620f), new Vector2(310f, 66f), 34f, TextAlignmentOptions.Center, Color.black);
            AddButton("Copy", "CopyIcon.png", D(808f, 620f), new Vector2(92f, 92f), owner.RequestCopyRoomCode, 1.08f);
            playerLabels[0] = AddText("Host Player", D(1310f, 303f), new Vector2(300f, 44f), 25f, TextAlignmentOptions.MidlineLeft, Color.black);
            playerLabels[1] = AddText("Guest Player", D(1310f, 416f), new Vector2(300f, 44f), 25f, TextAlignmentOptions.MidlineLeft, Color.black);
            recentLabels[0] = AddText("Recent Match 0", D(1320f, 554f), new Vector2(335f, 42f), 22f, TextAlignmentOptions.Center, Color.black);
            recentLabels[1] = AddText("Recent Match 1", D(1320f, 636f), new Vector2(335f, 42f), 22f, TextAlignmentOptions.Center, Color.black);
            recentLabels[2] = AddText("Recent Match 2", D(1320f, 716f), new Vector2(335f, 42f), 22f, TextAlignmentOptions.Center, Color.black);
            statusLabel = AddText("Lobby Status", D(836f, 777f), new Vector2(1120f, 46f), 25f, TextAlignmentOptions.Center, new Color(0.1f, 0.08f, 0.06f, 0.9f));
            serverLabel = AddText("Server Health", D(1452f, 93f), new Vector2(200f, 46f), 28f, TextAlignmentOptions.Center, Color.black);

            AddButton("Ready", "ReadyButton.png", D(180f, 866f), new Vector2(270f, 108f), owner.RequestReady);
            AddButton("Start Game", "StartGameButton.png", D(486f, 866f), new Vector2(330f, 108f), owner.RequestStartGame);
            AddButton("Refresh", "RefreshButton.png", D(815f, 866f), new Vector2(270f, 108f), owner.RequestRefreshLobby);
            AddButton("Leave Room", "LeaveRoomButton.png", D(1142f, 866f), new Vector2(330f, 108f), owner.RequestLeaveToModeSelection);
            AddButton("Back", "BackButton.png", D(1452f, 866f), new Vector2(270f, 108f), owner.RequestBackToMultiplayerChoice);
        }

        private Button AddButton(string name, string spriteName, Vector2 position, Vector2 size, Action action, float hoverScale = 1.035f)
        {
            Image image = AddImage(contentRoot, name, LoadSprite(spriteName), position, size);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            button.onClick.AddListener(() => action?.Invoke());

            HandDrawnPressable pressable = image.gameObject.AddComponent<HandDrawnPressable>();
            pressable.Configure(hoverScale, 0.965f, 1.1f, new Color(1f, 0.97f, 0.74f, 1f));
            return button;
        }

        private TMP_InputField AddInput(string name, Vector2 position, Vector2 size)
        {
            GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            RectTransform rect = inputObject.GetComponent<RectTransform>();
            rect.SetParent(contentRoot, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image background = inputObject.GetComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.01f);
            background.raycastTarget = true;

            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            input.characterLimit = 6;
            input.contentType = TMP_InputField.ContentType.Alphanumeric;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;

            TextMeshProUGUI text = AddText(rect, "Text", Vector2.zero, size - new Vector2(28f, 8f), 34f, TextAlignmentOptions.Center, Color.black);
            input.textViewport = rect;
            input.textComponent = text;
            input.onValueChanged.AddListener(value =>
            {
                string normalized = NormalizeRoomCode(value);
                if (!string.Equals(value, normalized, StringComparison.Ordinal))
                    input.SetTextWithoutNotify(normalized);
                owner.roomCodeInput = normalized;
            });
            return input;
        }

        private Image AddImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private TextMeshProUGUI AddText(string name, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            return AddText(contentRoot, name, position, size, fontSize, alignment, color);
        }

        private TextMeshProUGUI AddText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = ChessFontCatalog.TmpFont != null ? ChessFontCatalog.TmpFont : TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontSizeMin = Mathf.Max(13f, fontSize * 0.58f);
            text.fontSizeMax = fontSize;
            text.enableAutoSizing = true;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private RectTransform CreateChild(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private Sprite LoadSprite(params string[] names)
        {
            string folder = currentMode == NetworkLobbyUiMode.Lan
                ? "Assets/Materials/LANUI"
                : "Assets/Materials/MultiplayerUI";

            for (int i = 0; i < names.Length; i++)
            {
                string key = $"{folder}/{names[i]}";
                if (sprites.TryGetValue(key, out Sprite cached))
                    return cached;

                string fullPath = Path.Combine(Directory.GetCurrentDirectory(), folder, names[i]);
                if (!File.Exists(fullPath))
                    continue;

                byte[] bytes = File.ReadAllBytes(fullPath);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    continue;
                }

                texture.name = Path.GetFileNameWithoutExtension(names[i]);
                texture.filterMode = FilterMode.Bilinear;
                Rect spriteRect = ShouldTrimSprite(names[i])
                    ? GetAlphaBounds(texture, 8)
                    : new Rect(0f, 0f, texture.width, texture.height);
                Sprite sprite = Sprite.Create(texture, spriteRect, new Vector2(0.5f, 0.5f), 100f);
                sprite.name = texture.name;
                runtimeAssets.Add(texture);
                runtimeAssets.Add(sprite);
                sprites[key] = sprite;
                return sprite;
            }

            return null;
        }

        private static bool ShouldTrimSprite(string spriteName)
        {
            return !spriteName.EndsWith("Blank.png", StringComparison.OrdinalIgnoreCase) &&
                   !spriteName.EndsWith("Design.png", StringComparison.OrdinalIgnoreCase);
        }

        private static Rect GetAlphaBounds(Texture2D texture, int padding)
        {
            Color32[] pixels = texture.GetPixels32();
            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < texture.height; y++)
            {
                int row = y * texture.width;
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[row + x].a <= 8)
                        continue;

                    if (x < minX)
                        minX = x;
                    if (y < minY)
                        minY = y;
                    if (x > maxX)
                        maxX = x;
                    if (y > maxY)
                        maxY = y;
                }
            }

            if (maxX < minX || maxY < minY)
                return new Rect(0f, 0f, texture.width, texture.height);

            minX = Mathf.Max(0, minX - padding);
            minY = Mathf.Max(0, minY - padding);
            maxX = Mathf.Min(texture.width - 1, maxX + padding);
            maxY = Mathf.Min(texture.height - 1, maxY + padding);
            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static Vector2 D(float x, float y)
        {
            return new Vector2(x - DesignWidth * 0.5f, DesignHeight * 0.5f - y);
        }

        private static Color GetServerColor(string health)
        {
            if (string.Equals(health, "Online", StringComparison.OrdinalIgnoreCase))
                return new Color(0.06f, 0.45f, 0.16f, 1f);
            if (string.Equals(health, "Maintenance", StringComparison.OrdinalIgnoreCase))
                return new Color(0.85f, 0.48f, 0.04f, 1f);
            if (string.Equals(health, "Checking", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(health, "Reconnecting", StringComparison.OrdinalIgnoreCase))
                return new Color(0.1f, 0.28f, 0.78f, 1f);
            return new Color(0.72f, 0.08f, 0.08f, 1f);
        }
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
        return ResponsiveUi.GetFitScale(ReferenceWidth, ReferenceHeight);
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
