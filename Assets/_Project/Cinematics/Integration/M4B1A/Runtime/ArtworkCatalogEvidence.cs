#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ChessButWeird.CinematicIntegration.M4B1A
{
    public enum ArtworkEvidenceScope { Unverified, Fixture, EditorInspection }
    public enum ArtworkCategory { Unsupported, PieceSkin, Board, Environment }
    public enum ArtworkReferenceKind { AddressableGuid, CatalogSprite }

    // Caller-supplied provenance, not a cryptographic authorization or backend attestation.
    public sealed class ArtworkEvidence
    {
        public ArtworkEvidenceScope Scope { get; }
        public string Source { get; }
        public string SourceSha256 { get; }
        public ArtworkEvidence(ArtworkEvidenceScope scope, string source, string sourceSha256 = null)
        {
            if (!Enum.IsDefined(typeof(ArtworkEvidenceScope), scope)) throw new ArgumentOutOfRangeException(nameof(scope));
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Evidence source is required", nameof(source));
            if (scope == ArtworkEvidenceScope.EditorInspection && !IsHex(sourceSha256, 64))
                throw new ArgumentException("Inspection requires a source hash", nameof(sourceSha256));
            Scope = scope; Source = source; SourceSha256 = sourceSha256;
        }
        internal static bool IsHex(string value, int length) => value != null && value.Length == length &&
            value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F');
    }

    public sealed class ArtworkAssetReference
    {
        public ArtworkReferenceKind Kind { get; }
        public string Guid { get; }
        public string AssetPath { get; }
        public string ObservedType { get; }
        public string Address { get; }
        public long LocalFileId { get; }
        public string SubObjectName { get; }
        public bool IsMissing => string.IsNullOrWhiteSpace(Guid) && string.IsNullOrWhiteSpace(AssetPath);
        // Syntax/observed metadata only. This does not query a locator, load an asset or prove file existence.
        public bool HasUsableSpecification => Enum.IsDefined(typeof(ArtworkReferenceKind), Kind) &&
            ArtworkEvidence.IsHex(Guid, 32) && Guid.Any(c => c != '0') && AssetPath != null &&
            AssetPath.StartsWith("Assets/", StringComparison.Ordinal) && AssetPath.IndexOf('\\') < 0 &&
            !AssetPath.Split('/').Any(segment => segment == ".." || segment == "." || segment.Length == 0) &&
            !string.IsNullOrWhiteSpace(ObservedType) && (Kind != ArtworkReferenceKind.CatalogSprite || LocalFileId != 0);
        public ArtworkAssetReference(ArtworkReferenceKind kind, string guid, string path, string observedType,
            string address = null, long localFileId = 0, string subObjectName = null)
        { Kind = kind; Guid = guid; AssetPath = path; ObservedType = observedType; Address = address; LocalFileId = localFileId; SubObjectName = subObjectName; }
    }

    public sealed class ArtworkCatalogEntry
    {
        public ArtworkCategory Category { get; }
        public string Key { get; }
        public string InternalId { get; }
        public ArtworkAssetReference Data { get; }
        public ArtworkAssetReference Preview { get; }
        public ArtworkCatalogEntry(ArtworkCategory category, string key, string internalId, ArtworkAssetReference data, ArtworkAssetReference preview)
        {
            if (!Enum.IsDefined(typeof(ArtworkCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            Category = category; Key = key; InternalId = internalId; Data = data; Preview = preview;
        }
    }
    public sealed class ArtworkCatalogSnapshot
    {
        public ArtworkEvidence Evidence { get; }
        public IReadOnlyList<ArtworkCatalogEntry> Entries { get; }
        public ArtworkCatalogSnapshot(IEnumerable<ArtworkCatalogEntry> entries, ArtworkEvidence evidence)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
            var copy = entries.ToList(); if (copy.Any(e => e == null)) throw new ArgumentException("Null catalog entry", nameof(entries));
            Entries = new ReadOnlyCollection<ArtworkCatalogEntry>(copy);
        }
    }
    public sealed class ArtworkTypeBinding
    {
        public string ExactItemType { get; }
        public ArtworkCategory Category { get; }
        public ArtworkTypeBinding(string exactItemType, ArtworkCategory category)
        {
            if (string.IsNullOrWhiteSpace(exactItemType)) throw new ArgumentException("Exact type required", nameof(exactItemType));
            if (!Enum.IsDefined(typeof(ArtworkCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            ExactItemType = exactItemType; Category = category;
        }
    }
    public sealed class ArtworkTypePolicy
    {
        public ArtworkEvidence Evidence { get; }
        public IReadOnlyList<ArtworkTypeBinding> Bindings { get; }
        public static ArtworkTypePolicy Unresolved => new ArtworkTypePolicy(new ArtworkTypeBinding[0],
            new ArtworkEvidence(ArtworkEvidenceScope.Unverified, "No authoritative server type/category policy supplied"));
        public ArtworkTypePolicy(IEnumerable<ArtworkTypeBinding> bindings, ArtworkEvidence evidence)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
            var copy = bindings.ToList(); if (copy.Any(e => e == null)) throw new ArgumentException("Null binding", nameof(bindings));
            Bindings = new ReadOnlyCollection<ArtworkTypeBinding>(copy);
        }
    }
}

#endif
