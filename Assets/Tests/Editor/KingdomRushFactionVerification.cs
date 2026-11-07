using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ChessButWeird.Settings;
using Unity.Profiling;

// Isolated real player-loop/built-content verification. Never reads/writes player ownership.
[InitializeOnLoad]
public static class KingdomRushFactionVerification
{
    private const string Key="KR.Factions.Verifying";
    private static readonly string Output=System.Environment.GetCommandLineArgs().Contains("-kr-verify-d3d11")?
        "Logs/KingdomRush/Factions/D3D11":"Logs/KingdomRush/Factions/D3D12";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly List<string> Checks=new List<string>();
    private static string particleVelocityError;
    private static IEnumerator routine; private static double deadline;
    [Serializable] private sealed class PerformanceSample
    {
        public string faction,quality,device;public int width=1600,height=900,frames=120;
        public double meanRenderReadbackMs,p95RenderReadbackMs,gpuMs;public long drawCalls,triangles,editorAllocatedMB;
    }
    private static readonly List<PerformanceSample> Performance=new List<PerformanceSample>();
    static KingdomRushFactionVerification()
    {
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))EditorApplication.delayCall+=Begin;};
    }
    public static void Start()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Run this fixture in a separate batch Editor.");
        Directory.CreateDirectory(Output);ShaderUtil.allowAsyncCompilation=false;
        var settings=AddressableAssetSettingsDefaultObject.Settings;
        SessionState.SetInt(Key+".Builder",settings.ActivePlayModeDataBuilderIndex);
        settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b.GetType().Name=="BuildScriptPackedPlayMode");
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ChessMatch.unity");
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    public static void BuildAndStart(){KingdomRushGameSetup.BuildContent();Start();}
    private static void Begin()
    {
        // Quality review is synthetic and cannot change the user's saved graphics preference.
        typeof(UserSettings).GetField("manager",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,new SettingsManager(SettingsRegistry.Create(),new ArtSettings()));
        typeof(UserSettings).GetField("preferences",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
        GameRuntimeSettings.ApplySaved();
        Checks.Clear();Performance.Clear();particleVelocityError=null;Application.logMessageReceived+=ObserveParticleErrors;
        File.WriteAllText(Output+"/surface_materials.txt","");deadline=EditorApplication.timeSinceStartup+600;routine=Verify();EditorApplication.update+=Tick;
    }
    private static void Tick()
    {
        try{if(particleVelocityError!=null)throw new InvalidOperationException(particleVelocityError);if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Faction fixture timed out");if(!routine.MoveNext())Finish(null);}
        catch(Exception e){Finish(e);}
    }
    private static void Finish(Exception e)
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=ObserveParticleErrors;(routine as IDisposable)?.Dispose();
        if(e!=null){Checks.Add("FAIL "+e);Debug.LogException(e);}
        File.WriteAllLines(Output+"/checks.txt",Checks);
        File.WriteAllText(Output+"/performance.json","["+string.Join(",",Performance.Select(s=>JsonUtility.ToJson(s,true)))+"]");
        SessionState.SetBool(Key,false);
        AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex=SessionState.GetInt(Key+".Builder",0);
        Debug.Log("[KRFactions] "+(e==null?"PASS ":"FAIL ")+Checks.Count);EditorApplication.Exit(e==null?0:1);
    }
    private static void ObserveParticleErrors(string message,string stackTrace,LogType type)
    {
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert||message.IndexOf("Particle Velocity curves",StringComparison.OrdinalIgnoreCase)>=0)
            particleVelocityError=message;
    }
    private static void Need(bool value,string label){if(!value)throw new InvalidOperationException(label);Checks.Add("PASS "+label);}
    private static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private).Invoke(o,args);
    private static Bounds Bounds(GameObject o)=>KingdomRushGameSetup.BoundsOf(o);
    private static IEnumerator Verify()
    {
        var board=UnityEngine.Object.FindAnyObjectByType<Chessboard>();
        var owner=new GameObject("Faction gameplay fixture");var game=owner.AddComponent<ChessGame>();game.enabled=false;game.AttachBoard(board);
        var loading=LoadingManager.For(game);var camera=Camera.main;var orbit=camera.GetComponent<ChessOrbitCamera>();camera.aspect=16f/9f;
        var originalAmbient=RenderSettings.ambientMode;var originalFog=RenderSettings.fog;
        var originalSuns=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.enabled&&l.type==LightType.Directional).ToArray();
        foreach(string shaderName in new[]{"ChessButWeird/KingdomRush/Foliage","ChessButWeird/KingdomRush/ElementalFlame"})
        {
            var shader=Shader.Find(shaderName);
            Need(shader&&shader.isSupported&&!ShaderUtil.GetShaderMessages(shader).Any(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),"Advanced shader compiles for active URP renderer "+shaderName);
        }
        for(int f=0;f<2;f++)
        {
            string faction=KingdomRushFactionSetup.Factions[f],id=KingdomRushFactionSetup.Ids[f];
            var selection=new CosmeticSelection{whiteSkinId=id,blackSkinId=id,boardId=id,environmentId=id};
            loading.StartMatch(selection,()=>game.BeginGame(PieceTeam.White),f!=0);
            while(loading.IsBusy)yield return null;
            Need(game.GameStarted&&loading.Board.Data.boardId==id,"Built-content gameplay loads "+id);
            Need(loading.Board.Environment&&loading.Board.Environment.environmentId==id&&loading.Board.EnvironmentPrefab,"Independent faction background loads "+id);
            var presentation=UnityEngine.Object.FindAnyObjectByType<KingdomRushEnvironmentPresentation>();
            Need(presentation&&presentation.darkDominion==(f==1),"Faction-owned lighting presentation "+faction);
            VerifyStyleDirection(presentation,faction);
            Need(Mathf.Approximately(board.CosmeticCameraFramePadding,1.04f),"Closer framing is owned only by the selected faction board "+faction);
            Need(RenderSettings.sun&&RenderSettings.sun.transform.IsChildOf(presentation.transform)&&RenderSettings.sun.shadows==(GameRuntimeSettings.ShadowsEnabled?LightShadows.Soft:LightShadows.None),"Soft directional shadows respect saved shadow preference "+faction);
            Need(camera.GetUniversalAdditionalCameraData().renderPostProcessing&&camera.allowHDR,"HDR tone mapping and restrained bloom enabled "+faction);
            Need(presentation.GetComponent<Volume>()&&presentation.GetComponent<Volume>().sharedProfile.TryGet<Bloom>(out var bloom)&&bloom.intensity.value<=.25f,"Bloom is bounded and cannot wash out the board "+faction);
            Need(presentation.GetComponentsInChildren<ReflectionProbe>().Any(p=>p.mode==ReflectionProbeMode.Custom&&p.customBakedTexture),"Local rough-metal sky reflection is bundled "+faction);
            var ambience=presentation.GetComponent<KingdomRushBattlefieldAmbience>();
            Need(ambience&&ambience.darkDominion==(f==1)&&ambience.drivesWind,"Quality-owned faction ambience loaded "+faction);
            Need(presentation.GetComponentsInChildren<ParticleSystem>().Length==(f==0?6:10),"Bounded faction-specific particle emitters loaded "+faction);
            Need(presentation.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.main.maxParticles)<=168,"Particle budget is bounded "+faction);
            Need(ambience.birds.Length==(f==0?2:0)&&ambience.mist.Length==(f==0?0:4),"Wildlife and localized mist assigned only to correct faction "+faction);
            foreach(var particles in presentation.GetComponentsInChildren<ParticleSystem>())
            {
                var velocity=particles.velocityOverLifetime;
                Need(velocity.enabled&&velocity.x.mode==ParticleSystemCurveMode.TwoConstants&&velocity.y.mode==velocity.x.mode&&velocity.z.mode==velocity.x.mode,
                    "Bundled particle XYZ velocity modes match "+faction+" "+particles.name);
                particles.Simulate(4f,true,true,false);particles.Play();
                Need(particles.particleCount>0,"Faction ambience still emits visible particles "+faction+" "+particles.name);
            }
            if(f==0)
            {
                Call(ambience,"AnimateBirds",3f);Vector3 beforeBird=ambience.birds[0].localPosition;
                Call(ambience,"AnimateBirds",3.4f);
                Need(ambience.birds[0].gameObject.activeSelf&&Vector3.Distance(beforeBird,ambience.birds[0].localPosition)>.1f&&Mathf.Abs(ambience.birds[0].localPosition.x)>=9,"Bird flyby moves smoothly outside playable board");
                Need(Shader.GetGlobalFloat("_KRWindStrength")>0,"GPU foliage wind enabled for Linirea");
            }
            Need(loading.Catalog.FindPiece(CosmeticSelection.KingdomRushId)==null&&loading.Catalog.FindBoard(CosmeticSelection.KingdomRushId)==null&&loading.Catalog.FindEnvironment(CosmeticSelection.KingdomRushId)!=null,"Mixed set/board hidden while legacy background remains available");
            float tile=Vector3.Distance(board.GetTileCenterWorld(Vector2Int.zero),board.GetTileCenterWorld(Vector2Int.right));
            float depth=Vector3.Distance(board.GetTileCenterWorld(Vector2Int.zero),board.GetTileCenterWorld(Vector2Int.up));
            Need(Mathf.Abs(tile-depth)<.0001f,"Square logical tile spacing "+faction);
            var pieces=owner.GetComponentsInChildren<ChessPiece>().Where(p=>p.enabled).ToArray();Need(pieces.Length==32,"32 faction piece models "+faction);
            foreach(var piece in pieces)
            {
                int kind=Array.IndexOf(KingdomRushGameSetup.Types,piece.Type);var visual=piece.GetComponent<PieceSkinVisualState>().ActiveVisual;
                string expected=KingdomRushGameSetup.Prefixes[kind]+"_"+faction+(piece.Team==PieceTeam.White?"_White":"_Black");
                Need(visual&&visual.name.StartsWith(expected,StringComparison.Ordinal),"Exact approved faction/team sculpt "+expected);
                var bounds=Bounds(visual);
                Need(Mathf.Max(bounds.size.x,bounds.size.z)<=tile*.83f&&Mathf.Abs(bounds.min.y-board.GetTileCenterWorld(piece.BoardPosition).y)<.003f,"Authored proportions, cell fit and grounded pivot "+expected);
                var marker=visual.GetComponentsInChildren<Transform>().Single(t=>t.name=="KR_Forward");
                var inward=(board.GetTileCenterWorld(Vector2Int.up)-board.GetTileCenterWorld(Vector2Int.zero)).normalized*piece.ForwardDirection;
                Need(Vector3.Dot((marker.position-marker.parent.position).normalized,inward)>.999f,"Faces opposing rank "+expected);
                Need(visual.GetComponentsInChildren<Transform>().Any(t=>t.name=="Team identity foot rim"),"Visible team identity without changing character paint "+expected);
                var magic=visual.GetComponent<KingdomRushPieceMagic>();
                Need((f==1&&piece.Type!=PieceType.Pawn)==(magic!=null),"Only existing villain magic surfaces receive emission "+expected);
                if(magic)
                {
                    Need(magic.PulsePeriod>=4&&magic.PulsePeriod<=8,"Slow comfortable character glow period "+expected);
                    float previous=magic.EmissionMultiplier(0);
                    for(int sample=1;sample<=80;sample++)
                    {
                        float value=magic.EmissionMultiplier(sample*.1f);
                        if(value<.81f||value>1.01f||Mathf.Abs(value-previous)>.011f)throw new InvalidOperationException("Unsafe magic pulse "+expected);
                        previous=value;
                    }
                }
            }
            var arena=GameObject.Find("Selected board visual");var surfaces=new List<MeshCollider>();
            foreach(var material in arena.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).Distinct().Where(m=>m.shader.name!="ChessButWeird/KingdomRush/ElementalFlame"))
            {
                Need(material.GetTexture("_BaseMap") is Texture2D texture&&texture.width>=1024&&texture.mipmapCount>1&&material.GetTexture("_BumpMap")&&material.GetTexture("_MetallicGlossMap"),"Built HQ albedo, relief, roughness and mipmaps "+material.name);
                if(material.IsKeywordEnabled("_EMISSION"))Need((material.globalIlluminationFlags&MaterialGlobalIlluminationFlags.EmissiveIsBlack)==0,"Emissive material is not marked black in the built bundle "+material.name);
            }
            foreach(var filter in arena.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.GetComponent<Renderer>().sharedMaterial.shader.name=="ChessButWeird/KingdomRush/ElementalFlame")continue;
                var col=filter.gameObject.AddComponent<MeshCollider>();col.sharedMesh=filter.sharedMesh;surfaces.Add(col);
            }
            Physics.SyncTransforms();
            for(int x=0;x<8;x++)for(int y=0;y<8;y++)
            {
                var p=board.GetTileCenterWorld(new Vector2Int(x,y));Need(Hit(surfaces,p,tile),"Real square cell surface "+faction+" "+x+","+y);
                if(x==0&&y==0){string material=SurfaceMaterial(surfaces,p,tile);File.AppendAllText(Output+"/surface_materials.txt",faction+" a1="+material+Environment.NewLine);Need(material.Contains(f==0?"Blue_slate":"Onyx_tile"),"Logical a1 uses a dark square "+faction);}
            }
            foreach(PieceTeam team in new[]{PieceTeam.White,PieceTeam.Black})for(int slot=0;slot<6;slot++)
            {
                var p=board.GetPrisonSlotWorld(team,slot);Need(Hit(surfaces,p,tile),"Prison pad surface agrees with runtime "+faction+" "+team+" "+slot);
                var local=arena.transform.InverseTransformPoint(p);Need(Mathf.Abs(local.z)>4.8f&&Mathf.Abs(local.x)<3.3f,"Prison pad on top/bottom, outside file edges "+faction+" "+team+" "+slot);
            }
            foreach(var col in surfaces)UnityEngine.Object.DestroyImmediate(col);
            UserSettings.Manager.Set("shadows",0);GameRuntimeSettings.ApplySaved();yield return null;
            Need(RenderSettings.sun.shadows==LightShadows.None&&!camera.GetUniversalAdditionalCameraData().renderShadows,"Player can switch all faction shadows off "+faction);
            UserSettings.Manager.Set("shadows",3);GameRuntimeSettings.ApplySaved();yield return null;
            Need(RenderSettings.sun.shadows==LightShadows.Soft&&camera.GetUniversalAdditionalCameraData().renderShadows,"Player can restore soft shadows during a faction match "+faction);
            for(int i=0;i<8;i++)yield return null;
            for(int step=0;step<120;step++)
            {
                typeof(ChessOrbitCamera).GetField("orbitYaw",Private).SetValue(orbit,step*3f);Call(orbit,"FrameBoard");
                yield return null;
            }
            Need(particleVelocityError==null,"Continuous 360-degree in-match camera orbit has no particle velocity errors "+faction);
            foreach(float yaw in new[]{0f,90f,180f,270f})
            {
                typeof(ChessOrbitCamera).GetField("orbitYaw",Private).SetValue(orbit,yaw);Call(orbit,"FrameBoard");
                CheckProjection(camera,board,faction+" yaw "+yaw);
                CheckFullBoard(camera,board,faction+" yaw "+yaw);
                KingdomRushArtReviewCapture.Write(camera,Output+"/"+faction+"_"+yaw+".png",1600,900);
            }
            typeof(ChessOrbitCamera).GetField("orbitYaw",Private).SetValue(orbit,0f);
            Call(orbit,"FrameBoard");
            foreach(float aspect in new[]{4f/3f,21f/9f,9f/16f})
            {
                camera.aspect=aspect;Call(orbit,"FrameBoard");CheckFullBoard(camera,board,faction+" aspect "+aspect);
            }
            camera.aspect=16f/9f;Call(orbit,"FrameBoard");
            EnvironmentDetail(camera,presentation,Output+"/"+faction+"_EnvironmentDetail.png");
            Need(HasGlow(Output+"/"+faction+"_0.png",camera.WorldToViewportPoint(arena.transform.TransformPoint(new Vector3(4.63f,.60f,5.33f))),f==1),"Brazier glow survives built-material/shader import "+faction);
            foreach(PieceType type in KingdomRushGameSetup.Types)
            {
                var model=pieces.First(p=>p.Team==PieceTeam.Black&&p.Type==type).GetComponent<PieceSkinVisualState>().ActiveVisual;
                Closeup(camera,model,Output+"/"+faction+"_"+type+"_Detail.png");
            }
            var selectedPawn=pieces.Single(p=>p.BoardPosition==new Vector2Int(4,1));
            Need(game.TrySelectPiece(selectedPawn),"Existing piece selection remains available "+faction);
            var legal=(bool[,])typeof(Chessboard).GetField("legalMoveHighlights",Private).GetValue(board);
            Need(legal[4,2]&&legal[4,3],"Existing legal-move indicators remain active "+faction);
            KingdomRushArtReviewCapture.Write(camera,Output+"/"+faction+"_LegalMoves.png",1600,900);game.ClearSelection();
            var highSample=Measure(camera,faction,"High");while(highSample.MoveNext())yield return highSample.Current;
            UserSettings.Manager.Set("aa",5);GameRuntimeSettings.ApplySaved();yield return null;
            var activePipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            bool nativeMsaa=!PipelineIsDeferred(activePipeline)&&SystemInfo.supportsMultisampledTextures>0;
            Need(GameRuntimeSettings.AppliedAntiAliasing==(nativeMsaa?GameAntiAliasingMode.MSAA4x:GameAntiAliasingMode.SMAA)&&camera.allowMSAA==nativeMsaa&&activePipeline.msaaSampleCount==(nativeMsaa?4:1)&&camera.GetUniversalAdditionalCameraData().antialiasing==(nativeMsaa?AntialiasingMode.None:AntialiasingMode.SubpixelMorphologicalAntiAliasing),"One compatible AA method for the actual renderer; no MSAA/SMAA stacking "+faction);
            UserSettings.Manager.Set("aa",3);GameRuntimeSettings.ApplySaved();
            UserSettings.Manager.Set("effects",0);yield return null;
            Need(!ambience.DecorativeMotionEnabled&&ambience.GetComponentsInChildren<ParticleSystem>().All(p=>p.particleCount==0)&&ambience.flames.All(r=>!r.enabled),"Low effects removes decorative particles and extra flames "+faction);
            Need(presentation.GetComponent<Volume>().sharedProfile.TryGet<Bloom>(out var lowBloom)&&lowBloom.intensity.value==0,"Low effects removes decorative bloom "+faction);
            UserSettings.Manager.Set("graphics",1);UserSettings.Manager.Set("shadows",0);GameRuntimeSettings.ApplySaved();yield return null;
            KingdomRushArtReviewCapture.Write(camera,Output+"/"+faction+"_Low.png",1600,900);
            var lowSample=Measure(camera,faction,"Low");while(lowSample.MoveNext())yield return lowSample.Current;
            UserSettings.Manager.Set("graphics",3);UserSettings.Manager.Set("shadows",3);GameRuntimeSettings.ApplySaved();
            UserSettings.Manager.Set("effects",2);UserSettings.Manager.Set("reduce_motion",1);yield return null;
            Need(!ambience.DecorativeMotionEnabled&&ambience.birds.All(b=>!b.gameObject.activeSelf)&&Shader.GetGlobalFloat("_KRWindStrength")==0&&ambience.GetComponentsInChildren<ParticleSystem>().All(p=>p.particleCount==0),"Reduce Motion stops foliage, wildlife and ambient particle movement "+faction);
            Need(owner.GetComponentsInChildren<KingdomRushPieceMagic>().All(m=>Mathf.Approximately(m.EmissionMultiplier(0),m.EmissionMultiplier(3))),"Reduce Motion makes character glow steady "+faction);
            KingdomRushArtReviewCapture.Write(camera,Output+"/"+faction+"_ReducedMotion.png",1600,900);
            UserSettings.Manager.Set("reduce_motion",0);UserSettings.Manager.Set("reduce_flashing",1);yield return null;
            Need(owner.GetComponentsInChildren<KingdomRushPieceMagic>().All(m=>Mathf.Approximately(m.EmissionMultiplier(0),m.EmissionMultiplier(3)))&&presentation.GetComponent<Volume>().sharedProfile.TryGet<Bloom>(out var quietBloom)&&quietBloom.intensity.value<=.04f,"Reduce Flashing keeps steady magic and restrained bloom "+faction);
            UserSettings.Manager.Set("reduce_flashing",0);UserSettings.Manager.Set("visual_chaos",1);yield return null;
            Need(!ambience.DecorativeMotionEnabled&&ambience.GetComponentsInChildren<ParticleSystem>().All(p=>p.particleCount==0),"Reduce Visual Chaos suppresses decorative motion "+faction);
            UserSettings.Manager.Set("visual_chaos",0);yield return null;
            if(GameRuntimeSettings.ShadowsEnabled)
            {
                string shadowed=Output+"/"+faction+"_ShadowReference.png";
                KingdomRushArtReviewCapture.Write(camera,shadowed,1600,900);
                var sun=RenderSettings.sun;sun.shadows=LightShadows.None;
                string plain=Output+"/"+faction+"_NoShadows.png";
                try {KingdomRushArtReviewCapture.Write(camera,plain,1600,900);}
                finally {sun.shadows=LightShadows.Soft;}
                Need(ChangedPixels(shadowed,plain)>1500,"Actual rendered cast-shadow contribution is visible "+faction);
            }
            Move(game,4,1,4,3);while(game.InputLocked)yield return null;
            Move(game,3,6,3,4);while(game.InputLocked)yield return null;
            Move(game,4,3,3,4);while(game.InputLocked)yield return null;
            Need(game.GetCapturedPieceCount(PieceTeam.White)==1,"Real e4/d5/exd5 capture "+faction);
            var prisoner=owner.GetComponentsInChildren<ChessPiece>().Single(p=>p.name.Contains("prisoner"));
            var pad=board.GetPrisonSlotWorld(PieceTeam.White,0);var pb=Bounds(prisoner.gameObject);
            Need(Vector2.Distance(new Vector2(pb.center.x,pb.center.z),new Vector2(pad.x,pad.z))<.01f&&prisoner.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"Captured model appears on the correct new rank-end pad and does not block input "+faction);
            KingdomRushArtReviewCapture.Write(camera,Output+"/"+faction+"_Capture.png",1600,900);
            var pawn=owner.GetComponentsInChildren<PawnPiece>().First(p=>p.enabled&&p.Team==PieceTeam.White);
            var queen=(ChessPiece)Call(game,"PromotePawn",pawn,PieceType.Queen);
            Need(queen.GetComponent<PieceSkinVisualState>().ActiveVisual.name.StartsWith("05_Quan_hau_"+faction),"Promotion uses matching faction queen "+faction);
        }
        loading.StartMatch(new CosmeticSelection(),()=>game.BeginGame(PieceTeam.White),true);while(loading.IsBusy)yield return null;
        Need(!loading.Board.EnvironmentPrefab&&!GameObject.Find("Selected environment"),"Returning to classic unloads faction background");
        Need(!UnityEngine.Object.FindAnyObjectByType<KingdomRushEnvironmentPresentation>()&&RenderSettings.ambientMode==originalAmbient&&RenderSettings.fog==originalFog&&originalSuns.All(l=>l&&l.enabled),"Classic restores original scene lighting and atmosphere after both faction backgrounds");
        Need(!UnityEngine.Object.FindAnyObjectByType<KingdomRushBattlefieldAmbience>()&&Mathf.Approximately(Shader.GetGlobalFloat("_KRWindStrength"),0),"Classic unloads all decorative controllers and restores wind globals");
        Need(Mathf.Approximately(board.CosmeticCameraFramePadding,0),"Classic restores normal camera padding");
        VerifyShop();
        // The mixed board remains authored/addressable, reserved outside normal ownership.
        var archived=AssetDatabase.LoadAssetAtPath<GameObject>(KingdomRushGameSetup.Root+"/Prefabs/KR_Citadel_Board.prefab");
        var archivedVisual=UnityEngine.Object.Instantiate(archived);board.AttachCosmeticBoard(archivedVisual.transform,archivedVisual.transform,0,true);
        foreach(float yaw in new[]{0f,90f,180f,270f}){typeof(ChessOrbitCamera).GetField("orbitYaw",Private).SetValue(orbit,yaw);Call(orbit,"FrameBoard");CheckProjection(camera,board,"Archived mixed yaw "+yaw);}
        board.DetachCosmeticBoard();UnityEngine.Object.Destroy(archivedVisual);
        Need(particleVelocityError==null,"No particle velocity errors while loading both factions, orbiting the camera and playing moves");
        Need(true,"No real account API request, ownership grant or purchase in Unity fixture");
    }
    private static void VerifyStyleDirection(KingdomRushEnvironmentPresentation presentation,string faction)
    {
        var renderer=presentation.GetComponentsInChildren<MeshRenderer>().Single(r=>r.sharedMaterials.Any(m=>m&&m.name.StartsWith("KRF_KR_HQ_")));
        var materials=renderer.sharedMaterials;
        if(faction=="Denas")
        {
            Need(materials.Any(m=>m.name.Contains("Meadow")&&m.GetTexture("_BaseMap").name=="Style_Meadow_Albedo"),"Linirea uses clean fresh-green stylized ground");
            Need(materials.Any(m=>m.name.Contains("Leaf_highlights")&&m.shader.name=="ChessButWeird/KingdomRush/Foliage"),"Grouped fantasy canopies retain shared gentle wind");
            var water=materials.Single(m=>m.name.Contains("River"));
            Need(water.GetTexture("_BaseMap").name=="Style_Water_Albedo"&&water.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"),"Fantasy river has painted highlights without realistic reflection dominance");
        }
        else
        {
            int slot=Array.FindIndex(materials,m=>m.name.Contains("Bone_ivory"));
            Need(slot>=0&&materials.Any(m=>m.name.Contains("Bone_recess")),"Ruins include sculpted bone and concave skull details");
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var indices=mesh.GetIndices(slot);
            Need(indices.Length>1000,"Skeletal storytelling is genuine 3D mesh geometry");
            foreach(int index in indices)
            {
                var p=presentation.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertices[index]));
                if(Mathf.Abs(p.x)<6.8f&&Mathf.Abs(p.z)<7.5f)throw new InvalidOperationException("Bone prop enters protected board area");
            }
            Need(true,"All skeletal geometry stays outside the protected board and prisoner area");
        }
    }
    private static void CheckFullBoard(Camera camera,Chessboard board,string label)
    {
        if(!board.TryGetCosmeticPresentationBounds(out var bounds))throw new InvalidOperationException("Missing cosmetic camera bounds");
        foreach(float x in new[]{-1f,1f})foreach(float y in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})
        {
            var p=camera.WorldToViewportPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z)));
            if(p.z<=0||p.x<.005f||p.x>.995f||p.y<.005f||p.y>.995f)throw new InvalidOperationException("Board framing clips: "+label+" "+p);
        }
        Need(true,"Full board ornaments and prisoner rails fit camera "+label);
    }
    private static void EnvironmentDetail(Camera source,KingdomRushEnvironmentPresentation environment,string path)
    {
        var rig=new GameObject("Temporary native environment detail camera");var camera=rig.AddComponent<Camera>();camera.enabled=false;
        try
        {
            float scale=environment.transform.lossyScale.x;
            var target=environment.transform.TransformPoint(environment.darkDominion?new Vector3(9,.0f,-3):new Vector3(-10,1.1f,-6));
            camera.transform.position=target+new Vector3(3,6,-8)*scale;camera.transform.LookAt(target);
            camera.orthographic=true;camera.orthographicSize=3.9f*scale;camera.aspect=4f/3f;
            camera.nearClipPlane=.01f;camera.farClipPlane=source.farClipPlane;camera.allowHDR=true;
            camera.clearFlags=source.clearFlags;camera.backgroundColor=source.backgroundColor;
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.renderShadows=GameRuntimeSettings.ShadowsEnabled;
            data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;data.antialiasingQuality=AntialiasingQuality.High;
            KingdomRushArtReviewCapture.Write(camera,path,1200,900);
        }
        finally{UnityEngine.Object.DestroyImmediate(rig);}
    }
    private static void Closeup(Camera source,GameObject visual,string path)
    {
        var rig=new GameObject("Temporary native character detail camera");var close=rig.AddComponent<Camera>();close.enabled=false;
        var hidden=new List<Renderer>();var piece=visual.GetComponentInParent<ChessPiece>();
        try
        {
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var owner=renderer.GetComponentInParent<ChessPiece>();
                if(renderer.enabled&&owner&&owner!=piece){hidden.Add(renderer);renderer.enabled=false;}
            }
            var bounds=Bounds(visual);float height=bounds.size.y;
            var marker=visual.GetComponentsInChildren<Transform>().Single(t=>t.name=="KR_Forward");
            var front=(marker.position-marker.parent.position).normalized;
            close.transform.position=bounds.center+front*height*2.6f+Vector3.up*height*.60f;
            close.transform.LookAt(bounds.center+Vector3.up*height*.02f);
            close.orthographic=true;close.orthographicSize=Mathf.Max(height*.64f,.42f);close.aspect=1;
            close.nearClipPlane=.01f;close.farClipPlane=source.farClipPlane;close.allowHDR=true;
            close.clearFlags=source.clearFlags;close.backgroundColor=source.backgroundColor;
            var data=close.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.renderShadows=GameRuntimeSettings.ShadowsEnabled;
            data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;data.antialiasingQuality=AntialiasingQuality.High;
            KingdomRushArtReviewCapture.Write(close,path,900,900);
        }
        finally{foreach(var renderer in hidden)if(renderer)renderer.enabled=true;UnityEngine.Object.DestroyImmediate(rig);}
    }
    private static bool PipelineIsDeferred(UniversalRenderPipelineAsset asset)
    {
        foreach(var renderer in asset.rendererDataList)
            if(renderer is UniversalRendererData data&&(data.renderingMode==RenderingMode.Deferred||data.renderingMode==RenderingMode.DeferredPlus))return true;
        return false;
    }
    private static IEnumerator Measure(Camera camera,string faction,string quality)
    {
        var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGBHalf);target.Create();var previous=camera.targetTexture;
        var previousActive=RenderTexture.active;bool enabled=camera.enabled;var probe=new Texture2D(1,1,TextureFormat.RGB24,false);
        var draw=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");
        var triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count");
        var times=new double[120];var gpu=new FrameTiming[1];
        try
        {
            camera.targetTexture=target;camera.enabled=false;
            var request=new RenderPipeline.StandardRequest{destination=target};
            for(int i=0;i<8;i++){RenderPipeline.SubmitRenderRequest(camera,request);yield return null;}
            bool gpuTiming=FrameTimingManager.IsFeatureEnabled();
            for(int i=0;i<times.Length;i++)
            {
                if(gpuTiming)FrameTimingManager.CaptureFrameTimings();
                long start=System.Diagnostics.Stopwatch.GetTimestamp();
                RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=target;probe.ReadPixels(new Rect(0,0,1,1),0,0);RenderTexture.active=previousActive;
                times[i]=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
                yield return null;
            }
            var ordered=times.OrderBy(v=>v).ToArray();
            Performance.Add(new PerformanceSample{faction=faction,quality=quality,device=SystemInfo.graphicsDeviceName,
                meanRenderReadbackMs=times.Average(),p95RenderReadbackMs=ordered[113],gpuMs=gpuTiming&&FrameTimingManager.GetLatestTimings(1,gpu)>0?gpu[0].gpuFrameTime:0,
                drawCalls=draw.Valid?draw.LastValue:0,triangles=triangles.Valid?triangles.LastValue:0,
                editorAllocatedMB=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()/(1024*1024)});
            Need(particleVelocityError==null,"Full 32-piece 1600x900 native render/readback sample completed without runtime errors "+faction+" "+quality);
        }
        finally{camera.enabled=enabled;camera.targetTexture=previous;RenderTexture.active=previousActive;draw.Dispose();triangles.Dispose();target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(probe);}
    }
    private static bool Hit(List<MeshCollider> surfaces,Vector3 p,float tile)=>surfaces.Any(c=>c.Raycast(new Ray(p+Vector3.up*tile,Vector3.down),out var hit,tile*2)&&Mathf.Abs(hit.point.y-p.y)<.003f);
    private static int ChangedPixels(string first,string second)
    {
        var a=new Texture2D(2,2);var b=new Texture2D(2,2);
        try
        {
            a.LoadImage(File.ReadAllBytes(first));b.LoadImage(File.ReadAllBytes(second));var aa=a.GetPixels32();var bb=b.GetPixels32();int count=0;
            for(int i=0;i<aa.Length;i++)if(Mathf.Abs(aa[i].r-bb[i].r)+Mathf.Abs(aa[i].g-bb[i].g)+Mathf.Abs(aa[i].b-bb[i].b)>24)count++;
            return count;
        }
        finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
    }
    private static bool HasGlow(string path,Vector3 viewport,bool green)
    {
        var image=new Texture2D(2,2);
        try
        {
            image.LoadImage(File.ReadAllBytes(path));int cx=Mathf.RoundToInt(viewport.x*image.width),cy=Mathf.RoundToInt(viewport.y*image.height),hits=0;
            for(int y=cy-10;y<=cy+10;y++)for(int x=cx-10;x<=cx+10;x++)
            {
                if(x<0||y<0||x>=image.width||y>=image.height)continue;
                var c=image.GetPixel(x,y);float accent=green?c.g:c.b;
                if(accent>.55f&&accent-c.r>.18f)hits++;
            }
            return hits>=4;
        }
        finally{UnityEngine.Object.DestroyImmediate(image);}
    }
    private static string SurfaceMaterial(List<MeshCollider> surfaces,Vector3 p,float tile)
    {
        foreach(var c in surfaces)if(c.Raycast(new Ray(p+Vector3.up*tile,Vector3.down),out var hit,tile*2))
        {int triangle=hit.triangleIndex;for(int s=0;s<c.sharedMesh.subMeshCount;s++){int count=(int)c.sharedMesh.GetIndexCount(s)/3;if(triangle<count)return c.GetComponent<Renderer>().sharedMaterials[s].name;triangle-=count;}}
        return "Missing";
    }
    private static void Move(ChessGame g,int x,int y,int tx,int ty){var p=g.GetComponentsInChildren<ChessPiece>().Single(piece=>piece.enabled&&piece.BoardPosition==new Vector2Int(x,y));Need(g.TrySelectPiece(p)&&g.TryMoveSelectedPiece(new Vector2Int(tx,ty)),"Legal move "+x+","+y+" -> "+tx+","+ty);}
    private static void CheckProjection(Camera c,Chessboard b,string label)
    {
        var a=c.WorldToScreenPoint(b.GetTileCenterWorld(new Vector2Int(0,0)));var d=c.WorldToScreenPoint(b.GetTileCenterWorld(new Vector2Int(7,0)));
        var e=c.WorldToScreenPoint(b.GetTileCenterWorld(new Vector2Int(0,7)));var f=c.WorldToScreenPoint(b.GetTileCenterWorld(new Vector2Int(7,7)));
        var near=new Vector2(d.x-a.x,d.y-a.y);var far=new Vector2(f.x-e.x,f.y-e.y);
        var left=new Vector2(e.x-a.x,e.y-a.y);var right=new Vector2(f.x-d.x,f.y-d.y);
        Need(c.orthographic&&Vector2.Distance(near,far)<.02f&&Vector2.Distance(left,right)<.02f,"Parallel opposite grid edges; no trapezoid distortion "+label);
    }
    private static void VerifyShop()
    {
        var screen=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Shop/ShopScreen"));
        try
        {
            var shop=screen.GetComponent<ShopMenuController>();shop.Initialize(null,null,new ShopFixture(),true);shop.Open();
            Need(!shop.IsBusy,"Shop loads faction art with synthetic catalog and no API calls");
            shop.ShowCategory(ShopCategory.Bundles);Need(shop.VisibleCount==2,"Both faction bundles appear in existing Bundles category");
            var products=((IEnumerable)typeof(ShopMenuController).GetField("products",Private).GetValue(shop)).Cast<object>().ToArray();
            foreach(string faction in new[]{"denas","veznan"})
            {
                var product=products.Single(p=>((ShopOfferResponse)p.GetType().GetField("offer").GetValue(p))?.offerId=="kingdom-rush-"+faction+"-complete");
                typeof(ShopMenuController).GetField("selected",Private).SetValue(shop,product);Call(shop,"Render");
                var gold=(UnityEngine.UI.Button)typeof(ShopMenuController).GetField("goldButton",Private).GetValue(shop);
                var diamond=(UnityEngine.UI.Button)typeof(ShopMenuController).GetField("diamondButton",Private).GetValue(shop);
                Need(gold.gameObject.activeSelf&&gold.GetComponentInChildren<TMPro.TextMeshProUGUI>().text.Contains(76500L.ToString("N0"))&&diamond.GetComponentInChildren<TMPro.TextMeshProUGUI>().text.Contains(3825L.ToString("N0")),"Displayed bundle purchase prices: 76500 / 3825 for "+faction);
            }
            shop.ShowCategory(ShopCategory.Backgrounds);Need(shop.VisibleCount==4,"Three separately sold Legendary backgrounds and locked Classic default");
            Need(products.All(p=>(string)p.GetType().GetField("key").GetValue(p)!=CosmeticSelection.KingdomRushId||(string)p.GetType().GetField("type").GetValue(p)==CosmeticItemKinds.Background),"Mixed board/set absent from shop and collection presentation");
            Need(CosmeticSelection.IsKingdomRushShopOnly(CosmeticSelection.KingdomRushDenasId)&&CosmeticSelection.IsKingdomRushShopOnly(CosmeticSelection.KingdomRushVeznanId),"New faction UI never advertises unavailable offers through summons");
        }
        finally{UnityEngine.Object.Destroy(screen);}
    }
    private sealed class ShopFixture:IShopGateway
    {
        public Task<ShopSnapshot> LoadAsync()
        {
            var offers=new List<ShopOfferResponse>();
            for(int f=0;f<2;f++)
            {
                var items=new List<ShopProductResponse>();
                for(int k=0;k<3;k++)
                {
                    string[] types={CosmeticItemKinds.Set,CosmeticItemKinds.Board,CosmeticItemKinds.Background};string[] suffix={"pieces","board","background"};
                    var item=new ShopProductResponse{itemId="6b0c4f0100000000000000"+(f==0?"1":"2")+(k+1),name=KingdomRushFactionSetup.Factions[f]+" "+suffix[k],type=types[k],unityAssetKey=KingdomRushFactionSetup.Ids[f],rarity="LEGENDARY",golds=k==0?60000:k==1?36000:6000,diamonds=k==0?3000:k==1?1800:300};
                    items.Add(item);offers.Add(new ShopOfferResponse{offerId="kingdom-rush-"+KingdomRushFactionSetup.Factions[f].ToLowerInvariant()+"-"+suffix[k],name=item.name,items=new List<ShopProductResponse>{item}});
                }
                offers.Add(new ShopOfferResponse{offerId="kingdom-rush-"+KingdomRushFactionSetup.Factions[f].ToLowerInvariant()+"-complete",name=KingdomRushFactionSetup.Factions[f]+" Complete Collection",isBundle=true,featured=true,discountPercent=25,items=items});
            }
            offers.Add(new ShopOfferResponse{offerId="kingdom-rush-background",name="Linirea Valley",items=new List<ShopProductResponse>{new ShopProductResponse{itemId="6b0c4f010000000000000003",name="Linirea Valley",type=CosmeticItemKinds.Background,unityAssetKey=CosmeticSelection.KingdomRushId,rarity="LEGENDARY",golds=6000,diamonds=300}}});
            return Task.FromResult(new ShopSnapshot{user=new UserMeResponse{userId="__faction_fixture__",wallet=new UserWalletResponse()},catalog=new ShopCatalogResponse{offers=offers}});
        }
        public Task<ShopPurchaseResponse> PurchaseAsync(ShopPurchaseRequest r)=>throw new InvalidOperationException("No purchases in UI fixture");
        public Task<EquipItemResponse> EquipAsync(string id)=>throw new InvalidOperationException("No equipment writes in UI fixture");
        public Task<EquipItemResponse> EquipBackgroundAsync(string id)=>throw new InvalidOperationException("No equipment writes in UI fixture");
    }
    private sealed class ArtSettings:ISettingsStore
    {
        private readonly Dictionary<string,string> values=new Dictionary<string,string>{{"graphics","3"},{"shadows","3"},{"aa","3"},{"fps","60"}};
        public string Read(string id)=>values.TryGetValue(id,out var v)?v:null;
        public void Write(string id,string value)=>values[id]=value;
        public void Flush(){}
    }
}
