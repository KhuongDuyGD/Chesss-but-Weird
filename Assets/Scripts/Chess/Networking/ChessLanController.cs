using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class ChessLanController : MonoBehaviour
{
    private const int DefaultPort = 19847;
    private const int DiscoveryPort = 19846;
    private const string DiscoveryRequest = "CHESS_BUT_WEIRD_LAN_DISCOVER_V1";
    private const string DiscoveryResponsePrefix = "CHESS_BUT_WEIRD_LAN_HOST_V1";
    private const float DiscoveryTimeoutSeconds = 2f;
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    private readonly object discoveredHostsLock = new object();
    private readonly List<DiscoveredLanHost> discoveredHosts = new List<DiscoveredLanHost>();

    private ChessGame chessGame;
    private ChessTurnSelectionUI turnSelectionUI;
    private ChessLanSession session;
    private bool showLanPanel;
    private bool lanGameActive;
    private bool discoveryInProgress;
    private string statusMessage = "Host a LAN room or refresh to find one.";
    private string localAddressesSummary = "127.0.0.1";
    private float guiWidth = ReferenceWidth;
    private float guiHeight = ReferenceHeight;
    private UdpClient hostDiscoveryClient;
    private Thread hostDiscoveryThread;
    private Thread discoveryScanThread;
    private bool hostDiscoveryRunning;

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

        RefreshLocalAddresses();
    }

    private void OnDestroy()
    {
        StopHostDiscovery();

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
            if (!showLanPanel || lanGameActive)
            {
                DrawInGameLanHud();
                return;
            }

            float panelWidth = Mathf.Clamp(guiWidth * 0.50f, 760f, 1040f);
            float panelHeight = Mathf.Clamp(guiHeight * 0.64f, 620f, 780f);
            Rect panelRect = new Rect(
                (guiWidth - panelWidth) * 0.5f,
                (guiHeight - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.28f);
            GUI.DrawTexture(new Rect(0f, 0f, guiWidth, guiHeight), Texture2D.whiteTexture);
            GUI.color = previousColor;

            GUI.Box(panelRect, string.Empty);

            GUI.Label(new Rect(panelRect.x + 36f, panelRect.y + 24f, panelRect.width - 72f, 48f), "LAN Hub", GetTitleStyle());
            GUI.Label(
                new Rect(panelRect.x + 36f, panelRect.y + 78f, panelRect.width - 72f, 52f),
                "Host a room or refresh to find rooms on this network.",
                GetBodyStyle());
            GUI.Label(
                new Rect(panelRect.x + 36f, panelRect.y + 128f, panelRect.width - 72f, 48f),
                $"This machine: {session.LocalMachineName} | Local IPs: {localAddressesSummary}",
                GetBodyStyle());
            GUI.Label(new Rect(panelRect.x + 36f, panelRect.y + 174f, panelRect.width - 72f, 64f), statusMessage, GetStatusStyle());

            DrawSessionInfo(panelRect);
            DrawDiscoveredHosts(panelRect);
            DrawActionButtons(panelRect);
            DrawInGameLanHud();
        }
        finally
        {
            GUI.matrix = previousMatrix;
        }
    }

    public void ShowLanSetup()
    {
        showLanPanel = true;
        RefreshLocalAddresses();
        if (session != null && session.State == ChessLanSessionState.Idle)
            StartDiscoveryScan();
        else
            statusMessage = session != null ? session.StatusMessage : statusMessage;
    }

    public void HideLanSetup()
    {
        showLanPanel = false;
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

                GUI.enabled = !discoveryInProgress;
                if (GUI.Button(secondButtonRect, discoveryInProgress ? "Scanning..." : "Refresh"))
                    StartDiscoveryScan();
                GUI.enabled = true;

                if (GUI.Button(thirdButtonRect, "Back"))
                    BackToMultiplayerModes();
                break;

            case ChessLanSessionState.Hosting:
                if (GUI.Button(firstButtonRect, "Stop Hosting"))
                    StopHosting();

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

    private void DrawDiscoveredHosts(Rect panelRect)
    {
        Rect listRect = new Rect(panelRect.x + 36f, panelRect.y + 366f, panelRect.width - 72f, panelRect.height - 484f);
        GUI.Box(listRect, string.Empty);

        GUI.Label(new Rect(listRect.x + 18f, listRect.y + 12f, listRect.width - 36f, 30f), "Available LAN Rooms", GetHudTitleStyle());

        if (session.State != ChessLanSessionState.Idle && session.State != ChessLanSessionState.Error)
        {
            GUI.Label(new Rect(listRect.x + 18f, listRect.y + 52f, listRect.width - 36f, 40f), GetRoomStateLabel(), GetBodyStyle());
            return;
        }

        DiscoveredLanHost[] hosts = GetDiscoveredHostsSnapshot();
        if (hosts.Length == 0)
        {
            string emptyText = discoveryInProgress ? "Scanning the local network..." : "No rooms found. Press Refresh to scan again.";
            GUI.Label(new Rect(listRect.x + 18f, listRect.y + 52f, listRect.width - 36f, 48f), emptyText, GetBodyStyle());
            return;
        }

        float rowY = listRect.y + 54f;
        for (int i = 0; i < hosts.Length; i++)
        {
            DiscoveredLanHost host = hosts[i];
            Rect rowRect = new Rect(listRect.x + 18f, rowY, listRect.width - 36f, 48f);
            GUI.Box(rowRect, string.Empty);
            GUI.Label(new Rect(rowRect.x + 14f, rowRect.y + 8f, rowRect.width - 176f, 30f), host.DisplayName, GetBodyStyle());

            if (GUI.Button(new Rect(rowRect.x + rowRect.width - 142f, rowRect.y + 7f, 126f, 34f), "Join"))
                JoinDiscoveredHost(host);

            rowY += 56f;
        }
    }

    private void StartHosting()
    {
        statusMessage = "Starting LAN host...";
        session.Host(DefaultPort);

        if (session.State == ChessLanSessionState.Hosting)
            StartHostDiscovery();
    }

    private void StopHosting()
    {
        StopHostDiscovery();
        session.Disconnect();
    }

    private void JoinDiscoveredHost(DiscoveredLanHost host)
    {
        StopHostDiscovery();
        statusMessage = $"Connecting to {host.DisplayName}...";
        session.Join(host.Address, host.Port);
    }

    private void StartDiscoveryScan()
    {
        if (discoveryInProgress)
            return;

        lock (discoveredHostsLock)
        {
            discoveredHosts.Clear();
        }

        discoveryInProgress = true;
        statusMessage = "Scanning for LAN rooms...";

        discoveryScanThread = new Thread(DiscoveryScanLoop)
        {
            IsBackground = true,
            Name = "Chess LAN Discovery Scan"
        };
        discoveryScanThread.Start();
    }

    private void DiscoveryScanLoop()
    {
        try
        {
            using (UdpClient client = new UdpClient())
            {
                client.EnableBroadcast = true;
                client.Client.ReceiveTimeout = 250;

                byte[] request = Encoding.UTF8.GetBytes(DiscoveryRequest);
                client.Send(request, request.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));

                DateTime deadline = DateTime.UtcNow.AddSeconds(DiscoveryTimeoutSeconds);
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                        byte[] response = client.Receive(ref remoteEndpoint);
                        string message = Encoding.UTF8.GetString(response);
                        TryAddDiscoveryResponse(message, remoteEndpoint.Address.ToString());
                    }
                    catch (SocketException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            statusMessage = $"Unable to scan LAN rooms: {exception.Message}";
        }
        finally
        {
            discoveryInProgress = false;
            if (session != null && (session.State == ChessLanSessionState.Idle || session.State == ChessLanSessionState.Error))
            {
                int roomCount;
                lock (discoveredHostsLock)
                {
                    roomCount = discoveredHosts.Count;
                }

                statusMessage = roomCount == 0 ? "No LAN rooms found." : $"Found {roomCount} LAN room(s).";
            }
        }
    }

    private void TryAddDiscoveryResponse(string message, string address)
    {
        if (string.IsNullOrWhiteSpace(message) || !message.StartsWith(DiscoveryResponsePrefix, StringComparison.Ordinal))
            return;

        string[] parts = message.Split('|');
        if (parts.Length < 3 || !int.TryParse(parts[1], out int port))
            return;

        string machineName = parts[2];
        DiscoveredLanHost host = new DiscoveredLanHost(address, port, machineName);

        lock (discoveredHostsLock)
        {
            for (int i = 0; i < discoveredHosts.Count; i++)
            {
                if (discoveredHosts[i].Address == host.Address && discoveredHosts[i].Port == host.Port)
                {
                    discoveredHosts[i] = host;
                    return;
                }
            }

            discoveredHosts.Add(host);
        }
    }

    private DiscoveredLanHost[] GetDiscoveredHostsSnapshot()
    {
        lock (discoveredHostsLock)
        {
            return discoveredHosts.ToArray();
        }
    }

    private void StartHostDiscovery()
    {
        StopHostDiscovery();

        try
        {
            hostDiscoveryRunning = true;
            hostDiscoveryClient = new UdpClient();
            hostDiscoveryClient.EnableBroadcast = true;
            hostDiscoveryClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            hostDiscoveryClient.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

            hostDiscoveryThread = new Thread(HostDiscoveryLoop)
            {
                IsBackground = true,
                Name = "Chess LAN Host Discovery"
            };
            hostDiscoveryThread.Start();
        }
        catch (Exception exception)
        {
            hostDiscoveryRunning = false;
            statusMessage = $"Hosting, but LAN discovery failed: {exception.Message}";
        }
    }

    private void StopHostDiscovery()
    {
        hostDiscoveryRunning = false;

        if (hostDiscoveryClient != null)
        {
            hostDiscoveryClient.Close();
            hostDiscoveryClient = null;
        }

        hostDiscoveryThread = null;
    }

    private void HostDiscoveryLoop()
    {
        while (hostDiscoveryRunning && hostDiscoveryClient != null)
        {
            try
            {
                IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] request = hostDiscoveryClient.Receive(ref remoteEndpoint);
                string message = Encoding.UTF8.GetString(request);
                if (!string.Equals(message, DiscoveryRequest, StringComparison.Ordinal))
                    continue;

                string responseText = $"{DiscoveryResponsePrefix}|{DefaultPort}|{session.LocalMachineName}";
                byte[] response = Encoding.UTF8.GetBytes(responseText);
                hostDiscoveryClient.Send(response, response.Length, remoteEndpoint);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[ChessLAN] Discovery responder failed: {exception.Message}");
            }
        }
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

        StopHostDiscovery();
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
        StopHostDiscovery();
        if (!lanGameActive)
            return;

        lanGameActive = false;
        showLanPanel = false;
        chessGame.RestartToMainMenu();
    }

    private void HandleReturnedToMainMenu()
    {
        lanGameActive = false;
        StopHostDiscovery();
        if (session != null)
            session.Disconnect();
    }

    private void HandleSessionStateChanged()
    {
        statusMessage = session.StatusMessage;

        if (session.State != ChessLanSessionState.Hosting)
            StopHostDiscovery();
    }

    private void RefreshLocalAddresses()
    {
        string[] addresses = ChessLanSession.GetLocalIpv4Addresses();
        localAddressesSummary = string.Join(", ", addresses);
    }

    private void DrawSessionInfo(Rect panelRect)
    {
        float infoTop = panelRect.y + 248f;
        GUI.Label(
            new Rect(panelRect.x + 36f, infoTop, panelRect.width - 72f, 28f),
            $"Room state: {GetRoomStateLabel()}",
            GetBodyStyle());
        GUI.Label(
            new Rect(panelRect.x + 36f, infoTop + 28f, panelRect.width - 72f, 28f),
            $"Peer: {GetPeerDisplayText()}",
            GetBodyStyle());
        GUI.Label(
            new Rect(panelRect.x + 36f, infoTop + 56f, panelRect.width - 72f, 28f),
            $"Network: {GetNetworkDisplayText()}",
            GetBodyStyle());
    }

    private void DrawInGameLanHud()
    {
        if (!lanGameActive)
            return;

        float width = Mathf.Clamp(guiWidth * 0.18f, 300f, 360f);
        float height = 162f;
        Rect panelRect = new Rect(18f, guiHeight - height - 18f, width, height);
        GUI.Box(panelRect, string.Empty);

        GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 10f, panelRect.width - 28f, 24f), "LAN Room", GetHudTitleStyle());
        GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 36f, panelRect.width - 28f, 20f), $"Role: {session.Role} | You: {chessGame.PlayerTeam}", GetHudBodyStyle());
        GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 56f, panelRect.width - 28f, 20f), $"Turn: {chessGame.CurrentTurn} | Room: {GetRoomStateLabel()}", GetHudBodyStyle());
        GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 76f, panelRect.width - 28f, 20f), $"Peer: {GetPeerDisplayText()}", GetHudBodyStyle());
        GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 96f, panelRect.width - 28f, 20f), $"Network: {GetNetworkDisplayText()}", GetHudBodyStyle());

        if (GUI.Button(new Rect(panelRect.x + 14f, panelRect.y + 124f, panelRect.width - 28f, 26f), "Leave Room"))
            LeaveLanRoom();
    }

    private void LeaveLanRoom()
    {
        showLanPanel = false;
        lanGameActive = false;
        StopHostDiscovery();
        chessGame.RestartToMainMenu();
    }

    private string GetRoomStateLabel()
    {
        if (lanGameActive)
            return "In match";

        switch (session.State)
        {
            case ChessLanSessionState.Hosting:
                return "Hosting room";
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
        StopHostDiscovery();
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

    private struct DiscoveredLanHost
    {
        public readonly string Address;
        public readonly int Port;
        public readonly string MachineName;

        public DiscoveredLanHost(string address, int port, string machineName)
        {
            Address = address;
            Port = port;
            MachineName = string.IsNullOrWhiteSpace(machineName) ? "LAN Host" : machineName;
        }

        public string DisplayName => $"{MachineName} ({Address})";
    }
}
