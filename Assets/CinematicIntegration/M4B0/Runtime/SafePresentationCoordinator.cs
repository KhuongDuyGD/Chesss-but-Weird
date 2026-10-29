#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.Collections.Generic;
using System.Threading;

namespace ChessButWeird.CinematicIntegration.M4B0
{
    // Owner-thread state machine. Public commands/report callbacks enqueue; Pump is the only transition boundary.
    public sealed class SafePresentationCoordinator : IDisposable
    {
        private sealed class Attempt
        {
            public AttemptToken Token;
            public PresentationPlan Plan;
            public bool Fallback, Settled;
            public int Reported;
            public IVisualPresenter Presenter;
            public readonly PresentationResourceScope Resources = new PresentationResourceScope();
        }
        private readonly Queue<Action> commands = new Queue<Action>();
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private readonly IResultsBoundary results;
        private readonly List<string> faults = new List<string>();
        private bool pumping;
        private volatile bool disposed;
        private volatile bool hostOpen = true;
        private Attempt active;
        private PresentationPlan lastPlan;
        public ReceivedPresentationSession Session { get; }
        public event Action<AttemptSettlement> AttemptEnded;
        public IReadOnlyList<string> Faults => faults.AsReadOnly();
        public int RejectedCommands { get; private set; }
        public bool HostOpen => hostOpen;
        public bool IsDisposed => disposed;
        public AttemptToken ActiveToken => active?.Token;
        public int PendingCommands { get { lock (commands) return commands.Count; } }
        public SafePresentationCoordinator(ReceivedPresentationSession session, IResultsBoundary boundary)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            results = boundary ?? throw new ArgumentNullException(nameof(boundary));
            session.Bind(this);
        }
        private void Enqueue(Action action) { lock (commands) { if (!disposed) commands.Enqueue(action); } }
        public void RequestBegin(PresentationPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            Enqueue(() => Begin(plan, false));
        }
        public void RequestReplay() => Enqueue(() => { if (lastPlan == null) RejectedCommands++; else Begin(lastPlan, false); });
        public void RequestCancel() => Enqueue(() => { if (active == null) RejectedCommands++; else Settle(active, PresentationOutcome.Cancelled, "Explicit cancellation"); });
        public void RequestAcknowledge() => Enqueue(() =>
        {
            if (Session.State == PresentationState.ResultsShown && Session.ResultsAccepted) Session.State = PresentationState.Acknowledged;
            else RejectedCommands++;
        });
        public void CloseHost()
        {
            hostOpen = false; // Immediate suppression latch; cleanup/state mutation remains deferred.
            Enqueue(() => { if (active != null) Settle(active, PresentationOutcome.Cancelled, "Visual host closed"); });
        }
        public void RequestCleanup() => CloseHost();
        public void RequestOpenHost() => Enqueue(() => { if (!Session.IsAcknowledged) hostOpen = true; });
        public int Pump()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Pump must run on its owning thread");
            if (pumping || disposed) return 0;
            int count; lock (commands) count = Math.Min(commands.Count, 64);
            pumping = true;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    Action action; lock (commands) action = commands.Dequeue();
                    action();
                    if (disposed) { lock (commands) commands.Clear(); break; }
                }
            }
            finally { pumping = false; }
            return count;
        }
        private void Begin(PresentationPlan plan, bool fallback)
        {
            if (disposed || !hostOpen || Session.IsAcknowledged || !Session.HasPresentationPrerequisites) { RejectedCommands++; return; }
            if (active != null) Settle(active, PresentationOutcome.Cancelled, "Replaced by explicit Begin/Replay");
            if (!hostOpen) { RejectedCommands++; return; }
            lastPlan = plan;
            var attempt = new Attempt { Token = Session.NextAttempt(), Plan = plan, Fallback = fallback };
            active = attempt; Session.State = PresentationState.Preparing;
            try
            {
                var factory = fallback ? plan.Fallback : plan.Primary;
                if (factory == null || !factory.Supports(Session.Batch)) { Settle(attempt, PresentationOutcome.Failed, "Unsupported capability/artwork policy"); return; }
                attempt.Presenter = factory.Create() ?? throw new InvalidOperationException("Factory returned no presenter");
                var context = new PresentationAttemptContext(Session.Batch, attempt.Token, attempt.Resources,
                    (outcome, reason) => Signal(attempt, outcome, reason));
                attempt.Presenter.Prepare(context);
                if (!hostOpen) { Settle(attempt, PresentationOutcome.Cancelled, "Host closed during setup"); return; }
                if (Volatile.Read(ref attempt.Reported) != 0) return;
                Session.State = PresentationState.Playing;
                attempt.Presenter.Play();
            }
            catch (Exception error) { Settle(attempt, PresentationOutcome.Failed, "Presenter exception: " + error.GetType().Name); }
        }
        private void Signal(Attempt attempt, PresentationOutcome outcome, string reason)
        {
            if (Interlocked.CompareExchange(ref attempt.Reported, 1, 0) != 0) return;
            Enqueue(() =>
            {
                if (!ReferenceEquals(active, attempt) || attempt.Settled) return;
                if (!hostOpen) { Settle(attempt, PresentationOutcome.Cancelled, "Late event after host loss"); return; }
                if (!Enum.IsDefined(typeof(PresentationOutcome), outcome)) { Settle(attempt, PresentationOutcome.Failed, "Invalid terminal outcome"); return; }
                if (Session.State == PresentationState.Preparing && outcome == PresentationOutcome.Completed)
                    Settle(attempt, PresentationOutcome.Failed, "Completion before playback is a protocol failure");
                else Settle(attempt, outcome, reason);
            });
        }
        private void Settle(Attempt attempt, PresentationOutcome outcome, string reason)
        {
            if (!ReferenceEquals(active, attempt) || attempt.Settled) return;
            attempt.Settled = true; active = null;
            var settlement = new AttemptSettlement(attempt.Token, outcome, reason, attempt.Fallback);
            Session.AddSettlement(settlement);
            Session.State = outcome == PresentationOutcome.Cancelled ? PresentationState.CancelledRecoverable : PresentationState.FailedRecoverable;
            // Settle BEFORE invoking stop/dispose or observers: their callbacks cannot turn cancellation into success.
            if (outcome == PresentationOutcome.Cancelled || outcome == PresentationOutcome.Failed)
                Protect(() => attempt.Presenter?.Cancel(), "Presenter.Cancel");
            Protect(() => attempt.Presenter?.Dispose(), "Presenter.Dispose");
            attempt.Presenter = null; attempt.Resources.Dispose();
            foreach (string fault in attempt.Resources.Faults) faults.Add(fault);
            var observers = AttemptEnded;
            if (observers != null) foreach (Action<AttemptSettlement> observer in observers.GetInvocationList())
                Protect(() => observer(settlement), "Terminal observer");
            if ((outcome == PresentationOutcome.Completed || outcome == PresentationOutcome.Skipped) && hostOpen) Deliver(attempt.Token);
            else if ((outcome == PresentationOutcome.Completed || outcome == PresentationOutcome.Skipped) && !hostOpen)
                Session.State = PresentationState.CancelledRecoverable;
            else if (outcome == PresentationOutcome.Failed && hostOpen && attempt.Plan.AllowFailureFallback && !attempt.Fallback && attempt.Plan.Fallback != null)
                Enqueue(() =>
                {
                    if (active == null && ReferenceEquals(Session.LastSettlement?.Token, attempt.Token) && hostOpen && !Session.IsAcknowledged)
                        Begin(attempt.Plan, true);
                });
        }
        private void Deliver(AttemptToken token)
        {
            if (Session.DeliveryAttempts > 0)
            {
                Session.State = Session.ResultsAccepted ? PresentationState.ResultsShown : PresentationState.FailedRecoverable;
                return;
            }
            Session.DeliveryAttempts++;
            try
            {
                results.Show(new ResultDelivery(Session.Batch, token));
                Session.ResultsAccepted = hostOpen;
                Session.State = hostOpen ? PresentationState.ResultsShown : PresentationState.CancelledRecoverable;
            }
            catch (Exception error)
            {
                faults.Add("Result boundary: " + error.GetType().Name + "; delivery completion unknown, no automatic retry");
                Session.State = PresentationState.FailedRecoverable;
            }
        }
        private void Protect(Action action, string origin)
        { try { action(); } catch (Exception error) { faults.Add(origin + ": " + error.GetType().Name); } }
        public void Dispose()
        {
            hostOpen = false;
            Enqueue(() =>
            {
                if (active != null) Settle(active, PresentationOutcome.Cancelled, "Owner disposed");
                disposed = true; Session.Unbind(this);
            });
            if (Thread.CurrentThread.ManagedThreadId == ownerThread) Pump();
        }
    }
}

#endif
