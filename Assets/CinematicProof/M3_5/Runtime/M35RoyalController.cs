#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ChessButWeird.CinematicProof.M35
{
    [DisallowMultipleComponent]
    public sealed class M35RoyalController:MonoBehaviour
    {
        public enum FlowState { Ready, Cinematic, BatchReveal, Results, Cleaned }
        public enum OperationOutcome { Completed, Skipped, Cancelled }
        public event Action<OperationOutcome> OperationEnded;
        public int OperationCount {get;private set;}
        public int OperationEndCount {get;private set;}
        private bool operationActive;
        public event Action<object,OperationOutcome> TaggedOperationEnded;
        public object ActiveOperationTag=>operationTag;
        public bool IsOperationActive=>operationActive;
        public int ObserverFaultCount {get;private set;}
        private object operationTag;
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<object,object> usedTags=new System.Runtime.CompilerServices.ConditionalWeakTable<object,object>();
        private static readonly object usedTagMarker=new object();
        private int emitterThread;
        private long lifecycleEpoch;
        private bool publishingTerminal,beginning,cleaning,destroying,initialized;
        private void RequireEmitterThread()
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=emitterThread)
                throw new InvalidOperationException("Marshal to the Unity owner thread before controller access.");
        }
        private void RequireCommand()
        {
            RequireEmitterThread();
            if(publishingTerminal || beginning || cleaning)throw new InvalidOperationException("Queue presentation commands until lifecycle publication/setup unwinds.");
            if(!this || destroying || !initialized || !isActiveAndEnabled)throw new InvalidOperationException("Visual host is closed or initialization failed.");
        }
        private void CheckBeginEpoch(long epoch)
        {
            if(epoch!=lifecycleEpoch || !this || destroying || !isActiveAndEnabled)
                throw new InvalidOperationException("Host closed during replacement or setup.");
        }
        private void CleanupAfterFault()
        {
            try{Cleanup();}catch(Exception cleanupError){Debug.LogException(cleanupError);}
        }
        public bool CancelOperation(object originatingTag)
        {
            RequireEmitterThread();
            if(!operationActive || !ReferenceEquals(operationTag,originatingTag))return false;
            Cleanup();return true;
        }
        public PlayableDirector director;
        public Transform stage,importedCamera,rewardRoot,epicSymbols,legendaryHalo,architecture;
        public Camera cinematicCamera;
        public RawImage output;
        public RectTransform viewport;
        public AspectRatioFitter imageAspect;
        public CanvasScaler canvasScaler;
        public RenderTexture pcTarget,reducedTarget;
        public ParticleSystem motes,impact;
        public Light keyLight,accentLight;
        public AudioSource audioSource;
        public AudioClip awakeningCue,impactCue,resultCue;
        public AudioClip chargeCue,sealCue,settleCue;
        public M35NativeVFX nativeVFX;
        public RenderPipelineAsset pcPipeline,reducedPipeline;
        public M35RarityProfile[] profiles;
        public Text title,subtitle,phaseLabel,rewardLabel,rarityLabel,accessibilityLabel;
        public CanvasGroup rewardCard;
        public GameObject resultsPanel;
        public Text[] resultLabels;
        public Image[] resultBorders;
        public bool reducedMotion,reducedFlash,shakeEnabled=true,reducedQuality,muteAudio,acceleratedBatch;
        public MockRarity selectedRarity=MockRarity.Legendary;
        public FlowState State {get;private set;}=FlowState.Ready;
        public RenderTexture OwnedTarget {get;private set;}
        public int AllocationCount {get;private set;}
        public int ReleaseCount {get;private set;}
        public int CompletionCount {get;private set;}
        public int IntroStartCount {get;private set;}
        public int CueCount {get;private set;}
        public float SourceTime {get;private set;}
        public float Elapsed {get;private set;}
        public float CurrentDuration=>profile?profile.duration:7;
        public int BatchIndex {get;private set;}
        public int LiveParticles=>(motes?motes.particleCount:0)+(impact?impact.particleCount:0);
        public bool IsBatch=>rewards.Length>1;
        public IReadOnlyList<MockReward> Rewards=>readOnlyRewards;
        public int TextWriteCount {get;private set;}
        public string[] DisplayedOrder=>displayedOrder.ToArray();
        public event Action Completed;
        private MockReward[] rewards=Array.Empty<MockReward>();
        private IReadOnlyList<MockReward> readOnlyRewards=Array.AsReadOnly(Array.Empty<MockReward>());
        private MockReward introReward;
        private MockArtwork currentArtwork;
        private readonly M35RarityProfile[] indexedProfiles=new M35RarityProfile[4];
        private Material ownedSurface;
        private int lastPhase=-1;
        private RenderPipelineAsset previousPipeline,selectedPipeline;
        private bool ownsPipeline;
        private static readonly int Accent=Shader.PropertyToID("_Accent"),TimeId=Shader.PropertyToID("_SourceTime"),Flash=Shader.PropertyToID("_FlashStrength"),Build=Shader.PropertyToID("_LightBuild");
        private readonly List<string> displayedOrder=new List<string>();
        private M35RarityProfile profile;
        private Renderer[] stageRenderers;
        private Renderer[][] artwork;
        private bool running,completionSent;
        private float batchClock,aspect=16f/9f;
        private Vector3 firstCameraPosition,lastCameraOffset;
        private Quaternion firstCameraRotation;
        private Canvas ownedCanvas;
        private static readonly string[] Phases={"I  ·  AWAKENING","II  ·  GATHERING POWER","III  ·  BREAKING THE SEAL","IV  ·  ROYAL IMPACT","V  ·  REVELATION","VI  ·  THE NEXT PAGE"};
        public Vector3 CameraShakeOffset=>lastCameraOffset;

        private void Awake()
        {
            emitterThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
            try
            {
            ownedCanvas=output.canvas;
            // Overlay canvases render reliably at the hierarchy root. This prefab owns this detached UI.
            ownedCanvas.transform.SetParent(null,false);
            stageRenderers=stage.GetComponentsInChildren<Renderer>(true);
            foreach(var r in stageRenderers.Where(r=>r.name.StartsWith("Axis_")))r.enabled=false;
            ownedSurface=new Material(stageRenderers[0].sharedMaterial){name="M35_OwnedSketchSurface",hideFlags=HideFlags.DontSave};
            foreach(var r in stageRenderers){r.sharedMaterial=ownedSurface;r.SetPropertyBlock(null);}
            foreach(var p in profiles)indexedProfiles[(int)p.rarity]=p;
            WarmGlyphs();
            artwork=new[]{FindRenderers("RewardKing"),FindRenderers("RewardKnight"),FindRenderers("RewardFallback")};
                initialized=true;
            }
            catch
            {
                CleanupAfterFault();enabled=false;DestroyOwnedHostObjects();throw;
            }
        }
        private Renderer[] FindRenderers(string name)=>stage.GetComponentsInChildren<Transform>(true)
            .Single(t=>t.name==name).GetComponentsInChildren<Renderer>(true);
        public void WarmGlyphs()
        {
            RequireEmitterThread();
            // Legacy dynamic fonts can rebuild their atlas on a later reveal. Seed only runtime glyphs.
            const string glyphs="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ·/-.,&";
            var font=title.font;
            for(int size=16;size<=34;size++)font.RequestCharactersInTexture(glyphs,size,FontStyle.Normal);
            font.RequestCharactersInTexture(glyphs,48,FontStyle.Normal);
            foreach(var text in canvasScaler.GetComponentsInChildren<Text>(true))text.SetAllDirty();
            Canvas.ForceUpdateCanvases();
        }
        private void Start()=>PlaySingle(selectedRarity);
        private void Update()
        {
            if(canvasScaler)canvasScaler.matchWidthOrHeight=(float)Screen.width/Mathf.Max(1,Screen.height)>=16f/9f?1:0;
            if(!running)return;
            if(State==FlowState.Cinematic)
            {
                float previous=SourceTime;
                Elapsed=Mathf.Min(CurrentDuration,Elapsed+Time.unscaledDeltaTime);
                Evaluate(Elapsed/CurrentDuration*7);
                if(previous<.05f && SourceTime>=.05f) Cue(awakeningCue,.22f);
                if(previous<1.1f && SourceTime>=1.1f && profile.rarity>=MockRarity.Rare)Cue(chargeCue,.18f);
                if(previous<3.03f && SourceTime>=3.03f && profile.rarity>=MockRarity.Epic)Cue(sealCue,.22f);
                // A deliberate 270 ms breath removes the charge/seal tail before the impact.
                // Only the owned local source is touched; game volume and mixer settings stay external.
                if(previous<3.28f && SourceTime>=3.28f)audioSource.Stop();
                if(previous<3.55f && SourceTime>=3.55f) {impact.Emit(reducedFlash?Mathf.Min(6,profile.burstCount):profile.burstCount);Cue(impactCue,.12f+.055f*(int)profile.rarity);}
                if(previous<4.6f && SourceTime>=4.6f) Cue(profile.revealCue,.35f);
                if(previous<6.25f && SourceTime>=6.25f)Cue(settleCue,.15f);
                if(Elapsed>=CurrentDuration)
                {
                    if(IsBatch) {State=FlowState.BatchReveal;BatchIndex=0;batchClock=0;ShowBatchReward(0);}
                    else Finish();
                }
            }
            else if(State==FlowState.BatchReveal)
            {
                batchClock+=Time.unscaledDeltaTime;
                rewardCard.alpha=Mathf.Clamp01(batchClock/.12f);
                float dwell=acceleratedBatch?.36f:.62f;
                while(batchClock>=dwell && State==FlowState.BatchReveal)
                {
                    batchClock-=dwell;BatchIndex++;
                    if(BatchIndex>=rewards.Length)Finish();else ShowBatchReward(BatchIndex);
                }
            }
        }
        private void LateUpdate()
        {
            if(State!=FlowState.Cleaned && OwnedTarget) ApplyCamera();
        }
        public void PlayCommon()=>PlaySingle(MockRarity.Common);
        public void PlayRare()=>PlaySingle(MockRarity.Rare);
        public void PlayEpic()=>PlaySingle(MockRarity.Epic);
        public void PlayLegendary()=>PlaySingle(MockRarity.Legendary);
        public void PlaySingle(MockRarity rarity){RequireCommand();acceleratedBatch=false;selectedRarity=rarity;Begin(new[]{M35MockFixtures.Single(rarity)});}
        public void PlayTen(){RequireCommand();acceleratedBatch=false;Begin(M35MockFixtures.Ten());}
        public void PlayTenFast(){RequireCommand();acceleratedBatch=true;Begin(M35MockFixtures.Ten());}
        public void PlayMissing()=>Begin(new[]{new MockReward("mock-missing","An Unfinished Royal Sketch",selectedRarity,MockArtwork.Missing)});
        public void PlayLongName()=>Begin(new[]{M35MockFixtures.Ten()[6]});
        public void Replay(){RequireCommand();if(rewards.Length==0)PlaySingle(selectedRarity);else Begin(rewards);}
        public void Begin(IEnumerable<MockReward> input)=>Begin(input,new object());
        public void Begin(IEnumerable<MockReward> input,object localOperationTag)
        {
            RequireCommand();
            if(input==null)throw new ArgumentNullException(nameof(input));
            if(localOperationTag==null)throw new ArgumentNullException(nameof(localOperationTag));
            beginning=true;
            try
            {
                long beginEpoch=lifecycleEpoch;
                var copy=input.Select(r=>r==null?throw new ArgumentException("Null mock reward."):r.Copy()).ToArray();
                if(copy.Length==0 || copy.Length>10)throw new ArgumentException("M35 supports 1–10 local mock rewards.");
                foreach(var r in copy)if(!Enum.IsDefined(typeof(MockRarity),r.rarity))throw new ArgumentException("Unknown mock rarity.");
                if(usedTags.TryGetValue(localOperationTag,out _))throw new InvalidOperationException("Local operation identity cannot be reused.");
                CheckBeginEpoch(beginEpoch);
                EndOperation(operationTag,OperationOutcome.Cancelled);
                CheckBeginEpoch(beginEpoch);
                usedTags.Add(localOperationTag,usedTagMarker);
                operationTag=localOperationTag;operationActive=true;OperationCount++;completionSent=false;
                try
                {
            running=false;audioSource.Stop();StopParticles();director.Stop();rewards=copy;readOnlyRewards=Array.AsReadOnly(copy);
            displayedOrder.Clear();Elapsed=0;SourceTime=0;BatchIndex=0;batchClock=0;
            introReward=copy[0];foreach(var reward in copy)if(reward.rarity>introReward.rarity)introReward=reward;
            profile=indexedProfiles[(int)introReward.rarity];lastPhase=-1;
            Prepare();CheckBeginEpoch(beginEpoch);resultsPanel.SetActive(false);output.color=Color.white;
            director.Play();CheckBeginEpoch(beginEpoch);IntroStartCount++;Evaluate(0);CheckBeginEpoch(beginEpoch);
            firstCameraPosition=importedCamera.position;firstCameraRotation=importedCamera.rotation;
            ApplyTier(profile);ShowArtwork(introReward);SetRewardText(introReward,IsBatch?"TEN ROYAL SKETCHES AWAIT":"");
            title.text="THE ROYAL ASCENSION";subtitle.text=IsBatch?"One awakening · ten sketches":"A kingdom, drawn into being";
            State=FlowState.Cinematic;running=true;motes.Play();ApplyCamera();UpdateAccessibility();
                    CheckBeginEpoch(beginEpoch);
                }
                catch{CleanupAfterFault();throw;}
            }
            finally{beginning=false;}
        }
        private void Prepare()
        {
            if(!OwnedTarget)
            {
                var template=reducedQuality?reducedTarget:pcTarget;
                OwnedTarget=new RenderTexture(template){name="M35_Runtime_HDR",hideFlags=HideFlags.DontSave};
                OwnedTarget.width=Mathf.RoundToInt(template.height*aspect);OwnedTarget.Create();AllocationCount++;
            }
            var desired=reducedQuality?reducedPipeline:pcPipeline;
            if(desired && QualitySettings.renderPipeline!=desired){if(!ownsPipeline)previousPipeline=QualitySettings.renderPipeline;selectedPipeline=desired;QualitySettings.renderPipeline=desired;ownsPipeline=true;}
            cinematicCamera.enabled=true;cinematicCamera.targetTexture=OwnedTarget;
            output.gameObject.SetActive(true);output.texture=OwnedTarget;imageAspect.aspectRatio=aspect;
            cinematicCamera.aspect=aspect;
            var data=cinematicCamera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;
            data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;data.volumeLayerMask=1<<2;
            if(!director.playableGraph.IsValid())director.RebuildGraph();
        }
        private void Evaluate(float time)
        {
            SourceTime=Mathf.Clamp(time,0,6.99999f);director.time=SourceTime;director.Evaluate();
            importedCamera.GetComponent<Camera>().enabled=false;
            rewardRoot.localScale*=profile.heroScale;
            ApplyTreatment();ApplyCamera();UpdateCinematicText();
        }
        public void PauseForCapture(float time)
        {
            RequireCommand();
            running=false;director.Pause();Evaluate(time);
            // Deterministic native VFX preview, still rendered in Unity.
            StopParticles();if(time>=1 && time<4.6){motes.Emit(reducedQuality?10:18);motes.Simulate(.25f,true,false,false);}
            if(time>=3.5f && time<4.6){impact.Emit(reducedFlash?4:profile.burstCount);impact.Simulate(.18f,true,false,false);}
        }
        private void ApplyTier(M35RarityProfile tier)
        {
            profile=tier;
            epicSymbols.gameObject.SetActive(tier.orbitSymbols&&!reducedMotion);legendaryHalo.gameObject.SetActive(tier.coronationHalo);
            architecture.gameObject.SetActive(tier.rarity>=MockRarity.Epic);
            ownedSurface.SetColor(Accent,tier.accent);ownedSurface.SetFloat(Flash,reducedFlash?.18f:1);
            var main=motes.main;main.startColor=tier.accent;main.maxParticles=reducedQuality?24:64;
            var impactMain=impact.main;impactMain.startColor=tier.accent;impactMain.maxParticles=reducedQuality?24:64;
            accentLight.color=tier.accent;
        }
        private void ApplyTreatment()
        {
            float build=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1,3.4f,SourceTime));
            float impactPulse=Mathf.Clamp01(1-Mathf.Abs(SourceTime-3.65f)/.25f);
            keyLight.intensity=1.6f+build*.4f+impactPulse*(reducedFlash?.05f:.25f);
            accentLight.intensity=build*(reducedFlash?.7f:profile.lightPeak)+impactPulse*(reducedFlash?0:.6f);
            var emission=motes.emission;
            emission.rateOverTime=(SourceTime>1 && SourceTime<4.6?profile.motesPerSecond:0)*(reducedQuality?.45f:1)*(reducedMotion?.45f:1);
            // A single owned material + baked UV delay preserves SRP Batcher compatibility.
            ownedSurface.SetFloat(TimeId,SourceTime);ownedSurface.SetFloat(Build,build);
            nativeVFX.Apply(SourceTime,profile.accent,(int)profile.rarity,reducedMotion,reducedFlash,reducedQuality);
        }
        private void ApplyCamera()
        {
            if(!profile)return;
            float motion=reducedMotion?.10f:profile.cameraMotion;
            if(IsBatch && (State==FlowState.BatchReveal || State==FlowState.Results))motion=reducedMotion?.1f:1;
            Vector3 position=Vector3.Lerp(firstCameraPosition,importedCamera.position,motion);
            Quaternion rotation=Quaternion.Slerp(firstCameraRotation,importedCamera.rotation,motion);
            // The inherited knight is a thin extruded side silhouette. A three-quarter camera
            // preserves its +Y artwork convention while keeping the head readable in x10 too.
            if(currentArtwork==MockArtwork.KnightSketch)
            {
                var orbit=Quaternion.AngleAxis(-40,Vector3.up);
                position=orbit*position;rotation=orbit*rotation;
            }
            float peak=Mathf.Clamp01(1-Mathf.Abs(SourceTime-3.65f)/.32f);
            float shake=shakeEnabled&&!reducedMotion?(int)profile.rarity/3f*peak:0;
            lastCameraOffset=new Vector3(Mathf.Sin(SourceTime*31),Mathf.Sin(SourceTime*37),0)*(.022f*shake);
            cinematicCamera.transform.position=position+rotation*lastCameraOffset;
            // Composition is authored in the FBX take. Do not counter-rotate it to fit the full book
            // during the hero close-up. The opening remains wide; the later frame belongs to the reward.
            cinematicCamera.transform.rotation=rotation*Quaternion.Euler(Mathf.Sin(SourceTime*33)*shake*.22f,0,0);
            float reference=reducedMotion?48:46-4*Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.55f,4.9f,SourceTime));
            float widen=Mathf.Max(1,(16f/9f)/aspect);
            cinematicCamera.fieldOfView=2*Mathf.Atan(Mathf.Tan(reference*Mathf.Deg2Rad*.5f)*widen)*Mathf.Rad2Deg;
        }
        private void UpdateCinematicText()
        {
            int index=SourceTime<1?0:SourceTime<2.3f?1:SourceTime<3.5f?2:SourceTime<4.6f?3:SourceTime<6.2f?4:5;
            if(lastPhase!=index){lastPhase=index;phaseLabel.text=Phases[index];TextWriteCount++;}
            rewardCard.alpha=Mathf.Clamp01((SourceTime-4.55f)/.35f);
        }
        private void ShowArtwork(MockReward reward)
        {
            currentArtwork=reward.artwork;
            for(int i=0;i<artwork.Length;i++)foreach(var renderer in artwork[i])renderer.enabled=i==(int)reward.artwork;
        }
        private void SetRewardText(MockReward reward,string overrideName="")
        {
            rewardLabel.text=string.IsNullOrEmpty(overrideName)?reward.name:overrideName;
            rarityLabel.text=reward.rarity.ToString().ToUpperInvariant()+
                (reward.duplicate?"  ·  DUPLICATE MOCK":"")+(reward.artwork==MockArtwork.Missing?"  ·  SKETCH PENDING":"");
            rarityLabel.color=indexedProfiles[(int)reward.rarity].accent;TextWriteCount++;
        }
        private void ShowBatchReward(int index)
        {
            var reward=rewards[index];ApplyTier(indexedProfiles[(int)reward.rarity]);ShowArtwork(reward);
            Evaluate(7);
            SetRewardText(reward);phaseLabel.text=$"THE ROYAL COLLECTION  ·  {index+1} / {rewards.Length}";
            rewardCard.alpha=0;displayedOrder.Add(reward.id);Cue(resultCue,.16f);
        }
        public void Skip()
        {
            RequireCommand();
            if(!operationActive)return;
            if(State!=FlowState.Cinematic && State!=FlowState.BatchReveal)return;
            Evaluate(7);Finish(OperationOutcome.Skipped);
        }
        private bool EndOperation(object originatingTag,OperationOutcome outcome,bool notifyCompletion=false)
        {
            RequireEmitterThread();
            if(!operationActive || !ReferenceEquals(operationTag,originatingTag))return false;
            object captured=operationTag;long epoch=lifecycleEpoch;
            bool complete=notifyCompletion && outcome!=OperationOutcome.Cancelled && !completionSent;
            operationActive=false;operationTag=null;OperationEndCount++;
            if(complete)completionSent=true;
            var tagged=TaggedOperationEnded;var terminal=OperationEnded;var legacy=Completed;
            publishingTerminal=true;
            try
            {
                if(tagged!=null)foreach(Action<object,OperationOutcome> observer in tagged.GetInvocationList())
                    try{observer(captured,outcome);}catch(Exception error){ObserverFaultCount++;Debug.LogException(error);}
                if(terminal!=null)foreach(Action<OperationOutcome> observer in terminal.GetInvocationList())
                    try{observer(outcome);}catch(Exception error){ObserverFaultCount++;Debug.LogException(error);}
                if(complete && CanPublishCompletion(epoch))
                {
                    CompletionCount++;
                    if(legacy!=null)foreach(Action observer in legacy.GetInvocationList())
                    {
                        if(!CanPublishCompletion(epoch))break;
                        try{observer();}catch(Exception error){ObserverFaultCount++;Debug.LogException(error);}
                    }
                }
            }
            finally{publishingTerminal=false;}
            return true;
        }
        private bool CanPublishCompletion(long epoch)=>epoch==lifecycleEpoch && !destroying && this && isActiveAndEnabled;
        private void Finish(OperationOutcome outcome=OperationOutcome.Completed)
        {
            RequireEmitterThread();
            if(!operationActive)return;
            object captured=operationTag;
            try
            {
            running=false;director.Pause();StopParticles();audioSource.Stop();State=FlowState.Results;
            displayedOrder.Clear();displayedOrder.AddRange(rewards.Select(r=>r.id));
            resultsPanel.SetActive(true);output.color=new Color(1,1,1,.38f);rewardCard.alpha=0;
            for(int i=0;i<resultLabels.Length;i++)
            {
                bool show=i<rewards.Length;resultLabels[i].transform.parent.gameObject.SetActive(show);
                if(!show)continue;
                var r=rewards[i];resultLabels[i].text=$"{i+1:00}  ·  {r.rarity}\n{r.name}"+(r.duplicate?"\nDUPLICATE":"")+(r.artwork==MockArtwork.Missing?"\nSKETCH PENDING":"");
                var cell=resultBorders[i].rectTransform;
                cell.anchoredPosition=IsBatch?new Vector2((i%5-2)*286,78-(i/5)*150):Vector2.zero;
                cell.sizeDelta=IsBatch?new Vector2(274,132):new Vector2(780,160);
                resultLabels[i].rectTransform.sizeDelta=IsBatch?new Vector2(254,119):new Vector2(744,144);
                resultLabels[i].resizeTextMinSize=IsBatch?16:24;resultLabels[i].resizeTextMaxSize=IsBatch?20:30;
                resultBorders[i].color=indexedProfiles[(int)r.rarity].accent*.33f+new Color(.025f,.030f,.046f,.66f);
            }
            phaseLabel.text="MOCK RESULTS  ·  ORDER PRESERVED";
            }
            catch{CleanupAfterFault();throw;}
            // State is settled before observers. No native/shared-state continuation after publication.
            EndOperation(captured,outcome,true);
        }
        public void ToggleReducedMotion(){RequireCommand();reducedMotion=!reducedMotion;if(profile)ApplyTier(profile);UpdateAccessibility();ApplyCamera();}
        public void ToggleReducedFlash(){RequireCommand();reducedFlash=!reducedFlash;if(profile)ApplyTier(profile);UpdateAccessibility();}
        public void ToggleShake(){RequireCommand();shakeEnabled=!shakeEnabled;UpdateAccessibility();ApplyCamera();}
        public void ToggleAudio(){RequireCommand();muteAudio=!muteAudio;if(muteAudio)audioSource.Stop();UpdateAccessibility();}
        public void ToggleQuality(){RequireCommand();SetReducedQuality(!reducedQuality);UpdateAccessibility();}
        public void SetReducedQuality(bool value)
        {
            RequireCommand();
            if(reducedQuality==value)return;reducedQuality=value;
            if(OwnedTarget){ReleaseTarget();Prepare();}
            if(profile)ApplyTier(profile);
        }
        public void SetAspect(float value)
        {
            RequireCommand();
            if(value<1 || value>3)throw new ArgumentOutOfRangeException(nameof(value));
            aspect=value;if(OwnedTarget){ReleaseTarget();Prepare();ApplyCamera();}
        }
        private void UpdateAccessibility()=>accessibilityLabel.text=$"Motion {(reducedMotion?"reduced":"full")}   ·   Flash {(reducedFlash?"reduced":"soft")}   ·   Shake {(shakeEnabled?"on":"off")}   ·   {(reducedQuality?"Reduced / Mobile URP":"PC High")}   ·   Sound {(muteAudio?"off":"on")}";
        private void Cue(AudioClip clip,float volume){if(!muteAudio && clip){audioSource.PlayOneShot(clip,volume);CueCount++;}}
        private void StopParticles(){if(motes)motes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(impact)impact.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        private void ReleaseTarget()
        {
            // Detach failures must not bypass releasing the locally owned target.
            try{if(cinematicCamera)cinematicCamera.targetTexture=null;}
            finally
            {
                try{if(output)output.texture=null;}
                finally
                {
                    var target=OwnedTarget;OwnedTarget=null;
                    if(target)
                    {
                        try{target.Release();}
                        finally{if(UnityEngine.Application.isPlaying)Destroy(target);else DestroyImmediate(target);ReleaseCount++;}
                    }
                }
            }
        }
        public void Cleanup()
        {
            RequireEmitterThread();
            if(cleaning)return;
            lifecycleEpoch++;cleaning=true;
            var failures=new List<Exception>();
            Action<Action> release=step=>{try{step();}catch(Exception error){failures.Add(error);}};
            try
            {
                running=false;
                release(()=>{if(director)director.Stop();});
                release(()=>{if(motes)motes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);});
                release(()=>{if(impact)impact.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);});
                release(()=>{if(audioSource)audioSource.Stop();});
                release(ReleaseTarget);
                release(()=>{if(cinematicCamera)cinematicCamera.enabled=false;});
                release(()=>{if(output)output.gameObject.SetActive(false);});
                release(()=>{if(resultsPanel)resultsPanel.SetActive(false);});
                release(()=>{if(rewardCard)rewardCard.alpha=0;});
                lastCameraOffset=Vector3.zero;
                release(()=>{if(nativeVFX)nativeVFX.Clear();});
                release(()=>{try{if(ownsPipeline && QualitySettings.renderPipeline==selectedPipeline)QualitySettings.renderPipeline=previousPipeline;}finally{ownsPipeline=false;}});
                State=FlowState.Cleaned;
                EndOperation(operationTag,OperationOutcome.Cancelled);
            }
            finally{cleaning=false;}
            if(failures.Count>0)throw new AggregateException("M35 native cleanup failed; all teardown steps were attempted.",failures);
        }
        private void DestroyOwnedHostObjects()
        {
            try{if(ownedCanvas){if(UnityEngine.Application.isPlaying)Destroy(ownedCanvas.gameObject);else DestroyImmediate(ownedCanvas.gameObject);}}
            finally{if(ownedSurface){if(UnityEngine.Application.isPlaying)Destroy(ownedSurface);else DestroyImmediate(ownedSurface);ownedSurface=null;}}
        }
        private void OnEnable(){if(ownedCanvas)ownedCanvas.gameObject.SetActive(true);}
        private void OnDisable(){try{Cleanup();}finally{if(ownedCanvas)ownedCanvas.gameObject.SetActive(false);}}
        private void OnDestroy(){destroying=true;try{Cleanup();}finally{DestroyOwnedHostObjects();}}
    }
}


#endif
