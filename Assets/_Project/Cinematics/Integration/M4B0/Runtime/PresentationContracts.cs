#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using ChessButWeird.CinematicIntegration.M4A;

namespace ChessButWeird.CinematicIntegration.M4B0
{
    public enum PresentationState { Received, Preparing, Playing, ResultsShown, Acknowledged, CancelledRecoverable, FailedRecoverable }
    public enum PresentationOutcome { Completed, Skipped, Cancelled, Failed }
    [Flags] public enum PresentationBlocker { None = 0, InvalidBatch = 1, MissingRequestCorrelation = 2, MissingBanner = 4 }

    // Object identity, not the sequence alone, authorizes a callback. This is never a server request ID.
    public sealed class AttemptToken
    {
        public long Sequence { get; }
        internal AttemptToken(long sequence) { Sequence = sequence; }
    }
    public sealed class AttemptSettlement
    {
        public AttemptToken Token { get; }
        public PresentationOutcome Outcome { get; }
        public string Reason { get; }
        public bool UsedFallback { get; }
        internal AttemptSettlement(AttemptToken token, PresentationOutcome outcome, string reason, bool fallback)
        { Token = token; Outcome = outcome; Reason = reason; UsedFallback = fallback; }
    }
    public sealed class ResultDelivery
    {
        public GachaPresentationBatch Batch { get; }
        public AttemptToken Attempt { get; }
        internal ResultDelivery(GachaPresentationBatch batch, AttemptToken attempt) { Batch = batch; Attempt = attempt; }
    }
    public interface IResultsBoundary { void Show(ResultDelivery delivery); }
    public interface IVisualPresenter : IDisposable
    {
        void Prepare(PresentationAttemptContext context);
        void Play();
        void Cancel();
    }
    public interface IPresentationFactory
    {
        bool Supports(GachaPresentationBatch batch);
        IVisualPresenter Create();
    }
    public sealed class PresentationPlan
    {
        public IPresentationFactory Primary { get; }
        public IPresentationFactory Fallback { get; }
        public bool AllowFailureFallback { get; }
        public PresentationPlan(IPresentationFactory primary, IPresentationFactory fallback = null, bool allowFailureFallback = false)
        { Primary = primary ?? throw new ArgumentNullException(nameof(primary)); Fallback = fallback; AllowFailureFallback = allowFailureFallback; }
    }
    public sealed class PresentationAttemptContext
    {
        private readonly Action<PresentationOutcome, string> report;
        public GachaPresentationBatch Batch { get; }
        public AttemptToken Token { get; }
        public IResourceRegistration Resources { get; }
        internal PresentationAttemptContext(GachaPresentationBatch batch, AttemptToken token, IResourceRegistration resources, Action<PresentationOutcome, string> callback)
        { Batch = batch; Token = token; Resources = resources; report = callback; }
        // May arrive late/from a worker: enqueues only. The owner pumps on its owning thread.
        public void Report(PresentationOutcome outcome, string reason = null) => report(outcome, reason);
    }

    // A future-facing contract, not an adapter to the actual M35 component.
    public interface IOrderedVisualEventSource
    {
        event Action<AttemptToken, PresentationOutcome> OperationEnded;
        event Action Completed;
        void Begin(GachaPresentationBatch batch, AttemptToken token);
        void Stop();
        void Cleanup();
    }
    public sealed class OrderedEventPresenter : IVisualPresenter
    {
        private readonly IOrderedVisualEventSource source;
        private Action<AttemptToken, PresentationOutcome> terminal;
        private PresentationAttemptContext context;
        public OrderedEventPresenter(IOrderedVisualEventSource source) { this.source = source ?? throw new ArgumentNullException(nameof(source)); }
        public void Prepare(PresentationAttemptContext value)
        {
            if (context != null) throw new InvalidOperationException("One presenter per attempt");
            context = value;
            var captured = value;
            terminal = (token, outcome) => { if (ReferenceEquals(token, captured.Token)) captured.Report(outcome); };
            source.OperationEnded += terminal;
            // Intentionally NEVER subscribe to Completed as another result-delivery signal.
        }
        public void Play() => source.Begin(context.Batch, context.Token);
        public void Cancel() => source.Stop();
        public void Dispose()
        {
            if (terminal == null) return;
            source.OperationEnded -= terminal; terminal = null; context = null;
            source.Cleanup();
        }
    }
}

#endif
