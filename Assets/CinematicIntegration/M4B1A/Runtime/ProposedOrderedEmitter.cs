#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.Collections.Generic;
using System.Threading;
using ChessButWeird.CinematicIntegration.M4A;
using ChessButWeird.CinematicIntegration.M4B0;

namespace ChessButWeird.CinematicIntegration.M4B1A
{
    // Executable specification ONLY. No M35 component, native resources, acquisition or routing.
    public sealed class ProposedOrderedEmitter : IOrderedVisualEventSource
    {
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private readonly HashSet<AttemptToken> used = new HashSet<AttemptToken>();
        private AttemptToken active;
        private bool publishing, hostOpen = true;
        public event Action<AttemptToken, PresentationOutcome> OperationEnded;
        public event Action Completed;
        public GachaPresentationBatch Batch { get; private set; }
        public AttemptToken Active => active;
        public int TerminalCount { get; private set; }
        public int CompletionCount { get; private set; }
        public int ObserverFaults { get; private set; }
        public int CleanupCount { get; private set; }
        public bool ResourcesAttached { get; private set; } // Logical ownership marker, not a native leak test.
        private void CheckThread()
        { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Marshal to owner thread before emitter access"); }
        public void Begin(GachaPresentationBatch batch, AttemptToken token)
        {
            CheckThread();
            if (publishing) throw new InvalidOperationException("Reentrant Begin must be queued by the coordinator");
            if (!hostOpen) throw new InvalidOperationException("Host closed; create a new emitter for a new host");
            if (batch == null || token == null) throw new ArgumentNullException(batch == null ? nameof(batch) : nameof(token));
            if (used.Contains(token)) throw new InvalidOperationException("Attempt identity cannot be reused");
            if (active != null) End(active, PresentationOutcome.Cancelled);
            if (!hostOpen) throw new InvalidOperationException("Host closed during prior cancellation");
            used.Add(token); active = token; Batch = batch; ResourcesAttached = true;
        }
        public bool End(AttemptToken captured, PresentationOutcome outcome)
        {
            CheckThread();
            if (!Enum.IsDefined(typeof(PresentationOutcome), outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
            if (active == null || !ReferenceEquals(active, captured)) return false;
            active = null; TerminalCount++; // Settle before ANY subscriber can run.
            var terminal = OperationEnded; var compatibility = Completed;
            publishing = true;
            try
            {
                if (terminal != null) foreach (Action<AttemptToken, PresentationOutcome> observer in terminal.GetInvocationList())
                    try { observer(captured, outcome); } catch (Exception) { ObserverFaults++; }
                if (hostOpen && (outcome == PresentationOutcome.Completed || outcome == PresentationOutcome.Skipped))
                {
                    CompletionCount++;
                    if (compatibility != null) foreach (Action observer in compatibility.GetInvocationList())
                        try { observer(); } catch (Exception) { ObserverFaults++; }
                }
            }
            finally { publishing = false; }
            return true;
        }
        public void Stop() { CheckThread(); if (active != null) End(active, PresentationOutcome.Cancelled); }
        public void Cleanup()
        {
            CheckThread(); hostOpen = false;
            // Native implementation must detach consumers and release resources before terminal publication.
            if (ResourcesAttached) { ResourcesAttached = false; CleanupCount++; }
            Stop();
        }
    }
}

#endif
