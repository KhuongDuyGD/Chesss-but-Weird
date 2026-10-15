using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChessButWeird.Online;
using UnityEngine;

public partial class ChessLanController
{
    private enum OnlineMenuPage { Home, Rooms, FindRoom, Room, Loadout, Searching, Accept }
    private OnlineMenuPage menuPage;
    private int menuRevision, roomListPage = 1;
    private Page<RoomListing> roomListing;
    private bool loadoutLoaded, roomsLoading, forgetCancelledMatch;
    private Action loadoutContinuation;
    private string loadoutActionLabel = "PLAY", roomListError;
    private string roomContentFingerprint;
    private bool roomListRenderPending;
    private MatchState acceptanceClockState;
    private string searchingTicketId;
    private string verifyingAcceptanceMatchId;
    private float acceptanceClockReceivedAt, searchStartedAt, nextReadyAttempt, nextDeclineAttempt;

    private void SetMenuPage(OnlineMenuPage page)
    {
        if (menuPage != page) { menuPage = page; menuRevision++; }
        showLanPanel = true;
        if (lobbyUi == null) lobbyUi = new NetworkLobbyUiController(this);
        lobbyUi.Show(lobbyMode);
    }
    private bool HasLobbyReservation => online?.State?.IsActive == true || online?.Ticket?.status == "Queued" || online?.Room?.status == "Open";
    private bool CanEditLoadout => !HasLobbyReservation && !requestInFlight;
    private void OpenRooms() => OpenOnlineLoadout(() =>
    { SetMenuPage(OnlineMenuPage.Rooms); statusMessage = "Create a room or join a friend."; }, "CONTINUE");
    private void OpenRoomBrowser()
    { roomListPage = 1; SetMenuPage(OnlineMenuPage.FindRoom); RefreshRoomBrowser(); }
    private void OpenMatchmaking() => OpenOnlineLoadout(RequestFindMatch, "PLAY");
    private void OpenOnlineLoadout(Action next, string label)
    {
        if (!CanEditLoadout) return;
        loadoutContinuation = next; loadoutActionLabel = label;
        SetMenuPage(OnlineMenuPage.Loadout);
        statusMessage = "Choose your loadout, then continue.";
    }
    private void ChangeOnlineMode(string mode)
    {
        if (!CanEditLoadout) return;
        requestedGameMode = mode; queueRequest = roomRequest = null;
        if (menuPage != OnlineMenuPage.Loadout) menuRevision++;
        if (menuPage == OnlineMenuPage.FindRoom) { roomListPage = 1; RefreshRoomBrowser(); }
    }
    private void RefreshRoomBrowser() => Run(async () => await LoadRoomListAsync());
    private async Task LoadRoomListAsync()
    {
        roomsLoading = true; roomListError = null; int epoch = lifecycle;
        try
        {
            var list = await EnsureOnline().Rest.RoomsAsync(roomListPage, 8, requestedGameMode, onlineRegion);
            if (!this || epoch != lifecycle) return;
            roomListing = list;
            if (lobbyUi?.IsCodeFocused == true) roomListRenderPending = true; else menuRevision++;
            statusMessage = "Click a room to join, or enter its code.";
        }
        catch (Exception e)
        {
            if (this && epoch == lifecycle) { roomListError = e.Message; menuRevision++; }
            throw;
        }
        finally { if (this && epoch == lifecycle) roomsLoading = false; }
    }
    private void ChangeRoomListPage(int offset)
    {
        if (requestInFlight || roomsLoading) return;
        roomListPage = Mathf.Max(1, roomListPage + offset); RefreshRoomBrowser();
    }
    private void BackOnlineMenu()
    {
        if (requestInFlight) return;
        switch (menuPage)
        {
            case OnlineMenuPage.Home: RequestLeaveToModeSelection(); break;
            case OnlineMenuPage.Rooms: case OnlineMenuPage.Loadout: SetMenuPage(OnlineMenuPage.Home); break;
            case OnlineMenuPage.FindRoom: SetMenuPage(OnlineMenuPage.Rooms); break;
            case OnlineMenuPage.Room:
                Run(async () => { int epoch = lifecycle; await online.LeaveAsync(); if (!this || epoch != lifecycle) return; roomRequest = null; SetMenuPage(OnlineMenuPage.Rooms); statusMessage = "Left the room."; }); break;
            case OnlineMenuPage.Searching: RequestCancelSearch(); break;
            case OnlineMenuPage.Accept: DeclineFoundMatch("Match declined. Choose your loadout and try again."); break;
        }
    }
    private float AcceptanceSeconds
    {
        get
        {
            var state = online?.State;
            if (state?.acceptDeadline == null) return 0;
            return Mathf.Max(0, (float)(state.acceptDeadline.Value - state.serverTime).TotalSeconds - (Time.realtimeSinceStartup - acceptanceClockReceivedAt));
        }
    }
    private string SearchElapsed
    {
        get
        {
            int seconds = Mathf.Max(0, Mathf.FloorToInt(Time.realtimeSinceStartup - searchStartedAt));
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }
    }
    private bool LocalAccepted => online?.State?.players.Any(p => p.userId == PlayerAuthService.UserId && p.accepted) == true;
    private string FoundOpponent => Name(online?.State?.players.FirstOrDefault(p => p.userId != PlayerAuthService.UserId));
    private void AcceptFoundMatch()
    {
        if (LocalAccepted || AcceptanceSeconds <= 0 || online?.State?.acceptDeadline == null) return;
        Run(async () =>
        {
            int epoch = lifecycle; await online.AcceptAsync();
            if (!this || epoch != lifecycle || online.State?.status == "Cancelled") return;
            statusMessage = online.State?.acceptDeadline != null ? "Accepted. Waiting for the other player..." : "Both accepted. Loading match...";
        });
    }
    private void DeclineFoundMatch(string notice) => Run(async () =>
    {
        int epoch = lifecycle;
        // The server cancels the pending match and releases both players' seats.
        await EnsureOnline().LeaveAsync();
        if (!this || epoch != lifecycle) return;
        queueRequest = null; acceptanceClockState = null; verifyingAcceptanceMatchId = null; forgetCancelledMatch = false;
        SetMenuPage(OnlineMenuPage.Loadout); statusMessage = notice;
    });
    private void UpdateOnlineFlow()
    {
        if (roomListRenderPending && lobbyUi?.IsCodeFocused != true)
        { roomListRenderPending = false; menuRevision++; }
        if (forgetCancelledMatch) { forgetCancelledMatch = false; online?.ForgetFinishedMatch(); }
        var state = online?.State;
        if (state?.acceptDeadline != null && state.status == "AwaitingReady" && AcceptanceSeconds <= 0 &&
            !requestInFlight && Time.unscaledTime >= nextDeclineAttempt)
        {
            nextDeclineAttempt = Time.unscaledTime + 3;
            verifyingAcceptanceMatchId = state.matchId;
            SetMenuPage(OnlineMenuPage.Loadout);
            statusMessage = "Acceptance time ended. Confirming the match result with the server...";
            // Refresh advances the server's deadline. It also preserves a second
            // Accept that arrived on time but whose event reached us late.
            Run(async () => await EnsureOnline().RefreshAsync());
        }
        else if (state?.status == "AwaitingReady" && state.acceptDeadline == null && presentedMatchId == state.matchId &&
            !requestInFlight && online.Connected && !online.Recovering && !reconnecting &&
            state.players.Any(p => p.userId == PlayerAuthService.UserId && !p.ready) && Time.unscaledTime >= nextReadyAttempt)
        {
            nextReadyAttempt = Time.unscaledTime + 3;
            RequestReady();
        }
    }
    private bool ShowAcceptance(MatchState state)
    {
        if (state.status != "AwaitingReady" || state.acceptDeadline == null) return false;
        bool fresh = !ReferenceEquals(acceptanceClockState, state);
        if (fresh)
        { acceptanceClockState = state; acceptanceClockReceivedAt = Time.realtimeSinceStartup; }
        if (verifyingAcceptanceMatchId == state.matchId)
        {
            if (!fresh || state.serverTime >= state.acceptDeadline.Value) return true;
            verifyingAcceptanceMatchId = null;
        }
        SetMenuPage(OnlineMenuPage.Accept);
        statusMessage = LocalAccepted ? "Accepted. Waiting for the other player..." : "Match found! Accept within 30 seconds.";
        return true;
    }
    private void ShowMatchCancellation(MatchState state)
    {
        queueRequest = null; acceptanceClockState = null; verifyingAcceptanceMatchId = null; forgetCancelledMatch = true;
        aramPresenter?.Dispose(); aramPresenter = null;
        if (presentedMatchId == state.matchId) chessGame.RestartToMainMenu();
        SetMenuPage(OnlineMenuPage.Loadout);
        statusMessage = state.result?.reason == "AcceptanceTimeout" ?
            "Match acceptance timed out. Matchmaking failed; press Play to try again." :
            "Match cancelled: " + state.result?.reason + ". Press Play to try again.";
    }
    private void ResetOnlineFlow()
    {
        menuPage = OnlineMenuPage.Home; menuRevision++; roomListPage = 1; roomListing = null;
        loadoutLoaded = roomsLoading = forgetCancelledMatch = false;
        loadoutContinuation = null; loadoutActionLabel = "PLAY";
        roomListError = roomContentFingerprint = null; roomListRenderPending = false; acceptanceClockState = null;
        searchingTicketId = null;
        verifyingAcceptanceMatchId = null;
        nextReadyAttempt = nextDeclineAttempt = 0;
    }
}
