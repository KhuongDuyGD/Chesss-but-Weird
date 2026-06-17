using System;
using UnityEngine;

public class ChessOnlineController : MonoBehaviour
{
    private const int DefaultPort = 19848;
    private const string DefaultServerAddress = "127.0.0.1";
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    private ChessGame chessGame;
    private ChessTurnSelectionUI turnSelectionUI;
    private ChessOnlineSession session;
    private bool showOnlinePanel;
    private bool onlineGameActive;
    private string serverAddress = DefaultServerAddress;
    private string portText = DefaultPort.ToString();
    private string roomCodeText = string.Empty;
    private string statusMessage = "Create or join an online room through a relay server.";
    private float guiWidth = ReferenceWidth;
    private float guiHeight = ReferenceHeight;

    public void Initialize(ChessGame newChessGame, ChessTurnSelectionUI newTurnSelectionUI)
    {
        chessGame = newChessGame;
        turnSelectionUI = newTurnSelectionUI;
        session = gameObject.AddComponent<ChessOnlineSession>();
        session.StateChanged += HandleSessionStateChanged;
        session.StartGameRequested += HandleStartGameRequested;
        session.MoveReceived += HandleMoveReceived;
        session.PeerDisconnected += HandlePeerDisconnected;
        chessGame.MoveCommitted += HandleMoveCommitted;
        chessGame.ReturnedToMainMenu += HandleReturnedToMainMenu;
    }

    private void OnDestroy()
    {
        if (session != null)
        {
            session.StateChanged -= HandleSessionStateChanged;
            session.StartGameRequested -= HandleStartGameRequested;
            session.MoveReceived -= HandleMoveReceived;
            session.PeerDisconnected -= HandlePeerDisconnected;
        }

        if (chessGame != null)
        {
            chessGame.MoveCommitted -= HandleMoveCommitted;
            chessGame.ReturnedToMainMenu -= HandleReturnedToMainMenu;
        }
    }

    private void OnGUI()
    {
        Matrix4x4 previousMatrix = GUI.matrix;
        float guiScale = GetGuiScale();
        guiWidth = Screen.width / guiScale;
        guiHeight = Screen.height / guiScale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1f));

        try
        {
            if (!showOnlinePanel || onlineGameActive)
            {
                DrawInGameOnlineHud();
                return;
            }

            float panelWidth = Mathf.Clamp(guiWidth * 0.50f, 780f, 1020f);
            float panelHeight = Mathf.Clamp(guiHeight * 0.62f, 600f, 760f);
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

            GUI.Label(new Rect(panelRect.x + 36f, panelRect.y + 24f, panelRect.width - 72f, 48f), "Online Multiplayer", GetTitleStyle());
            GUI.Label(
                new Rect(panelRect.x + 36f, panelRect.y + 78f, panelRect.width - 72f, 64f),
                "Room-code matchmaking through a simple relay server.",
                GetBodyStyle());
            GUI.Label(new Rect(panelRect.x + 36f, panelRect.y + 142f, panelRect.width - 72f, 72f), statusMessage, GetStatusStyle());

            DrawSessionInfo(panelRect);
            DrawServerField(panelRect);
            DrawPortField(panelRect);
            DrawRoomCodeField(panelRect);
            DrawActionButtons(panelRect);
            DrawInGameOnlineHud();
        }
        finally
        {
            GUI.matrix = previousMatrix;
        }
    }

    public void ShowOnlineSetup()
    {
        showOnlinePanel = true;
        statusMessage = session != null ? session.StatusMessage : statusMessage;
    }

    public void HideOnlineSetup()
    {
        showOnlinePanel = false;
    }

    private void DrawServerField(Rect panelRect)
    {
        Rect labelRect = new Rect(panelRect.x + 36f, panelRect.y + 366f, 150f, 36f);
        Rect fieldRect = new Rect(panelRect.x + 190f, panelRect.y + 362f, panelRect.width - 226f, 38f);
        GUI.Label(labelRect, "Server", GetBodyStyle());
        serverAddress = GUI.TextField(fieldRect, serverAddress, 128);
    }

    private void DrawPortField(Rect panelRect)
    {
        Rect labelRect = new Rect(panelRect.x + 36f, panelRect.y + 416f, 150f, 36f);
        Rect fieldRect = new Rect(panelRect.x + 190f, panelRect.y + 412f, 160f, 38f);
        GUI.Label(labelRect, "Port", GetBodyStyle());
        portText = GUI.TextField(fieldRect, portText, 8);
    }

    private void DrawRoomCodeField(Rect panelRect)
    {
        Rect labelRect = new Rect(panelRect.x + 36f, panelRect.y + 466f, 150f, 36f);
        Rect fieldRect = new Rect(panelRect.x + 190f, panelRect.y + 462f, 210f, 38f);
        GUI.Label(labelRect, "Room Code", GetBodyStyle());
        roomCodeText = GUI.TextField(fieldRect, roomCodeText, 12).Trim().ToUpperInvariant();
    }

    private void DrawActionButtons(Rect panelRect)
    {
        Rect firstButtonRect = new Rect(panelRect.x + 36f, panelRect.y + panelRect.height - 98f, 220f, 52f);
        Rect secondButtonRect = new Rect(panelRect.x + 274f, panelRect.y + panelRect.height - 98f, 220f, 52f);
        Rect thirdButtonRect = new Rect(panelRect.x + panelRect.width - 226f, panelRect.y + panelRect.height - 98f, 190f, 52f);

        switch (session.State)
        {
            case ChessOnlineSessionState.Idle:
            case ChessOnlineSessionState.Error:
                if (GUI.Button(firstButtonRect, "Create Room"))
                    CreateRoom();

                if (GUI.Button(secondButtonRect, "Join Room"))
                    JoinRoom();

                if (GUI.Button(thirdButtonRect, "Back"))
                    BackToMultiplayerModes();
                break;

            case ChessOnlineSessionState.Connecting:
            case ChessOnlineSessionState.Connected:
                GUI.Label(firstButtonRect, "Connecting...", GetBodyStyle());

                if (GUI.Button(secondButtonRect, "Cancel"))
                    session.Disconnect();

                if (GUI.Button(thirdButtonRect, "Back"))
                    BackToMultiplayerModes();
                break;

            case ChessOnlineSessionState.InRoom:
                DrawInRoomButtons(firstButtonRect, secondButtonRect, thirdButtonRect);
                break;
        }
    }

    private void DrawInRoomButtons(Rect firstButtonRect, Rect secondButtonRect, Rect thirdButtonRect)
    {
        if (session.Role == ChessOnlineSessionRole.Host)
        {
            if (session.HasPeer)
            {
                if (GUI.Button(firstButtonRect, "Start As White"))
                    StartOnlineMatchAsHost(PieceTeam.White);

                if (GUI.Button(secondButtonRect, "Start As Black"))
                    StartOnlineMatchAsHost(PieceTeam.Black);
            }
            else
            {
                GUI.Label(firstButtonRect, "Waiting for peer...", GetBodyStyle());
                GUI.Label(secondButtonRect, $"Code: {session.RoomCode}", GetBodyStyle());
            }
        }
        else
        {
            GUI.Label(firstButtonRect, "Waiting for host...", GetBodyStyle());
            GUI.Label(secondButtonRect, $"Code: {session.RoomCode}", GetBodyStyle());
        }

        if (GUI.Button(thirdButtonRect, "Disconnect"))
            session.Disconnect();
    }

    private void CreateRoom()
    {
        if (!TryParseConnection(out int port))
            return;

        statusMessage = "Creating online room...";
        session.CreateRoom(serverAddress.Trim(), port);
    }

    private void JoinRoom()
    {
        if (!TryParseConnection(out int port))
            return;

        if (string.IsNullOrWhiteSpace(roomCodeText))
        {
            statusMessage = "Enter a room code to join.";
            return;
        }

        statusMessage = $"Joining online room {roomCodeText}...";
        session.JoinRoom(serverAddress.Trim(), port, roomCodeText);
    }

    private bool TryParseConnection(out int port)
    {
        port = 0;
        if (string.IsNullOrWhiteSpace(serverAddress))
        {
            statusMessage = "Enter a valid server address.";
            return false;
        }

        if (!int.TryParse(portText, out port) || port < 1 || port > 65535)
        {
            statusMessage = "Port must be a number between 1 and 65535.";
            return false;
        }

        return true;
    }

    private void StartOnlineMatchAsHost(PieceTeam hostTeam)
    {
        if (!session.IsConnected || !session.HasPeer)
        {
            statusMessage = "A peer must be connected before the match can start.";
            return;
        }

        if (!session.SendStartGame(hostTeam))
        {
            statusMessage = "Unable to send online start message to the peer.";
            return;
        }

        onlineGameActive = true;
        showOnlinePanel = false;
        chessGame.BeginLanGame(hostTeam, hostTeam);
    }

    private void HandleStartGameRequested(PieceTeam hostTeam)
    {
        if (onlineGameActive)
            return;

        PieceTeam localTeam = hostTeam == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        onlineGameActive = true;
        showOnlinePanel = false;
        chessGame.BeginLanGame(hostTeam, localTeam);
    }

    private void HandleMoveCommitted(ChessLanMove move)
    {
        if (!onlineGameActive || session == null || !session.IsConnected)
            return;

        if (!session.SendMove(move))
            HandlePeerDisconnected("Unable to send online move to the peer.");
    }

    private void HandleMoveReceived(ChessLanMove move)
    {
        if (!onlineGameActive)
            return;

        if (!chessGame.ApplyNetworkMove(move))
            HandlePeerDisconnected("Received an out-of-sync online move.");
    }

    private void HandlePeerDisconnected(string message)
    {
        statusMessage = string.IsNullOrWhiteSpace(message) ? "Online peer disconnected." : message;
        if (!onlineGameActive)
            return;

        onlineGameActive = false;
        showOnlinePanel = false;
        chessGame.RestartToMainMenu();
    }

    private void HandleReturnedToMainMenu()
    {
        onlineGameActive = false;
        if (session != null)
            session.Disconnect();
    }

    private void HandleSessionStateChanged()
    {
        statusMessage = session.StatusMessage;
        if (!string.IsNullOrWhiteSpace(session.RoomCode))
            roomCodeText = session.RoomCode;
    }

    private void DrawSessionInfo(Rect panelRect)
    {
        float infoTop = panelRect.y + 226f;
        GUI.Label(
            new Rect(panelRect.x + 36f, infoTop, panelRect.width - 72f, 28f),
            $"This player: {session.LocalPlayerName}",
            GetBodyStyle());
        GUI.Label(
            new Rect(panelRect.x + 36f, infoTop + 28f, panelRect.width - 72f, 28f),
            $"Room state: {GetRoomStateLabel()}",
            GetBodyStyle());
        GUI.Label(
            new Rect(panelRect.x + 36f, infoTop + 56f, panelRect.width - 72f, 28f),
            $"Room code: {GetRoomCodeDisplayText()}",
            GetBodyStyle());
        GUI.Label(
            new Rect(panelRect.x + 36f, infoTop + 84f, panelRect.width - 72f, 28f),
            $"Peer: {GetPeerDisplayText()}",
            GetBodyStyle());
        GUI.Label(
            new Rect(panelRect.x + 36f, infoTop + 112f, panelRect.width - 72f, 28f),
            $"Network: {GetNetworkDisplayText()}",
            GetBodyStyle());
    }

    private void DrawInGameOnlineHud()
    {
        if (!onlineGameActive)
            return;

        float width = Mathf.Clamp(guiWidth * 0.20f, 320f, 390f);
        float height = 162f;
        Rect panelRect = new Rect(18f, guiHeight - height - 18f, width, height);
        GUI.Box(panelRect, string.Empty);

        GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 10f, panelRect.width - 28f, 24f), "Online Room", GetHudTitleStyle());
        GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 36f, panelRect.width - 28f, 20f), $"Role: {session.Role} | You: {chessGame.PlayerTeam}", GetHudBodyStyle());
        GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 56f, panelRect.width - 28f, 20f), $"Turn: {chessGame.CurrentTurn} | Room: {session.RoomCode}", GetHudBodyStyle());
        GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 76f, panelRect.width - 28f, 20f), $"Peer: {GetPeerDisplayText()}", GetHudBodyStyle());
        GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 96f, panelRect.width - 28f, 20f), $"Network: {GetNetworkDisplayText()}", GetHudBodyStyle());

        if (GUI.Button(new Rect(panelRect.x + 14f, panelRect.y + 124f, panelRect.width - 28f, 26f), "Leave Room"))
            LeaveOnlineRoom();
    }

    private void LeaveOnlineRoom()
    {
        showOnlinePanel = false;
        onlineGameActive = false;
        chessGame.RestartToMainMenu();
    }

    private string GetRoomStateLabel()
    {
        if (onlineGameActive)
            return "In match";

        switch (session.State)
        {
            case ChessOnlineSessionState.Connecting:
            case ChessOnlineSessionState.Connected:
                return "Connecting";
            case ChessOnlineSessionState.InRoom:
                if (session.Role == ChessOnlineSessionRole.Host)
                    return session.HasPeer ? "Peer connected, ready to start" : "Waiting for peer";

                return "Connected, waiting for host";
            case ChessOnlineSessionState.Error:
                return "Connection error";
            default:
                return "Not connected";
        }
    }

    private string GetRoomCodeDisplayText()
    {
        return string.IsNullOrWhiteSpace(session.RoomCode) ? "No room yet" : session.RoomCode;
    }

    private string GetPeerDisplayText()
    {
        return string.IsNullOrWhiteSpace(session.RemotePlayerName) ? "No peer joined yet" : session.RemotePlayerName;
    }

    private string GetNetworkDisplayText()
    {
        string health = GetConnectionHealthLabel(session.ConnectionHealth);
        if (!session.IsConnected)
            return health;

        string pingText = session.RoundTripMilliseconds >= 0d
            ? $"{Math.Round(session.RoundTripMilliseconds)} ms"
            : "measuring...";
        double secondsSinceMessage = session.SecondsSinceLastMessage;
        string silenceText = double.IsInfinity(secondsSinceMessage)
            ? "n/a"
            : $"{secondsSinceMessage:0.0}s ago";
        return $"{health} | Ping: {pingText} | Last msg: {silenceText}";
    }

    private static string GetConnectionHealthLabel(ChessLanConnectionHealth health)
    {
        switch (health)
        {
            case ChessLanConnectionHealth.Good:
                return "Stable";
            case ChessLanConnectionHealth.Weak:
                return "Weak";
            case ChessLanConnectionHealth.Lost:
                return "Lost";
            default:
                return "Unknown";
        }
    }

    private void BackToMultiplayerModes()
    {
        session.Disconnect();
        showOnlinePanel = false;
        turnSelectionUI.ShowMultiplayerModeSelection();
    }

    private static GUIStyle GetTitleStyle()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold
        };
        return style;
    }

    private static GUIStyle GetBodyStyle()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            wordWrap = true
        };
        return style;
    }

    private static GUIStyle GetHudTitleStyle()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        return style;
    }

    private static GUIStyle GetHudBodyStyle()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
        return style;
    }

    private static GUIStyle GetStatusStyle()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 19,
            fontStyle = FontStyle.Italic,
            wordWrap = true
        };
        return style;
    }

    private static float GetGuiScale()
    {
        float widthScale = Screen.width / ReferenceWidth;
        float heightScale = Screen.height / ReferenceHeight;
        return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 1f, 2f);
    }
}
