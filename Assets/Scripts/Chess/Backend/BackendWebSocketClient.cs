using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

public sealed class BackendWebSocketClient : IBackendWebSocketTransport
{
    private readonly SynchronizationContext mainThreadContext;
    private ClientWebSocket socket;
    private CancellationTokenSource cancellation;
    private Task receiveTask;

    public bool IsConnected => socket != null && socket.State == WebSocketState.Open;
    public DateTime LastMessageUtc { get; private set; } = DateTime.MinValue;

    public event Action Connected;
    public event Action<BackendSocketEnvelope> MessageReceived;
    public event Action<string> Closed;
    public event Action<string> Error;

    public BackendWebSocketClient()
    {
        mainThreadContext = SynchronizationContext.Current ?? new SynchronizationContext();
    }

    public async Task ConnectAsync(string roomCode, string token)
    {
        await DisconnectAsync();

        cancellation = new CancellationTokenSource();
        socket = new ClientWebSocket();
        Uri uri = new Uri($"{BackendConfig.WebSocketUrl}?roomCode={Uri.EscapeDataString(roomCode)}&token={Uri.EscapeDataString(token)}");

        try
        {
            await socket.ConnectAsync(uri, cancellation.Token);
            LastMessageUtc = DateTime.UtcNow;
            Post(() => Connected?.Invoke());
            receiveTask = Task.Run(ReceiveLoopAsync);
        }
        catch (Exception exception)
        {
            Post(() => Error?.Invoke($"WebSocket connect failed: {exception.Message}"));
            await DisconnectAsync();
        }
    }

    public async Task SendAsync(string type, string requestId, object payload)
    {
        if (!IsConnected)
            return;

        try
        {
            string json = JsonConvert.SerializeObject(new
            {
                type,
                requestId,
                payload = payload ?? new { }
            });
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellation.Token);
        }
        catch (Exception exception)
        {
            Post(() => Error?.Invoke($"WebSocket send failed: {exception.Message}"));
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            cancellation?.Cancel();
            if (socket != null)
            {
                if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", CancellationToken.None);

                socket.Dispose();
                socket = null;
            }
        }
        catch
        {
            if (socket != null)
            {
                socket.Dispose();
                socket = null;
            }
        }
        finally
        {
            cancellation?.Dispose();
            cancellation = null;
            receiveTask = null;
        }
    }

    private async Task ReceiveLoopAsync()
    {
        byte[] buffer = new byte[16384];

        try
        {
            while (socket != null && socket.State == WebSocketState.Open && cancellation != null && !cancellation.IsCancellationRequested)
            {
                StringBuilder builder = new StringBuilder();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Post(() => Closed?.Invoke("WebSocket closed by server."));
                        await DisconnectAsync();
                        return;
                    }

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                LastMessageUtc = DateTime.UtcNow;
                string json = builder.ToString();
                BackendSocketEnvelope envelope = JsonConvert.DeserializeObject<BackendSocketEnvelope>(json);
                if (envelope != null)
                    Post(() => MessageReceived?.Invoke(envelope));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Post(() => Error?.Invoke($"WebSocket receive failed: {exception.Message}"));
            Post(() => Closed?.Invoke("WebSocket disconnected unexpectedly."));
        }
    }

    private void Post(Action action)
    {
        mainThreadContext.Post(_ => action?.Invoke(), null);
    }

    public void Dispose()
    {
        _ = DisconnectAsync();
    }
}
