using System;
using System.Threading.Tasks;

public interface IBackendWebSocketTransport : IDisposable
{
    bool IsConnected { get; }
    DateTime LastMessageUtc { get; }
    event Action Connected;
    event Action<BackendSocketEnvelope> MessageReceived;
    event Action<string> Closed;
    event Action<string> Error;
    Task ConnectAsync(string roomCode, string token);
    Task SendAsync(string type, string requestId, object payload);
    Task DisconnectAsync();
}
