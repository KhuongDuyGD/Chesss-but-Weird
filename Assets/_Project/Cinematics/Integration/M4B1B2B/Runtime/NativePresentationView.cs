#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ChessButWeird.CinematicIntegration.M4A;
using ChessButWeird.CinematicIntegration.M4B0;
using ChessButWeird.CinematicIntegration.M4B1A;

namespace ChessButWeird.CinematicIntegration.M4B1B2B
{
    // Borrowed consumers. This component never releases textures, Sprites or Addressables handles.
    public sealed class NativePresentationView : MonoBehaviour
    {
        public RawImage ritual;
        public Image artwork;
        public Text identity, phase, results;
        public AttemptToken ConsumerToken { get; private set; }
        public int Slot { get; private set; } = -1;
        public bool IsUsable=>ritual&&artwork&&identity&&phase&&results;
        private Sprite shown;
        private int cachedSlot=-1;
        private GachaPresentationBatch displayedBatch;
        private string displayedText;
        public void Claim(AttemptToken token)
        {
            ConsumerToken=token??throw new ArgumentNullException(nameof(token));
            ritual.texture=null;artwork.sprite=null;artwork.enabled=false;Slot=-1;
            shown=null;cachedSlot=-1;results.transform.parent.gameObject.SetActive(false);
            results.text="";identity.text="Preparing received fixture batch…";
            phase.text="OFFLINE FIXTURE · RITUAL BACKGROUND ONLY";
        }
        public bool Owns(AttemptToken token)=>ReferenceEquals(ConsumerToken,token);
        public void AttachTarget(AttemptToken token,RenderTexture target)
        { if(Owns(token))ritual.texture=target; }
        public void Show(RealRewardPresentationSpec spec,int slot,Sprite sprite)
        {
            if(!Owns(spec.Token)||spec.Lifetime.IsCancellationRequested||!spec.Lifetime.IsHostOpen)return;
            var reward=spec.Slots[slot].Reward;Slot=slot;
            // No default art, model inference, substring category or Unknown -> Common promotion.
            bool known=reward.CanonicalRarity!=PresentationRarity.Unknown;
            var next=known?sprite:null;
            if(identity.text.Length>0&&cachedSlot==slot&&shown==next)return;
            cachedSlot=slot;shown=next;
            identity.color=known?new Color(.96f,.9f,.76f):new Color(.80f,.82f,.86f);
            artwork.sprite=known?sprite:null;artwork.enabled=artwork.sprite;
            artwork.preserveAspect=true;
            identity.text=$"{slot+1} / {spec.Batch.ActualCount}  ·  {reward.CanonicalRarity}\n{reward.DisplayLabel}\n"+
                (reward.Source.IsDuplicate?"DUPLICATE  ·  ":"")+
                (reward.Source.DuplicateBonus==null?"":$"{reward.Source.DuplicateBonus.Currency} {reward.Source.DuplicateBonus.Amount}\n")+
                (artwork.sprite?"Verified fixture Sprite":"TEXT ONLY · artwork unavailable or unsupported");
            phase.text=known?"OFFLINE FIXTURE · REWARD IDENTITY FROM RECEIVED SLOT":"OFFLINE FIXTURE · UNKNOWN RARITY · NEUTRAL TEXT";
        }
        public void Detach(AttemptToken token)
        {
            if(!Owns(token))return;
            ConsumerToken=null; // Clear logical ownership even when a Unity child was already destroyed.
            try{if(ritual)ritual.texture=null;}
            finally{if(artwork){artwork.sprite=null;artwork.enabled=false;}}
        }
        public void ShowResults(GachaPresentationBatch batch)
        {
            if(!results||!identity)throw new InvalidOperationException("Result UI consumer missing; display cannot be acknowledged");
            displayedBatch=batch;
            displayedText="RECEIVED FIXTURE RESULTS — original order\n"+string.Join("\n",batch.Rewards.Select(r=>
                $"{r.OriginalSlotIndex+1:00} · {r.CanonicalRarity} · {r.DisplayLabel}"+
                (r.Source.IsDuplicate?" · DUPLICATE":"")+
                (r.Source.DuplicateBonus==null?"":$" · {r.Source.DuplicateBonus.Currency} {r.Source.DuplicateBonus.Amount}")));
            RestoreAlreadyDisplayed(batch);
        }
        public void RestoreAlreadyDisplayed(GachaPresentationBatch batch)
        {
            // Repaint only an already displayed identical immutable batch. No external delivery/ack callback.
            if(!ReferenceEquals(displayedBatch,batch)||!results||!identity)return;
            results.text=displayedText;results.transform.parent.gameObject.SetActive(true);
            identity.text="Presentation finished. Replay uses the same received batch.";
        }
        public void HideResults(){if(results)results.transform.parent.gameObject.SetActive(false);}
    }
}

#endif
