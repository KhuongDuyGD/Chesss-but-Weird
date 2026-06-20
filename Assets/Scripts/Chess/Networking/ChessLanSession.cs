using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public enum ChessLanSessionRole
{
    None,
    Host,
    Client
}

public enum ChessLanSessionState
{
    Idle,
    Hosting,
    Connecting,
    Connected,
    Error
}

public enum ChessLanConnectionHealth
{
    Unknown,
    Good,
    Weak,
    Lost
}

public class ChessLanSession : MonoBehaviour
{
    private const string LogPrefix = "[ChessLAN]";
    private const string StartCommand = "START";
    private const string MoveCommand = "MOVE";
    private const string HelloCommand = "HELLO";
    private const string PingCommand = "PING";
    private const string PongCommand = "PONG";
    private const int ConnectTimeoutMilliseconds = 6000;
    private const double HeartbeatIntervalSeconds = 2d;
    private const double WeakConnectionSilenceSeconds = 4d;
    private const double LostConnectionSilenceSeconds = 8d;
    private const double WeakRoundTripThresholdMilliseconds = 250d;

    private readonly Queue<Action> mainThreadActions = new Queue<Action>();
    private readonly object queueLock = new object();
    private readonly object sendLock = new object();

    private TcpListener listener;
    private TcpClient client;
    private StreamReader reader;
    private StreamWriter writer;
    private Thread acceptThread;
    private Thread connectThread;
    private Thread receiveThread;
    private bool shutdownRequested;
    private DateTime lastHeartbeatSentUtc;
    private DateTime lastMessageReceivedUtc;
    private double roundTripMilliseconds = -1d;
    private ChessLanConnectionHealth connectionHealth = ChessLanConnectionHealth.Unknown;
    private string remoteMachineName = string.Empty;

    public ChessLanSessionRole Role { get; private set; } = ChessLanSessionRole.None;
    public ChessLanSessionState State { get; private set; } = ChessLanSessionState.Idle;
    public string StatusMessage { get; private set; } = "Idle.";
    public string RemoteEndpointDisplay { get; private set; } = string.Empty;
    public int ActivePort { get; private set; }
    public bool IsConnected => State == ChessLanSessionState.Connected && client != null && client.Connected;
    public string LocalMachineName => Environment.MachineName;
    public string RemoteMachineName => remoteMachineName;
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
            NotifyPeerDisconnected("LAN connection lost.");
    }

    private void OnDestroy()
    {
        DisconnectInternal(false, true, "Session destroyed.");
    }

    public void Host(int port)
    {
        Log($"Host requested on TCP port {port}.");
        Disconnect();

        try
        {
            shutdownRequested = false;
            ActivePort = port;
            Role = ChessLanSessionRole.Host;
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Log($"TCP listener started on 0.0.0.0:{port}. Local IPv4: {string.Join(", ", GetLocalIpv4Addresses())}");
            SetState(ChessLanSessionState.Hosting, $"Hosting on port {port}. Waiting for LAN peer...");

            acceptThread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "Chess LAN Accept"
            };
            acceptThread.Start();
        }
        catch (Exception exception)
        {
            LogError($"Unable to start TCP host on port {port}: {exception.GetType().Name}: {exception.Message}");
            Fail($"Unable to start LAN host: {exception.Message}");
        }
    }

    public void Join(string address, int port)
    {
        Log($"Join requested for TCP endpoint {address}:{port}.");
        Disconnect();

        shutdownRequested = false;
        ActivePort = port;
        Role = ChessLanSessionRole.Client;
        SetState(ChessLanSessionState.Connecting, $"Connecting to {address}:{port}...");

        connectThread = new Thread(() => ConnectLoop(address, port))
        {
            IsBackground = true,
            Name = "Chess LAN Connect"
        };
        connectThread.Start();
    }

    public void Disconnect()
    {
        Log($"Disconnect requested. Current state={State}, role={Role}, remote={RemoteEndpointDisplay}.");
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

    public static string[] GetLocalIpv4Addresses()
    {
        List<string> addresses = new List<string>();

        try
        {
            IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());
            for (int i = 0; i < hostEntry.AddressList.Length; i++)
            {
                IPAddress address = hostEntry.AddressList[i];
                if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                    continue;

                string addressText = address.ToString();
                if (!addresses.Contains(addressText))
                    addresses.Add(addressText);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[ChessLAN] Unable to list local IP addresses: {exception.Message}");
        }

        if (addresses.Count == 0)
            addresses.Add("127.0.0.1");

        return addresses.ToArray();
    }

    private void AcceptLoop()
    {
        try
        {
            Log($"Accept thread waiting for TCP peer on port {ActivePort}.");
            TcpClient acceptedClient = listener.AcceptTcpClient();
            if (shutdownRequested)
            {
                Log("Accept thread received a peer after shutdown; closing accepted socket.");
                acceptedClient.Close();
                return;
            }

            acceptedClient.NoDelay = true;
            string remoteDisplay = acceptedClient.Client.RemoteEndPoint?.ToString() ?? "LAN peer";
            Log($"TCP peer accepted from {remoteDisplay}. Queueing stream attach on main thread.");
            EnqueueMainThread(() => AttachClient(acceptedClient, remoteDisplay));
        }
        catch (SocketException exception)
        {
            LogWarning($"TCP accept socket exception: {exception.SocketErrorCode} / {exception.Message}");
            if (!shutdownRequested)
                EnqueueMainThread(() => Fail("LAN host stopped listening unexpectedly."));
        }
        catch (Exception exception)
        {
            LogError($"TCP accept failed: {exception.GetType().Name}: {exception.Message}");
            if (!shutdownRequested)
                EnqueueMainThread(() => Fail($"LAN host failed: {exception.Message}"));
        }
    }

    private void ConnectLoop(string address, int port)
    {
        TcpClient connectingClient = new TcpClient();
        try
        {
            connectingClient.NoDelay = true;
            Log($"TCP connect begin to {address}:{port} with {ConnectTimeoutMilliseconds}ms timeout.");
            IAsyncResult connectResult = connectingClient.BeginConnect(address, port, null, null);
            bool connected = connectResult.AsyncWaitHandle.WaitOne(ConnectTimeoutMilliseconds);
            if (!connected)
                throw new TimeoutException($"Connection to {address}:{port} timed out after {ConnectTimeoutMilliseconds}ms.");

            connectingClient.EndConnect(connectResult);
            Log($"TCP connect succeeded to {address}:{port}. Local endpoint={connectingClient.Client.LocalEndPoint}, remote={connectingClient.Client.RemoteEndPoint}.");
            if (shutdownRequested)
            {
                Log("TCP connect succeeded after shutdown; closing socket.");
                connectingClient.Close();
                return;
            }

            string remoteDisplay = connectingClient.Client.RemoteEndPoint?.ToString() ?? $"{address}:{port}";
            EnqueueMainThread(() => AttachClient(connectingClient, remoteDisplay));
        }
        catch (SocketException exception)
        {
            LogWarning($"TCP connect socket exception to {address}:{port}: {exception.SocketErrorCode} / {exception.Message}");
            connectingClient.Close();
            if (!shutdownRequested)
                EnqueueMainThread(() => Fail($"Unable to connect to {address}:{port}: {exception.Message}"));
        }
        catch (Exception exception)
        {
            LogWarning($"TCP connect failed to {address}:{port}: {exception.GetType().Name}: {exception.Message}");
            connectingClient.Close();
            if (!shutdownRequested)
                EnqueueMainThread(() => Fail($"Unable to connect to {address}:{port}: {exception.Message}"));
        }
    }

    private void AttachClient(TcpClient connectedClient, string remoteDisplay)
    {
        try
        {
            Log($"Attaching TCP stream. Role={Role}, remote={remoteDisplay}, local={connectedClient.Client.LocalEndPoint}.");
            client = connectedClient;
            RemoteEndpointDisplay = remoteDisplay;
            remoteMachineName = string.Empty;
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

            SetState(ChessLanSessionState.Connected, $"Connected to {remoteDisplay}.");
            Log($"TCP stream ready. Sending HELLO as {LocalMachineName}.");
            SendLine($"{HelloCommand}|{LocalMachineName}", false);

            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "Chess LAN Receive"
            };
            receiveThread.Start();
            Log("Receive thread started.");
        }
        catch (Exception exception)
        {
            LogError($"Unable to attach TCP stream: {exception.GetType().Name}: {exception.Message}");
            Fail($"Unable to open LAN data stream: {exception.Message}");
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
                {
                    LogWarning("Receive loop got end-of-stream from peer.");
                    break;
                }

                string capturedLine = line;
                Log($"RX line: {GetSafeProtocolLog(capturedLine)}");
                EnqueueMainThread(() => HandleIncomingLine(capturedLine));
            }
        }
        catch (IOException exception)
        {
            LogWarning($"Receive loop IO exception: {exception.Message}");
            if (!shutdownRequested)
            {
                disconnectAlreadyQueued = true;
                EnqueueMainThread(() => NotifyPeerDisconnected("Connection closed."));
            }
        }
        catch (ObjectDisposedException exception)
        {
            LogWarning($"Receive loop disposed: {exception.Message}");
            if (!shutdownRequested)
            {
                disconnectAlreadyQueued = true;
                EnqueueMainThread(() => NotifyPeerDisconnected("Connection disposed."));
            }
        }
        catch (Exception exception)
        {
            LogError($"Receive loop failed: {exception.GetType().Name}: {exception.Message}");
            if (!shutdownRequested)
            {
                disconnectAlreadyQueued = true;
                EnqueueMainThread(() => NotifyPeerDisconnected($"LAN receive failed: {exception.Message}"));
            }
        }
        finally
        {
            if (!shutdownRequested && !disconnectAlreadyQueued)
                EnqueueMainThread(() => NotifyPeerDisconnected("Peer disconnected."));
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

        if (string.Equals(command, HelloCommand, StringComparison.OrdinalIgnoreCase))
        {
            remoteMachineName = payload?.Trim() ?? string.Empty;
            Log($"HELLO received. Remote machine='{remoteMachineName}'.");
            StateChanged?.Invoke();
            return;
        }

        if (string.Equals(command, PingCommand, StringComparison.OrdinalIgnoreCase))
        {
            Log("PING received. Sending PONG.");
            SendLine($"{PongCommand}|{payload}", false);
            return;
        }

        if (string.Equals(command, PongCommand, StringComparison.OrdinalIgnoreCase))
        {
            if (long.TryParse(payload, out long sentTicks))
            {
                DateTime sentUtc = new DateTime(sentTicks, DateTimeKind.Utc);
                roundTripMilliseconds = Math.Max(0d, (DateTime.UtcNow - sentUtc).TotalMilliseconds);
                Log($"PONG received. RTT={roundTripMilliseconds:0.0}ms.");
                RefreshConnectionHealth();
            }

            return;
        }

        if (string.Equals(command, StartCommand, StringComparison.OrdinalIgnoreCase))
        {
            Log($"START received. Payload='{payload}'.");
            if (Enum.TryParse(payload, true, out PieceTeam hostTeam))
                StartGameRequested?.Invoke(hostTeam);
            else
                Fail($"Received invalid LAN start payload: {payload}");

            return;
        }

        if (string.Equals(command, MoveCommand, StringComparison.OrdinalIgnoreCase))
        {
            Log($"MOVE received. Payload='{payload}'.");
            if (ChessLanMove.TryParse(payload, out ChessLanMove move))
                MoveReceived?.Invoke(move);
            else
                Fail($"Received invalid LAN move payload: {payload}");

            return;
        }

        Fail($"Received unknown LAN command: {command}");
    }

    private bool SendLine(string line, bool reportFailure = true)
    {
        if (!IsConnected || writer == null)
        {
            LogWarning($"TX skipped because stream is not connected. Line={GetSafeProtocolLog(line)}");
            return false;
        }

        try
        {
            lock (sendLock)
            {
                Log($"TX line: {GetSafeProtocolLog(line)}");
                writer.WriteLine(line);
                writer.Flush();
            }

            return true;
        }
        catch (Exception exception)
        {
            LogWarning($"TX failed: {exception.GetType().Name}: {exception.Message}. Line={GetSafeProtocolLog(line)}");
            if (reportFailure)
                Fail($"Unable to send LAN data: {exception.Message}");

            return false;
        }
    }

    private void NotifyPeerDisconnected(string message)
    {
        LogWarning($"Peer disconnected. Message='{message}'.");
        DisconnectInternal(false, true, message);
        PeerDisconnected?.Invoke(message);
    }

    private void Fail(string message)
    {
        LogError($"Session fail. Message='{message}'. State={State}, role={Role}, remote={RemoteEndpointDisplay}.");
        DisconnectInternal(false, true, message);
        SetState(ChessLanSessionState.Error, message);
    }

    private void DisconnectInternal(bool requestedByUser, bool clearTransport, string statusMessage)
    {
        Log($"DisconnectInternal requestedByUser={requestedByUser}, clearTransport={clearTransport}, status='{statusMessage}', state={State}, role={Role}.");
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

            if (listener != null)
            {
                listener.Stop();
                listener = null;
            }
        }

        acceptThread = null;
        connectThread = null;
        receiveThread = null;
        roundTripMilliseconds = -1d;
        lastHeartbeatSentUtc = DateTime.MinValue;
        lastMessageReceivedUtc = default;
        connectionHealth = ChessLanConnectionHealth.Unknown;
        remoteMachineName = string.Empty;

        if (requestedByUser)
        {
            Role = ChessLanSessionRole.None;
            RemoteEndpointDisplay = string.Empty;
            ActivePort = 0;
            SetState(ChessLanSessionState.Idle, statusMessage);
        }
        else if (State != ChessLanSessionState.Error)
        {
            Role = ChessLanSessionRole.None;
            RemoteEndpointDisplay = string.Empty;
            ActivePort = 0;
            SetState(ChessLanSessionState.Idle, statusMessage);
        }
    }

    private void SetState(ChessLanSessionState newState, string message)
    {
        Log($"State change: {State} -> {newState}. Message='{message}'.");
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

        Log($"Connection health changed: {connectionHealth} -> {nextHealth}. Silence={SecondsSinceLastMessage:0.0}s, RTT={roundTripMilliseconds:0.0}ms.");
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

    private static string GetSafeProtocolLog(string line)
    {
        if (string.IsNullOrEmpty(line))
            return "<empty>";

        if (line.StartsWith(MoveCommand, StringComparison.OrdinalIgnoreCase))
            return line;

        if (line.Length <= 160)
            return line;

        return $"{line.Substring(0, 160)}...";
    }

    private static void Log(string message)
    {
        Debug.Log($"{LogPrefix} {message}");
    }

    private static void LogWarning(string message)
    {
        Debug.LogWarning($"{LogPrefix} {message}");
    }

    private static void LogError(string message)
    {
        Debug.LogError($"{LogPrefix} {message}");
    }
}
