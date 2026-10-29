#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.IO;
using System.Linq;
using ChessButWeird.U1A3;
using ChessButWeird.U1A4;

namespace ChessButWeird.U1A3
{
    // Accepted receiver port only. The demo never instantiates a request coordinator or transport.
    public interface ICommittedReceiptConsumer { bool AcceptOnce(string accountId,CommittedReceipt receipt); }
}
namespace ChessButWeird.CinematicDemo.FinalSprint
{
    // A deliberately unauthenticated, development-only fixture identity, isolated from real accounts.
    internal sealed class DemoOnlyAccount : ITrustedAccountProvider
    {
        internal const string Id="FINAL_DEMO_ONLY_NOT_A_REAL_AUTHENTICATED_ACCOUNT";
        private bool alive=true;
        public TrustedAccountLease Capture()=>alive?new TrustedAccountLease(Id,1):null;
        public bool IsCurrent(TrustedAccountLease lease)=>alive&&lease!=null&&lease.AccountId==Id&&lease.Epoch==1;
        internal void Close(){alive=false;}
    }
    public sealed class DemoReceiptInbox : IDisposable
    {
        private readonly DemoOnlyAccount account=new DemoOnlyAccount();
        private readonly DurableCommittedResultStore store;
        public int NewRecords {get;private set;}
        public int Count=>store.List().Count;
        public DemoReceiptInbox(string root){store=new DurableCommittedResultStore(root,account);}
        public CommittedReceipt ReceiveFixture(string immutableJson)
        {
            var receipt=CommittedRewardContract.Parse(immutableJson);bool existing=store.Get(receipt.RequestId)!=null;
            if(!store.AcceptOnce(DemoOnlyAccount.Id,receipt))throw new IOException("Fixture result was not durably acknowledged");
            if(!existing)NewRecords++;
            return store.Get(receipt.RequestId).Receipt;
        }
        public CommittedReceipt Reopen(string requestId)=>store.Get(requestId)?.Receipt??throw new IOException("No committed demo result");
        public string[] StoredRequests()=>store.List().Select(r=>r.Receipt.RequestId).ToArray();
        public string DiagnosticPath(string requestId)=>store.DiagnosticPath(requestId);
        public void Dispose()=>account.Close(); // Never deletes committed results.
    }
}
#endif
