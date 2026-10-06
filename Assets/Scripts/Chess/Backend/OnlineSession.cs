using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ChessButWeird.Online
{
    /// <summary>Owns transport, snapshot watermarks and command recovery.
    /// Public methods are called on Unity's main thread.</summary>
    public sealed class OnlineSession : IDisposable
    {
        public readonly OnlineRestService Rest = new OnlineRestService();
        private readonly SignalRGameTransport hub = new SignalRGameTransport();
        private readonly SemaphoreSlim connectionGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim commandGate = new SemaphoreSlim(1, 1);
        private readonly SortedDictionary<long, ServerEvent> buffer = new SortedDictionary<long, ServerEvent>();
        private readonly HashSet<string> ignoredMatches = new HashSet<string>();
        private readonly HashSet<string> ignoredRooms = new HashSet<string>();
        private readonly SemaphoreSlim snapshotGate = new SemaphoreSlim(1, 1);
        private string subscribingMatchId;
        private bool disposed, subscribing;
        private string pendingMethod;
        private object[] pendingArguments;
        private Task resync;
        private readonly string userId = PlayerAuthService.UserId;
        private string connectedAccessToken;
        public bool NeedsTokenRenewal => Connected && connectedAccessToken != AuthStorage.GetAccessToken();
        private string RoomSaveKey => "chess.online.room.v1." + userId;
        private void RequireSession()
        { if (disposed || PlayerAuthService.UserId != userId || !PlayerAuthService.CanUseOnlineFeatures) throw new OperationCanceledException("The online account session changed."); }
        private void RememberRoom()
        {
            if (disposed || PlayerAuthService.UserId != userId) return;
            if (Room?.status == "Open") PlayerPrefs.SetString(RoomSaveKey, Room.roomId);
            else PlayerPrefs.DeleteKey(RoomSaveKey);
            PlayerPrefs.Save();
        }
        private async Task RestoreRoomAsync()
        {
            string id = Room?.roomId ?? PlayerPrefs.GetString(RoomSaveKey, "");
            if (string.IsNullOrEmpty(id)) return;
            try { Room = await Rest.RoomAsync(id); RequireSession(); if (Room?.status != "Open") Room = null; RememberRoom(); }
            catch (ApiException e) when (e.StatusCode == 403 || e.StatusCode == 404)
            { RequireSession(); Room = null; RememberRoom(); }
        }
        public MatchState State { get; private set; }
        public Ticket Ticket { get; private set; }
        public Room Room { get; private set; }
        public bool Connected => !disposed && hub.IsConnected;
        public bool HasPendingCommand => pendingArguments != null;
        public bool Recovering => subscribing || buffer.Count > 0 || (resync != null && !resync.IsCompleted);
        public event Action<MatchState> StateChanged;
        public event Action LobbyChanged;
        public event Action<string> Notice;
        public event Action ConnectionChanged;
        public event Action HistoryChanged;

        public async Task RenewConnectionAsync()
        { hub.Disconnect(); await ConnectAsync(); }
        public void ForgetFinishedMatch()
        {
            if (State != null && !State.IsActive) { ignoredMatches.Add(State.matchId); State = null; buffer.Clear(); }
            if (Room?.status != "Open") { Room = null; RememberRoom(); }
        }

        public OnlineSession()
        {
            hub.EventReceived += OnEvent;
            hub.Closed += reason => { ConnectionChanged?.Invoke(); Notice?.Invoke(reason); };
        }
        public async Task ConnectAsync()
        {
            await connectionGate.WaitAsync();
            try
            {
                RequireSession();
                if (Connected) return;
                string token = await ApiClient.Shared.GetAccessTokenAsync();
                RequireSession();
                await hub.ConnectAsync(token);
                connectedAccessToken = token;
                if (disposed) throw new OperationCanceledException();
                var match = await Rest.CurrentMatchAsync();
                if (match != null && !ignoredMatches.Contains(match.matchId)) await SubscribeAsync(match.matchId);
                else if (State != null && !ignoredMatches.Contains(State.matchId)) await SubscribeAsync(State.matchId);
                Ticket = await Rest.CurrentTicketAsync();
                await RestoreRoomAsync();
                if (disposed) return;
                ConnectionChanged?.Invoke(); LobbyChanged?.Invoke();
                HistoryChanged?.Invoke();
            }
            catch
            {
                hub.Disconnect(); ConnectionChanged?.Invoke(); throw;
            }
            finally { connectionGate.Release(); }
        }
        public async Task RefreshAsync()
        {
            await ConnectAsync();
            var match = await Rest.CurrentMatchAsync();
            if (match != null && !ignoredMatches.Contains(match.matchId)) await SubscribeAsync(match.matchId);
            else if (State != null && !ignoredMatches.Contains(State.matchId)) await SubscribeAsync(State.matchId);
            Ticket = await Rest.CurrentTicketAsync();
            await RestoreRoomAsync();
            LobbyChanged?.Invoke();
        }
        public async Task SubscribeAsync(string matchId)
        {
            if (disposed) return;
            await snapshotGate.WaitAsync();
            subscribing = true; subscribingMatchId = matchId;
            try
            {
                var subscription = await hub.InvokeAsync<Subscription>("SubscribeMatch", matchId);
                if (disposed || ignoredMatches.Contains(matchId)) return;
                if (State?.IsActive == true && State.matchId != matchId && !subscription.state.IsActive) return;
                Apply(subscription.state);
                foreach (var sequence in buffer.Keys.Where(k => k <= subscription.resumeAfterSequence).ToList()) buffer.Remove(sequence);
            }
            finally { subscribing = false; subscribingMatchId = null; snapshotGate.Release(); }
            Drain();
            HistoryChanged?.Invoke();
        }
        private void Apply(MatchState state)
        {
            if (disposed || state == null || ignoredMatches.Contains(state.matchId)) return;
            if (State?.IsActive == true && State.matchId != state.matchId && !state.IsActive) return;
            if (State != null && State.matchId == state.matchId && state.eventSequence < State.eventSequence) return;
            if (State == null || State.matchId != state.matchId)
                foreach (var k in buffer.Keys.Where(k => buffer[k].matchId != state.matchId).ToList()) buffer.Remove(k);
            State = state;
            StateChanged?.Invoke(state);
        }
        private void OnEvent(ServerEvent e)
        {
            if (disposed || PlayerAuthService.UserId != userId || !PlayerAuthService.CanUseOnlineFeatures) return;
            if (e.type == "GameStateUpdated" && e.payload?["command"] != null) HistoryChanged?.Invoke();
            if (e.type == "QueueStatusChanged")
            {
                Ticket = (e.payload?["ticket"] ?? e.payload)?.ToObject<Ticket>();
                LobbyChanged?.Invoke(); return;
            }
            if (e.type == "RoomUpdated" || e.type == "RoomClosed")
            {
                var room = (e.payload?["room"] ?? e.payload)?.ToObject<Room>();
                if (room != null && ignoredRooms.Contains(room.roomId)) return;
                if (e.type == "RoomClosed")
                { if (room != null) ignoredRooms.Add(room.roomId); Room = null; RememberRoom(); LobbyChanged?.Invoke(); return; }
                // A delayed update cannot reopen a room after leaving it.
                if (room != null && room.members.Contains(PlayerAuthService.UserId) &&
                    (Room == null || Room.roomId == room.roomId)) Room = room;
                else if (room != null && Room?.roomId == room.roomId) Room = null;
                RememberRoom();
                LobbyChanged?.Invoke(); return;
            }
            if (e.type == "CommandRejected") { Notice?.Invoke((string)e.payload?["errorCode"] ?? "Command rejected."); return; }
            if (e.type == "RematchRequested") Notice?.Invoke("A rematch was requested. Both players can request a rematch to agree.");
            if (string.IsNullOrEmpty(e.matchId) || ignoredMatches.Contains(e.matchId)) return;
            if (subscribing && e.matchId == subscribingMatchId) { buffer[e.sequence] = e; return; }
            var state = e.payload?["state"]?.ToObject<MatchState>();
            if (State == null || State.matchId != e.matchId)
            {
                if (state != null && (State == null || !State.IsActive)) Apply(state);
                return;
            }
            if (e.sequence <= State.eventSequence) return;
            buffer[e.sequence] = e;
            if (!subscribing) Drain();
        }
        private void Drain()
        {
            if (disposed || subscribing || State == null) return;
            while (buffer.TryGetValue(State.eventSequence + 1, out var e))
            {
                buffer.Remove(e.sequence);
                var state = e.payload?["state"]?.ToObject<MatchState>();
                if (state != null) Apply(state);
                else
                {
                    // Events without a full state (e.g. expired draw offers) also
                    // need a new snapshot; do not advance past unobserved state.
                    StartResync(); return;
                }
            }
            if (buffer.Count > 0) StartResync();
        }
        private void StartResync()
        { if (resync == null || resync.IsCompleted) resync = ResyncAsync(); }
        private async Task ResyncAsync()
        {
            try
            {
                for (int attempt = 0; attempt < 3 && Connected && State != null; attempt++)
                {
                    await SubscribeAsync(State.matchId);
                    if (buffer.Count == 0) return;
                    await Task.Delay(250);
                }
                if (!disposed && buffer.Count > 0) { hub.Disconnect(); ConnectionChanged?.Invoke(); }
            }
            catch (Exception)
            {
                if (!disposed) { hub.Disconnect(); ConnectionChanged?.Invoke(); Notice?.Invoke("State recovery is pending. Reconnecting..."); }
            }
        }
        public async Task QueueAsync(LobbyRequest request)
        {
            await ConnectAsync(); Ticket = await Rest.QueueAsync(request); LobbyChanged?.Invoke();
            if (!string.IsNullOrEmpty(Ticket.matchId)) await SubscribeAsync(Ticket.matchId);
        }
        public async Task CancelQueueAsync()
        {
            if (Ticket == null) return;
            Ticket = await Rest.CancelQueueAsync(Ticket.ticketId);
            if (!string.IsNullOrEmpty(Ticket.matchId))
            {
                await ConnectAsync();
                Apply(await hub.InvokeAsync<MatchState>("DeclineMatch", Ticket.matchId));
            }
            LobbyChanged?.Invoke();
        }
        public async Task CreateRoomAsync(LobbyRequest request)
        { await ConnectAsync(); Room = await Rest.CreateRoomAsync(request); RememberRoom(); LobbyChanged?.Invoke(); }
        public async Task ChangeSettingsAsync(GameSettings settings)
        {
            await ConnectAsync();
            if (Room == null) throw new ApiException("Create or join a room first.");
            Room = await Rest.SettingsAsync(Room.roomId, settings); RememberRoom(); LobbyChanged?.Invoke();
        }
        public async Task CloseRoomAsync()
        {
            await ConnectAsync();
            if (Room == null) throw new ApiException("Create or join a room first.");
            string id = Room.roomId; await Rest.CloseRoomAsync(id);
            ignoredRooms.Add(id); Room = null; RememberRoom(); LobbyChanged?.Invoke();
        }
        public async Task JoinRoomAsync(string code)
        { await ConnectAsync(); Room = await Rest.JoinRoomAsync(code); ignoredRooms.Remove(Room.roomId); RememberRoom(); LobbyChanged?.Invoke(); if (Room.matchId != null) await SubscribeAsync(Room.matchId); }
        public async Task StartRoomAsync()
        {
            await ConnectAsync();
            if (Room == null) throw new ApiException("Create or join a room first.");
            var state = await Rest.StartRoomAsync(Room.roomId); await SubscribeAsync(state.matchId);
        }
        public async Task LeaveAsync()
        {
            if (Ticket?.status == "Queued") await CancelQueueAsync();
            if (State?.IsActive == true)
            {
                await ConnectAsync();
                if (State.status == "AwaitingReady") Apply(await hub.InvokeAsync<MatchState>("DeclineMatch", State.matchId));
                else await CommandAsync("Resign", false);
            }
            if (State != null) ignoredMatches.Add(State.matchId);
            if (Room?.status == "Open")
            { string id = Room.roomId; await Rest.LeaveRoomAsync(id); ignoredRooms.Add(id); }
            State = null; buffer.Clear(); Room = null; Ticket = null; RememberRoom(); LobbyChanged?.Invoke();
        }
        public async Task ReadyAsync()
        { await ConnectAsync(); RequireMatch(); Apply(await hub.InvokeAsync<MatchState>("SetReady", State.matchId)); }
        public async Task AcceptAsync()
        { await ConnectAsync(); RequireMatch(); Apply(await hub.InvokeAsync<MatchState>("AcceptMatch", State.matchId)); }
        public Task<CommandAck> MoveAsync(MoveCommand move) => CommandAsync("SubmitMove", true, move);
        public Task<CommandAck> AbilityAsync(AbilityCommand ability) => CommandAsync("UseAbility", true, ability);
        public Task<CommandAck> ResignAsync() => CommandAsync("Resign", false);
        public Task<CommandAck> OfferDrawAsync() => CommandAsync("OfferDraw", false);
        public Task<CommandAck> ClaimDrawAsync() => CommandAsync("ClaimDraw", false);
        public async Task<CommandAck> RespondDrawAsync(bool accept)
        {
            await ConnectAsync();
            RequireMatch();
            if (State.drawOffer == null) throw new ApiException("No draw offer is pending.");
            var ack = await hub.InvokeAsync<CommandAck>("RespondDraw", State.matchId, State.drawOffer.id, accept);
            await SubscribeAsync(State.matchId); return ack;
        }
        public async Task RematchAsync(bool? accept = null)
        {
            await ConnectAsync(); RequireMatch();
            var next = accept == null ? await hub.InvokeAsync<MatchState>("RequestRematch", State.matchId) :
                await hub.InvokeAsync<MatchState>("RespondRematch", State.matchId, accept.Value);
            Apply(next);
        }
        private void RequireMatch()
        { if (State == null) throw new ApiException("There is no current match."); }
        private async Task<CommandAck> CommandAsync(string method, bool versioned, object payload = null)
        {
            await commandGate.WaitAsync();
            try
            {
                await ConnectAsync(); RequireMatch();
                if (pendingArguments != null) throw new ApiException("Recover the previous command before submitting another.");
                string id = Guid.NewGuid().ToString("N");
                pendingMethod = method;
                pendingArguments = versioned ? new object[] { State.matchId, id, State.stateVersion, payload } : new object[] { State.matchId, id };
                return await SendPendingAsync();
            }
            finally { commandGate.Release(); }
        }
        public async Task<CommandAck> RetryPendingAsync()
        {
            await commandGate.WaitAsync();
            try { await ConnectAsync(); return pendingArguments == null ? null : await SendPendingAsync(); }
            finally { commandGate.Release(); }
        }
        private async Task<CommandAck> SendPendingAsync()
        {
            string matchId = (string)pendingArguments[0];
            CommandAck ack;
            try { ack = await hub.InvokeAsync<CommandAck>(pendingMethod, pendingArguments); }
            catch (ApiException e) when (e.ErrorCode != "OnlineDisconnected" && e.ErrorCode != "OnlineTemporarilyUnavailable")
            { pendingArguments = null; throw; }
            catch (ApiException e) when (e.ErrorCode == "OnlineTemporarilyUnavailable")
            { hub.Disconnect(); ConnectionChanged?.Invoke(); throw; }
            pendingArguments = null;
            if (!ack.accepted) Notice?.Invoke(ack.errorCode);
            // Also recover if the command committed but its push was lost.
            await SubscribeAsync(matchId);
            HistoryChanged?.Invoke();
            return ack;
        }
        public void Dispose()
        {
            disposed = true; hub.Dispose(); buffer.Clear();
            StateChanged = null; LobbyChanged = null; Notice = null; ConnectionChanged = null; HistoryChanged = null;
        }
    }
}
