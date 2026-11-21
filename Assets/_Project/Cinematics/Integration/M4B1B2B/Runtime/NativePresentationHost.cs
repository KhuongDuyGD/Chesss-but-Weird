#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using UnityEngine;
using ChessButWeird.CinematicIntegration.M4A;
using ChessButWeird.CinematicIntegration.M4B0;

namespace ChessButWeird.CinematicIntegration.M4B1B2B
{
    public enum DisplayAcknowledgement { NotAttempted, Accepted, Indeterminate }
    public sealed class ReceivedResultsBoundary : IResultsBoundary
    {
        private readonly Action<GachaPresentationBatch> display;
        public DisplayAcknowledgement Acknowledgement {get;private set;}
        public GachaPresentationBatch PreservedBatch {get;private set;}
        public int Calls {get;private set;}
        public ReceivedResultsBoundary(Action<GachaPresentationBatch> display){this.display=display??throw new ArgumentNullException(nameof(display));}
        public void Show(ResultDelivery value)
        {
            if(Calls!=0)throw new InvalidOperationException("Explicit future idempotent acknowledgement required before retry");
            Calls++;PreservedBatch=value.Batch;Acknowledgement=DisplayAcknowledgement.Indeterminate;
            display(value.Batch);Acknowledgement=DisplayAcknowledgement.Accepted;
        }
    }
    public sealed class NativePresentationHost : MonoBehaviour
    {
        public SafePresentationCoordinator Coordinator {get;private set;}
        public NativePresenterFactory Factory {get;private set;}
        public ReceivedPresentationSession Session=>Coordinator?.Session;
        public void Initialize(ReceivedPresentationSession session,NativePresenterFactory factory,IResultsBoundary results)
        {
            if(Coordinator!=null)throw new InvalidOperationException("Explicit new host required for another session");
            Factory=factory??throw new ArgumentNullException(nameof(factory));Factory.Parent=transform;
            Coordinator=new SafePresentationCoordinator(session,results);
            Coordinator.AttemptEnded+=OnSettled;
            if(!isActiveAndEnabled)Coordinator.CloseHost();
        }
        public void Play(){if(isActiveAndEnabled)Coordinator?.RequestBegin(new PresentationPlan(Factory,new TextOnlyFactory(),true));}
        public void Replay(){if(isActiveAndEnabled)Coordinator?.RequestReplay();}
        public void Skip(){if(isActiveAndEnabled)Factory?.Current?.Skip();}
        public void Cancel()=>Coordinator?.RequestCancel();
        public void Reopen(){if(isActiveAndEnabled)Coordinator?.RequestOpenHost();}
        public void Pump(float delta)
        {Coordinator?.Pump();Factory?.Current?.Tick(delta);}
        private void Update()=>Pump(Time.unscaledDeltaTime);
        private void OnEnable()
        {
            // Reopen admission only. No implicit replay, result popup or new batch on enable.
            if(Coordinator!=null&&!Coordinator.IsDisposed)Coordinator.RequestOpenHost();
        }
        public void CloseVisuals()
        {
            Coordinator?.CloseHost(); // Immediate suppression precedes all native callbacks/disposal.
            if(Factory?.View)Factory.View.HideResults();
            Factory?.Current?.CloseHost();
            Coordinator?.Pump();
        }
        private void OnDisable()=>CloseVisuals();
        private void OnSettled(AttemptSettlement settlement)
        {
            if(!Factory.View||!Factory.View.IsUsable){CloseVisuals();return;}
            if(!isActiveAndEnabled||!Coordinator.HostOpen||!Session.ResultsAccepted)return;
            if(settlement.Outcome!=PresentationOutcome.Completed&&settlement.Outcome!=PresentationOutcome.Skipped)return;
            if(Factory.View)Factory.View.RestoreAlreadyDisplayed(Session.Batch);
        }
        private void OnDestroy(){try{CloseVisuals();}finally{if(Coordinator!=null){Coordinator.AttemptEnded-=OnSettled;Coordinator.Dispose();}}}
        private sealed class TextOnlyFactory : IPresentationFactory
        {
            public bool Supports(GachaPresentationBatch batch)=>batch.IsStructurallyValid;
            public IVisualPresenter Create()=>new TextOnlyPresenter();
        }
        private sealed class TextOnlyPresenter : IVisualPresenter
        {
            private PresentationAttemptContext context;
            public void Prepare(PresentationAttemptContext value){context=value;}
            public void Play()=>context.Report(PresentationOutcome.Completed,"Recoverable visual failure; unchanged received batch to results boundary");
            public void Cancel(){}public void Dispose(){}
        }
    }
}

#endif
