using System;
using UnityEngine;

public class ChessLanController : MonoBehaviour
{
    private const int DefaultPort = 19847;

    private ChessGame chessGame;
    private ChessTurnSelectionUI turnSelectionUI;
    private ChessLanSession session;
    private bool showLanPanel;
    private bool lanGameActive;
    private string joinAddress = "127.0.0.1";
    private string portText = DefaultPort.ToString();
    private string statusMessage = "Choose Host or Join to start a LAN match.";
    private string localAddressesSummary = "127.0.0.1";

    public void Initialize(ChessGame newChessGame, ChessTurnSelectionUI newTurnSelectionUI)
    {
        chessGame = newChessGame;
        turnSelectionUI = newTurnSelectionUI;
        session = gameObject.AddComponent<ChessLanSession>();
        session.StateChanged += HandleSessionStateChanged;
        session.StartGameRequested += HandleStartGameRequested;
        session.MoveReceived += HandleMoveReceived;
        session.PeerDisconnected += HandlePeerDisconnected;
        chessGame.MoveCommitted += HandleMoveCommitted;
        chessGame.ReturnedToMainMenu += HandleReturnedToMainMenu;

        RefreshLocalAddresses(true);
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
        if (!showLanPanel || lanGameActive)
        {
            DrawInGameLanHud();
            return;
        }

        float panelWidth = Mathf.Clamp(Screen.width * 0.44f, 660f, 860f);
        float panelHeight = Mathf.Clamp(Screen.height * 0.52f, 460f, 640f);
        Rect panelRect = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.28f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUI.Box(panelRect, string.Empty);

        Rect titleRect = new Rect(panelRect.x + 36f, panelRect.y + 24f, panelRect.width - 72f, 48f);
        GUI.Label(titleRect, "LAN Multiplayer", GetTitleStyle());

        Rect helpRect = new Rect(panelRect.x + 36f, panelRect.y + 78f, panelRect.width - 72f, 64f);
        GUI.Label(helpRect, "Direct IP connection on the same local network.", GetBodyStyle());

        Rect addressesRect = new Rect(panelRect.x + 36f, panelRect.y + 140f, panelRect.width - 72f, 56f);
        GUI.Label(addressesRect, $"Local IPs: {localAddressesSummary}", GetBodyStyle());

        Rect statusRect = new Rect(panelRect.x + 36f, panelRect.y + 196f, panelRect.width - 72f, 72f);
        GUI.Label(statusRect, statusMessage, GetStatusStyle());

        DrawSessionInfo(panelRect);
        DrawPortField(panelRect);
        DrawAddressField(panelRect);
        DrawActionButtons(panelRect);
        DrawInGameLanHud();
    }

    public void ShowLanSetup()
    {
        showLanPanel = true;
        RefreshLocalAddresses(false);
        statusMessage = session != null ? session.StatusMessage : statusMessage;
    }

    public void HideLanSetup()
    {
        showLanPanel = false;
    }

    private void DrawPortField(Rect panelRect)
    {
        Rect labelRect = new Rect(panelRect.x + 36f, panelRect.y + 348f, 120f, 36f);
        Rect fieldRect = new Rect(panelRect.x + 160f, panelRect.y + 344f, 160f, 38f);
        GUI.Label(labelRect, "Port", GetBodyStyle());
        portText = GUI.TextField(fieldRect, portText, 8);
    }

    private void DrawAddressField(Rect panelRect)
    {
        Rect labelRect = new Rect(panelRect.x + 36f, panelRect.y + 398f, 120f, 36f);
        Rect fieldRect = new Rect(panelRect.x + 160f, panelRect.y + 394f, panelRect.width - 196f, 38f);
        GUI.Label(labelRect, "Join IP", GetBodyStyle());
        joinAddress = GUI.TextField(fieldRect, joinAddress, 64);
    }

    private void DrawActionButtons(Rect panelRect)
    {
        Rect firstButtonRect = new Rect(panelRect.x + 36f, panelRect.y + panelRect.height - 98f, 220f, 52f);
        Rect secondButtonRect = new Rect(panelRect.x + 274f, panelRect.y + panelRect.height - 98f, 220f, 52f);
        Rect thirdButtonRect = new Rect(panelRect.x + panelRect.width - 226f, panelRect.y + panelRect.height - 98f, 190f, 52f);

        switch (session.State)
        {
            case ChessLanSessionState.Idle:
            case ChessLanSessionState.Error:
                if (GUI.Button(firstButtonRect, "Host LAN"))
                    StartHosting();

                if (GUI.Button(secondButtonRect, "Join LAN"))
                    StartJoining();

                if (GUI.Button(thirdButtonRect, "Back"))
                    BackToMultiplayerModes();
                break;

            case ChessLanSessionState.Hosting:
                if (GUI.Button(firstButtonRect, "Stop Hosting"))
                    session.Disconnect();

                GUI.Label(secondButtonRect, "Waiting for a peer...", GetBodyStyle());

                if (GUI.Button(thirdButtonRect, "Back"))
                    BackToMultiplayerModes();
                break;

            case ChessLanSessionState.Connecting:
                GUI.Label(firstButtonRect, "Connecting...", GetBodyStyle());

                if (GUI.Button(secondButtonRect, "Cancel"))
                    session.Disconnect();

                if (GUI.Button(thirdButtonRect, "Back"))
                    BackToMultiplayerModes();
                break;

            case ChessLanSessionState.Connected:
                if (session.Role == ChessLanSessionRole.Host)
                {
                    if (GUI.Button(firstButtonRect, "Start As White"))
                        StartLanMatchAsHost(PieceTeam.White);

                    if (GUI.Button(secondButtonRect, "Start As Black"))
                        StartLanMatchAsHost(PieceTeam.Black);
                }
                else
                {
                    GUI.Label(firstButtonRect, "Connected. Waiting for host...", GetBodyStyle());
                    GUI.Label(secondButtonRect, session.RemoteEndpointDisplay, GetBodyStyle());
                }

                if (GUI.Button(thirdButtonRect, "Disconnect"))
                    session.Disconnect();
                break;
        }
    }

    private void StartHosting()
    {
        if (!TryParsePort(out int port))
            return;

        statusMessage = "Starting LAN host...";
        session.Host(port);
    }

    private void StartJoining()
    {
        if (!TryParsePort(out int port))
            return;

        if (string.IsNullOrWhiteSpace(joinAddress))
        {
            statusMessage = "Enter a valid join IP address.";
            return;
        }

        statusMessage = $"Connecting to {joinAddress}:{port}...";
        session.Join(joinAddress.Trim(), port);
    }

    private bool TryParsePort(out int port)
    {
        if (!int.TryParse(portText, out port) || port < 1 || port > 65535)
        {
            statusMessage = "Port must be a number between 1 and 65535.";
            return false;
        }

        return true;
    }

    private void StartLanMatchAsHost(PieceTeam hostTeam)
    {
        if (!session.IsConnected)
        {
            statusMessage = "A LAN peer must be connected before the match can start.";
            return;
        }

        if (!session.SendStartGame(hostTeam))
        {
            statusMessage = "Unable to send LAN start message to the peer.";
            return;
        }

        lanGameActive = true;
        showLanPanel = false;
        chessGame.BeginLanGame(hostTeam, hostTeam);
    }

    private void HandleStartGameRequested(PieceTeam hostTeam)
    {
        if (lanGameActive)
            return;

        PieceTeam localTeam = hostTeam == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        lanGameActive = true;
        showLanPanel = false;
        chessGame.BeginLanGame(hostTeam, localTeam);
    }

    private void HandleMoveCommitted(ChessLanMove move)
    {
        if (!lanGameActive || session == null || !session.IsConnected)
            return;

        if (!session.SendMove(move))
            HandlePeerDisconnected("Unable to send LAN move to the peer.");
    }

    private void HandleMoveReceived(ChessLanMove move)
    {
        if (!lanGameActive)
            return;

        if (!chessGame.ApplyNetworkMove(move))
            HandlePeerDisconnected("Received an out-of-sync LAN move.");
    }

    private void HandlePeerDisconnected(string message)
    {
        statusMessage = string.IsNullOrWhiteSpace(message) ? "LAN peer disconnected." : message;
        if (!lanGameActive)
            return;

        lanGameActive = false;
        showLanPanel = false;
        chessGame.RestartToMainMenu();
    }

    private void HandleReturnedToMainMenu()
    {
        lanGameActive = false;
        if (session != null)
            session.Disconnect();
    }

    private void HandleSessionStateChanged()
    {
        statusMessage = session.StatusMessage;
    }

    private void RefreshLocalAddresses(bool overwriteJoinAddress)
    {
        string[] addresses = ChessLanSession.GetLocalIpv4Addresses();
        localAddressesSummary = string.Join(", ", addresses);
        if (overwriteJoinAddress && addresses.Length > 0)
            joinAddress = addresses[0];
    }

    private void DrawSessionInfo(Rect panelRect)
    {
        float infoTop = panelRect.y + 260f;
        GUI.Label(
            new Rect(panelRect.x + 36f, infoTop, panelRect.width - 72f, 28f),
            $"This machine: {session.LocalMachineName}",
            GetBodyStyle());
        GUI.Label(
            new Rect(panelRect.x + 36f, infoTop + 28f, panelRect.width - 72f, 28f),
            $"Room state: {GetRoomStateLabel()}",
            GetBodyStyle());
        GUI.Label(
            new Rect(panelRect.x + 36f, infoTop + 56f, panelRect.width - 72f, 28f),
            $"Peer: {GetPeerDisplayText()}",
            GetBodyStyle());
        GUI.Label(
            new Rect(panelRect.x + 36f, infoTop + 84f, panelRect.width - 72f, 28f),
            $"Network: {GetNetworkDisplayText()}",
            GetBodyStyle());
    }

    private void DrawInGameLanHud()
    {
        if (!lanGameActive)
            return;

        float width = Mathf.Clamp(Screen.width * 0.22f, 320f, 420f);
        float height = 210f;
        Rect panelRect = new Rect(Screen.width - width - 18f, 18f, width, height);
        GUI.Box(panelRect, string.Empty);

        GUI.Label(new Rect(panelRect.x + 16f, panelRect.y + 12f, panelRect.width - 32f, 28f), "LAN Room", GetHudTitleStyle());
        GUI.Label(new Rect(panelRect.x + 16f, panelRect.y + 44f, panelRect.width - 32f, 22f), $"Role: {session.Role}", GetHudBodyStyle());
        GUI.Label(new Rect(panelRect.x + 16f, panelRect.y + 66f, panelRect.width - 32f, 22f), $"Machine: {session.LocalMachineName}", GetHudBodyStyle());
        GUI.Label(new Rect(panelRect.x + 16f, panelRect.y + 88f, panelRect.width - 32f, 22f), $"Peer: {GetPeerDisplayText()}", GetHudBodyStyle());
        GUI.Label(new Rect(panelRect.x + 16f, panelRect.y + 110f, panelRect.width - 32f, 22f), $"Room: {GetRoomStateLabel()}", GetHudBodyStyle());
        GUI.Label(new Rect(panelRect.x + 16f, panelRect.y + 132f, panelRect.width - 32f, 22f), $"Network: {GetNetworkDisplayText()}", GetHudBodyStyle());
        GUI.Label(new Rect(panelRect.x + 16f, panelRect.y + 154f, panelRect.width - 32f, 22f), $"You: {chessGame.PlayerTeam} | Turn: {chessGame.CurrentTurn}", GetHudBodyStyle());

        if (GUI.Button(new Rect(panelRect.x + 16f, panelRect.y + 176f, panelRect.width - 32f, 24f), "Leave Room"))
            LeaveLanRoom();
    }

    private void LeaveLanRoom()
    {
        showLanPanel = false;
        lanGameActive = false;
        chessGame.RestartToMainMenu();
    }

    private string GetRoomStateLabel()
    {
        if (lanGameActive)
            return "In match";

        switch (session.State)
        {
            case ChessLanSessionState.Hosting:
                return "Waiting for peer";
            case ChessLanSessionState.Connecting:
                return "Connecting";
            case ChessLanSessionState.Connected:
                return session.Role == ChessLanSessionRole.Host ? "Peer connected, ready to start" : "Connected, waiting for host";
            case ChessLanSessionState.Error:
                return "Connection error";
            default:
                return "Not connected";
        }
    }

    private string GetPeerDisplayText()
    {
        if (session.State == ChessLanSessionState.Hosting)
            return "No peer joined yet";

        string remoteName = string.IsNullOrWhiteSpace(session.RemoteMachineName) ? "Unknown machine" : session.RemoteMachineName;
        string endpoint = string.IsNullOrWhiteSpace(session.RemoteEndpointDisplay) ? "No endpoint" : session.RemoteEndpointDisplay;
        return $"{remoteName} ({endpoint})";
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
        showLanPanel = false;
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
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
        return style;
    }

    private static GUIStyle GetHudBodyStyle()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14
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
}
