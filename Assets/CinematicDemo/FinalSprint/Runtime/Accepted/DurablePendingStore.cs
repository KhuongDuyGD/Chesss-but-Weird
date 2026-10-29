#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChessButWeird.U1A3
{
    public enum PendingPhase { Prepared, Uncertain, ReceiptObserved, Delivered }
    public sealed class PendingRecord
    {
        public string AccountId { get; }
        public string RequestId { get; }
        public string BannerCode { get; }
        public int Count { get; }
        public PendingPhase Phase { get; }
        public PendingRecord(string accountId, string requestId, string bannerCode, int count, PendingPhase phase)
        {
            if (string.IsNullOrWhiteSpace(accountId) || accountId.Length > 256 || !Guid.TryParseExact(requestId,"D",out var id) || id==Guid.Empty || requestId!=id.ToString("D") || string.IsNullOrWhiteSpace(bannerCode) || bannerCode.Length>256 || bannerCode!=bannerCode.Trim().ToUpperInvariant() || (count!=1 && count!=10) || !Enum.IsDefined(typeof(PendingPhase),phase)) throw new ArgumentException("Invalid recovery identifiers/state");
            AccountId=accountId;RequestId=requestId;BannerCode=bannerCode;Count=count;Phase=phase;
        }
        public PendingRecord WithPhase(PendingPhase phase) => new PendingRecord(AccountId,RequestId,BannerCode,Count,phase);
        internal JObject Payload() => new JObject { ["version"]=1,["accountId"]=AccountId,["requestId"]=RequestId,["bannerCode"]=BannerCode,["count"]=Count,["phase"]=Phase.ToString() };
    }
    public sealed class PendingSnapshot
    {
        public PendingRecord Record { get; }
        public string Token { get; }
        internal PendingSnapshot(PendingRecord record,string token) { Record=record;Token=token; }
    }
    // A single per-account file. Explicit root; no PlayerPrefs, token, wallet or receipt persistence.
    public sealed class DurablePendingStore
    {
        readonly string root;
        readonly Action<string> fault;
        public DurablePendingStore(string root,Action<string> fault=null)
        { if(string.IsNullOrWhiteSpace(root))throw new ArgumentException(nameof(root));this.root=Path.GetFullPath(root);this.fault=fault;Directory.CreateDirectory(this.root); }
        public static string Hash(string text) { using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant(); }
        public string PathFor(string account)
        { if(string.IsNullOrWhiteSpace(account)||account.Length>256)throw new ArgumentException(nameof(account));return Path.Combine(root,Hash(account)+".pending.json"); }
        FileStream Exclusive(string account) => new FileStream(PathFor(account)+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        PendingSnapshot ReadUnlocked(string account)
        {
            var path=PathFor(account);
            // Never turn an interrupted/corrupt durable write into permission for a fresh POST.
            if(File.Exists(path+".tmp"))throw new IOException("Interrupted pending write: preserve for review");
            if(!File.Exists(path))return null;
            if(new FileInfo(path).Length>16384)throw new IOException("Oversized pending record");
            var envelope=CommittedRewardContract.ReadObject(File.ReadAllText(path,Encoding.UTF8));
            if(envelope.Count!=2 || !(envelope["payload"] is JObject payload) || payload.Count!=6 || envelope["sha256"]?.Type!=JTokenType.String)throw new IOException("Invalid pending envelope");
            var canonical=payload.ToString(Formatting.None);var hash=Hash(canonical);
            if(hash!=(string)envelope["sha256"] || payload["version"]?.Type!=JTokenType.Integer || (int)payload["version"]!=1 || payload["count"]?.Type!=JTokenType.Integer)throw new IOException("Pending checksum/schema mismatch");
            var phaseText=CommittedRewardContract.Text(payload,"phase",true);
            if(!Enum.TryParse(phaseText,false,out PendingPhase phase)||phase.ToString()!=phaseText)throw new IOException("Unknown pending state");
            var r=new PendingRecord(CommittedRewardContract.Text(payload,"accountId",true),CommittedRewardContract.Text(payload,"requestId",true),CommittedRewardContract.Text(payload,"bannerCode",true),(int)payload["count"],phase);
            if(r.AccountId!=account)throw new IOException("Wrong account pending record");
            return new PendingSnapshot(r,hash);
        }
        public PendingSnapshot Read(string account) { using(Exclusive(account))return ReadUnlocked(account); }
        public PendingSnapshot CompareExchange(string account,PendingSnapshot expected,PendingRecord next)
        {
            if(next.AccountId!=account)throw new ArgumentException("Wrong account write");
            using(Exclusive(account))
            {
                var actual=ReadUnlocked(account);
                if(actual?.Token!=expected?.Token)throw new IOException("Concurrent/stale pending writer");
                var path=PathFor(account);var temp=path+".tmp";var payload=next.Payload();var token=Hash(payload.ToString(Formatting.None));
                var bytes=Encoding.UTF8.GetBytes(new JObject{["payload"]=payload,["sha256"]=token}.ToString(Formatting.None));
                fault?.Invoke("BeforeWrite");
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough))
                {
                    stream.Write(bytes,0,bytes.Length/2);fault?.Invoke("AfterPartialWrite");stream.Write(bytes,bytes.Length/2,bytes.Length-bytes.Length/2);stream.Flush(true);fault?.Invoke("AfterFlush");
                }
                // Same-directory publish. No delete-then-move gap and no fallback to a non-atomic rewrite.
                if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
                fault?.Invoke("AfterPublish");return new PendingSnapshot(next,token);
            }
        }
    }
}

#endif
