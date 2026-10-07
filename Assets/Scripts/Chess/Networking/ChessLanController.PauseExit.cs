using System;
using System.Threading.Tasks;

public partial class ChessLanController
{
    private Task pauseExitTask;

    public Task LeaveMatchFromPauseAsync()
    {
        if (pauseExitTask != null && !pauseExitTask.IsCompleted) return pauseExitTask;
        pauseExitTask = LeavePauseSessionAsync();
        return pauseExitTask;
    }

    private async Task LeavePauseSessionAsync()
    {
        var current = online;
        int epoch = lifecycle;
        leaving = requestInFlight = true;
        try
        {
            if (current != null)
            {
                // Recover an existing command ID before using the established leave/resign path.
                if (current.HasPendingCommand) await current.RetryPendingAsync();
                await current.LeaveAsync(requireAcknowledgement: true);
            }
            if (!this || epoch != lifecycle || current != online)
                throw new OperationCanceledException("The online session changed during exit.");
            CloseSessionFromPause();
        }
        finally
        {
            if (this && epoch == lifecycle) leaving = requestInFlight = false;
        }
    }

    public void CloseSessionFromPause()
    {
        lifecycle++;
        var closed = online;
        online = null;
        closed?.Dispose();
        aramPresenter?.Dispose(); aramPresenter = null;
        matchHud?.Hide();
        presentedMatchId = refreshedResultId = preparedContentMatchId = preparedLoadoutFingerprint = trackedRoomId = null;
        queueRequest = roomRequest = null;
        historyLoading = historyDirty = false;
        ResetOnlineFlow();
        requestInFlight = reconnecting = commandBusy = leaving = false;
        HideLanSetup();
    }

    public void ShowLobbyAfterPauseExit()
    {
        ShowLobby(lobbyMode, requestedGameMode);
    }
}
