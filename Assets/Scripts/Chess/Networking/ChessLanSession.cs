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
    private const string StartCommand = "START";
    private const string MoveCommand = "MOVE";
    private const string HelloCommand = "HELLO";
    private const string PingCommand = "PING";
    private const string PongCommand = "PONG";
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
    }

    private void OnDestroy()
    {
        DisconnectInternal(false, true, "Session destroyed.");
    }

    public void Host(int port)
    {
        Disconnect();

        try
        {
            shutdownRequested = false;
            ActivePort = port;
            Role = ChessLanSessionRole.Host;
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
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
            Fail($"Unable to start LAN host: {exception.Message}");
        }
    }

    public void Join(string address, int port)
    {
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
            TcpClient acceptedClient = listener.AcceptTcpClient();
            if (shutdownRequested)
            {
                acceptedClient.Close();
                return;
            }

            acceptedClient.NoDelay = true;
            string remoteDisplay = acceptedClient.Client.RemoteEndPoint?.ToString() ?? "LAN peer";
            EnqueueMainThread(() => AttachClient(acceptedClient, remoteDisplay));
        }
        catch (SocketException)
        {
            if (!shutdownRequested)
                EnqueueMainThread(() => Fail("LAN host stopped listening unexpectedly."));
        }
        catch (Exception exception)
        {
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
            connectingClient.Connect(address, port);
            if (shutdownRequested)
            {
                connectingClient.Close();
                return;
            }

            string remoteDisplay = connectingClient.Client.RemoteEndPoint?.ToString() ?? $"{address}:{port}";
            EnqueueMainThread(() => AttachClient(connectingClient, remoteDisplay));
        }
        catch (Exception exception)
        {
            connectingClient.Close();
            if (!shutdownRequested)
                EnqueueMainThread(() => Fail($"Unable to connect to {address}:{port}: {exception.Message}"));
        }
    }

    private void AttachClient(TcpClient connectedClient, string remoteDisplay)
    {
        try
        {
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
            SendLine($"{HelloCommand}|{LocalMachineName}", false);

            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "Chess LAN Receive"
            };
            receiveThread.Start();
        }
        catch (Exception exception)
        {
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
                EnqueueMainThread(() => NotifyPeerDisconnected("Connection closed."));
            }
        }
        catch (ObjectDisposedException)
        {
            if (!shutdownRequested)
            {
                disconnectAlreadyQueued = true;
                EnqueueMainThread(() => NotifyPeerDisconnected("Connection disposed."));
            }
        }
        catch (Exception exception)
        {
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
            StateChanged?.Invoke();
            return;
        }

        if (string.Equals(command, PingCommand, StringComparison.OrdinalIgnoreCase))
        {
            SendLine($"{PongCommand}|{payload}", false);
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
                Fail($"Received invalid LAN start payload: {payload}");

            return;
        }

        if (string.Equals(command, MoveCommand, StringComparison.OrdinalIgnoreCase))
        {
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
                Fail($"Unable to send LAN data: {exception.Message}");

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
        SetState(ChessLanSessionState.Error, message);
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
}
