#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ChessButWeird.CinematicIntegration.M4B1A;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ChessButWeird.CinematicIntegration.M4B1B2A
{
    public enum AcquisitionState { Pending, Available, MissingReference, Unsupported, Failed, Cancelled, StaleGeneration, Released }

    // Owned operation capability. Providers must complete on the creating Unity thread and
    // deliver at most once. The consumer never gets the native handle or release capability.
    public interface ISpriteOperation
    {
        bool IsDone { get; }
        bool Succeeded { get; }
        Sprite Asset { get; }
        string Error { get; }
        event Action Completed;
        void Release();
    }
    public interface ISpriteLoader { ISpriteOperation Load(string verifiedGuid); }

    public sealed class NativeLoadTrace
    {
        public string OwnerId { get; internal set; }
        public string Guid { get; internal set; }
        public int HandleHash { get; internal set; }
        public bool PendingAtCreation { get; internal set; }
        public string CompletionStatus { get; internal set; }
        public int ResultInstanceId { get; internal set; }
        public int ReleaseCalls { get; internal set; }
        public bool ValidBeforeRelease { get; internal set; }
        public bool ValidAfterRelease { get; internal set; }
    }

    public sealed class NativeSpriteLoader : ISpriteLoader
    {
        private static int unityThread;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void IdentifyUnityThread() => unityThread = Thread.CurrentThread.ManagedThreadId;
        private readonly int owner = Thread.CurrentThread.ManagedThreadId;
        private readonly List<NativeLoadTrace> trace = new List<NativeLoadTrace>();
        public IReadOnlyList<NativeLoadTrace> Trace => trace.AsReadOnly();
        public NativeSpriteLoader()
        {
            if (unityThread == 0 || owner != unityThread) throw new InvalidOperationException("Create native loader on Unity's initialized PlayMode thread");
        }
        public ISpriteOperation Load(string verifiedGuid)
        {
            if (owner != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Unity owner thread required");
            var handle = Addressables.LoadAssetAsync<Sprite>(verifiedGuid);
            var record = new NativeLoadTrace { OwnerId = System.Guid.NewGuid().ToString("N"), Guid = verifiedGuid,
                HandleHash = handle.GetHashCode(), PendingAtCreation = !handle.IsDone };
            trace.Add(record);
            try { return new Operation(handle, record); }
            catch { if (handle.IsValid()) Addressables.Release(handle); throw; }
        }
        private sealed class Operation : ISpriteOperation
        {
            private AsyncOperationHandle<Sprite> handle;
            private bool released;
            private readonly NativeLoadTrace trace;
            private readonly int owner = Thread.CurrentThread.ManagedThreadId;
            public event Action Completed;
            private void VerifyThread()
            { if (owner != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Unity owner thread required"); }
            public bool IsDone { get { VerifyThread(); return !released && handle.IsValid() && handle.IsDone; } }
            public bool Succeeded => IsDone && handle.Status == AsyncOperationStatus.Succeeded && handle.Result;
            public Sprite Asset => Succeeded ? handle.Result : null;
            public string Error => IsDone ? handle.OperationException?.GetType().Name : null;
            public Operation(AsyncOperationHandle<Sprite> value, NativeLoadTrace trace) { handle = value; this.trace = trace; handle.Completed += Done; }
            private void Done(AsyncOperationHandle<Sprite> value)
            {
                trace.CompletionStatus = value.Status.ToString();
                trace.ResultInstanceId = value.Result ? value.Result.GetInstanceID() : 0;
                Completed?.Invoke();
            }
            public void Release()
            {
                if (owner != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Unity owner thread required");
                if (released) return;
                released = true;
                trace.ReleaseCalls++;
                trace.ValidBeforeRelease = handle.IsValid();
                if (!handle.IsValid()) throw new InvalidOperationException("Owned native handle became invalid externally");
                trace.CompletionStatus = handle.Status.ToString();
                trace.ResultInstanceId = handle.Result ? handle.Result.GetInstanceID() : 0;
                handle.Completed -= Done;
                Addressables.Release(handle); // Asset handle only. Never Destroy(asset) / ReleaseInstance.
                trace.ValidAfterRelease = handle.IsValid(); // May stay valid when another owner retains a shared operation.
            }
        }
    }

    // One scope per B0 attempt. Availability is separate from B1A specification and
    // NEVER establishes backend identity. Poll dispatch on the owner thread; native
    // completion still releases a closed scope's late handle without a future Pump.
    public sealed class SpriteAcquisitionScope : IDisposable
    {
        private readonly int owner = Thread.CurrentThread.ManagedThreadId;
        private readonly ISpriteLoader loader;
        private readonly List<SpriteAcquisition> requests = new List<SpriteAcquisition>();
        private readonly List<string> faults = new List<string>();
        private bool closed, pumping;
        private AcquisitionState closure = AcquisitionState.Cancelled;
        public RealRewardPresentationSpec Specification { get; }
        public IReadOnlyList<string> Faults => faults.AsReadOnly();
        public bool IsClosed => closed || Specification.Lifetime.IsCancellationRequested || !Specification.Lifetime.IsHostOpen;
        public int OutstandingOwned => requests.Count(r => r.HasOutstandingOperation);
        public SpriteAcquisitionScope(RealRewardPresentationSpec specification, ISpriteLoader loader)
        {
            Specification = specification ?? throw new ArgumentNullException(nameof(specification));
            Specification.VerifyReadyForPlay();
            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }
        internal void VerifyThread()
        { if (owner != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Unity owner thread required"); }
        internal void Fault(Exception error) => faults.Add(error.GetType().Name + ": " + error.Message);
        public SpriteAcquisition Acquire(int slot, Action<SpriteAcquisition> completion = null)
        {
            VerifyThread();
            if (pumping) throw new InvalidOperationException("Queue acquisition after dispatch");
            if (slot < 0 || slot >= Specification.Slots.Count) throw new ArgumentOutOfRangeException(nameof(slot));
            // Distinct request identities even for repeated item IDs and the same slot.
            var request = new SpriteAcquisition(this, slot, completion);
            requests.Add(request);
            request.Start(loader);
            return request;
        }
        public void Pump()
        {
            VerifyThread();
            if (pumping) throw new InvalidOperationException("Recursive dispatch");
            pumping = true;
            try { foreach (var request in requests.ToArray()) request.Dispatch(); }
            finally { pumping = false; }
        }
        internal AcquisitionState ClosedState => closure;
        public void Supersede() => Close(AcquisitionState.StaleGeneration);
        public void Dispose() => Close(AcquisitionState.Cancelled);
        private void Close(AcquisitionState reason)
        {
            VerifyThread();
            if (closed) return;
            closed = true; closure = reason;
            Specification.Lifetime.Cancel();
            foreach (var request in requests) request.CancelForScope(reason);
        }
    }

    public sealed class SpriteAcquisition : IDisposable
    {
        private readonly SpriteAcquisitionScope scope;
        private Action<SpriteAcquisition> completion;
        private ISpriteOperation operation;
        private bool dispatched, released, completed;
        private AcquisitionState state = AcquisitionState.Pending;
        public int OriginalSlotIndex { get; }
        public ArtworkDescriptor Descriptor => scope.Specification.Slots[OriginalSlotIndex].Artwork.Descriptor;
        public AcquisitionState State => scope.IsClosed && (state == AcquisitionState.Pending || state == AcquisitionState.Available) ? scope.ClosedState : state;
        public Sprite Asset { get { scope.VerifyThread(); return State == AcquisitionState.Available && !released ? operation?.Asset : null; } }
        public bool HasOutstandingOperation => operation != null && !released;
        public int ReleaseAttempts { get; private set; }
        public int SuccessfulReleases { get; private set; }
        public int CompletionDispatches { get; private set; }
        public string Error { get; private set; }
        internal SpriteAcquisition(SpriteAcquisitionScope owner, int slot, Action<SpriteAcquisition> callback)
        { scope = owner; OriginalSlotIndex = slot; completion = callback; }
        internal void Start(ISpriteLoader loader)
        {
            if (scope.IsClosed) { state = scope.ClosedState; return; }
            var reference = Descriptor.Entry?.Preview;
            if (!Descriptor.HasSpecifiedReference || reference == null || !reference.HasUsableSpecification)
            { state = Descriptor.Status == ArtworkStatus.UnsupportedArtwork ? AcquisitionState.Unsupported : AcquisitionState.MissingReference; return; }
            // Direct catalog sprites are borrowed by their catalog owner. This loader deliberately
            // accepts only independently inspected, single-Sprite Addressables GUID specifications.
            if (reference.Kind != ArtworkReferenceKind.AddressableGuid || reference.ObservedType != "Sprite" || !string.IsNullOrEmpty(reference.SubObjectName))
            { state = AcquisitionState.Unsupported; return; }
            try
            {
                operation = loader.Load(reference.Guid);
                if (operation == null) throw new InvalidOperationException("Loader returned no owned operation");
                operation.Completed += OnCompleted;
                if (operation.IsDone) OnCompleted();
            }
            catch (Exception error) { Error = error.GetType().Name; state = AcquisitionState.Failed; ReleaseIfDone(); }
        }
        private void OnCompleted()
        {
            scope.VerifyThread();
            if (completed) return;
            completed = true;
            if (scope.IsClosed) state = scope.ClosedState;
            else if (state == AcquisitionState.Pending)
            {
                state = operation.Succeeded && operation.Asset ? AcquisitionState.Available : AcquisitionState.Failed;
                Error = operation.Error;
            }
            if (state != AcquisitionState.Available) ReleaseIfDone();
        }
        internal void Dispatch()
        {
            if (scope.IsClosed) CancelForScope(scope.ClosedState);
            if (state == AcquisitionState.Pending || dispatched) return;
            dispatched = true; CompletionDispatches++;
            var callback = completion; completion = null;
            try { callback?.Invoke(this); }
            catch (Exception error) { scope.Fault(error); Dispose(); }
        }
        internal void CancelForScope(AcquisitionState reason)
        {
            if (state == AcquisitionState.Pending || state == AcquisitionState.Available) state = reason;
            // A cancelled native load may continue. Never release a still-running instance/load
            // here; OnCompleted retains this request and releases the late operation exactly once.
            ReleaseIfDone();
        }
        public void Cancel() { scope.VerifyThread(); CancelForScope(AcquisitionState.Cancelled); }
        public void Dispose()
        {
            scope.VerifyThread();
            if (state == AcquisitionState.Available) state = AcquisitionState.Released;
            else if (state == AcquisitionState.Pending) state = AcquisitionState.Cancelled;
            ReleaseIfDone();
        }
        private void ReleaseIfDone()
        {
            if (operation == null || released || !operation.IsDone) return;
            released = true; operation.Completed -= OnCompleted; ReleaseAttempts++;
            try { operation.Release(); SuccessfulReleases++; operation = null; }
            catch (Exception error) { Error = "Release outcome unknown; no automatic retry"; scope.Fault(error); }
        }
    }
}

#endif
