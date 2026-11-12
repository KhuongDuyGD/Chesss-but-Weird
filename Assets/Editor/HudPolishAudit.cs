using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChessButWeird.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Local mesh/shadow render audit. No match, network session or account is started.</summary>
public static class HudPolishAudit
{
    private const string Output="Logs/hud-polish";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class MemoryStore:ISettingsStore
    {public string Read(string id)=>null;public void Write(string id,string value){}public void Flush(){}}
    private static void Check(bool valid,string message){if(!valid)throw new InvalidOperationException(message);}
    public static void Run()
    {
        Check(Application.isBatchMode,"Run this audit in a separate batch Editor.");
        Directory.CreateDirectory(Output);
        var managerField=typeof(UserSettings).GetField("manager",BindingFlags.Static|BindingFlags.NonPublic);
        var preferencesField=typeof(UserSettings).GetField("preferences",BindingFlags.Static|BindingFlags.NonPublic);
        var previousManager=managerField.GetValue(null);var previousPreferences=preferencesField.GetValue(null);
        var previousPipeline=QualitySettings.renderPipeline;
        string qualityPath="ProjectSettings/QualitySettings.asset";
        byte[] qualityFile=File.ReadAllBytes(qualityPath);
        float previousDistance=QualitySettings.shadowDistance,previousSplit=QualitySettings.shadowCascade2Split;
        var previousResolution=QualitySettings.shadowResolution;var previousShadows=QualitySettings.shadows;
        int previousCascades=QualitySettings.shadowCascades,previousAa=QualitySettings.antiAliasing;
        var pipeline=UnityEngine.Object.Instantiate(GraphicsSettings.currentRenderPipeline);
        QualitySettings.renderPipeline=pipeline;
        var report=new List<string>();
        try
        {
            var settings=new SettingsManager(SettingsRegistry.Create(),new MemoryStore());
            managerField.SetValue(null,settings);preferencesField.SetValue(null,null);
            settings.Set("shadows",3);settings.Set("effects",0);
            foreach(string scenePath in new[]{"Assets/Scenes/ChessClassic.unity","Assets/Game/Scenes/ChessMatch.unity"})
            {
                var scene=EditorSceneManager.OpenScene(scenePath);
                var roots=scene.GetRootGameObjects();
                var board=roots.SelectMany(r=>r.GetComponentsInChildren<Chessboard>(true)).Single();
                var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Single(c=>c.CompareTag("MainCamera"));
                var sun=roots.SelectMany(r=>r.GetComponentsInChildren<Light>(true)).First(l=>l.type==LightType.Directional);
                RenderSettings.sun=sun;
                typeof(Chessboard).GetMethod("Awake",Private).Invoke(board,null);
                var data=AssetDatabase.LoadAssetAtPath<BoardSkinData>("Assets/Game/Data/Boards/TazjiCelestial.asset");
                using(var assets=new AssetLoader())
                {
                    var manager=new BoardSkinManager(assets);
                    typeof(BoardSkinManager).GetProperty("Data").SetValue(manager,data);
                    typeof(BoardSkinManager).GetProperty("BoardPrefab").SetValue(manager,AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(data.boardPrefab.AssetGUID)));
                    var wrapper=new GameObject("Celestial audit wrapper");
                    var model=manager.InstantiateBoard(wrapper.transform);
                    foreach(var animation in model.GetComponentsInChildren<Animation>())if(animation.clip)animation.clip.SampleAnimation(model,0);
                    board.AttachCosmeticBoard(wrapper.transform,model.transform,data.prisonLayoutYaw);
                    var presentation=model.GetComponent<LegacyBoardPresentation>();
                    // Match the runtime presentation, whose OnEnable is inactive in an edit-mode audit.
                    var volume=model.AddComponent<Volume>();volume.isGlobal=true;volume.priority=15;
                    var profile=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=profile;
                    profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
                    typeof(LegacyBoardPresentation).GetField("profile",Private).SetValue(presentation,profile);
                    camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
                    float tile=Vector3.Distance(board.GetTileCenterWorld(Vector2Int.zero),board.GetTileCenterWorld(Vector2Int.right));
                    var centre=board.GetBoardCenterWorld();
                    camera.GetComponent<ChessOrbitCamera>().enabled=false;
                    camera.orthographic=true;camera.orthographicSize=tile*6.5f;camera.aspect=1.6f;camera.nearClipPlane=.03f;camera.farClipPlane=80;
                    camera.transform.position=centre+new Vector3(0,14,-7)*tile;camera.transform.LookAt(centre);
                    camera.ResetProjectionMatrix();
                    foreach(string set in new[]{"TazjiCrystal","TazjiLowPoly","TazjiWoodland"})
                    {
                        var pieces=CreatePieces(board,set,tile);
                        var renderers=model.GetComponentsInChildren<Renderer>().Concat(pieces.GetComponentsInChildren<Renderer>()).ToArray();
                        foreach(int tier in new[]{0,1,2,3})
                        {
                            settings.Set("shadows",tier);GameRuntimeSettings.RefreshMatchCameras();
                            typeof(LegacyBoardPresentation).GetMethod("Refresh",Private).Invoke(presentation,new object[]{null});
                            Check(sun.shadows==(tier==0?LightShadows.None:tier==1?LightShadows.Hard:LightShadows.Soft),"Shadow tier did not reach Sun");
                            Check(!sun.GetUniversalAdditionalLightData().usePipelineSettings,"Light still ignores its custom bias");
                            foreach(var r in renderers.Where(r=>r is MeshRenderer||r is SkinnedMeshRenderer))
                                Check(r.receiveShadows==(tier>0)&&r.shadowCastingMode==(tier>0?ShadowCastingMode.On:ShadowCastingMode.Off),"Model ignored shadow setting: "+r.name);
                            if((tier==2||tier==0&&set=="TazjiCrystal")&&SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
                                KingdomRushArtReviewCapture.Write(camera,Output+"/"+scene.name+"-"+set+(tier==0?"-off":"")+".png",1600,1000);
                            report.Add("PASS "+scene.name+" / "+set+" / shadow tier "+tier+" / scene Sun, custom bias, board and all 32 pieces");
                        }
                        UnityEngine.Object.DestroyImmediate(pieces);
                    }
                    var surface=model.GetComponentsInChildren<Renderer>().First(r=>r.name=="CL_Chess_Board");
                    Check(surface.sharedMaterials.All(m=>!m.IsKeywordEnabled("_EMISSION")),"Playable squares still wash out piece shadows");
                    presentation.enabled=false;
                    typeof(LegacyBoardPresentation).GetMethod("OnDisable",Private).Invoke(presentation,null);
                    Check(surface.sharedMaterials.Any(m=>m.IsKeywordEnabled("_EMISSION")),"Disabling the presentation did not restore imported materials");
                    UnityEngine.Object.DestroyImmediate(wrapper);
                }
            }
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)CaptureMenus();
            report.Add("GPU: "+SystemInfo.graphicsDeviceType);
            File.WriteAllLines(Output+"/checks.txt",report);
            Debug.Log("[HudPolish] PASS "+(report.Count-1)+" shadow combinations; six mesh renders. "+SystemInfo.graphicsDeviceType);
        }
        catch(Exception error){File.WriteAllText(Output+"/checks.txt","FAIL "+error);throw;}
        finally
        {
            managerField.SetValue(null,previousManager);preferencesField.SetValue(null,previousPreferences);
            QualitySettings.renderPipeline=previousPipeline;UnityEngine.Object.DestroyImmediate(pipeline);
            QualitySettings.shadowDistance=previousDistance;QualitySettings.shadowCascade2Split=previousSplit;
            QualitySettings.shadowResolution=previousResolution;QualitySettings.shadows=previousShadows;
            QualitySettings.shadowCascades=previousCascades;QualitySettings.antiAliasing=previousAa;
            File.WriteAllBytes(qualityPath,qualityFile);
        }
    }
    private static void CaptureMenus()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Menu preview camera",typeof(Camera)).GetComponent<Camera>();
        camera.orthographic=true;camera.orthographicSize=540;camera.aspect=16f/9;camera.transform.position=new Vector3(0,0,-1000);
        camera.nearClipPlane=1;camera.farClipPlane=2000;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=SketchbookUI.Paper;
        var canvas=new GameObject("Menu preview",typeof(RectTransform),typeof(Canvas));canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        ((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);
        var page=new GameObject("Menu",typeof(RectTransform));page.transform.SetParent(canvas.transform,false);((RectTransform)page.transform).sizeDelta=new Vector2(1920,1080);
        var main=page.AddComponent<MainMenuPresentation>();main.Build(MainMenuLogoOriginal.LoadSprite(),()=>{},()=>{},()=>{});
        Canvas.ForceUpdateCanvases();
        var logout=page.GetComponentsInChildren<Button>().Single(b=>b.name=="Logout");var quit=page.GetComponentsInChildren<Button>().Single(b=>b.name=="Quit Game");
        Check(((RectTransform)logout.transform).anchoredPosition.x==1726,"Logout did not return to its original position");
        Check(((RectTransform)quit.transform).rect.width==48&&quit.GetComponentInChildren<MatchHudIconGraphic>(),"Quit did not become an edge icon");
        KingdomRushArtReviewCapture.Write(camera,Output+"/main-menu.png",1920,1080);
        UnityEngine.Object.DestroyImmediate(page);
        page=new GameObject("Mode hub",typeof(RectTransform));page.transform.SetParent(canvas.transform,false);((RectTransform)page.transform).sizeDelta=new Vector2(1920,1080);
        var actions=new ModeSelectionPresentation.Actions{Play=()=>{},Gacha=()=>{},Inventory=()=>{},Profile=()=>{},Settings=()=>{},Shop=()=>{},Back=()=>{},Logout=()=>{},Mailbox=()=>{},News=()=>{}};
        page.AddComponent<ModeSelectionPresentation>().Build(MainMenuLogoOriginal.LoadSprite(),actions);Canvas.ForceUpdateCanvases();
        quit=page.GetComponentsInChildren<Button>().Single(b=>b.name=="Quit Game");
        Check(quit.isActiveAndEnabled&&quit.GetComponentInChildren<MatchHudIconGraphic>(),"Mode hub lost the quit icon");
        foreach(var graphic in page.GetComponentsInChildren<Graphic>())graphic.SetAllDirty();
        Canvas.ForceUpdateCanvases();
        KingdomRushArtReviewCapture.Write(camera,Output+"/mode-hub.png",1920,1080);
    }
    private static GameObject CreatePieces(Chessboard board,string set,float tile)
    {
        var data=AssetDatabase.LoadAssetAtPath<PieceSkinData>("Assets/Game/Data/PieceSkins/"+set+".asset");
        var root=new GameObject(set+" shadow fixture");
        var order=new[]{PieceType.Rook,PieceType.Knight,PieceType.Bishop,PieceType.Queen,PieceType.King,PieceType.Bishop,PieceType.Knight,PieceType.Rook};
        foreach(var side in new[]{PieceTeam.White,PieceTeam.Black})for(int x=0;x<8;x++)for(int rank=0;rank<2;rank++)
        {
            var type=rank==1?PieceType.Pawn:order[x];
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(data.GetPrefab(type,side).AssetGUID));
            var model=UnityEngine.Object.Instantiate(prefab,root.transform);
            model.transform.localRotation=Quaternion.Euler(data.GetTuning(type).RotationEuler);
            var renderers=model.GetComponentsInChildren<Renderer>();
            Bounds bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            float height=(type==PieceType.Pawn?.52f:type==PieceType.King?.95f:.8f)*tile;
            model.transform.localScale*=Mathf.Min(height/bounds.size.y,.65f*tile/Mathf.Max(bounds.size.x,bounds.size.z));
            bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            model.transform.position+=board.GetTileCenterWorld(new Vector2Int(x,side==PieceTeam.White?rank:7-rank))-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            typeof(ChessGame).Assembly.GetType("ChessModelRendering").GetMethod("ApplySettings",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{model});
        }
        return root;
    }
}
