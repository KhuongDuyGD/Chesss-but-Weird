#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ChessButWeird.U1A3;

namespace ChessButWeird.U1A4
{
    public sealed class TrustedAccountLease
    {
        public string AccountId { get; }
        public long Epoch { get; }
        internal TrustedAccountLease(string account,long epoch){AccountId=account;Epoch=epoch;}
    }
    public interface ITrustedAccountProvider
    {
        TrustedAccountLease Capture();
        bool IsCurrent(TrustedAccountLease lease);
    }
    public sealed class StoredCommittedResult
    {
        public string AccountId { get; }
        public string OperationKey { get; }
        public CommittedReceipt Receipt { get; }
        public string IdentityHash { get; }
        internal StoredCommittedResult(string account,CommittedReceipt receipt,string identityHash)
        {AccountId=account;Receipt=receipt;IdentityHash=identityHash;OperationKey=DurablePendingStore.Hash(account)+":"+Guid.Parse(receipt.RequestId).ToString("D");}
    }
    // Write-once application result inbox; neither economic inventory nor animation history.
    public sealed class DurableCommittedResultStore : ICommittedReceiptConsumer
    {
        readonly string root;readonly ITrustedAccountProvider accounts;readonly Action<string> fault;
        public DurableCommittedResultStore(string root,ITrustedAccountProvider accounts,Action<string> fault=null)
        {this.root=Path.GetFullPath(root??throw new ArgumentNullException(nameof(root)));if(this.root.Length>120)throw new IOException("Result root exceeds validated Windows/Mono path budget; use an explicit shorter app-owned root");this.accounts=accounts??throw new ArgumentNullException(nameof(accounts));this.fault=fault;Directory.CreateDirectory(this.root);}
        static string Id(string request){if(!Guid.TryParse(request,out var id)||id==Guid.Empty)throw new ArgumentException("Invalid RequestId");return id.ToString("D");}
        TrustedAccountLease Lease(string account=null)
        {var lease=accounts.Capture();if(lease==null||(account!=null&&lease.AccountId!=account))throw new UnauthorizedAccessException("Trusted account required");return lease;}
        void Current(TrustedAccountLease lease){if(!accounts.IsCurrent(lease))throw new UnauthorizedAccessException("Account session changed");}
        string Folder(TrustedAccountLease lease){var p=Path.Combine(root,DurablePendingStore.Hash(lease.AccountId));Directory.CreateDirectory(p);return p;}
        string PathFor(TrustedAccountLease lease,string request)=>Path.Combine(Folder(lease),Id(request)+".result.json");
        // Diagnostic path is account scoped; it cannot select another account's directory.
        public string DiagnosticPath(string request)=>PathFor(Lease(),request);
        static JToken Ordered(JToken token)
        {
            if(token is JObject o)return new JObject(o.Properties().OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>new JProperty(p.Name,Ordered(p.Value))));
            if(token is JArray a)return new JArray(a.Select(Ordered));return token.DeepClone();
        }
        static string Canonical(JToken token)=>Ordered(token).ToString(Formatting.None);
        static JObject Projection(CommittedReceipt receipt)
        {
            var raw=CommittedRewardContract.ReadObject(receipt.EconomicJson);
            var wire=new JObject{["requestId"]=Id(receipt.RequestId),["bannerCode"]=receipt.BannerCode,["count"]=receipt.Count,["results"]=raw["results"].DeepClone()};
            if(receipt.RawSnapshotJson!=null)wire["presentationSnapshot"]=JToken.Parse(receipt.RawSnapshotJson);
            CommittedRewardContract.Parse(Canonical(wire));return wire;
        }
        static string Identity(JObject wire)
        {var core=(JObject)wire.DeepClone();core.Remove("presentationSnapshot");return DurablePendingStore.Hash(Canonical(core));}
        StoredCommittedResult ReadUnlocked(TrustedAccountLease lease,string request)
        {
            Current(lease);var path=PathFor(lease,request);
            if(File.Exists(path+".tmp"))throw new IOException("Interrupted result write retained; operator review required");
            if(!File.Exists(path))return null;
            if(new FileInfo(path).Length>1024*1024)throw new IOException("Oversized result record");
            var e=CommittedRewardContract.ReadObject(File.ReadAllText(path,Encoding.UTF8));
            if(e.Count!=2||!(e["payload"] is JObject p)||p.Count!=5||e["sha256"]?.Type!=JTokenType.String||p["version"]?.Type!=JTokenType.Integer||(int)p["version"]!=1)throw new IOException("Invalid result envelope");
            if(DurablePendingStore.Hash(Canonical(p))!=(string)e["sha256"]||CommittedRewardContract.Text(p,"accountId",true)!=lease.AccountId||CommittedRewardContract.Text(p,"requestId",true)!=Id(request)||CommittedRewardContract.Text(p,"scope",true)!="U1A4_OFFLINE_APP_RESULT_ONLY"||!(p["receipt"] is JObject wire))throw new IOException("Result checksum/owner mismatch");
            var receipt=CommittedRewardContract.Parse(Canonical(wire));
            if(Id(receipt.RequestId)!=Id(request))throw new IOException("Result request identity mismatch");
            Current(lease);return new StoredCommittedResult(lease.AccountId,receipt,Identity(wire));
        }
        public StoredCommittedResult Get(string request)
        {var lease=Lease();using(var guard=new FileStream(PathFor(lease,request)+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))return ReadUnlocked(lease,request);}
        public IReadOnlyList<StoredCommittedResult> List()
        {
            var lease=Lease();var folder=Folder(lease);if(Directory.GetFiles(folder,"*.tmp").Length!=0)throw new IOException("Interrupted result inbox requires review");
            var result=new List<StoredCommittedResult>();foreach(var p in Directory.GetFiles(folder,"*.result.json").OrderBy(x=>x,StringComparer.Ordinal))
            {Current(lease);var id=Path.GetFileName(p).Replace(".result.json","");var row=Get(id);if(row==null)throw new IOException("Inbox changed while reading");result.Add(row);}
            Current(lease);return result.AsReadOnly();
        }
        public bool AcceptOnce(string accountId,CommittedReceipt receipt)
        {
            if(receipt==null)throw new ArgumentNullException(nameof(receipt));var lease=Lease(accountId);var wire=Projection(receipt);var identity=Identity(wire);var path=PathFor(lease,receipt.RequestId);
            using(var guard=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            {
                var previous=ReadUnlocked(lease,receipt.RequestId);
                if(previous!=null)
                {if(previous.IdentityHash!=identity)throw new IOException("Conflicting/stale result for immutable operation");Current(lease);return true;}
                var payload=new JObject{["version"]=1,["accountId"]=lease.AccountId,["requestId"]=Id(receipt.RequestId),["receipt"]=wire,["scope"]="U1A4_OFFLINE_APP_RESULT_ONLY"};
                var bytes=Encoding.UTF8.GetBytes(Canonical(new JObject{["payload"]=payload,["sha256"]=DurablePendingStore.Hash(Canonical(payload))}));
                if(bytes.Length>1024*1024)throw new IOException("Oversized result write");
                fault?.Invoke("BeforeWrite");Current(lease);
                using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough))
                {stream.Write(bytes,0,bytes.Length/2);fault?.Invoke("AfterPartialWrite");stream.Write(bytes,bytes.Length/2,bytes.Length-bytes.Length/2);stream.Flush(true);fault?.Invoke("AfterFlush");}
                Current(lease);File.Move(path+".tmp",path); // Atomic same-directory publish, never replace an existing result.
                fault?.Invoke("AfterPublish");var saved=ReadUnlocked(lease,receipt.RequestId);
                if(saved==null||saved.IdentityHash!=identity)throw new IOException("Durable result readback failed");
                Current(lease);return true; // ACK boundary: flush + publish + readback under exclusive operation lock.
            }
        }
    }
}

#endif
