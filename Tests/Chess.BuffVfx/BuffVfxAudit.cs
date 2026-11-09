using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ChessButWeird.BuffVfx;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

// Executed only inside the disposable project made by verify-buff-vfx.ps1.
public static partial class BuffVfxAudit
{
    private static int assertions;
    private static readonly List<string> report=new List<string>();
    private static readonly List<string> runtimeErrors=new List<string>();
    private static string Root=>Directory.GetCurrentDirectory();
    public static void Run()
    {
        if(!Application.isBatchMode||!Root.Replace('\\','/').Contains("/Logs/buff-vfx-qa/"))throw new InvalidOperationException("Isolated QA project required.");
        EditorSettings.enterPlayModeOptionsEnabled=true;
        EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        EditorApplication.update+=AwaitPlay;
        EditorApplication.EnterPlaymode();
    }
    private static int ticks;
    private static void AwaitPlay()
    {
        if(!EditorApplication.isPlaying||++ticks<10)return;
        EditorApplication.update-=AwaitPlay;Execute();
    }
    private static void Check(bool condition,string message)
    {if(!condition)throw new Exception("Assertion failed: "+message);assertions++;}
    private static async void Execute()
    {
        try
        {
            Application.logMessageReceived+=(message,stack,type)=>{if((type==LogType.Error||type==LogType.Assert||type==LogType.Exception)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))runtimeErrors.Add(message+"\n"+stack);};
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Config/New Universal Render Pipeline Asset.asset");
            Check(pipeline,"Actual project URP asset loads");
            // Custom hover material is unrelated to this isolated scene; its layer is unused.
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Config/New Universal Render Pipeline Asset_Renderer.asset");
            foreach(var feature in renderer.rendererFeatures)if(feature)feature.SetActive(false);
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            var host=new GameObject("Production presentation QA");var gallery=host.AddComponent<BuffVfxDebugGallery>();
            string[] kinds={"King","Queen","Rook","Bishop","Knight","Pawn"};
            gallery.PieceModels=kinds.Select(k=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Pieces/Chess_"+k+".fbx")).ToArray();
            gallery.Build();var fx=gallery.Effects;fx.enabled=false;
            // OnDisable clears pools but leaves their resources available for deterministic manual ticks.
            await Task.Delay(300);
            Check(fx.Ready,"Production registry/shaders initialized");
            Check(fx.Registry.Count==35,"All 35 actual IDs registered");
            Check(Enumerable.Range(0,35).Count(id=>fx.Registry.Find(id).implemented)==35,"All 35 actual recipes have production presentations");
            foreach(string model in new[]{"Sniper","Cannon","KingRifle"})
            {
                var asset=Resources.Load<GameObject>("BuffVfx/Models/"+model);Check(asset,"Authored "+model+" imported");
                var meshes=asset.GetComponentsInChildren<MeshFilter>();
                Check(meshes.Sum(m=>m.sharedMesh.vertexCount)>300,"Detailed authored mesh: "+model);
                Check(asset.GetComponentsInChildren<Renderer>().Length<=4,"Grouped palette draw budget: "+model);
            }
            foreach(string shader in new[]{"BoardInk","PropInk"})Check(!ShaderUtil.ShaderHasError(Resources.Load<Shader>("BuffVfx/"+shader)),"Shader compiles: "+shader);
            int[] ids={6,17,4,7,16,25};string[] labels={"noble","betrayal","bomber","sniper","cannon","king-rifle"};
            for(int i=0;i<ids.Length;i++)
            {
                gallery.Select(i);Capture(labels[i]+"-status");Check(gallery.PlaySelected(),"Play "+labels[i]);fx.Advance(.2f);Capture(labels[i]+"-draw");
                fx.Advance(.21f);Capture(labels[i]+"-fire");
                if(i>=3)Closeup(gallery.Caster,labels[i]+"-weapon");
                if(i>=3)Check(fx.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m&&m.name.StartsWith("Buff prop palette ")).Select(m=>m.name).Distinct().Count()>=3,"Authored paint groups preserved on mounted "+labels[i]);
                fx.Advance(.26f);Capture(labels[i]+"-impact");
                fx.Advance(1);Check(fx.ActiveCount==0,"Finish "+labels[i]);
                gallery.Select(i,true,PieceTeam.Black);Check(gallery.PlaySelected(true),"Black edge "+labels[i]);fx.Advance(.72f);fx.Advance(1);Check(fx.ActiveCount==0,"Edge cleanup "+labels[i]);
                for(int repeat=0;repeat<3;repeat++){Check(gallery.PlaySelected(true),"Repeated "+labels[i]);fx.Advance(1);fx.Advance(1);}
                gallery.PlaySelected(true);fx.Paused=true;fx.Advance(1);Check(fx.ActiveCount==1,"Pause "+labels[i]);fx.Paused=false;fx.PlaybackSpeed=8;fx.Advance(.2f);Check(fx.ActiveCount==0,"Fast-forward "+labels[i]);fx.PlaybackSpeed=1;
            }
            gallery.Select(2,true);UserSettings.Presentation.Shake=1;gallery.PlaySelected(true);fx.Advance(.67f);Capture("bomber-edge");UserSettings.Presentation.Shake=0;
            gallery.Select(1,false,PieceTeam.Black);gallery.PlaySelected(false,"Transferred");fx.Advance(.72f);Capture("betrayal-transferred-black");
            // Repeated events reuse the same geometry and shared materials after warm-up.
            fx.ClearVisuals(true);int materialCount=Resources.FindObjectsOfTypeAll<Material>().Length;
            for(int n=0;n<60;n++){gallery.PlaySelected();fx.Advance(1);fx.Advance(1);}
            Check(fx.CreatedInstances==35,"Sequential playback reuses thirty-five prewarmed instances");
            Check(Resources.FindObjectsOfTypeAll<Material>().Length==materialCount,"No material growth across sixty plays");
            var bad=new BuffVisualEvent{Id="invalid",BuffId=4,ViewerAuthorized=false};
            Check(!fx.Play(bad),"Unauthorized event rejected");bad.ViewerAuthorized=true;bad.Target=new Vector3(float.NaN,0,0);Check(!fx.Play(bad),"Non-finite position rejected");
            bad.Target=Vector3.zero;bad.BuffId=99;Check(!fx.Play(bad),"Unknown recipe rejected");bad.BuffId=4;bad.Tiles=new Vector3[10];Check(!fx.Play(bad),"Oversized board area rejected");
            bad.Tiles=null;Check(fx.Play(bad),"Valid event accepted");Check(!fx.Play(bad),"Duplicate event ID rejected");fx.ClearVisuals(true);
            for(int i=0;i<ids.Length;i++)for(int j=0;j<4;j++)fx.Play(new BuffVisualEvent{Id="overlap:"+i+":"+j,BuffId=ids[i],ViewerAuthorized=true,Source=Vector3.zero,Target=Vector3.one,Actor=gallery.Caster});
            Check(fx.ActiveCount==12&&fx.CreatedInstances==41,"Overlap cap: twelve active instances, two per recipe");Check(fx.DroppedEvents==12,"Excess transient cues dropped safely");fx.ClearVisuals(true);
            var actor=gallery.Caster;actor.gameObject.SetActive(true);var statuses=new[]{new BuffVisualStatus(6,1,actor,PieceTeam.Black),new BuffVisualStatus(7,1,actor,PieceTeam.Black,4),new BuffVisualStatus(17,1,actor,PieceTeam.Black,-1,25)};
            fx.SyncStatuses(statuses);Check(fx.ActiveStatuses==3,"Multiple real statuses stack on one actor");fx.SyncStatuses(statuses);Check(fx.ActiveStatuses==3,"Repeated status projection is idempotent");
            Check(fx.GetComponentsInChildren<TMPro.TextMeshPro>().Any(t=>t.text=="4"),"Remaining moves rendered");Check(fx.GetComponentsInChildren<TMPro.TextMeshPro>().Any(t=>t.text=="25%"),"Confirmed risk rendered");
            fx.Paused=true;actor.gameObject.SetActive(false);fx.Advance(.1f);Check(fx.ActiveStatuses==0,"Destroyed/inactive actor clears all sigils even while paused");actor.gameObject.SetActive(true);fx.Paused=false;
            fx.SyncStatuses(statuses);fx.SyncStatuses(Array.Empty<BuffVisualStatus>());fx.Advance(.2f);Check(fx.GetComponentsInChildren<TMPro.TextMeshPro>().Length==0,"Expired sigils fade and return to pool");
            gallery.Select(3);gallery.PlaySelected();fx.Paused=true;fx.Advance(1);Check(fx.ActiveCount==1,"Pause keeps animation time stable");fx.Paused=false;fx.PlaybackSpeed=8;fx.Advance(.2f);Check(fx.ActiveCount==0,"Fast-forward cleanup");fx.PlaybackSpeed=1;
            gallery.PlaySelected();UserSettings.Presentation.AnimationSpeed=0;fx.Advance(.01f);Check(fx.ActiveCount==0,"Instant animation setting");UserSettings.Presentation.AnimationSpeed=1;
            UserSettings.Presentation.ReducedMotion=true;UserSettings.Presentation.Flash=0;gallery.Select(2);gallery.PlaySelected();fx.Advance(.2f);Capture("bomber-reduced-motion");
            Check(fx.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount)==0,"Reduced motion suppresses debris");fx.Advance(1);UserSettings.Presentation.ReducedMotion=false;UserSettings.Presentation.Flash=1;
            int fixtureCount=RunBackendFixtures();
            RunLandscapeCases(gallery);
            fixtureCount+=RunLandscapeFixtures();
            RunStrategyCases(gallery);fixtureCount+=RunStrategyFixtures();
            gallery.Select(4);gallery.PlaySelected();Object.Destroy(gallery.Caster.gameObject);await Task.Delay(20);fx.Advance(.7f);fx.Advance(1);Check(fx.ActiveCount==0,"Transient survives actor destruction, then cleans up");
            fx.ClearVisuals(true);Check(fx.ActiveCount==0&&fx.ActiveStatuses==0,"Exit/restart clears all visuals");
            foreach(int id in ids)fx.Play(new BuffVisualEvent{Id="exit:"+id,BuffId=id,ViewerAuthorized=true,Source=Vector3.zero,Target=Vector3.one});
            Check(fx.ActiveCount==6,"All six recipes are active before actual scene-owner destruction");
            Object.Destroy(host);await Task.Delay(30);
            Check(!fx,"Scene owner disposal destroys the orchestrator");
            Check(!Resources.FindObjectsOfTypeAll<Material>().Any(m=>m.name=="Buff shared ink"||m.name.StartsWith("Buff prop palette ")),"Owned shared materials disposed on scene exit");
            Check(runtimeErrors.Count==0,"No gameplay/presentation runtime errors: "+string.Join("\n",runtimeErrors));
            report.Add("PASS: "+assertions+" assertions; "+fixtureCount+" real backend viewer transitions.");
            report.Add("Unity "+Application.unityVersion+" / "+SystemInfo.graphicsDeviceType+" / "+SystemInfo.graphicsDeviceName+" / URP "+pipeline.name);
            report.Add("Production renderers, shaders, models and online adapter. Synthetic gallery events; real backend fixture snapshots. Settings/team ports only.");
            report.Add("No full interactive two-client match or GPU frame-time benchmark performed.");
            File.WriteAllLines(Path.Combine(Root,"audit-result.txt"),report);Debug.Log(string.Join("\n",report));EditorApplication.Exit(0);
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(Root,"audit-result.txt"),"FAIL after "+assertions+" assertions\n"+ex);Debug.LogException(ex);EditorApplication.Exit(1);}
    }
    private static void Closeup(Transform actor,string name)
    {
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize;
        camera.transform.position=actor.position+new Vector3(2.6f,2.3f,-3.1f);camera.transform.LookAt(actor.position+Vector3.up*.55f);camera.orthographicSize=1.6f;
        Capture(name);camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=size;
    }
    private static int RunBackendFixtures()
    {
        var host=new GameObject("Backend projection QA");var fx=host.AddComponent<BuffVfxOrchestrator>();fx.Initialize(2);fx.enabled=false;
        int count=0;var fixtures=JArray.Parse(File.ReadAllText(Path.Combine(Root,"backend-fixtures.json")));
        foreach(var fixture in fixtures)foreach(string viewer in new[]{"white","black"})
        {
            fx.ClearVisuals(true);var actors=new Dictionary<int,Transform>();
            Vector3 Map(Vector2Int p)=>new Vector3(20-2*p.y,0,2*p.x);
            var adapter=new BuffVfxOnlineAdapter(fx,Map,id=>actors.TryGetValue(id,out var a)?a:null);
            void Board(JToken snapshot)
            {
                foreach(var a in actors.Values)Object.Destroy(a.gameObject);actors.Clear();
                foreach(var piece in snapshot["board"])
                {
                    Check(BuffVfxOnlineAdapter.TrySquare((string)piece["square"],out var sq),"Fixture board square");
                    var go=new GameObject("Backend actor "+piece["id"]);go.transform.position=Map(sq);actors[(int)piece["id"]]=go.transform;
                }
            }
            var before=fixture[viewer]["before"];var after=fixture[viewer]["after"];string match=(string)before["matchId"]??"fixture";
            Board(before);adapter.Apply(match,(long)before["stateVersion"],(JObject)before["chaos"],false);Check(fx.ActiveCount==0,"First snapshot never replays historical cues");
            Board(after);adapter.Apply(match,(long)after["stateVersion"],(JObject)after["chaos"],false);Check(fx.ActiveCount==1,"Confirmed "+fixture["name"]+" / "+viewer);
            Check(adapter.LastSequence==(long)after["chaos"]["visualSequence"],"Sequence watermark matches server");
            adapter.Apply(match,(long)after["stateVersion"],(JObject)after["chaos"],false);Check(fx.ActiveCount==1,"Repeated snapshot does not duplicate");
            adapter.Apply(match,(long)before["stateVersion"],(JObject)before["chaos"],false);Check(fx.ActiveCount==1,"Stale version ignored");
            Check(Vector3.Distance(adapter.BoardPoint(new Vector3(2,.5f,3)),new Vector3(14,1,4))<.001f,"Rotated/scaled board coordinate mapping");
            if((string)fixture["name"]=="bomber-edge")Check(fx.GetComponentsInChildren<LineRenderer>().Count(l=>l.name.StartsWith("Resolved tile"))==4,"Actual server blast clips to four edge tiles");
            if((string)fixture["name"]=="king-rifle")
            {
                var impact=after["chaos"]["visualCues"].Last["impact"];var expected=adapter.BoardPoint(new Vector3((float)impact[0],(float)impact[1],(float)impact[2]));fx.Advance(.4f);
                Check(fx.GetComponentsInChildren<LineRenderer>().Any(l=>l.name=="Confirmed trajectory"&&Vector3.Distance(l.GetPosition(1),expected)<.001f),"Rifle endpoint uses server ray");
            }
            fx.ClearVisuals();var reconnect=new BuffVfxOnlineAdapter(fx,Map,id=>actors.TryGetValue(id,out var a)?a:null);
            reconnect.Apply(match,(long)after["stateVersion"],(JObject)after["chaos"],false);Check(fx.ActiveCount==0,"Reconnect does not repeat old resolution");
            reconnect.Apply(match,(long)after["stateVersion"]+1,(JObject)after["chaos"],true);Check(fx.ActiveCount==0&&fx.ActiveStatuses==0,"Match completion cleanup");
            foreach(var a in actors.Values)Object.Destroy(a.gameObject);count++;
        }
        Object.Destroy(host);return count;
    }
    private static void Capture(string name,bool includeHud=false)
    {
        // Manual sequence stepping does not advance Unity's native particle clock.
        foreach(var particles in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude))if(particles.particleCount>0)particles.Simulate(.12f,true,false);
        var camera=Camera.main;var position=camera.transform.position;
        var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);target.Create();
        var canvases=includeHud?Object.FindObjectsByType<Canvas>().Where(c=>c.renderMode==RenderMode.ScreenSpaceOverlay&&c.name=="CHAOS objective presentation").ToArray():Array.Empty<Canvas>();
        var oldTarget=camera.targetTexture;camera.targetTexture=target;
        foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.5f;}Canvas.ForceUpdateCanvases();
        var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
        Check(RenderPipeline.SupportsRenderRequest(camera,request),"URP supports single-camera capture");RenderPipeline.SubmitRenderRequest(camera,request);
        var old=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
        string directory=Path.Combine(Root,"captures");Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,name+".png"),texture.EncodeToPNG());
        RenderTexture.active=old;Object.Destroy(texture);target.Release();Object.Destroy(target);
        camera.targetTexture=oldTarget;foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;}
        Check(camera.transform.position==position,"Camera base pose restored after render");
    }
}
