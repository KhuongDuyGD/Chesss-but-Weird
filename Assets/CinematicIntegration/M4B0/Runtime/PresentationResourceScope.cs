#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ChessButWeird.CinematicIntegration.M4B0
{
    public enum PresentationResourceKind { RenderTexture, RuntimeMaterial, PlayableGraph, AddressableLoad, AddressableInstance, ManualInstance, AsyncAsset, BorrowedArtwork }
    public interface IResourceRegistration
    {
        void Own(object identity, PresentationResourceKind kind, Action release, Action detachConsumers = null);
        void Borrow(object identity);
    }
    // No native API is used here. Real adapters must supply the correct main-thread release operation per kind.
    public sealed class PresentationResourceScope : IResourceRegistration, IDisposable
    {
        private sealed class IdentityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
        private sealed class Entry
        {
            public PresentationResourceKind Kind;
            public Action Detach, Release;
            public bool Detached, ReleaseAttempted;
        }
        private readonly HashSet<object> identities = new HashSet<object>(new IdentityComparer());
        private readonly List<Entry> owned = new List<Entry>();
        private readonly List<string> faults = new List<string>();
        public bool IsClosed { get; private set; }
        public int OwnedAcquisitions { get; private set; }
        public int ReleaseAttempts { get; private set; }
        public int SuccessfulReleases { get; private set; }
        public IReadOnlyList<string> Faults => faults.AsReadOnly();
        public void Borrow(object identity)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (!identities.Add(identity)) throw new InvalidOperationException("Resource identity already registered");
            // No release capability accepted for a borrowed object.
        }
        public void Own(object identity, PresentationResourceKind kind, Action release, Action detachConsumers = null)
        {
            if (identity == null || release == null) throw new ArgumentNullException(identity == null ? nameof(identity) : nameof(release));
            if (!Enum.IsDefined(typeof(PresentationResourceKind), kind) || kind == PresentationResourceKind.BorrowedArtwork)
                throw new ArgumentException("Borrowed/unknown kind cannot be owned");
            if (!identities.Add(identity)) throw new InvalidOperationException("Resource identity already registered");
            var entry = new Entry { Kind = kind, Release = release, Detach = detachConsumers };
            owned.Add(entry); OwnedAcquisitions++;
            if (IsClosed) { Detach(entry); Release(entry); }
        }
        private void Detach(Entry entry)
        {
            if (entry.Detached) return;
            try { entry.Detach?.Invoke(); entry.Detached = true; entry.Detach = null; }
            catch (Exception error) { faults.Add("Detach " + entry.Kind + ": " + error.GetType().Name); }
        }
        private void Release(Entry entry)
        {
            // Unknown detachment state is quarantined rather than releasing a texture still in use.
            if (!entry.Detached || entry.ReleaseAttempted) return;
            entry.ReleaseAttempted = true; ReleaseAttempts++;
            var action = entry.Release; entry.Release = null;
            try { action(); SuccessfulReleases++; }
            catch (Exception error) { faults.Add("Release " + entry.Kind + ": " + error.GetType().Name + "; completion unknown, no automatic retry"); }
        }
        public void Dispose()
        {
            if (IsClosed) return;
            IsClosed = true;
            // Detach ALL consumers first, then reverse acquisition order (instances before their loaded assets).
            for (int i = owned.Count - 1; i >= 0; i--) Detach(owned[i]);
            for (int i = owned.Count - 1; i >= 0; i--) Release(owned[i]);
        }
    }
}

#endif
