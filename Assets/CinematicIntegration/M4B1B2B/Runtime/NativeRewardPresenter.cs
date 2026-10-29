#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using ChessButWeird.CinematicIntegration.M4A;
using ChessButWeird.CinematicIntegration.M4B0;
using ChessButWeird.CinematicIntegration.M4B1A;
using ChessButWeird.CinematicIntegration.M4B1B2A;
using ChessButWeird.CinematicProof.M35;
using Object=UnityEngine.Object;

namespace ChessButWeird.CinematicIntegration.M4B1B2B
{
    public sealed class NativePresenterFactory : IPresentationFactory
    {
        public readonly List<NativeRewardPresenter> Attempts=new List<NativeRewardPresenter>();
        public GameObject RitualPrefab;
        public Transform Parent;
        public NativePresentationView View;
        public ArtworkCatalogSnapshot Catalog;
        public ArtworkTypePolicy Policy=ArtworkTypePolicy.Unresolved;
        public PresentationCapabilities Capabilities=new PresentationCapabilities();
        public Func<ISpriteLoader> Loader=()=>new NativeSpriteLoader();
        public int TargetHeight=720;
        public float IntroDurationOverride; // Zero preserves authored duration. Disposable test fixture only.
        public float DwellOverride;
        public NativeRewardPresenter Current=>Attempts.LastOrDefault();
        public bool Supports(GachaPresentationBatch batch)=>batch!=null&&batch.IsStructurallyValid&&batch.ActualCount<=10;
        public IVisualPresenter Create()
        { var presenter=new NativeRewardPresenter(this);Attempts.Add(presenter);if(Attempts.Count>32)Attempts.RemoveAt(0);return presenter; }
    }

    // The M35 clone is generic ritual ONLY. Real reward identity is never passed as MockReward.
    public sealed class NativeRewardPresenter : IVisualPresenter,IRealRewardPresenterPort
    {
        private readonly NativePresenterFactory services;
        private readonly List<Object> owned=new List<Object>();
        private readonly List<string> faults=new List<string>();
        private readonly int owner=System.Threading.Thread.CurrentThread.ManagedThreadId;
        private GameObject container;
        private Action<object,M35RoyalController.OperationOutcome> terminal;
        private bool playing,bootstrapped,disposed,reported,ritualFinished,skipPending;
        private float slotClock;
        private int visibleSlot;
        private readonly List<int> presentedSlots=new List<int>();
        public IReadOnlyList<int> PresentedSlots=>presentedSlots.AsReadOnly();
        public object AdmittedNativeTag {get;private set;}
        public RealRewardPresentationSpec Specification {get;private set;}
        public SpriteAcquisitionScope Acquisition {get;private set;}
        public ISpriteLoader Loader {get;private set;}
        public IReadOnlyList<SpriteAcquisition> Requests=>requests.AsReadOnly();
        private readonly List<SpriteAcquisition> requests=new List<SpriteAcquisition>();
        public M35RoyalController Ritual {get;private set;}
        public int AcceptedTaggedTerminals {get;private set;}
        public int RejectedTaggedTerminals {get;private set;}
        public int AutoplayOperations {get;private set;}
        public bool IsDisposed=>disposed;
        public IReadOnlyList<string> Faults=>faults.AsReadOnly();
        public bool IsRevealing=>ritualFinished&&!reported&&!disposed;
        public NativeRewardPresenter(NativePresenterFactory services){this.services=services??throw new ArgumentNullException(nameof(services));}
        private void ThreadCheck(){if(System.Threading.Thread.CurrentThread.ManagedThreadId!=owner)throw new InvalidOperationException("Unity owner thread required");}
        void IVisualPresenter.Prepare(PresentationAttemptContext context)
        {
            if(services.Catalog==null)throw new InvalidOperationException("An explicit inspected/fixture catalog is required");
            Prepare(RealRewardPresentationSpec.Create(context,context.Batch.Rewards.Select(r=>
                ArtworkAcquisitionObservation.NotRequested(ArtworkResolver.Resolve(r,services.Catalog,services.Policy))),services.Capabilities,new PresentationLifetime(context.Token)));
        }
        public void Prepare(RealRewardPresentationSpec spec)
        {
            ThreadCheck();if(disposed||Specification!=null)throw new InvalidOperationException("One presenter per attempt");
            Specification=spec??throw new ArgumentNullException(nameof(spec));spec.VerifyReadyForPlay();
            if(!services.View)throw new InvalidOperationException("Visual host missing");
            services.View.Claim(spec.Token);
            Loader=services.Loader();Acquisition=new SpriteAcquisitionScope(spec,Loader);
            spec.Attempt.Resources.Own(Acquisition,PresentationResourceKind.AddressableLoad,Acquisition.Dispose,Detach);
            for(int i=0;i<spec.Slots.Count;i++)
            {
                int index=i;
                // Unknown never starts an artwork acquisition or borrows a known rarity's representation.
                requests.Add(spec.Slots[i].Treatment==RewardTreatment.NeutralUnknown?null:Acquisition.Acquire(i,r=>
                {if(!disposed&&ritualFinished&&visibleSlot==index)Show(index);}));
            }
            if(spec.Slots.All(s=>s.Treatment==RewardTreatment.NeutralUnknown)){bootstrapped=true;ritualFinished=true;return;}
            if(!services.RitualPrefab)throw new InvalidOperationException("M35 ritual prefab missing");
            container=new GameObject("B1B2B_OWNED_Ritual");container.SetActive(false);
            container.transform.SetParent(services.Parent,false);
            var clone=Object.Instantiate(services.RitualPrefab,container.transform,false);
            Ritual=clone.GetComponent<M35RoyalController>();
            if(!Ritual)throw new InvalidOperationException("Expected hardened tagged M35 emitter");
            // Both entries equal the captured current pipeline: M35 Prepare cannot switch global policy.
            Ritual.pcPipeline=QualitySettings.renderPipeline;Ritual.reducedPipeline=QualitySettings.renderPipeline;
            int h=Mathf.Clamp(services.TargetHeight,54,2160);
            var pc=new RenderTexture(Mathf.RoundToInt(h*16f/9),h,24,RenderTextureFormat.ARGBHalf){name="B1B2B_OWNED_PC_Template",antiAliasing=1};
            var reduced=new RenderTexture(Mathf.RoundToInt(h*16f/9),h,24,RenderTextureFormat.ARGBHalf){name="B1B2B_OWNED_Reduced_Template",antiAliasing=1};
            owned.Add(pc);owned.Add(reduced);Ritual.pcTarget=pc;Ritual.reducedTarget=reduced;
            Ritual.reducedMotion=spec.Capabilities.ReducedMotion;Ritual.reducedFlash=spec.Capabilities.ReducedFlash;
            Ritual.shakeEnabled=spec.Capabilities.CameraShake;Ritual.muteAudio=true; // Hidden autoplay must remain silent.
            if(services.IntroDurationOverride>0)
                Ritual.profiles=Ritual.profiles.Select(p=>{var c=Object.Instantiate(p);c.duration=services.IntroDurationOverride;owned.Add(c);return c;}).ToArray();
            var canvas=Ritual.output.GetComponentInParent<Canvas>(true);
            // Graphic.canvas is null if Canvas is disabled before M35 Awake. Hide first, then disable after Awake.
            var conceal=canvas.gameObject.AddComponent<CanvasGroup>();conceal.alpha=0;conceal.interactable=false;conceal.blocksRaycasts=false;
            int roots=0;
            foreach(var t in Ritual.stage.GetComponentsInChildren<Transform>(true))
                if(t.name=="RewardKing"||t.name=="RewardKnight"||t.name=="RewardFallback")
                {roots++;foreach(var renderer in t.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=true;}
            if(roots!=3)throw new InvalidOperationException("Fail closed: unexpected M35 reward hierarchy");
            container.SetActive(true); // Awake now; Start's one known mock autoplay remains hidden/unsubscribed.
            canvas.enabled=false;
        }
        public void Play(){ThreadCheck();Specification.VerifyReadyForPlay();playing=true;}
        public void Tick(float delta)
        {
            ThreadCheck();if(disposed||reported||!playing)return;
            try
            {
                if(!services.View||!services.View.IsUsable){Specification.Lifetime.CloseHost();Report(PresentationOutcome.Cancelled);return;}
                Specification.VerifyReadyForPlay();Acquisition.Pump();
                if(!bootstrapped)
                {
                    if(!Ritual||Ritual.OperationCount==0)return; // Wait for the unchanged M35 Start.
                    AutoplayOperations=Ritual.OperationCount;
                    if(AutoplayOperations!=1)throw new InvalidOperationException("Unexpected autoplay admission");
                    Ritual.Cleanup();
                    Ritual.SetAspect(Mathf.Clamp((float)Screen.width/Mathf.Max(1,Screen.height),1,3));
                    Ritual.muteAudio=!Specification.Capabilities.Sound;
                    terminal=OnTaggedTerminal;Ritual.TaggedOperationEnded+=terminal;
                    var highest=Specification.Slots.Max(s=>(int)s.Treatment)-1;
                    Ritual.Begin(new[]{new MockReward("B1B2B_GENERIC_RITUAL","RITUAL ONLY — NO ITEM IDENTITY",(MockRarity)highest,MockArtwork.Missing)},Specification.Token);
                    AdmittedNativeTag=Ritual.ActiveOperationTag;
                    services.View.AttachTarget(Specification.Token,Ritual.OwnedTarget);bootstrapped=true;
                    // Display the received identity during the hero shot; no mock geometry is visible.
                    Show(0);
                }
                if(skipPending){skipPending=false;Skip();return;}
                if(!ritualFinished)
                {if(Ritual.SourceTime>=4.55f)Show(visibleSlot);return;}
                Show(visibleSlot);slotClock+=Mathf.Max(0,delta);
                float dwell=services.DwellOverride>0?services.DwellOverride:Specification.AcceleratedBatch?.36f:.85f;
                if(slotClock<dwell)return;slotClock=0;
                if(++visibleSlot>=Specification.Slots.Count){Report(PresentationOutcome.Completed);return;}
                Show(visibleSlot);
            }
            catch(Exception error){faults.Add("Playback: "+error.GetType().Name);Report(PresentationOutcome.Failed);}
        }
        private void Show(int slot)
        {
            if(!services.View||slot<0||slot>=requests.Count)return;
            if(ritualFinished&&(presentedSlots.Count==0||presentedSlots[presentedSlots.Count-1]!=slot))presentedSlots.Add(slot);
            services.View.AttachTarget(Specification.Token,Specification.Slots[slot].Treatment==RewardTreatment.NeutralUnknown?null:Ritual?Ritual.OwnedTarget:null);
            services.View.Show(Specification,slot,(ritualFinished||(Ritual&&Ritual.SourceTime>=4.55f))?requests[slot]?.Asset:null);
        }
        private void OnTaggedTerminal(object token,M35RoyalController.OperationOutcome outcome)
        {
            if(disposed||!ReferenceEquals(token,Specification.Token)||ritualFinished){RejectedTaggedTerminals++;return;}
            AcceptedTaggedTerminals++;
            if(outcome==M35RoyalController.OperationOutcome.Cancelled){Report(PresentationOutcome.Cancelled);return;}
            if(outcome==M35RoyalController.OperationOutcome.Skipped){Report(PresentationOutcome.Skipped);return;}
            ritualFinished=true;slotClock=0; // Native intro ended; ordered slot reveal still belongs to this attempt.
        }
        private void Report(PresentationOutcome outcome)
        {if(reported||disposed)return;reported=true;Specification.Attempt.Report(outcome,"B1B2B native presenter");}
        public void Skip()
        {
            ThreadCheck();if(disposed||reported)return;
            if(!bootstrapped){skipPending=true;return;}
            if(Ritual&&Ritual.IsOperationActive)Ritual.Skip();else Report(PresentationOutcome.Skipped);
        }
        private void Detach()
        {if(!ReferenceEquals(services.View,null)&&Specification!=null)services.View.Detach(Specification.Token);}
        public void CloseHost(){ThreadCheck();Specification?.Lifetime.CloseHost();Detach();}
        public void Cancel(){ThreadCheck();Specification?.Lifetime.Cancel();Dispose();}
        public void Dispose()
        {
            ThreadCheck();if(disposed)return;disposed=true;playing=false;
            void Guard(Action action,string label){try{action();}catch(Exception e){faults.Add(label+": "+e.GetType().Name);}}
            Specification?.Lifetime.Cancel();Guard(Detach,"Consumer detachment"); // Before any resource release; child-loss null guards live in the view.
            if(Ritual&&terminal!=null)Ritual.TaggedOperationEnded-=terminal;terminal=null;
            Guard(()=>Acquisition?.Dispose(),"Sprite scope");
            Guard(()=>{if(Ritual)Ritual.Cleanup();},"M35 cleanup");
            Guard(()=>{if(container)Object.Destroy(container);},"Owned clone");
            foreach(var item in owned)Guard(()=>{if(item)Object.Destroy(item);},"Owned template/profile");
            owned.Clear();
        }
    }
}

#endif
