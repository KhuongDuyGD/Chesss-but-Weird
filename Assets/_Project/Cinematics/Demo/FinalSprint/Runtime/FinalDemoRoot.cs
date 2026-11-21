#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets.ResourceLocators;
using ChessButWeird.U1A3;
using ChessButWeird.CinematicIntegration.M4A;
using ChessButWeird.CinematicIntegration.M4B0;
using ChessButWeird.CinematicIntegration.M4B1A;
using ChessButWeird.CinematicIntegration.M4B1B2A;
using ChessButWeird.CinematicIntegration.M4B1B2B;

namespace ChessButWeird.CinematicDemo.FinalSprint
{
    [Serializable]
    public sealed class DemoArtworkBinding
    {
        public string alias,itemId,itemCode,rawType,localId,category,dataGuid,dataPath,previewGuid,previewPath,previewAddress;
    }
    // Scene-local, explicitly opt-in. No runtime initializer, AuthStorage, ApiClient or GachaService.
    public sealed partial class FinalDemoRoot : MonoBehaviour
    {
        public GameObject ritualPrefab;
        public TextAsset one,ten;
        public DemoArtworkBinding[] bindings;
        public NativePresentationHost Host {get;private set;}
        public NativePresentationView View {get;private set;}
        public ReceivedResultsBoundary Results {get;private set;}
        public DemoReceiptInbox Inbox {get;private set;}
        public CommittedReceipt Current {get;private set;}
        public bool Ready {get;private set;}
        public bool LocalArtOnlyValidated {get;private set;}
        public string Error {get;private set;}
        public Canvas Canvas {get;private set;}
        public readonly List<NativePresenterFactory> PastFactories=new List<NativePresenterFactory>();
        public int InitializationAcquired {get;private set;}
        public int InitializationReleased {get;private set;}
        bool admitted,closed,hasInit,reducedMotion,reducedFlash,shake=true,sound=true,fastTen;
        string exactResults;Text disclosure;GameObject hostObject;
        AsyncOperationHandle<IResourceLocator> init;
        public static string Argument(string name)=>Environment.GetCommandLineArgs().FirstOrDefault(x=>x.StartsWith(name+"=",StringComparison.Ordinal))?.Substring(name.Length+1);
        void Start()
        {
            MakeUI();
            if(Environment.GetCommandLineArgs().Contains("--royal-demo"))EnableDemo();
            else disclosure.text="DEVELOPMENT DEMO ONLY | Click Enter demo. No server, currency or grants.";
        }
        public void EnableDemo(){if(admitted)return;admitted=true;StartCoroutine(Initialize());}
        IEnumerator Initialize()
        {
            string data=Argument("--demo-data")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CBW","RoyalDemoV1");
            try{Inbox=new DemoReceiptInbox(data);init=Addressables.InitializeAsync(false);hasInit=true;InitializationAcquired++;}
            catch(Exception e){Error=e.GetType().Name+": "+e.Message;disclosure.text=Error;yield break;}
            yield return init;
            if(closed)yield break;
            if(init.Status!=AsyncOperationStatus.Succeeded){Error="Local Addressables initialization failed";disclosure.text=Error;yield break;}
            try
            {
                foreach(var binding in bindings.Where(b=>!string.IsNullOrEmpty(b.previewGuid)))
                {
                    var locations=Addressables.ResourceLocators.SelectMany(l=>l.Locate(binding.previewGuid,typeof(Sprite),out var found)?found:Enumerable.Empty<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation>()).ToArray();
                    if(locations.Length==0)throw new InvalidOperationException("Local fixture art address missing: "+binding.alias);
                    foreach(var location in locations)RequireLocal(location,new HashSet<string>());
                }
                LocalArtOnlyValidated=true;
            }
            catch(Exception e){Error=e.GetType().Name+": "+e.Message;disclosure.text=Error;yield break;}
            Ready=true;disclosure.text="ROYAL ASCENSION | OFFLINE COMMITTED FIXTURES | No authenticated player / no transactions";
            if(Environment.GetCommandLineArgs().Contains("--demo-validate"))
            {if(!FindFirstObjectByType<FinalDemoValidation>())new GameObject("FinalDemoValidation").AddComponent<FinalDemoValidation>();}
            else if(!FindFirstObjectByType<FinalDemoValidation>())PlayOne();
        }
        static void RequireLocal(UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation location,HashSet<string> seen)
        {
            if(!seen.Add(location.InternalId))return;
            if(Uri.TryCreate(location.InternalId,UriKind.Absolute,out var uri)&&(uri.Scheme=="http"||uri.Scheme=="https"))throw new InvalidOperationException("Remote artwork is not authorized for this offline demo");
            if(location.HasDependencies)foreach(var dependency in location.Dependencies)RequireLocal(dependency,seen);
        }
        public void PlayOne()=>Begin(1);
        public void PlayTen()=>Begin(10);
        public void Begin(int count)
        {
            if(!Ready||closed)return;
            Current=Inbox.ReceiveFixture((count==1?one:ten).text);OpenCurrent();
        }
        public void Reopen(){if(!Ready)return;var id=Current?.RequestId??Inbox.StoredRequests().LastOrDefault();if(id!=null)OpenStored(id);}
        public void OpenStored(string requestId){if(!Ready)return;Current=Inbox.Reopen(requestId);OpenCurrent();}
        public void OpenCurrent()
        {
            CloseVisuals();
            var dto=Current.ToLegacyDto();
            // A serialized empty local ID is unavailable, not a fabricated lookup key.
            var metadata=GachaRewardMetadataSnapshot.FromAlreadyReceived(catalog:bindings.Select(b=>new ItemCatalogResponse{itemId=b.itemId,code=b.itemCode,type=b.rawType,unityAssetKey=string.IsNullOrWhiteSpace(b.localId)?null:b.localId}));
            var batch=GachaRewardPresentationAdapter.Map(dto,metadata);
            if(!batch.IsStructurallyValid)throw new InvalidOperationException("Committed fixture projection invalid");
            for(int i=0;i<Current.Count;i++)if(batch.Rewards[i].Source.ItemId!=Current.Rewards[i].ItemId||batch.Rewards[i].OriginalSlotIndex!=i||batch.Rewards[i].Source.RawRarity!=Current.Rewards[i].BackendRarity||batch.Rewards[i].Source.IsDuplicate!=Current.Rewards[i].IsDuplicate)throw new InvalidOperationException("Identity projection drift");
            var evidence=new ArtworkEvidence(ArtworkEvidenceScope.Fixture,"Final demo synthetic ItemIds only; seven inspected local candidates; never a production authority manifest");
            var catalog=new ArtworkCatalogSnapshot(bindings.Where(b=>!string.IsNullOrEmpty(b.previewGuid)).Select(b=>new ArtworkCatalogEntry(b.category=="PieceSkin"?ArtworkCategory.PieceSkin:ArtworkCategory.Board,b.localId,b.localId,
                new ArtworkAssetReference(ArtworkReferenceKind.AddressableGuid,b.dataGuid,b.dataPath,b.category=="PieceSkin"?"PieceSkinData":"BoardSkinData"),
                new ArtworkAssetReference(ArtworkReferenceKind.AddressableGuid,b.previewGuid,b.previewPath,"Sprite",b.previewAddress))),evidence);
            var policy=new ArtworkTypePolicy(new[]{new ArtworkTypeBinding("CHESS_PIECE_SKIN",ArtworkCategory.PieceSkin),new ArtworkTypeBinding("BOARD_SKIN",ArtworkCategory.Board)},evidence);
            hostObject=new GameObject("Demo-owned presentation host");hostObject.transform.SetParent(transform,false);Host=hostObject.AddComponent<NativePresentationHost>();
            Results=new ReceivedResultsBoundary(View.ShowResults);
            var factory=new NativePresenterFactory{RitualPrefab=ritualPrefab,View=View,Catalog=catalog,Policy=policy,Capabilities=Capabilities(),TargetHeight=720};
            PastFactories.Add(factory);if(PastFactories.Count>32)PastFactories.RemoveAt(0);
            Host.Initialize(new ReceivedPresentationSession(batch),factory,Results);
            exactResults="COMMITTED DEMO RESULTS — original order / no inventory grants\n"+string.Join("\n",Current.Rewards.Select(r=>$"{r.Slot+1:00}  {r.ItemCode}  |  {r.BackendRarity}  |  duplicate={r.IsDuplicate}\n      ItemId: {r.ItemId}"));
            Host.Play();
        }
        PresentationCapabilities Capabilities()=>new PresentationCapabilities(reducedMotion,reducedFlash,shake,sound,fastTen);
        public void Replay(){if(Host){Host.Factory.Capabilities=Capabilities();Host.Replay();}else Reopen();}
        public void Skip()=>Host?.Skip();
        public void CloseVisuals()
        {
            if(Host)Host.CloseVisuals();
            if(hostObject)Destroy(hostObject);hostObject=null;Host=null;
            if(View){if(View.ConsumerToken!=null)View.Detach(View.ConsumerToken);View.HideResults();}
        }
        public void Reload()=>UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        public void Exit(){CloseVisuals();UnityEngine.Application.Quit(0);}
        public void ToggleMotion(){reducedMotion=!reducedMotion;if(Host?.Factory.Current?.Ritual)Host.Factory.Current.Ritual.ToggleReducedMotion();}
        public void ToggleFlash(){reducedFlash=!reducedFlash;if(Host?.Factory.Current?.Ritual)Host.Factory.Current.Ritual.ToggleReducedFlash();}
        public void ToggleShake(){shake=!shake;if(Host?.Factory.Current?.Ritual)Host.Factory.Current.Ritual.ToggleShake();}
        public void ToggleSound(){sound=!sound;if(Host?.Factory.Current?.Ritual)Host.Factory.Current.Ritual.ToggleAudio();}
        public void ToggleFast(){fastTen=!fastTen;}
        void LateUpdate()
        {
            if(Host&&Host.Session.ResultsAccepted&&View.results.gameObject.activeInHierarchy&&View.results.text!=exactResults)View.results.text=exactResults;
            if(Host&&!Host.Session.ResultsAccepted&&View.Slot>=0&&View.Slot<Current.Count&&!View.identity.text.Contains(Current.Rewards[View.Slot].ItemId))
                View.identity.text+="\nItemId: "+Current.Rewards[View.Slot].ItemId;
        }
        void OnDisable(){CloseVisuals();}
        void OnDestroy()
        {
            closed=true;Ready=false;CloseVisuals();Inbox?.Dispose();
            if(hasInit){hasInit=false;if(init.IsValid())Addressables.Release(init);InitializationReleased++;}
        }
        void MakeUI()
        {
            var c=new GameObject("Demo overlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));c.transform.SetParent(transform,false);Canvas=c.GetComponent<Canvas>();Canvas.renderMode=RenderMode.ScreenSpaceOverlay;Canvas.sortingOrder=80;
            var scaler=c.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=1;
            View=gameObject.AddComponent<NativePresentationView>();
            View.ritual=UI<RawImage>("Actual M35 3D camera",c.transform,0,0,1,1);View.ritual.raycastTarget=false;
            disclosure=Label("Offline disclosure",c.transform,.015f,.965f,.985f,1,16);disclosure.text="DEVELOPMENT DEMO ONLY";
            View.phase=Label("Phase",c.transform,.02f,.925f,.98f,.962f,17);
            View.artwork=UI<Image>("Reward art",c.transform,.37f,.38f,.63f,.79f);View.artwork.enabled=false;View.artwork.raycastTarget=false;
            var idPanel=UI<Image>("Identity backing",c.transform,.12f,.115f,.88f,.32f);idPanel.color=new Color(.03f,.025f,.02f,.9f);
            View.identity=Label("Exact fixture identity",idPanel.transform,.015f,.02f,.985f,.98f,18);
            var panel=UI<Image>("All ten committed results",c.transform,.045f,.33f,.955f,.9f);panel.color=new Color(.045f,.035f,.025f,.96f);panel.gameObject.AddComponent<RectMask2D>();
            var scroll=panel.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.viewport=panel.rectTransform;
            View.results=Label("Ordered result ledger",panel.transform,.01f,1,.99f,1,16);View.results.alignment=TextAnchor.UpperLeft;View.results.rectTransform.pivot=new Vector2(.5f,1);View.results.rectTransform.anchoredPosition=new Vector2(0,-10);View.results.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=View.results.rectTransform;panel.gameObject.SetActive(false);
            var actions=new Action[]{EnableDemo,PlayOne,PlayTen,Replay,Skip,CloseVisuals,Reopen,Reload,Exit};var labels=new[]{"Enter demo","x1 Legendary","x10 fixtures","Replay","Skip","Close","Reopen","Reload","Exit"};
            for(int i=0;i<actions.Length;i++)Button(labels[i],c.transform,.01f+i*.109f,.06f,.105f+i*.109f,.1f,actions[i]);
            var toggles=new Action[]{ToggleMotion,ToggleFlash,ToggleShake,ToggleSound,ToggleFast};var names=new[]{"Reduced motion","Reduced flash","Camera shake","Sound","Fast x10 (next)"};
            for(int i=0;i<toggles.Length;i++)Button(names[i],c.transform,.07f+i*.175f,.01f,.235f+i*.175f,.05f,toggles[i]);
            if(!EventSystem.current){var es=new GameObject("Demo event system",typeof(EventSystem),typeof(InputSystemUIInputModule));es.transform.SetParent(transform,false);}
        }
        static T UI<T>(string name,Transform parent,float x,float y,float x2,float y2)where T:Graphic
        {var go=new GameObject(name,typeof(RectTransform),typeof(T));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();rt.anchorMin=new Vector2(x,y);rt.anchorMax=new Vector2(x2,y2);rt.offsetMin=rt.offsetMax=Vector2.zero;return go.GetComponent<T>();}
        static Text Label(string name,Transform parent,float x,float y,float x2,float y2,int size)
        {var t=UI<Text>(name,parent,x,y,x2,y2);t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.color=new Color(.96f,.9f,.76f);t.alignment=TextAnchor.MiddleCenter;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;t.raycastTarget=false;return t;}
        static void Button(string name,Transform parent,float x,float y,float x2,float y2,Action action)
        {var im=UI<Image>(name,parent,x,y,x2,y2);im.color=new Color(.16f,.12f,.075f);var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;b.onClick.AddListener(()=>action());Label(name,im.transform,0,0,1,1,15).text=name;}
    }
}
#endif
