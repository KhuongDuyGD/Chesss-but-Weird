using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChessButWeird.Online;
using UnityEngine;

// Preserve the scene's existing component and lobby buttons. Networking is owned
// by OnlineSession/OnlineRestService/SignalRGameTransport, never by button scripts.
public partial class ChessLanController
{
    private const float ReferenceWidth = 1920f, ReferenceHeight = 1080f;
    private ChessGame chessGame;
    private ChessTurnSelectionUI turnSelectionUI;
    private NetworkLobbyUiController lobbyUi;
    private OnlineSession online;
    private OnlineAramPresenter aramPresenter;
    private bool showLanPanel, requestInFlight, reconnecting, leaving, commandBusy;
    private int lifecycle;
    private string presentedMatchId, refreshedResultId;
    private string preparedContentMatchId;
    private string preparedLoadoutFingerprint;
    private string trackedRoomId;
    private float nextReconnectAt, nextPollAt;
    private string requestedGameMode = "Classic", roomCodeInput = "", statusMessage = "Sign in to play online.";
    private string serverHealthStatus = "Offline";
    private NetworkLobbyUiMode lobbyMode;
    private readonly List<MatchSummary> recentMatches = new List<MatchSummary>();
    private LobbyRequest queueRequest, roomRequest;
    [SerializeField] private string onlineRegion = "VN";
    [SerializeField] private int onlineInitialSeconds = 600;
    [SerializeField] private int onlineIncrementSeconds = 5;
    public bool IsNetworkGameActive => online?.State?.IsActive == true;
    public string MatchConnectionState => online?.Connected == true ? (online.Recovering ? "Restoring state" : "Connected") : reconnecting ? "Reconnecting" : "Disconnected";
    // Online clocks continue during a local pause. No unsupported PAUSE hub call.
    public event Action<bool, string> OpponentPauseChanged;

    public void Initialize(ChessGame game, ChessTurnSelectionUI ui)
    {
        chessGame = game; turnSelectionUI = ui;
        game.OnlineMoveRequested += HandleMoveRequested;
        game.OnlineBoardClicked += HandleOnlineBoardClick;
        game.OnlinePointerBlocked = BlocksOnlineBoardPointer;
        game.ReturnedToMainMenu += HandleReturnedToMainMenu;
    }
    private OnlineSession EnsureOnline()
    {
        if (online != null) return online;
        online = new OnlineSession();
        online.StateChanged += HandleState;
        online.LobbyChanged += HandleLobby;
        online.HistoryChanged += () => { historyDirty = true; };
        online.Notice += message => { statusMessage = message; };
        online.ConnectionChanged += () =>
        {
            if (online == null) return;
            serverHealthStatus = online.Connected ? "Online" : "Reconnecting";
            if (!online.Connected) { nextReconnectAt = Time.unscaledTime + 3; chessGame?.SetOnlineInputAvailable(false); }
        };
        return online;
    }
    private void Update()
    {
        UpdateOnlineFlow();
        lobbyUi?.Refresh();
        if (online == null || leaving) return;
        if (online.State?.IsActive == true && online.State.matchId != presentedMatchId && !LoadingManager.For(chessGame).IsBusy)
            HandleState(online.State);
        aramPresenter?.Update();
        if (historyDirty && !historyLoading && presentedMatchId != null)
        { historyDirty = false; _ = RestoreMovesAsync(presentedMatchId); }
        bool expired = online.NeedsTokenRenewal || (DateTimeOffset.TryParse(AuthStorage.AccessTokenExpiresAt, out var expiry) && expiry <= DateTimeOffset.UtcNow.AddSeconds(5));
        if ((!online.Connected || expired) && !reconnecting && Time.unscaledTime >= nextReconnectAt && PlayerAuthService.CanUseOnlineFeatures)
            _ = ReconnectAsync(expired);
        if (showLanPanel && online.Connected && !requestInFlight && !reconnecting && Time.unscaledTime >= nextPollAt)
        { nextPollAt = Time.unscaledTime + 8; Run(async () => { await online.RefreshAsync(); if (menuPage == OnlineMenuPage.FindRoom && lobbyUi?.IsCodeFocused != true) await LoadRoomListAsync(); }); }
        chessGame?.SetOnlineInputAvailable(CanSendOnlineCommand);
    }
    private async Task ReconnectAsync(bool renew)
    {
        reconnecting = true; int epoch = lifecycle; var current = online;
        try
        {
            if (renew)
            {
                // Refresh through ApiClient's shared gate, then recreate only the
                // transport connection while retaining command IDs and watermark.
                await current.RenewConnectionAsync();
            }
            else await current.ConnectAsync();
            if (epoch != lifecycle || !this) return;
            if (current.HasPendingCommand) await current.RetryPendingAsync();
            statusMessage = "Connected. State restored.";
        }
        catch (Exception e) { if (epoch == lifecycle && this) ShowError(e); }
        finally { if (epoch == lifecycle && this) { reconnecting = false; nextReconnectAt = Time.unscaledTime + 3; } }
    }
    private void OnDestroy()
    {
        lifecycle++;
        if (chessGame)
        {
            chessGame.OnlineMoveRequested -= HandleMoveRequested;
            chessGame.OnlineBoardClicked -= HandleOnlineBoardClick;
            chessGame.OnlinePointerBlocked = null;
            chessGame.ReturnedToMainMenu -= HandleReturnedToMainMenu;
        }
        online?.Dispose(); aramPresenter?.Dispose(); lobbyUi?.Destroy();
    }
    public void ShowLanSetup() => ShowLobby(NetworkLobbyUiMode.Lan, "Classic");
    public void ShowMultiplayerSetup() => ShowLobby(NetworkLobbyUiMode.Multiplayer, "Classic");
    public void ShowAramLanSetup() => ShowLobby(NetworkLobbyUiMode.Lan, "Aram");
    public void ShowAramMultiplayerSetup() => ShowLobby(NetworkLobbyUiMode.Multiplayer, "Aram");
    private void ShowLobby(NetworkLobbyUiMode mode, string gameMode)
    {
        if (!PlayerAuthService.CanUseOnlineFeatures) { turnSelectionUI?.RequestAuthentication("Sign in to play online."); return; }
        requestedGameMode = gameMode; lobbyMode = mode; menuPage = OnlineMenuPage.Home; menuRevision++; showLanPanel = true;
        if (lobbyUi == null) lobbyUi = new NetworkLobbyUiController(this);
        lobbyUi.Show(mode);
        var orbit = FindAnyObjectByType<ChessOrbitCamera>(); orbit?.ResetToBoardView(false);
        EnsureOnline(); RequestRefreshLobby();
    }
    public void HideLanSetup() { showLanPanel = false; lobbyUi?.Hide(); }
    public void ResetForAuthenticationChange(bool authenticated = false)
    {
        lifecycle++; online?.Dispose(); online = null; aramPresenter?.Dispose(); aramPresenter = null;
        presentedMatchId = refreshedResultId = null; queueRequest = roomRequest = null;
        preparedContentMatchId = null; historyLoading = historyDirty = false;
        preparedLoadoutFingerprint = null;
        trackedRoomId = null; ResetOnlineFlow();
        requestInFlight = reconnecting = leaving = commandBusy = false;
        OpponentPauseChanged?.Invoke(false, string.Empty);
        recentMatches.Clear(); roomCodeInput = ""; HideLanSetup();
        statusMessage = authenticated ? "Online play is ready." : "Sign in to play online.";
    }
    private GameSettings Settings() => new GameSettings { mode = requestedGameMode, region = onlineRegion,
        initialSeconds = onlineInitialSeconds, incrementSeconds = onlineIncrementSeconds };
    private LobbyRequest NewRequest() => new LobbyRequest { requestId = Guid.NewGuid().ToString("N"), settings = Settings() };
    public void RequestCreateRoom() => Run(async () =>
    {
        int epoch = lifecycle;
        roomRequest = roomRequest ?? NewRequest(); await EnsureOnline().CreateRoomAsync(roomRequest);
        if (!this || epoch != lifecycle) return;
        SetMenuPage(OnlineMenuPage.Room);
        statusMessage = "Room created. Share its code; the owner starts the match when both players join.";
    });
    public void RequestJoinRoom(string code) => Run(async () =>
    {
        int epoch = lifecycle;
        roomCodeInput = NormalizeRoomCode(code); await EnsureOnline().JoinRoomAsync(roomCodeInput);
        if (!this || epoch != lifecycle) return;
        SetMenuPage(OnlineMenuPage.Room);
        statusMessage = "Joined room. Wait for its owner to start.";
    });
    public void RequestFindMatch() => Run(async () =>
    {
        if (!loadoutLoaded || loadoutLoading || HasLobbyReservation) return;
        int epoch = lifecycle;
        searchStartedAt = Time.realtimeSinceStartup; queueRequest = queueRequest ?? NewRequest();
        SetMenuPage(OnlineMenuPage.Searching); statusMessage = "Finding an opponent...";
        try { await EnsureOnline().QueueAsync(queueRequest); }
        catch
        {
            if (this && epoch == lifecycle && online?.State?.IsActive != true && online?.Ticket?.status != "Queued")
                SetMenuPage(OnlineMenuPage.Loadout);
            throw;
        }
        if (!this || epoch != lifecycle) return;
        if (online.Ticket?.status == "Queued") statusMessage = "Finding an opponent...";
        else if (online.State?.IsActive == true && online.State.acceptDeadline == null) statusMessage = "Loading match...";
        // MatchFound/cancellation events own their notices and screen transitions.
    });
    public void RequestCancelSearch() => Run(async () =>
    {
        int epoch = lifecycle; var current = EnsureOnline(); await current.CancelQueueAsync();
        if (!this || epoch != lifecycle) return;
        current.ForgetFinishedMatch(); queueRequest = null;
        if (current.State?.IsActive != true) { SetMenuPage(OnlineMenuPage.Loadout); statusMessage = "Search cancelled."; }
    });
    public void RequestCloseRoom() => Run(async () =>
    {
        int epoch = lifecycle; await EnsureOnline().CloseRoomAsync();
        if (!this || epoch != lifecycle) return;
        roomRequest = null; SetMenuPage(OnlineMenuPage.Rooms); statusMessage = "Room closed.";
    });
    public void RequestChangeTimeControl(int seconds, int increment) => Run(async () =>
    {
        var settings = Settings(); settings.initialSeconds = seconds; settings.incrementSeconds = increment;
        await EnsureOnline().ChangeSettingsAsync(settings);
        onlineInitialSeconds = seconds; onlineIncrementSeconds = increment;
        statusMessage = $"Room clock: {seconds / 60} minutes + {increment} seconds.";
    });
    public void RequestRematch() => Run(async () => { await EnsureOnline().RematchAsync(); statusMessage = "Rematch requested. Both players must agree within 2 minutes."; });
    public void RequestReady() => Run(async () =>
    {
        if (EnsureOnline().State == null) throw new ApiException("Start the room first. Ready confirms match loading.");
        await online.ReadyAsync(); statusMessage = "Ready. Waiting for the other player and setup.";
    });
    public void RequestStartGame() => Run(async () => { int epoch = lifecycle; await EnsureOnline().StartRoomAsync(); if (this && epoch == lifecycle) statusMessage = "Loading match. Ready is sent automatically when loading completes."; });
    public void RequestRefreshLobby() => Run(async () =>
    {
        int epoch = lifecycle;
        var current = EnsureOnline(); await current.RefreshAsync();
        var history = await current.Rest.HistoryAsync(1, 3);
        if (!this || epoch != lifecycle) return;
        recentMatches.Clear(); recentMatches.AddRange(history.items);
        serverHealthStatus = "Online";
    });
    public void RequestCopyRoomCode() { GUIUtility.systemCopyBuffer = online?.Room?.code ?? ""; statusMessage = "Room code copied."; }
    public void RequestBackToMultiplayerChoice() => LeaveLobby(false);
    public void RequestLeaveToModeSelection() => LeaveLobby(true);
    private void LeaveLobby(bool modeSelection) => Run(async () =>
    {
        leaving = true;
        try
        {
            if (online != null) await online.LeaveAsync();
            queueRequest = roomRequest = null; aramPresenter?.Dispose(); aramPresenter = null;
            HideLanSetup();
            if (chessGame?.UsesDotNetOnline == true) chessGame.RestartToMainMenu();
            else if (modeSelection) turnSelectionUI.ShowTurnSelection();
            else if (IsAramGameMode(requestedGameMode)) turnSelectionUI.ShowAramModeSelection();
            else turnSelectionUI.ShowTurnSelection();
        }
        finally { leaving = false; }
    });
    public void SendPauseState(bool paused) { if (paused && IsNetworkGameActive) statusMessage = "Local pause: the server clock continues."; }
    public void QuitActiveMatch() => Run(async () =>
    {
        if (online?.State?.status == "InProgress") await online.ResignAsync();
        else if (online?.State?.status == "AwaitingReady") await online.LeaveAsync();
        chessGame?.RestartToMainMenu();
    });
    private void HandleReturnedToMainMenu()
    {
        aramPresenter?.Dispose(); aramPresenter = null; presentedMatchId = null;
        if (!leaving && online?.State?.IsActive == true) Run(async () => await online.LeaveAsync());
        online?.ForgetFinishedMatch();
        HideLanSetup();
    }
    private async void HandleMoveRequested(ChessMove move)
    {
        if (!CanSendOnlineCommand) return;
        int epoch = lifecycle; var current = online;
        commandBusy = true;
        try
        {
            var ack = await current.MoveAsync(new MoveCommand { from = ToSquare(move.from), to = ToSquare(move.to),
                promotion = move.hasPromotion ? move.promotionType.ToString() : null });
            if (this && epoch == lifecycle && ack.accepted) statusMessage = "Move accepted.";
        }
        catch (Exception e) { if (this && epoch == lifecycle) ShowError(e); }
        finally { if (this && epoch == lifecycle) commandBusy = false; }
    }
    private bool HandleOnlineBoardClick(Vector2Int square, int? pieceId) => aramPresenter?.HandleClick(square, pieceId) == true;
    internal bool CanSendOnlineCommand => PlayerAuthService.CanUseOnlineFeatures && online?.Connected == true && !online.Recovering && !requestInFlight && !commandBusy && !online.HasPendingCommand && !reconnecting && !leaving;
    internal void SetOnlineNotice(string message) { statusMessage = message; }
    internal bool BlocksOnlineBoardPointer(Vector2 screen)
    {
        var point = new Vector2(screen.x, Screen.height - screen.y) / GetGuiScale();
        return new Rect(20, 12, 560, 230).Contains(point) || aramPresenter?.ContainsPointer(point) == true;
    }
    internal void SendAbility(AbilityCommand command) => Run(async () => { await online.AbilityAsync(command); });
    private void HandleState(MatchState state)
    {
        if (!this || leaving || state == null) return;
        lastStateReceivedAt = DateTime.UtcNow;
        requestedGameMode = state.settings.mode;
        if (state.status == "Cancelled") { ShowMatchCancellation(state); return; }
        if (ShowAcceptance(state)) return;
        var white = state.players.First(p => p.color == "White"); var black = state.players.First(p => p.color == "Black");
        turnSelectionUI.SetMatchPlayers(Name(white), Name(black));
        string loadoutFingerprint = string.Join("|", state.players.SelectMany(p => p.loadout.Select(l => p.color + ":" + l.type + ":" + l.itemId + ":" + l.unityAssetKey)));
        if (preparedContentMatchId != state.matchId || preparedLoadoutFingerprint != loadoutFingerprint)
        {
            var loader = LoadingManager.For(chessGame);
            if (loader.IsBusy) return;
            int epoch = lifecycle; string matchId = state.matchId;
            presentedMatchId = null; aramPresenter?.Dispose(); aramPresenter = null;
            loader.StartMatch(FrozenSelection(state), () =>
            {
                if (!this || epoch != lifecycle || online?.State?.matchId != matchId) return;
                preparedContentMatchId = matchId; preparedLoadoutFingerprint = loadoutFingerprint; HandleState(online.State);
            }, true); return;
        }
        if (presentedMatchId != state.matchId)
        {
            presentedMatchId = state.matchId;
            chessGame.BeginDotNetMatch(state);
            var orbit = FindAnyObjectByType<ChessOrbitCamera>(); orbit?.ConfigureForPlayerSide(chessGame.PlayerTeam, true);
            _ = RestoreMovesAsync(state.matchId);
        }
        else chessGame.ApplyDotNetState(state);
        HideLanSetup();
        if (state.aram != null)
        {
            if (aramPresenter == null) aramPresenter = new OnlineAramPresenter(this, chessGame);
            aramPresenter.Apply(state);
        }
        if (state.status == "Finished" && refreshedResultId != state.matchId)
        { refreshedResultId = state.matchId; _ = RefreshAccountAsync(lifecycle); }
    }
    private async Task RestoreMovesAsync(string matchId)
    {
        if (historyLoading) { historyDirty = true; return; }
        historyLoading = true; int epoch = lifecycle; var current = online;
        try
        {
            var records = new List<MoveRecord>(); int page = 1;
            Page<MoveRecord> batch;
            do { batch = await current.Rest.MovesAsync(matchId, page++); records.AddRange(batch.items); }
            while (records.Count < batch.total && batch.items.Count > 0);
            if (this && epoch == lifecycle && presentedMatchId == matchId) chessGame.RestoreDotNetMoves(records, current.State);
        }
        catch { if (this && epoch == lifecycle && presentedMatchId == matchId) chessGame.MarkStatisticsRecoveryIncomplete(0); }
        finally { if (this && epoch == lifecycle) historyLoading = false; }
    }
    private bool historyDirty, historyLoading;
    private CosmeticSelection FrozenSelection(MatchState state)
    {
        var selection = new CosmeticSelection();
        var catalog = LoadingManager.For(chessGame).Catalog;
        foreach (var player in state.players)
        {
            var piece = player.loadout.FirstOrDefault(l => l.type?.IndexOf("CHESS", StringComparison.OrdinalIgnoreCase) >= 0);
            string skin = catalog && catalog.FindPiece(piece?.unityAssetKey) != null ? piece.unityAssetKey : CosmeticSelection.LowPolyId;
            if (player.color == "White") selection.whiteSkinId = skin; else selection.blackSkinId = skin;
            if (player.userId == PlayerAuthService.UserId)
            {
                var board = player.loadout.FirstOrDefault(l => l.type?.IndexOf("BOARD", StringComparison.OrdinalIgnoreCase) >= 0);
                if (catalog && catalog.FindBoard(board?.unityAssetKey) != null) selection.boardId = board.unityAssetKey;
            }
        }
        return selection;
    }
    private async Task RefreshAccountAsync(int epoch)
    {
        try { var user = await new UserService().GetMeAsync(); if (this && epoch == lifecycle) PlayerAuthService.ApplyApiUser(user); }
        catch (Exception e) { if (this && epoch == lifecycle) statusMessage = "Match saved. Profile refresh pending: " + e.Message; }
    }
    private void HandleLobby()
    {
        roomCodeInput = online?.Room?.code ?? roomCodeInput;
        if (online?.Room != null) trackedRoomId = online.Room.roomId;
        else if (trackedRoomId != null)
        {
            trackedRoomId = null; roomRequest = null;
            if (showLanPanel && menuPage == OnlineMenuPage.Room) { SetMenuPage(OnlineMenuPage.Rooms); statusMessage = "Room closed or expired."; }
        }
        if (online?.Room != null)
        {
            requestedGameMode = online.Room.settings.mode;
            onlineInitialSeconds = online.Room.settings.initialSeconds; onlineIncrementSeconds = online.Room.settings.incrementSeconds;
            if (showLanPanel && online.Room.status == "Open" && online.State?.IsActive != true) SetMenuPage(OnlineMenuPage.Room);
        }
        if (online?.Ticket?.status == "Queued" && online.State?.IsActive != true)
        {
            if (searchingTicketId != online.Ticket.ticketId)
            {
                searchingTicketId = online.Ticket.ticketId;
                searchStartedAt = Time.realtimeSinceStartup - Mathf.Max(0, (float)(DateTime.UtcNow - online.Ticket.createdAt).TotalSeconds);
            }
            if (showLanPanel) SetMenuPage(OnlineMenuPage.Searching);
            statusMessage = "Finding an opponent...";
        }
        else if ((online?.Ticket?.status == "Expired" || online?.Ticket?.status == "Cancelled") &&
            (menuPage == OnlineMenuPage.Searching || (menuPage == OnlineMenuPage.Loadout && queueRequest != null)))
        {
            SetMenuPage(OnlineMenuPage.Loadout);
            statusMessage = online.Ticket.status == "Expired" ? "Search expired. Press Play to try again." : "Search cancelled. Check your loadout and try again.";
        }
        if (online?.Ticket != null && online.Ticket.status != "Queued" && online.Ticket.status != "Matched") queueRequest = null;
        string roomFingerprint = online?.Room == null ? "" : online.Room.roomId + ":" + online.Room.status + ":" +
            online.Room.settings.mode + ":" + online.Room.settings.initialSeconds + ":" + online.Room.settings.incrementSeconds;
        if (roomContentFingerprint != roomFingerprint)
        { roomContentFingerprint = roomFingerprint; if (menuPage == OnlineMenuPage.Room) menuRevision++; }
        lobbyUi?.Refresh();
    }
    private async void Run(Func<Task> action)
    {
        if (requestInFlight || !PlayerAuthService.CanUseOnlineFeatures) return;
        int epoch = lifecycle; requestInFlight = true;
        try { await action(); }
        catch (OperationCanceledException) { }
        catch (Exception e) { if (this && epoch == lifecycle) ShowError(e); }
        finally { if (this && epoch == lifecycle) requestInFlight = false; }
    }
    private void ShowError(Exception e)
    {
        statusMessage = e.Message;
        if (e is ApiException api && (api.StatusCode == 401 || api.ErrorCode == "Unauthorized"))
            statusMessage = "Session expired. Sign in again.";
        serverHealthStatus = online?.Connected == true ? "Online" : "Reconnecting";
        nextReconnectAt = Time.unscaledTime + 3;
    }
    private string GetLobbyRoomCode() => online?.Room?.code ?? "";
    private string GetServerHealth() => serverHealthStatus;
    private string GetHostPlayerLine() => PlayerLine(0);
    private string GetGuestPlayerLine() => PlayerLine(1);
    private string PlayerLine(int index)
    {
        if (online?.State != null && online.State.IsActive)
        { var p = online.State.players.ElementAtOrDefault(index); return p == null ? "Waiting" : Name(p) + (p.ready ? " - Ready" : " - Loading"); }
        var member = online?.Room?.members.ElementAtOrDefault(index);
        return member == null ? "Waiting for player" : member == PlayerAuthService.UserId ? PlayerAuthService.CurrentDisplayName : "Opponent joined";
    }
    private string GetRecentMatchLine(int index)
    {
        var m = recentMatches.ElementAtOrDefault(index);
        return m == null ? "No recent match" : m.settings.mode + " - " + (m.result?.outcome ?? m.status);
    }
    private static string Name(Player p) => p == null ? "Opponent" : string.IsNullOrWhiteSpace(p.displayName) ? p.username : p.displayName;
    private static bool IsAramGameMode(string mode) => string.Equals(mode, "Aram", StringComparison.OrdinalIgnoreCase);
    private static string NormalizeRoomCode(string value) => new string((value ?? "").Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).Take(8).ToArray());
    private static string ToSquare(Vector2Int square) => ((char)('a' + square.x)).ToString() + (square.y + 1);
    private void OnGUI()
    {
        if (online?.State == null || presentedMatchId != online.State.matchId) return;
        var before = GUI.matrix; GUI.matrix = Matrix4x4.Scale(Vector3.one * GetGuiScale());
        try
        {
            var state = online.State;
            GUILayout.BeginArea(new Rect(20, 12, 560, 230), GUI.skin.box);
            GUILayout.Label($"{state.settings.mode} | {MatchConnectionState} | {statusMessage}");
            double elapsed = Math.Max(0, (DateTime.UtcNow - lastStateReceivedAt).TotalMilliseconds);
            double white = state.clocks.whiteMilliseconds - (state.clocks.runningColor == "White" ? elapsed : 0);
            double black = state.clocks.blackMilliseconds - (state.clocks.runningColor == "Black" ? elapsed : 0);
            GUILayout.Label($"White {Clock(white)}    Black {Clock(black)}");
            var opponent = state.players.FirstOrDefault(p => p.userId != PlayerAuthService.UserId);
            if (opponent != null && !opponent.connected) GUILayout.Label("Opponent disconnected. Server clock and reconnect grace continue.");
            GUI.enabled = CanSendOnlineCommand;
            if (state.status == "AwaitingReady" && GUILayout.Button("Ready - confirm loading")) RequestReady();
            if (state.status == "InProgress")
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Offer draw")) Run(async () => await online.OfferDrawAsync());
                if (state.aram == null && GUILayout.Button("Claim draw")) Run(async () => await online.ClaimDrawAsync());
                if (GUILayout.Button("Resign")) Run(async () => await online.ResignAsync());
                GUILayout.EndHorizontal();
                if (state.drawOffer != null && state.drawOffer.userId != PlayerAuthService.UserId)
                {
                    GUILayout.Label("Opponent offers a draw."); GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Accept")) Run(async () => await online.RespondDrawAsync(true));
                    if (GUILayout.Button("Decline")) Run(async () => await online.RespondDrawAsync(false));
                    GUILayout.EndHorizontal();
                }
            }
            if (state.status == "Finished")
            {
                var reward = state.result?.players.FirstOrDefault(p => p.userId == PlayerAuthService.UserId);
                if (reward != null) GUILayout.Label($"Server reward: {reward.golds} Gold, {reward.diamonds} Diamonds, {reward.tickets} Tickets | Elo {reward.ratingChange:+0;-0;0}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Request rematch")) RequestRematch();
                if (GUILayout.Button("Accept rematch")) Run(async () => await online.RematchAsync(true));
                if (GUILayout.Button("Decline rematch")) Run(async () => await online.RematchAsync(false));
                GUILayout.EndHorizontal();
            }
            GUI.enabled = online.Connected && !requestInFlight && !commandBusy;
            if (online.HasPendingCommand && GUILayout.Button("Recover previous command")) Run(async () => await online.RetryPendingAsync());
            GUI.enabled = true; GUILayout.EndArea();
            aramPresenter?.Draw();
        }
        finally { GUI.enabled = true; GUI.matrix = before; }
    }
    private DateTime lastStateReceivedAt;
    internal static float OnlineGuiScale => GetGuiScale();
    private static string Clock(double milliseconds) => TimeSpan.FromMilliseconds(Math.Max(0, milliseconds)).ToString(@"mm\:ss");
}
