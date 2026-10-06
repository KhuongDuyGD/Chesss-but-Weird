using System;
using System.Linq;
using ChessButWeird.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UI = SketchbookUI;

public partial class ChessLanController
{
    private OnlineMatchHud matchHud;

    private sealed class OnlineMatchHud
    {
        private readonly ChessLanController owner;
        private readonly GameObject canvasRoot;
        private readonly TextMeshProUGUI notice;
        private readonly Button offer, claim, resign, acceptDraw, declineDraw, ready, leave, recover;
        private readonly Button rematch, acceptRematch, declineRematch;
        private string observedNotice, observedMatchId;
        private float noticeUntil;

        public OnlineMatchHud(ChessLanController controller)
        {
            owner = controller;
            canvasRoot = new GameObject("Online Match HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(owner.transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            ResponsiveUi.ConfigureCanvasScaler(canvasRoot.GetComponent<CanvasScaler>(), new Vector2(1280, 720));
            var safe = MatchHudStyle.Rect(canvasRoot.transform, "Online HUD Safe Area", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<ResponsiveSafeArea>();
            var footer = MatchHudStyle.Rect(safe, "Match Actions", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(640, 96), new Vector2(0, 56));
            var paper = UI.Card(footer, "Paper Toolbar", new Rect(0, 0, 640, 96), UI.Paper, 0, false, 16);
            // Only the actual toolbar blocks the board; the full-screen root has no Graphic.
            paper.raycastTarget = true;
            notice = UI.Text(footer, "Online Notice", "", new Rect(16, 5, 608, 36), 20, UI.Ink, TextAlignmentOptions.Center, true);
            offer = Action(footer, "Offer Draw", 0, UI.Blue, () => owner.Run(async () => await owner.online.OfferDrawAsync()));
            claim = Action(footer, "Claim Draw", 1, UI.Yellow, () => owner.Run(async () => await owner.online.ClaimDrawAsync()));
            resign = Action(footer, "Resign", 2, UI.Pink, () => owner.Run(async () => await owner.online.ResignAsync()));
            acceptDraw = Action(footer, "Accept Draw", 0, UI.Green, () => owner.Run(async () => await owner.online.RespondDrawAsync(true)));
            declineDraw = Action(footer, "Decline Draw", 1, UI.White, () => owner.Run(async () => await owner.online.RespondDrawAsync(false)));
            ready = Action(footer, "Ready", 0, UI.Green, owner.RequestReady);
            leave = Action(footer, "Leave Match", 2, UI.Pink, owner.QuitActiveMatch);
            rematch = Action(footer, "Rematch", 0, UI.Yellow, owner.RequestRematch);
            acceptRematch = Action(footer, "Accept Rematch", 1, UI.Green, () => owner.Run(async () => await owner.online.RematchAsync(true)));
            declineRematch = Action(footer, "Decline Rematch", 2, UI.White, () => owner.Run(async () => await owner.online.RematchAsync(false)));
            var recoveryGroup = MatchHudStyle.Rect(footer, "Recovery Controls", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            recover = UI.Button(recoveryGroup, "Recover Command", "Recover Previous Command", new Rect(12, 49, 406, 40), UI.Yellow,
                () => owner.Run(async () => await owner.online.RetryPendingAsync()), 23);
            Hide();
        }
        private static Button Action(Transform parent, string label, int column, Color color, UnityEngine.Events.UnityAction callback)
        {
            var group = MatchHudStyle.Rect(parent, label + " Controls", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return UI.Button(group, label, label, new Rect(12 + column * 210, 49, 196, 40), color, callback, 23);
        }

        public void Hide() { if (canvasRoot) canvasRoot.SetActive(false); }
        public void Destroy() { if (canvasRoot) UnityEngine.Object.Destroy(canvasRoot); }
        public void Refresh()
        {
            var state = owner.online?.State;
            if (state == null || owner.presentedMatchId != state.matchId || owner.showLanPanel ||
                !owner.chessGame || !owner.chessGame.UsesDotNetOnline || owner.chessGame.PauseLocked)
            { Hide(); return; }
            canvasRoot.SetActive(true);
            if (observedMatchId != state.matchId)
            { observedMatchId = state.matchId; observedNotice = owner.statusMessage; noticeUntil = 0; }
            else if (observedNotice != owner.statusMessage)
            { observedNotice = owner.statusMessage; noticeUntil = IsActionNotice(observedNotice) ? Time.unscaledTime + 6 : 0; }

            bool active = state.status == "InProgress", finished = state.status == "Finished";
            bool canSend = owner.CanSendOnlineCommand && !owner.chessGame.HasPendingPromotion;
            bool pending = owner.online.HasPendingCommand;
            bool drawing = active && state.drawOffer != null && Remaining(state, state.drawOffer.expiresAt) > 0;
            bool incomingDraw = drawing && state.drawOffer.userId != PlayerAuthService.UserId;
            var local = state.players.FirstOrDefault(p => p.userId == PlayerAuthService.UserId);
            Show(offer, active && !incomingDraw && !pending, canSend && !drawing);
            Show(claim, active && state.aram == null && !incomingDraw && !pending, canSend && state.turn == local?.color);
            Show(resign, active, canSend);
            Show(acceptDraw, incomingDraw && !pending, canSend);
            Show(declineDraw, incomingDraw && !pending, canSend);
            Show(ready, state.status == "AwaitingReady" && local?.ready != true, canSend && state.acceptDeadline == null);
            Show(leave, state.status == "AwaitingReady", canSend);
            Show(rematch, finished && !pending, canSend);
            Show(acceptRematch, finished && !pending, canSend);
            Show(declineRematch, finished && !pending, canSend);
            Show(recover, pending, owner.online.Connected && !owner.requestInFlight && !owner.commandBusy && !owner.reconnecting);

            if (!owner.online.Connected || owner.reconnecting) notice.text = "Reconnecting... Board input is temporarily locked.";
            else if (owner.online.Recovering) notice.text = "Restoring the latest server state...";
            else if (pending) notice.text = "The last command is unconfirmed. Recover it before playing.";
            else if (state.status == "AwaitingReady")
                notice.text = local?.ready == true ? "Ready. Waiting for your opponent and setup..." : "Loading complete. Sending Ready...";
            else if (drawing) notice.text = incomingDraw ? $"Opponent offers a draw · {Mathf.CeilToInt(Remaining(state, state.drawOffer.expiresAt))}s to respond" : "Draw offered. Waiting for your opponent...";
            else if (Time.unscaledTime < noticeUntil) notice.text = observedNotice;
            else if (finished)
            {
                var result = state.result?.players.FirstOrDefault(p => p.userId == PlayerAuthService.UserId);
                notice.text = result == null ? "Match finished. Both players can agree to a rematch." :
                    $"{result.golds} Gold · {result.diamonds} Diamonds · {result.tickets} Tickets · Elo {result.ratingChange:+0;-0;0}";
            }
            else if (owner.requestInFlight || owner.commandBusy) notice.text = "Sending command...";
            else if (owner.chessGame.HasPendingPromotion) notice.text = "Choose a piece to complete your promotion.";
            else if (state.players.Any(p => p.userId != PlayerAuthService.UserId && !p.connected)) notice.text = "Opponent disconnected. The server clock continues.";
            else notice.text = state.turn == local?.color ? $"You play {local.color} · Your turn. Select a piece." : $"You play {local?.color} · Opponent's turn.";
        }
        private float Remaining(MatchState state, DateTime deadline) => Mathf.Max(0,
            (float)(deadline - state.serverTime).TotalSeconds - (float)(DateTime.UtcNow - owner.lastStateReceivedAt).TotalSeconds);
        private static void Show(Button button, bool visible, bool enabled)
        { button.transform.parent.gameObject.SetActive(visible); button.interactable = enabled; }
        private static bool IsActionNotice(string message) => !string.IsNullOrWhiteSpace(message) &&
            !message.StartsWith("Ready.", StringComparison.Ordinal) &&
            !message.StartsWith("Connected.", StringComparison.Ordinal) &&
            !message.StartsWith("Loading", StringComparison.Ordinal) &&
            !message.StartsWith("Match found", StringComparison.Ordinal) &&
            !message.StartsWith("Match created", StringComparison.Ordinal) &&
            !message.StartsWith("Both accepted", StringComparison.Ordinal);
    }
}
