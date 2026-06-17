using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public enum ChessOnlineSessionRole
{
    None,
    Host,
    Client
}

public enum ChessOnlineSessionState
{
    Idle,
    Connecting,
    Connected,
    InRoom,
    Error
}

public class ChessOnlineSession : MonoBehaviour
{
    private const string CreateCommand = "CREATE";
    private const string JoinCommand = "JOIN";
    private const string RoomCommand = "ROOM";
    private const string PeerCommand = "PEER";
    private const string PeerLeftCommand = "PEERLEFT";
    private const string StartCommand = "START";
    private const string MoveCommand = "MOVE";
    private const string HelloCommand = "HELLO";
    private const string PingCommand = "PING";
    private const string PongCommand = "PONG";
    private const string ErrorCommand = "ERROR";
    private const string InfoCommand = "INFO";
    private const double HeartbeatIntervalSeconds = 2d;
    private const double WeakConnectionSilenceSeconds = 5d;
    private const double LostConnectionSilenceSeconds = 12d;
    private const double WeakRoundTripThresholdMilliseconds = 350d;

    private readonly Queue<Action> mainThreadActions = new Queue<Action>();
    private readonly object queueLock = new object();
    private readonly object sendLock = new object();

    private TcpClient client;
    private StreamReader reader;
    private StreamWriter writer;
    private Thread connectThread;
    private Thread receiveThread;
    private bool shutdownRequested;
    private string pendingRoomCommand = string.Empty;
    private DateTime lastHeartbeatSentUtc;
    private DateTime lastMessageReceivedUtc;
    private double roundTripMilliseconds = -1d;
    private ChessLanConnectionHealth connectionHealth = ChessLanConnectionHealth.Unknown;
    private string remotePlayerName = string.Empty;

    public ChessOnlineSessionRole Role { get; private set; } = ChessOnlineSessionRole.None;
    public ChessOnlineSessionState State { get; private set; } = ChessOnlineSessionState.Idle;
    public string StatusMessage { get; private set; } = "Idle.";
    public string ServerEndpointDisplay { get; private set; } = string.Empty;
    public string RoomCode { get; private set; } = string.Empty;
    public int ActivePort { get; private set; }
    public bool HasPeer => !string.IsNullOrWhiteSpace(remotePlayerName);
    public bool IsConnected => State != ChessOnlineSessionState.Idle &&
        State != ChessOnlineSessionState.Error &&
        client != null &&
        client.Connected;
    public string LocalPlayerName => Environment.MachineName;
    public string RemotePlayerName => remotePlayerName;
    public double RoundTripMilliseconds => roundTripMilliseconds;
    public ChessLanConnectionHealth ConnectionHealth => connectionHealth;
    public double SecondsSinceLastMessage =>
        lastMessageReceivedUtc == default ? double.PositiveInfinity : (DateTime.UtcNow - lastMessageReceivedUtc).TotalSeconds;

    public event Action StateChanged;
    public event Action<PieceTeam> StartGameRequested;
    public event Action<ChessLanMove> MoveReceived;
    public event Action<string> PeerDisconnected;

    private void Update()
    {
        while (true)
        {
            Action action = null;
            lock (queueLock)
            {
                if (mainThreadActions.Count > 0)
                    action = mainThreadActions.Dequeue();
            }

            if (action == null)
                break;

            action.Invoke();
        }

        if (!IsConnected)
            return;

        DateTime utcNow = DateTime.UtcNow;
        if ((utcNow - lastHeartbeatSentUtc).TotalSeconds >= HeartbeatIntervalSeconds)
        {
            lastHeartbeatSentUtc = utcNow;
            SendLine($"{PingCommand}|{utcNow.Ticks}", false);
        }

        RefreshConnectionHealth();

        if (connectionHealth == ChessLanConnectionHealth.Lost)
            NotifyPeerDisconnected("Online server connection lost.");
    }

    private void OnDestroy()
    {
        DisconnectInternal(false, true, "Session destroyed.");
    }

    public void CreateRoom(string serverAddress, int port)
    {
        Connect(serverAddress, port, CreateCommand);
    }

    public void JoinRoom(string serverAddress, int port, string roomCode)
    {
        Connect(serverAddress, port, $"{JoinCommand}|{NormalizeRoomCode(roomCode)}");
    }

    public void Disconnect()
    {
        if (IsConnected)
            SendLine("LEAVE", false);

        DisconnectInternal(true, true, "Disconnected.");
    }

    public bool SendStartGame(PieceTeam hostTeam)
    {
        return SendLine($"{StartCommand}|{hostTeam}");
    }

    public bool SendMove(ChessLanMove move)
    {
        return SendLine($"{MoveCommand}|{move.ToProtocolPayload()}");
    }

    private void Connect(string serverAddress, int port, string roomCommand)
    {
        Disconnect();

        shutdownRequested = false;
        pendingRoomCommand = roomCommand;
        ActivePort = port;
        ServerEndpointDisplay = $"{serverAddress}:{port}";
        SetState(ChessOnlineSessionState.Connecting, $"Connecting to online server {ServerEndpointDisplay}...");

        connectThread = new Thread(() => ConnectLoop(serverAddress, port))
        {
            IsBackground = true,
            Name = "Chess Online Connect"
        };
        connectThread.Start();
    }

    private void ConnectLoop(string address, int port)
    {
        TcpClient connectingClient = new TcpClient();
        try
        {
            connectingClient.NoDelay = true;
            connectingClient.Connect(address, port);
            if (shutdownRequested)
            {
                connectingClient.Close();
                return;
            }

            EnqueueMainThread(() => AttachClient(connectingClient));
        }
        catch (Exception exception)
        {
            connectingClient.Close();
            if (!shutdownRequested)
                EnqueueMainThread(() => Fail($"Unable to connect to online server: {exception.Message}"));
        }
    }

    private void AttachClient(TcpClient connectedClient)
    {
        try
        {
            client = connectedClient;
            RoomCode = string.Empty;
            Role = ChessOnlineSessionRole.None;
            remotePlayerName = string.Empty;
            roundTripMilliseconds = -1d;
            lastHeartbeatSentUtc = DateTime.MinValue;
            lastMessageReceivedUtc = DateTime.UtcNow;
            connectionHealth = ChessLanConnectionHealth.Unknown;

            NetworkStream stream = client.GetStream();
            reader = new StreamReader(stream);
            writer = new StreamWriter(stream)
            {
                AutoFlush = true
            };

            SetState(ChessOnlineSessionState.Connected, $"Connected to online server {ServerEndpointDisplay}.");
            SendLine($"{HelloCommand}|{LocalPlayerName}", false);
            if (!string.IsNullOrWhiteSpace(pendingRoomCommand))
                SendLine(pendingRoomCommand, false);

            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "Chess Online Receive"
            };
            receiveThread.Start();
        }
        catch (Exception exception)
        {
            Fail($"Unable to open online data stream: {exception.Message}");
        }
    }

    private void ReceiveLoop()
    {
        bool disconnectAlreadyQueued = false;

        try
        {
            while (!shutdownRequested && reader != null)
            {
                string line = reader.ReadLine();
                if (line == null)
                    break;

                string capturedLine = line;
                EnqueueMainThread(() => HandleIncomingLine(capturedLine));
            }
        }
        catch (IOException)
        {
            if (!shutdownRequested)
            {
                disconnectAlreadyQueued = true;
                EnqueueMainThread(() => NotifyPeerDisconnected("Online connection closed."));
            }
        }
        catch (ObjectDisposedException)
        {
            if (!shutdownRequested)
            {
                disconnectAlreadyQueued = true;
                EnqueueMainThread(() => NotifyPeerDisconnected("Online connection disposed."));
            }
        }
        catch (Exception exception)
        {
            if (!shutdownRequested)
            {
                disconnectAlreadyQueued = true;
                EnqueueMainThread(() => NotifyPeerDisconnected($"Online receive failed: {exception.Message}"));
            }
        }
        finally
        {
            if (!shutdownRequested && !disconnectAlreadyQueued)
                EnqueueMainThread(() => NotifyPeerDisconnected("Online server disconnected."));
        }
    }

    private void HandleIncomingLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        lastMessageReceivedUtc = DateTime.UtcNow;
        int separatorIndex = line.IndexOf('|');
        string command = separatorIndex >= 0 ? line.Substring(0, separatorIndex) : line;
        string payload = separatorIndex >= 0 && separatorIndex + 1 < line.Length
            ? line.Substring(separatorIndex + 1)
            : string.Empty;

        if (string.Equals(command, InfoCommand, StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = string.IsNullOrWhiteSpace(payload) ? StatusMessage : payload;
            StateChanged?.Invoke();
            return;
        }

        if (string.Equals(command, ErrorCommand, StringComparison.OrdinalIgnoreCase))
        {
            Fail(string.IsNullOrWhiteSpace(payload) ? "Online server returned an error." : payload);
            return;
        }

        if (string.Equals(command, RoomCommand, StringComparison.OrdinalIgnoreCase))
        {
            HandleRoomPayload(payload);
            return;
        }

        if (string.Equals(command, PeerCommand, StringComparison.OrdinalIgnoreCase))
        {
            remotePlayerName = string.IsNullOrWhiteSpace(payload) ? "Online peer" : payload.Trim();
            SetState(ChessOnlineSessionState.InRoom, $"Peer connected: {remotePlayerName}.");
            return;
        }

        if (string.Equals(command, PeerLeftCommand, StringComparison.OrdinalIgnoreCase))
        {
            NotifyPeerDisconnected(string.IsNullOrWhiteSpace(payload) ? "Online peer left the room." : payload);
            return;
        }

        if (string.Equals(command, PongCommand, StringComparison.OrdinalIgnoreCase))
        {
            if (long.TryParse(payload, out long sentTicks))
            {
                DateTime sentUtc = new DateTime(sentTicks, DateTimeKind.Utc);
                roundTripMilliseconds = Math.Max(0d, (DateTime.UtcNow - sentUtc).TotalMilliseconds);
                RefreshConnectionHealth();
            }

            return;
        }

        if (string.Equals(command, StartCommand, StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse(payload, true, out PieceTeam hostTeam))
                StartGameRequested?.Invoke(hostTeam);
            else
                Fail($"Received invalid online start payload: {payload}");

            return;
        }

        if (string.Equals(command, MoveCommand, StringComparison.OrdinalIgnoreCase))
        {
            if (ChessLanMove.TryParse(payload, out ChessLanMove move))
                MoveReceived?.Invoke(move);
            else
                Fail($"Received invalid online move payload: {payload}");

            return;
        }

        Fail($"Received unknown online command: {command}");
    }

    private void HandleRoomPayload(string payload)
    {
        string[] tokens = payload.Split('|');
        if (tokens.Length < 2)
        {
            Fail($"Received invalid online room payload: {payload}");
            return;
        }

        RoomCode = NormalizeRoomCode(tokens[0]);
        if (!Enum.TryParse(tokens[1], true, out ChessOnlineSessionRole parsedRole))
        {
            Fail($"Received invalid online role: {tokens[1]}");
            return;
        }

        Role = parsedRole;
        string roleText = Role == ChessOnlineSessionRole.Host ? "Host" : "Client";
        SetState(ChessOnlineSessionState.InRoom, $"{roleText} room {RoomCode}. Waiting for peer...");
    }

    private bool SendLine(string line, bool reportFailure = true)
    {
        if (!IsConnected || writer == null)
            return false;

        try
        {
            lock (sendLock)
            {
                writer.WriteLine(line);
                writer.Flush();
            }

            return true;
        }
        catch (Exception exception)
        {
            if (reportFailure)
                Fail($"Unable to send online data: {exception.Message}");

            return false;
        }
    }

    private void NotifyPeerDisconnected(string message)
    {
        DisconnectInternal(false, true, message);
        PeerDisconnected?.Invoke(message);
    }

    private void Fail(string message)
    {
        DisconnectInternal(false, true, message);
        SetState(ChessOnlineSessionState.Error, message);
    }

    private void DisconnectInternal(bool requestedByUser, bool clearTransport, string statusMessage)
    {
        shutdownRequested = true;

        lock (queueLock)
        {
            mainThreadActions.Clear();
        }

        if (clearTransport)
        {
            if (reader != null)
            {
                reader.Dispose();
                reader = null;
            }

            if (writer != null)
            {
                writer.Dispose();
                writer = null;
            }

            if (client != null)
            {
                client.Close();
                client = null;
            }
        }

        connectThread = null;
        receiveThread = null;
        pendingRoomCommand = string.Empty;
        roundTripMilliseconds = -1d;
        lastHeartbeatSentUtc = DateTime.MinValue;
        lastMessageReceivedUtc = default;
        connectionHealth = ChessLanConnectionHealth.Unknown;
        remotePlayerName = string.Empty;

        if (requestedByUser)
        {
            Role = ChessOnlineSessionRole.None;
            RoomCode = string.Empty;
            ServerEndpointDisplay = string.Empty;
            ActivePort = 0;
            SetState(ChessOnlineSessionState.Idle, statusMessage);
        }
        else if (State != ChessOnlineSessionState.Error)
        {
            Role = ChessOnlineSessionRole.None;
            RoomCode = string.Empty;
            ActivePort = 0;
            SetState(ChessOnlineSessionState.Idle, statusMessage);
        }
    }

    private void SetState(ChessOnlineSessionState newState, string message)
    {
        State = newState;
        StatusMessage = message;
        StateChanged?.Invoke();
    }

    private void RefreshConnectionHealth()
    {
        ChessLanConnectionHealth nextHealth;
        if (!IsConnected)
        {
            nextHealth = ChessLanConnectionHealth.Unknown;
        }
        else
        {
            double silenceSeconds = SecondsSinceLastMessage;
            if (silenceSeconds >= LostConnectionSilenceSeconds)
                nextHealth = ChessLanConnectionHealth.Lost;
            else if (silenceSeconds >= WeakConnectionSilenceSeconds ||
                (roundTripMilliseconds >= 0d && roundTripMilliseconds >= WeakRoundTripThresholdMilliseconds))
                nextHealth = ChessLanConnectionHealth.Weak;
            else
                nextHealth = ChessLanConnectionHealth.Good;
        }

        if (nextHealth == connectionHealth)
            return;

        connectionHealth = nextHealth;
        StateChanged?.Invoke();
    }

    private void EnqueueMainThread(Action action)
    {
        lock (queueLock)
        {
            mainThreadActions.Enqueue(action);
        }
    }

    private static string NormalizeRoomCode(string roomCode)
    {
        return string.IsNullOrWhiteSpace(roomCode) ? string.Empty : roomCode.Trim().ToUpperInvariant();
    }
}
