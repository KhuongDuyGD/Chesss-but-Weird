using System;
using System.Threading.Tasks;

namespace ChessButWeird.Online
{
    public sealed class OnlineRestService
    {
        private readonly ApiClient client;
        public OnlineRestService(ApiClient client = null) { this.client = client ?? ApiClient.Shared; }
        private static string Id(string id) => Uri.EscapeDataString(id ?? throw new ArgumentNullException(nameof(id)));
        public Task<Ticket> QueueAsync(LobbyRequest request) => client.PostAsync<Ticket>("/api/matchmaking/tickets", request, true);
        public Task<Ticket> CurrentTicketAsync() => client.GetAsync<Ticket>("/api/matchmaking/tickets/current", true);
        public Task<Ticket> CancelQueueAsync(string id) => client.DeleteAsync<Ticket>("/api/matchmaking/tickets/" + Id(id), true);
        public Task<Room> CreateRoomAsync(LobbyRequest request) => client.PostAsync<Room>("/api/rooms", request, true);
        public Task<Page<RoomListing>> RoomsAsync(int page = 1, int pageSize = 8, string mode = "Classic", string region = "VN") =>
            client.GetAsync<Page<RoomListing>>($"/api/rooms?page={page}&pageSize={pageSize}&mode={Id(mode)}&region={Id(region)}", true);
        public Task<Room> JoinRoomAsync(string code) => client.PostAsync<Room>("/api/rooms/join", new { code }, true);
        public Task<Room> RoomAsync(string id) => client.GetAsync<Room>("/api/rooms/" + Id(id), true);
        public Task<Room> SettingsAsync(string id, GameSettings settings) => client.PatchAsync<Room>("/api/rooms/" + Id(id) + "/settings", settings, true);
        public Task<Room> LeaveRoomAsync(string id) => client.PostAsync<Room>("/api/rooms/" + Id(id) + "/leave", null, true);
        public Task<Room> CloseRoomAsync(string id) => client.DeleteAsync<Room>("/api/rooms/" + Id(id), true);
        public Task<MatchState> StartRoomAsync(string id) => client.PostAsync<MatchState>("/api/rooms/" + Id(id) + "/start", null, true);
        public Task<MatchState> CurrentMatchAsync() => client.GetAsync<MatchState>("/api/matches/current", true);
        public Task<MatchSummary> MatchAsync(string id) => client.GetAsync<MatchSummary>("/api/matches/" + Id(id), true);
        public Task<MatchState> StateAsync(string id) => client.GetAsync<MatchState>("/api/matches/" + Id(id) + "/state", true);
        public Task<Result> ResultAsync(string id) => client.GetAsync<Result>("/api/matches/" + Id(id) + "/result", true);
        public Task<Page<MatchSummary>> HistoryAsync(int page = 1, int pageSize = 20) =>
            client.GetAsync<Page<MatchSummary>>($"/api/users/me/matches?page={page}&pageSize={pageSize}", true);
        public Task<Page<MoveRecord>> MovesAsync(string id, int page = 1, int pageSize = 100, long afterSequence = 0) =>
            client.GetAsync<Page<MoveRecord>>($"/api/matches/{Id(id)}/moves?page={page}&pageSize={pageSize}&afterSequence={afterSequence}", true);
    }
}
