#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChessButWeird.U1A3
{
    // Isolated additive projection. No catalog lookup, asset loading, grants or production route.
    public sealed class CommittedReward
    {
        public int Slot { get; internal set; }
        public string ItemId { get; internal set; }
        public string ItemCode { get; internal set; }
        public string ItemName { get; internal set; }
        public string BackendRarity { get; internal set; }
        public bool? IsDuplicate { get; internal set; }
        public string DuplicateRewardJson { get; internal set; }
        public string SnapshotMetadataJson { get; internal set; }
        public string AuthorityJson { get; internal set; }
        public string AuthorityState { get; internal set; }
        public bool ProductionArtworkAllowed => false; // No production authority registry is activated.
    }

    public sealed class CommittedReceipt
    {
        public string RequestId { get; internal set; }
        public string BannerCode { get; internal set; }
        public int Count { get; internal set; }
        public IReadOnlyList<CommittedReward> Rewards { get; internal set; }
        public string SnapshotState { get; internal set; }
        public string RawSnapshotJson { get; internal set; }
        public string EconomicJson { get; internal set; }
        public GachaRollResponse ToLegacyDto() => JsonConvert.DeserializeObject<GachaRollResponse>(EconomicJson);
    }

    public static class CommittedRewardContract
    {
        public const string BackendCommit = "7b8a1659fb7adda169ad6731bf1b5b58aa94530a";
        public static JObject ReadObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 1024 * 1024) throw new JsonException("Missing/oversized JSON");
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 48 })
            {
                var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new JsonException("Trailing JSON content");
                UniqueProperties(root); return root;
            }
        }
        static void UniqueProperties(JToken token)
        {
            if (token is JObject obj && obj.Properties().GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new JsonException("Ambiguous JSON property casing");
            foreach (var child in token.Children()) UniqueProperties(child);
        }
        public static string Text(JObject o, string name, bool required = false)
        {
            var t = o[name];
            if (t == null || t.Type == JTokenType.Null) { if (required) throw new JsonException("Missing " + name); return null; }
            if (t.Type != JTokenType.String) throw new JsonException("Expected string " + name);
            var s = (string)t; if (required && string.IsNullOrWhiteSpace(s)) throw new JsonException("Empty " + name); return s;
        }
        static void Require(bool condition, string message) { if (!condition) throw new JsonException(message); }
        static bool NullOr(JToken t, JTokenType type) => t == null || t.Type == JTokenType.Null || t.Type == type;
        public static CommittedReceipt Parse(string json)
        {
            var root = ReadObject(json); var request = Text(root, "requestId", true); Require(Guid.TryParse(request, out var guid) && guid != Guid.Empty, "Invalid requestId");
            var banner = Text(root, "bannerCode", true); Require(root["count"]?.Type == JTokenType.Integer, "Missing integer count");
            int count = (int)root["count"]; Require(count == 1 || count == 10, "Unsupported count");
            Require(root["results"] is JArray && ((JArray)root["results"]).Count == count, "Invalid result slots");
            // Exactly the serializer used by ApiClient; retain economic JSON, never apply its wallet/pity.
            var economic = (JObject)root.DeepClone(); economic.Remove("presentationSnapshot");
            var dto = JsonConvert.DeserializeObject<GachaRollResponse>(economic.ToString(Formatting.None));
            Require(dto != null && dto.results != null && dto.results.Count == count, "Economic DTO unreadable");
            var rows = new List<CommittedReward>();
            for (int i = 0; i < count; i++)
            {
                var row = root["results"][i] as JObject; Require(row != null, "Missing reward slot");
                Require(NullOr(row["isDuplicate"], JTokenType.Boolean), "Invalid duplicate flag");
                rows.Add(new CommittedReward { Slot = i, ItemId = Text(row, "itemId", true), ItemCode = Text(row, "itemCode"), ItemName = Text(row, "itemName"), BackendRarity = Text(row, "rarity"),
                    IsDuplicate = row["isDuplicate"]?.Type == JTokenType.Boolean ? (bool?)row["isDuplicate"] : null,
                    DuplicateRewardJson = row["duplicateReward"]?.ToString(Formatting.None), AuthorityState = "MISSING_UNATTESTED" });
            }
            var result = new CommittedReceipt { RequestId = request, BannerCode = banner, Count = count, Rewards = new ReadOnlyCollection<CommittedReward>(rows), EconomicJson = economic.ToString(Formatting.None),
                RawSnapshotJson = root["presentationSnapshot"]?.ToString(Formatting.None), SnapshotState = "MISSING" };
            var optional = root["presentationSnapshot"];
            if (optional == null || optional.Type == JTokenType.Null) return result;
            try { ReadSnapshot(optional, result, rows); }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is InvalidCastException || ex is OverflowException)
            {
                result.SnapshotState = "MALFORMED_UNAVAILABLE";
                foreach (var row in rows) { row.SnapshotMetadataJson = null; row.AuthorityJson = null; row.AuthorityState = "MALFORMED_UNATTESTED"; }
            }
            return result;
        }
        static void ReadSnapshot(JToken token, CommittedReceipt receipt, List<CommittedReward> rows)
        {
            var snapshot = token as JObject; Require(snapshot != null, "Snapshot must be object");
            Require(snapshot["schemaVersion"]?.Type == JTokenType.Integer, "Snapshot version missing");
            if ((int)snapshot["schemaVersion"] != 1) { receipt.SnapshotState = "UNSUPPORTED_SCHEMA_UNAVAILABLE"; return; }
            Require(Text(snapshot, "metadataSource", true) == "TRANSACTION_ITEM_READ", "Unrecognized V1 source");
            Require(snapshot["catalogVersion"] == null || snapshot["catalogVersion"].Type == JTokenType.Null, "V1 has no catalog revision authority");
            var metadata = snapshot["rewards"] as JArray; Require(metadata != null && metadata.Count == rows.Count, "Snapshot count mismatch");
            for (int i = 0; i < rows.Count; i++)
            {
                var m = metadata[i] as JObject; Require(m != null && m["slotIndex"]?.Type == JTokenType.Integer && (int)m["slotIndex"] == i, "Snapshot slot mismatch");
                Require(Text(m, "itemId", true) == rows[i].ItemId && Text(m, "itemCode") == rows[i].ItemCode, "Snapshot identity mismatch");
                Text(m, "rawItemType"); Text(m, "unityAssetKey");
                rows[i].SnapshotMetadataJson = m.ToString(Formatting.None);
                var a = m["presentationAuthority"]; rows[i].AuthorityJson = a?.ToString(Formatting.None);
                if (a == null || a.Type == JTokenType.Null) continue;
                rows[i].AuthorityState = "MALFORMED_UNATTESTED";
                if (!(a is JObject authority)) continue;
                try
                {
                    foreach (var name in new[] { "itemId","metadataSource","resolutionStatus","typeStatus","category","unityPresentationCategory","approvedUnityAssetKey","policyVersion","manifestVersion","approvalScope","contractVersion","candidateUnityLocalCatalogId","candidateUnityAddressablesAddress","categoryAuthorityStatus","teamAuthorityStatus","runtimeVerificationStatus","productionStatus","unityAssetAvailability" }) Text(authority, name);
                    Require(NullOr(authority["isEquippable"], JTokenType.Boolean) && NullOr(authority["isProductionArtworkVerified"], JTokenType.Boolean), "Invalid authority flags");
                    rows[i].AuthorityState = Text(authority, "itemId") == rows[i].ItemId && Text(authority, "contractVersion") == "C2_ADDITIVE_V1" ? "RETAINED_UNATTESTED" : "UNRECOGNIZED_UNATTESTED";
                }
                catch (JsonException) { /* Optional authority cannot invalidate the economic receipt. Raw evidence is retained. */ }
            }
            receipt.SnapshotState = "V1_RAW_OBSERVATION_ONLY";
        }
    }
}

#endif
