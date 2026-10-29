#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ChessButWeird.CinematicIntegration.M4A;

namespace ChessButWeird.CinematicIntegration.M4B0
{
    // The caller retains this owner independently of any visual host or coordinator.
    public sealed class ReceivedPresentationSession
    {
        private object controller;
        private long sequence;
        private readonly List<AttemptSettlement> settlements = new List<AttemptSettlement>();
        public GachaPresentationBatch Batch { get; }
        public IReadOnlyList<GachaArtworkResolutionRequest> Artwork { get; }
        public PresentationBlocker Blockers { get; }
        public bool HasPresentationPrerequisites => Blockers == PresentationBlocker.None;
        public PresentationState State { get; internal set; } = PresentationState.Received;
        public IReadOnlyList<AttemptSettlement> Settlements { get; }
        public AttemptSettlement LastSettlement => settlements.Count == 0 ? null : settlements[settlements.Count - 1];
        public int DeliveryAttempts { get; internal set; }
        public bool ResultsAccepted { get; internal set; }
        public bool IsAcknowledged => State == PresentationState.Acknowledged;
        public ReceivedPresentationSession(GachaPresentationBatch batch)
        {
            Batch = batch ?? throw new ArgumentNullException(nameof(batch));
            if (!batch.IsStructurallyValid) Blockers |= PresentationBlocker.InvalidBatch;
            if (string.IsNullOrWhiteSpace(batch.RequestId)) Blockers |= PresentationBlocker.MissingRequestCorrelation;
            if (string.IsNullOrWhiteSpace(batch.BannerCode)) Blockers |= PresentationBlocker.MissingBanner;
            Artwork = new ReadOnlyCollection<GachaArtworkResolutionRequest>(batch.Rewards.Select(GachaArtworkResolutionRequest.For).ToList());
            Settlements = settlements.AsReadOnly();
        }
        internal void Bind(object owner)
        {
            if (controller != null) throw new InvalidOperationException("Session already has a presentation controller");
            controller = owner;
        }
        internal void Unbind(object owner) { if (ReferenceEquals(controller, owner)) controller = null; }
        internal AttemptToken NextAttempt() => new AttemptToken(checked(++sequence));
        internal void AddSettlement(AttemptSettlement value) { settlements.Add(value); }
    }
}

#endif
