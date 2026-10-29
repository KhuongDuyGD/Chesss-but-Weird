#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ChessButWeird.CinematicIntegration.M4B0;
using ChessButWeird.CinematicIntegration.M4B1A;
using ChessButWeird.CinematicIntegration.M4B1B2A;
using ChessButWeird.CinematicIntegration.M4B1B2B;

namespace ChessButWeird.CinematicDemo.FinalSprint
{
    // Development build only, invoked by an explicit CLI flag. Actual Player assertions, not Editor promises.
    public sealed class FinalDemoValidation : MonoBehaviour
    {
        readonly JArray cases=new JArray();readonly JObject[] missingArt=new JObject[3];string output;int assertions;bool failed;
        void Awake(){DontDestroyOnLoad(gameObject);}
        void Check(string name,bool condition,JToken detail=null)
        {assertions++;cases.Add(new JObject{["case"]=name,["status"]=condition?"PASS":"FAIL",["detail"]=detail});if(!condition)throw new InvalidOperationException(name);}
        IEnumerator Start()
        {
            output=FinalDemoRoot.Argument("--demo-output");
            if(string.IsNullOrWhiteSpace(output)||Directory.Exists(output)){Debug.LogError("Explicit fresh demo output required");UnityEngine.Application.Quit(2);yield break;}
            Directory.CreateDirectory(output);var work=Run();
            while(true)
            {
                bool move;object current=null;
                try{move=work.MoveNext();if(move)current=work.Current;}
                catch(Exception error){failed=true;cases.Add(new JObject{["case"]="uncaught_runtime_failure",["status"]="FAIL",["error"]=error.ToString()});break;}
                if(!move)break;yield return current;
            }
            foreach(var root in FindObjectsByType<FinalDemoRoot>(FindObjectsSortMode.None))root.CloseVisuals();
            yield return null;yield return null;
            File.WriteAllText(output+"/results.json",new JObject{["utc"]=DateTime.UtcNow.ToString("O"),["unity"]=UnityEngine.Application.unityVersion,["status"]=failed?"FAIL":"PASS",["assertions"]=assertions,["cases"]=cases,["mockAccountOnly"]=true,["liveEconomicTransportConstructed"]=false,["graphics"]=SystemInfo.graphicsDeviceName,["api"]=SystemInfo.graphicsDeviceType.ToString(),["development"]=Debug.isDebugBuild}.ToString());
            UnityEngine.Application.Quit(failed?1:0);
        }
        IEnumerator Run()
        {
            // Golden fixture metadata is supplied explicitly by the local diagnostic runner.
            // It is not an authority registry, replacement reward or network catalog.
            var artSpecPath=FinalDemoRoot.Argument("--demo-art-spec");
            Check("explicit_fixture_art_evidence_present",!string.IsNullOrEmpty(artSpecPath)&&File.Exists(artSpecPath));
            var artSpec=JObject.Parse(File.ReadAllText(artSpecPath));
            var root=FindFirstObjectByType<FinalDemoRoot>();float deadline=Time.realtimeSinceStartup+30;
            while(!root.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
            Check("demo_entrypoint_and_local_catalog_ready",root.Ready,root.Error);
            Check("seven_art_addresses_and_dependencies_are_local",root.LocalArtOnlyValidated);
            Check("development_D3D11",Debug.isDebugBuild&&SystemInfo.graphicsDeviceType==GraphicsDeviceType.Direct3D11);
            string x10id=null;
            foreach(int count in new[]{1,10})
            {
                root.Begin(count);var original=root.Current;var factory=root.Host.Factory;bool had3D=false,hadGraph=false,hadSurface=false;float maxTime=0;int shot=0;
                var expected=original.Rewards.Select(r=>new{r.ItemId,r.Slot,r.BackendRarity,r.IsDuplicate}).ToArray();
                var times=new[]{.5f,2.8f,3.65f,5.1f};deadline=Time.realtimeSinceStartup+45;
                while(!root.Host.Session.ResultsAccepted&&Time.realtimeSinceStartup<deadline)
                {
                    var presenter=factory.Current;
                    if(presenter?.Ritual&&presenter.AdmittedNativeTag!=null)
                    {
                        var ritual=presenter.Ritual;had3D|=ritual.stage.GetComponentsInChildren<MeshRenderer>(true).Length>10;hadGraph|=ritual.director.playableGraph.IsValid();hadSurface|=root.View.ritual.texture==ritual.OwnedTarget&&ritual.OwnedTarget&&ritual.OwnedTarget.IsCreated();maxTime=Mathf.Max(maxTime,ritual.SourceTime);
                        if(shot<times.Length&&ritual.SourceTime>=times[shot]){Capture(root,"x"+count+"-beat"+shot);shot++;}
                    }
                    int slot=root.View.Slot;
                    if(count==10&&slot>=6&&slot<=8&&missingArt[slot-6]==null&&root.View.identity.text.Contains(original.Rewards[slot].ItemId))
                    {
                        var reward=original.Rewards[slot];var binding=root.bindings.Single(b=>b.itemId==reward.ItemId);
                        var raw=((JArray)artSpec["items"]).OfType<JObject>().Single(r=>(string)r["itemId"]==reward.ItemId);
                        var acquisition=presenter.Requests[slot];var descriptor=acquisition.Descriptor;
                        string key=descriptor.Reward.CatalogLookupKey.Value;
                        int matches=factory.Catalog.Entries.Count(e=>e.Category==descriptor.Category&&key!=null&&string.Equals(e.Key,key,StringComparison.OrdinalIgnoreCase));
                        var row=new JObject{
                            ["alias"]=binding.alias,["itemId"]=reward.ItemId,["rawType"]=(string)raw["rawItemType"],["rawKey"]=(string)raw["rawUnityAssetKey"],
                            ["backendRarity"]=reward.BackendRarity,["serializedLocalId"]=binding.localId,["descriptorStatus"]=descriptor.Status.ToString(),
                            ["categoryStatus"]=descriptor.CategoryStatus.ToString(),["catalogKeyState"]=descriptor.Reward.CatalogLookupKey.State.ToString(),["catalogKey"]=key,
                            ["catalogLookupMatches"]=matches,["catalogEntryStatus"]=descriptor.EntryStatus.ToString(),["hasEntry"]=descriptor.Entry!=null,
                            ["specifiedReference"]=descriptor.HasSpecifiedReference,["acquisitionState"]=acquisition.State.ToString(),["outstandingAssetOperation"]=acquisition.HasOutstandingOperation,
                            ["acquiredSpriteInstance"]=acquisition.Asset?acquisition.Asset.GetInstanceID():0,["visualSpriteInstance"]=root.View.artwork.sprite?root.View.artwork.sprite.GetInstanceID():0,
                            ["visualSpriteEnabled"]=root.View.artwork.enabled,["fallbackText"]=root.View.identity.text,["visualSlot"]=root.View.Slot,
                            ["visualSlotItemId"]=presenter.Specification.Slots[slot].Reward.Source.ItemId,["displayLabel"]=presenter.Specification.Slots[slot].Reward.DisplayLabel,
                            ["bindingRawType"]=binding.rawType,["substitutionObserved"]=descriptor.Entry!=null||root.View.artwork.sprite||acquisition.HasOutstandingOperation,
                            ["nativeLoadGuidsSoFar"]=JArray.FromObject(((NativeSpriteLoader)presenter.Loader).Trace.Select(t=>t.Guid))};
                        missingArt[slot-6]=row;File.WriteAllText(output+"/missing-art-observations.json",JArray.FromObject(missingArt).ToString());
                        Capture(root,"missing-"+binding.alias);
                    }
                    yield return null;
                }
                Check("x"+count+"_actual_M35_mesh_Timeline_RenderTexture_playback",had3D&&hadGraph&&hadSurface&&maxTime>=4.55f,new JObject{["mesh"]=had3D,["graph"]=hadGraph,["surface"]=hadSurface,["maxSourceTime"]=maxTime});
                Check("x"+count+"_natural_completion_no_text_only_substitute",root.Host.Session.ResultsAccepted&&root.Host.Session.LastSettlement.Outcome==PresentationOutcome.Completed&&!root.Host.Session.LastSettlement.UsedFallback);
                Check("x"+count+"_all_slots_presented_in_order",factory.Current.PresentedSlots.SequenceEqual(Enumerable.Range(0,count)));
                for(int i=0;i<count;i++)Check("x"+count+"_committed_identity_slot_"+i,root.Current.Rewards[i].ItemId==expected[i].ItemId&&root.Current.Rewards[i].Slot==i&&root.Current.Rewards[i].BackendRarity==expected[i].BackendRarity&&root.Current.Rewards[i].IsDuplicate==expected[i].IsDuplicate);
                Check("x"+count+"_one_results_callback",root.Results.Calls==1);
                Check("x"+count+"_presenter_clean",factory.Current.IsDisposed&&factory.Current.Faults.Count==0&&factory.Current.Acquisition.OutstandingOwned==0);
                if(count==10)
                {
                    x10id=original.RequestId;
                    Check("A05_A06_RARE_A10_LEGENDARY",original.Rewards[4].BackendRarity=="RARE"&&original.Rewards[5].BackendRarity=="RARE"&&original.Rewards[9].BackendRarity=="LEGENDARY");
                    Check("all_three_missing_art_slots_observed",missingArt.All(r=>r!=null),JArray.FromObject(missingArt));
                    foreach(var row in missingArt)
                    {
                        int slot=(int)row["visualSlot"];var reward=original.Rewards[slot];
                        // Empty local ID is filtered by adapter Resolve, so no catalog lookup can occur.
                        // MissingCatalogEntry instead requires a resolved nonempty key with zero matches.
                        Check((string)row["alias"]+"_unavailable_art_status_and_identity",
                            (string)row["descriptorStatus"]=="MissingCatalogKey"&&(string)row["catalogKeyState"]=="Unresolved"&&
                            (string)row["categoryStatus"]=="Matched"&&string.IsNullOrEmpty((string)row["serializedLocalId"])&&
                            (int)row["catalogLookupMatches"]==0&&!(bool)row["hasEntry"]&&!(bool)row["specifiedReference"]&&
                            (string)row["acquisitionState"]=="MissingReference"&&!(bool)row["outstandingAssetOperation"]&&
                            (int)row["acquiredSpriteInstance"]==0&&(int)row["visualSpriteInstance"]==0&&!(bool)row["visualSpriteEnabled"]&&!(bool)row["substitutionObserved"]&&
                            (string)row["visualSlotItemId"]==reward.ItemId&&(string)row["backendRarity"]==reward.BackendRarity&&
                            (string)row["bindingRawType"]==(string)row["rawType"]&&
                            ((string)row["fallbackText"]).Contains(reward.ItemId)&&((string)row["fallbackText"]).Contains(reward.ItemName)&&
                            ((string)row["fallbackText"]).Contains("TEXT ONLY"),row);
                    }
                    Check("A07_A08_A09_missing_no_substitute",missingArt.All(r=>!(bool)r["substitutionObserved"]),JArray.FromObject(missingArt));
                    var native=(NativeSpriteLoader)factory.Current.Loader;var expectedGuids=root.bindings.Where(b=>!string.IsNullOrEmpty(b.previewGuid)).Select(b=>b.previewGuid).OrderBy(g=>g);
                    Check("seven_local_art_handles_acquired_released_no_substitution_attempt",native.Trace.Count==7&&native.Trace.All(t=>t.ReleaseCalls==1)&&native.Trace.Select(t=>t.Guid).OrderBy(g=>g).SequenceEqual(expectedGuids),JArray.FromObject(native.Trace));
                    yield return null;Capture(root,"x10-all-committed-results");
                }
                var before=File.ReadAllBytes(root.Inbox.DiagnosticPath(original.RequestId));int records=root.Inbox.Count;
                root.Replay();yield return null;yield return null;root.Skip();deadline=Time.realtimeSinceStartup+10;
                while(root.Host.Session.LastSettlement?.Outcome!=PresentationOutcome.Skipped&&Time.realtimeSinceStartup<deadline)yield return null;
                Check("x"+count+"_Replay_Skip_visual_only",root.Host.Session.LastSettlement.Outcome==PresentationOutcome.Skipped&&root.Results.Calls==1&&root.Inbox.Count==records&&before.SequenceEqual(File.ReadAllBytes(root.Inbox.DiagnosticPath(original.RequestId))));
                root.CloseVisuals();yield return null;yield return null;root.Reopen();yield return null;yield return null;root.Skip();yield return null;yield return null;
                deadline=Time.realtimeSinceStartup+10;while(root.Host.Session.LastSettlement?.Outcome!=PresentationOutcome.Skipped&&Time.realtimeSinceStartup<deadline)yield return null;
                Check("x"+count+"_close_reopen_same_committed_record",root.Host.Session.LastSettlement?.Outcome==PresentationOutcome.Skipped&&root.Results.Calls==1&&root.Inbox.Count==records&&root.Current.RequestId==original.RequestId&&before.SequenceEqual(File.ReadAllBytes(root.Inbox.DiagnosticPath(original.RequestId))));
            }
            root.CloseVisuals();yield return null;yield return null;
            // Real native loads with completion deliberately withheld at the consumer boundary.
            foreach(bool destroyHost in new[]{false,true})
            {
                root.Begin(10);var delayed=new DelayedLoader();root.Host.Factory.Loader=()=>delayed;root.Host.Pump(0);
                string action=destroyHost?"destroy":"disable";
                Check(action+"_controlled_pending_asset_load_started",delayed.Operations.Count==7&&root.Host.Factory.Current.Acquisition.OutstandingOwned==7);
                var pendingPresenter=root.Host.Factory.Current;var results=root.Results;
                if(destroyHost)Destroy(root.Host.gameObject);else root.Host.enabled=false;
                yield return null;yield return null;delayed.Open();deadline=Time.realtimeSinceStartup+10;
                while(pendingPresenter.Acquisition.OutstandingOwned>0&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(action+"_retires_pending_handles_no_dead_view_publication",pendingPresenter.IsDisposed&&pendingPresenter.Acquisition.OutstandingOwned==0&&root.View.ConsumerToken==null&&results.Calls==0&&delayed.Inner.Trace.All(t=>t.ReleaseCalls==1),JArray.FromObject(delayed.Inner.Trace));
                root.CloseVisuals();yield return null;yield return null;
            }
            var previousFactories=root.PastFactories.ToArray();
            var old=root;var reload=SceneManager.LoadSceneAsync(0);while(!reload.isDone)yield return null;yield return null;
            root=FindFirstObjectByType<FinalDemoRoot>();deadline=Time.realtimeSinceStartup+30;while(!root.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
            Check("scene_reload_releases_old_initialization_owner",old.InitializationAcquired==old.InitializationReleased);
            root.OpenStored(x10id);yield return null;yield return null;root.Skip();yield return null;yield return null;
            deadline=Time.realtimeSinceStartup+10;while(root.Host.Session.LastSettlement?.Outcome!=PresentationOutcome.Skipped&&Time.realtimeSinceStartup<deadline)yield return null;
            Check("scene_reload_reopens_durable_x10_without_new_delivery",root.Host.Session.LastSettlement?.Outcome==PresentationOutcome.Skipped&&root.Results.Calls==1&&root.Current.Count==10&&root.Current.RequestId==x10id&&root.Inbox.Count==2);
            root.CloseVisuals();yield return null;yield return null;
            var allFactories=previousFactories.Concat(root.PastFactories).ToArray();
            var initialOwners=OwnerSnapshot(allFactories);float drainStart=Time.realtimeSinceStartup;deadline=drainStart+10;
            // Cancellation may retain an in-flight native load until its completion callback.
            // Require the SAME zero-owner condition after a bounded drain; never force-release it.
            while(!AllRetired(allFactories)&&Time.realtimeSinceStartup<deadline)yield return null;
            var ownerEvidence=new JObject{["initial"]=initialOwners,["final"]=OwnerSnapshot(allFactories),["drainSeconds"]=Time.realtimeSinceStartup-drainStart};
            File.WriteAllText(output+"/attempt-owner-cleanup.json",ownerEvidence.ToString());
            Check("all_attempt_resources_retired",AllRetired(allFactories),ownerEvidence);
            Check("all_owned_cinematic_RenderTextures_released",Resources.FindObjectsOfTypeAll<RenderTexture>().All(t=>t.name!="M35_Runtime_HDR"&&!t.name.StartsWith("B1B2B_OWNED_",StringComparison.Ordinal)));
            Check("owned_rig_instances_destroyed",Resources.FindObjectsOfTypeAll<GameObject>().All(o=>o.name!="B1B2B_OWNED_Ritual"));
            Destroy(root.gameObject);yield return null;yield return null;
            Check("destroy_releases_final_initialization_owner",root.InitializationAcquired==root.InitializationReleased);
        }
        static bool AllRetired(NativePresenterFactory[] factories)=>factories.All(f=>f.Attempts.All(a=>a.IsDisposed&&a.Acquisition!=null&&a.Acquisition.OutstandingOwned==0));
        static JArray OwnerSnapshot(NativePresenterFactory[] factories)
        {
            var result=new JArray();
            for(int f=0;f<factories.Length;f++)for(int a=0;a<factories[f].Attempts.Count;a++)
            {
                var attempt=factories[f].Attempts[a];var native=attempt.Loader as NativeSpriteLoader;var delayed=attempt.Loader as DelayedLoader;
                result.Add(new JObject{["factory"]=f,["attempt"]=a,["disposed"]=attempt.IsDisposed,["outstanding"]=attempt.Acquisition?.OutstandingOwned??-1,["faults"]=JArray.FromObject(attempt.Faults),
                    ["requests"]=JArray.FromObject(attempt.Requests.Select(r=>new{slot=r?.OriginalSlotIndex,state=r?.State.ToString(),outstanding=r?.HasOutstandingOperation,releases=r?.SuccessfulReleases})),
                    ["nativeTrace"]=JArray.FromObject(native!=null?native.Trace:delayed!=null?delayed.Inner.Trace:Array.Empty<NativeLoadTrace>())});
            }
            return result;
        }
        void Capture(FinalDemoRoot root,string name)
        {
            var ritual=root.Host?.Factory.Current?.Ritual;
            if(ritual&&ritual.OwnedTarget){var rr=new RenderPipeline.StandardRequest{destination=ritual.OwnedTarget};if(RenderPipeline.SupportsRenderRequest(ritual.cinematicCamera,rr))RenderPipeline.SubmitRenderRequest(ritual.cinematicCamera,rr);}
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);target.Create();var go=new GameObject("Owned demo capture");var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.07f,.045f);camera.targetTexture=target;
            var previous=RenderTexture.active;Texture2D image=null;
            try
            {
                root.Canvas.renderMode=RenderMode.ScreenSpaceCamera;root.Canvas.worldCamera=camera;root.Canvas.planeDistance=1;Canvas.ForceUpdateCanvases();
                var request=new RenderPipeline.StandardRequest{destination=target};Check("URP_capture_"+name,RenderPipeline.SupportsRenderRequest(camera,request));RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=target;image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;root.Canvas.renderMode=RenderMode.ScreenSpaceOverlay;root.Canvas.worldCamera=null;target.Release();Destroy(target);if(image)Destroy(image);Destroy(go);}
        }
        sealed class DelayedLoader:ISpriteLoader
        {
            public readonly NativeSpriteLoader Inner=new NativeSpriteLoader();public readonly System.Collections.Generic.List<Operation> Operations=new System.Collections.Generic.List<Operation>();
            public ISpriteOperation Load(string guid){var op=new Operation(Inner.Load(guid));Operations.Add(op);return op;}
            public void Open(){foreach(var op in Operations)op.Open();}
            public sealed class Operation:ISpriteOperation
            {
                readonly ISpriteOperation inner;bool open,released;public Operation(ISpriteOperation inner){this.inner=inner;inner.Completed+=Done;}
                public bool IsDone=>open&&!released&&inner.IsDone;public bool Succeeded=>IsDone&&inner.Succeeded;public Sprite Asset=>Succeeded?inner.Asset:null;public string Error=>IsDone?inner.Error:null;
                public event Action Completed;void Done(){if(open&&!released)Completed?.Invoke();}public void Open(){open=true;if(inner.IsDone)Done();}public void Release(){if(released)return;released=true;inner.Completed-=Done;inner.Release();}
            }
        }
    }
}
#endif
