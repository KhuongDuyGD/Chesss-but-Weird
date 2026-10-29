#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using ChessButWeird.CinematicIntegration.M4A;
using ChessButWeird.CinematicIntegration.M4B0;

namespace ChessButWeird.CinematicIntegration.M4B1A
{
    public enum RewardTreatment { NeutralUnknown, Common, Rare, Epic, Legendary }
    public enum ArtworkPresentationIntent { TextOnlyUnresolved, ReferencePendingAcquisition }

    public sealed class PresentationCapabilities
    {
        public bool ReducedMotion { get; }
        public bool ReducedFlash { get; }
        public bool CameraShake { get; }
        public bool Sound { get; }
        public bool FastTen { get; }
        public PresentationCapabilities(bool reducedMotion = false, bool reducedFlash = false,
            bool cameraShake = true, bool sound = true, bool fastTen = false)
        { ReducedMotion = reducedMotion; ReducedFlash = reducedFlash; CameraShake = cameraShake; Sound = sound; FastTen = fastTen; }
    }

    // One-way per-attempt lifetime. The future adapter MUST close this on Cancel/Dispose/host loss.
    public sealed class PresentationLifetime
    {
        private int cancelled, hostOpen = 1;
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        public AttemptToken Token { get; }
        public bool IsCancellationRequested => Volatile.Read(ref cancelled) != 0;
        public bool IsHostOpen => Volatile.Read(ref hostOpen) != 0;
        public PresentationLifetime(AttemptToken token) { Token = token ?? throw new ArgumentNullException(nameof(token)); }
        public void Cancel() => Interlocked.Exchange(ref cancelled, 1);
        public void CloseHost() { Interlocked.Exchange(ref hostOpen, 0); Cancel(); }
        public void VerifyOwnerThread()
        { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Dispatch presentation to its owner thread"); }
    }

    public sealed class RewardSlotSpecification
    {
        public GachaRewardPresentationData Reward { get; }
        public ArtworkAcquisitionObservation Artwork { get; }
        public RewardTreatment Treatment { get; }
        public ArtworkPresentationIntent Intent { get; }
        public bool MayDisplayAcquiredArtwork => false; // B1A has no acquisition implementation or success state.
        internal RewardSlotSpecification(GachaRewardPresentationData reward, ArtworkAcquisitionObservation artwork)
        {
            Reward = reward; Artwork = artwork;
            switch (reward.CanonicalRarity)
            {
                case PresentationRarity.Common: Treatment = RewardTreatment.Common; break;
                case PresentationRarity.Rare: Treatment = RewardTreatment.Rare; break;
                case PresentationRarity.Epic: Treatment = RewardTreatment.Epic; break;
                case PresentationRarity.Legendary: Treatment = RewardTreatment.Legendary; break;
                default: Treatment = RewardTreatment.NeutralUnknown; break;
            }
            Intent = Treatment != RewardTreatment.NeutralUnknown && artwork.Descriptor.HasSpecifiedReference &&
                artwork.State == ArtworkAcquisitionState.NotRequested ? ArtworkPresentationIntent.ReferencePendingAcquisition : ArtworkPresentationIntent.TextOnlyUnresolved;
        }
    }

    public sealed class RealRewardPresentationSpec
    {
        public PresentationAttemptContext Attempt { get; }
        public GachaPresentationBatch Batch => Attempt.Batch;
        public AttemptToken Token => Attempt.Token;
        public PresentationLifetime Lifetime { get; }
        public PresentationCapabilities Capabilities { get; }
        public IReadOnlyList<RewardSlotSpecification> Slots { get; }
        public bool IsTen => Batch.ActualCount == 10;
        public bool AcceleratedBatch => IsTen && Capabilities.FastTen;
        private RealRewardPresentationSpec(PresentationAttemptContext attempt, IEnumerable<ArtworkAcquisitionObservation> artwork,
            PresentationCapabilities capabilities, PresentationLifetime lifetime)
        {
            Attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
            Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
            Lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            Lifetime.VerifyOwnerThread();
            if (!ReferenceEquals(Token, lifetime.Token)) throw new ArgumentException("Lifetime belongs to another attempt", nameof(lifetime));
            if (!Batch.IsStructurallyValid || string.IsNullOrWhiteSpace(Batch.RequestId) || string.IsNullOrWhiteSpace(Batch.BannerCode))
                throw new ArgumentException("Original batch lacks presentation prerequisites", nameof(attempt));
            if (artwork == null) throw new ArgumentNullException(nameof(artwork));
            var copy = artwork.ToArray();
            if (copy.Length != Batch.ActualCount) throw new ArgumentException("One descriptor per original slot is required", nameof(artwork));
            var slots = new List<RewardSlotSpecification>();
            for (int index = 0; index < copy.Length; index++)
            {
                if (copy[index] == null || !ReferenceEquals(copy[index].Descriptor.Reward, Batch.Rewards[index]))
                    throw new ArgumentException("Descriptor belongs to another batch or slot; matching item IDs are insufficient", nameof(artwork));
                slots.Add(new RewardSlotSpecification(Batch.Rewards[index], copy[index]));
            }
            Slots = new ReadOnlyCollection<RewardSlotSpecification>(slots);
            VerifyReadyForPlay();
        }
        public static RealRewardPresentationSpec Create(PresentationAttemptContext attempt, IEnumerable<ArtworkAcquisitionObservation> artwork,
            PresentationCapabilities capabilities, PresentationLifetime lifetime) => new RealRewardPresentationSpec(attempt, artwork, capabilities, lifetime);
        public void VerifyReadyForPlay()
        {
            Lifetime.VerifyOwnerThread();
            if (Lifetime.IsCancellationRequested || !Lifetime.IsHostOpen) throw new OperationCanceledException("Presentation lifetime closed");
            // This check is not routing authority. B0 still owns settlement/generation; native adapters need fresh owner dispatch.
        }
    }

    // Future native adapter boundary ONLY. No implementation, scene/prefab or production consumer in B1A.
    public interface IRealRewardPresenterPort : IDisposable
    {
        void Prepare(RealRewardPresentationSpec specification);
        void Play();
        void Cancel();
    }
}

#endif
