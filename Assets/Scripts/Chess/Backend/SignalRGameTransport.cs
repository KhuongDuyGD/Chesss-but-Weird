using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChessButWeird.Online
{
    /// <summary>SignalR JSON v1 over native WebSockets, with completion IDs,
    /// record framing, handshake, keepalive and bounded message sizes.
    /// Uses the documented direct WebSocket connection (skip negotiation).
    /// Reconnection/state replay belongs to OnlineSession, not this transport.</summary>
    public sealed class SignalRGameTransport : IDisposable
    {
        private readonly SynchronizationContext context = SynchronizationContext.Current;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JToken>> pending = new ConcurrentDictionary<string, TaskCompletionSource<JToken>>();
        private readonly SemaphoreSlim sendGate = new SemaphoreSlim(1, 1);
        private ClientWebSocket socket;
        private CancellationTokenSource lifetime;
        private TaskCompletionSource<bool> handshake;
        private int generation;
        private long invocationId, lastReceived;
        private volatile bool connected;
        public bool IsConnected => connected && socket != null && socket.State == WebSocketState.Open;
        public event Action<ServerEvent> EventReceived;
        public event Action<string> Closed;

        public async Task ConnectAsync(string accessToken)
        {
            Disconnect();
            int epoch = generation;
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new PlatformNotSupportedException("This online transport requires a native Unity build.");
#else
            var ws = new ClientWebSocket();
            var cancel = new CancellationTokenSource();
            socket = ws; lifetime = cancel;
            handshake = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var scheme = new UriBuilder(ApiConfig.BaseUrl.TrimEnd('/') + "/hubs/game");
            scheme.Scheme = scheme.Scheme == "https" ? "wss" : "ws";
            ws.Options.SetRequestHeader("Authorization", "Bearer " + accessToken);
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(ApiConfig.TimeoutSeconds));
                    await ws.ConnectAsync(scheme.Uri, timeout.Token);
                }
                Interlocked.Exchange(ref lastReceived, DateTime.UtcNow.Ticks);
                _ = ReceiveAsync(ws, cancel, epoch);
                await SendAsync(new { protocol = "json", version = 1 }, cancel.Token);
                await AwaitWithTimeout(handshake.Task, 15000, cancel.Token);
                if (epoch != generation) throw new OperationCanceledException();
                connected = true;
                _ = KeepAliveAsync(cancel, epoch);
            }
            catch
            {
                if (epoch == generation) Disconnect();
                throw;
            }
#endif
        }
        public async Task<T> InvokeAsync<T>(string target, params object[] arguments)
        {
            if (!IsConnected) throw new ApiException("Online connection is unavailable.", errorCode: "OnlineDisconnected");
            string id = Interlocked.Increment(ref invocationId).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var completion = new TaskCompletionSource<JToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[id] = completion;
            var token = lifetime.Token;
            try
            {
                await SendAsync(new { type = 1, invocationId = id, target, arguments }, token);
                await AwaitWithTimeout(completion.Task, 20000, token);
                var value = await completion.Task;
                return value == null || value.Type == JTokenType.Null ? default : value.ToObject<T>();
            }
            finally { pending.TryRemove(id, out _); }
        }
        private async Task SendAsync(object message, CancellationToken ct)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message) + '\u001e');
            await sendGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var ws = socket;
                if (ws == null || ws.State != WebSocketState.Open) throw new ApiException("Online connection closed.", errorCode: "OnlineDisconnected");
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
            }
            finally { sendGate.Release(); }
        }
        private async Task ReceiveAsync(ClientWebSocket ws, CancellationTokenSource cancel, int epoch)
        {
            string reason = "Online connection closed. Reconnecting...";
            var buffer = new byte[8192];
            using (var frame = new MemoryStream())
            {
                try
                {
                    while (!cancel.IsCancellationRequested)
                    {
                        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancel.Token).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        if (result.MessageType != WebSocketMessageType.Text) throw new InvalidDataException("Unsupported hub frame.");
                        Interlocked.Exchange(ref lastReceived, DateTime.UtcNow.Ticks);
                        for (int i = 0; i < result.Count; i++)
                        {
                            if (buffer[i] == 0x1e)
                            {
                                var json = Encoding.UTF8.GetString(frame.ToArray()); frame.SetLength(0);
                                if (json.Length != 0) Process(JObject.Parse(json), epoch);
                            }
                            else frame.WriteByte(buffer[i]);
                            if (frame.Length > 2 * 1024 * 1024) throw new InvalidDataException("Hub frame exceeds limit.");
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception) { reason = "Online connection interrupted. Reconnecting..."; }
                finally
                {
                    if (epoch == generation)
                    {
                        connected = false;
                        FailPending(new ApiException("Online connection interrupted.", errorCode: "OnlineDisconnected"));
                        handshake?.TrySetException(new ApiException("Could not open the game hub. The online service may be recovering."));
                        cancel.Cancel(); ws.Abort();
                        Dispatch(epoch, () => Closed?.Invoke(reason));
                    }
                }
            }
        }
        private void Process(JObject message, int epoch)
        {
            if (epoch != generation) return;
            if (message["type"] == null)
            {
                if (message["error"] != null) handshake.TrySetException(new ApiException((string)message["error"]));
                else handshake.TrySetResult(true);
                return;
            }
            switch ((int)message["type"])
            {
                case 1:
                    var envelope = message["arguments"]?[0]?.ToObject<ServerEvent>();
                    if (envelope != null) Dispatch(epoch, () => EventReceived?.Invoke(envelope));
                    break;
                case 3:
                    if (pending.TryRemove((string)message["invocationId"] ?? "", out var call))
                    {
                        if (message["error"] != null)
                        {
                            string error = (string)message["error"];
                            // ASP.NET prepends a HubException description to the domain code.
                            int marker = error.LastIndexOf("HubException: ", StringComparison.Ordinal);
                            string code = marker >= 0 ? error.Substring(marker + 14).Trim() : error;
                            call.TrySetException(new ApiException(code, errorCode: code));
                        }
                        else call.TrySetResult(message["result"]);
                    }
                    break;
                case 6: break;
                case 7: socket?.Abort(); break;
            }
        }
        private async Task KeepAliveAsync(CancellationTokenSource cancel, int epoch)
        {
            try
            {
                while (!cancel.IsCancellationRequested && epoch == generation)
                {
                    await Task.Delay(10000, cancel.Token).ConfigureAwait(false);
                    if (DateTime.UtcNow.Ticks - Interlocked.Read(ref lastReceived) > TimeSpan.FromSeconds(45).Ticks)
                    { socket?.Abort(); return; }
                    await SendAsync(new { type = 6 }, cancel.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch { if (epoch == generation) socket?.Abort(); }
        }
        private void Dispatch(int epoch, Action action)
        {
            if (context != null) context.Post(_ => { if (epoch == generation) action(); }, null);
            else if (epoch == generation) action();
        }
        private static async Task AwaitWithTimeout(Task task, int milliseconds, CancellationToken ct)
        {
            if (await Task.WhenAny(task, Task.Delay(milliseconds, ct)).ConfigureAwait(false) != task)
            { ct.ThrowIfCancellationRequested(); throw new TimeoutException("The game server did not acknowledge the request. Reconnect to recover its outcome."); }
            await task.ConfigureAwait(false);
        }
        private void FailPending(Exception error)
        { foreach (var id in pending.Keys) if (pending.TryRemove(id, out var call)) call.TrySetException(error); }
        public void Disconnect()
        {
            generation++; connected = false;
            lifetime?.Cancel(); socket?.Abort(); socket?.Dispose(); socket = null;
            lifetime = null;
            handshake?.TrySetCanceled();
            FailPending(new OperationCanceledException("Online session was closed."));
        }
        public void Dispose() { Disconnect(); }
    }
}
