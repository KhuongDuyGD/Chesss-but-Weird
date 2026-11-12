using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Native cinematic photography. Run through tools/capture-cosmetic-cinematics.ps1.</summary>
public static class CosmeticThumbnailCapture
{
    private const string Output="Photography";
    private const int Width=2048,Height=1536;
    private static readonly List<Object> Temporary=new List<Object>();
    private static CosmeticCatalog catalog;
    private static VolumeProfile grade;
    private static Theme theme;
    [Serializable] private sealed class CaptureReport { public string style="Native perspective cinematics, 4K masters / 2K menu art"; public List<Photo> images=new List<Photo>(); }
    [Serializable] private sealed class Photo { public string id,category,previewPath,masterPath; public Vector3 cameraPosition,cameraTarget; public float fieldOfView; public int sourceWidth,sourceHeight; }
    private struct Theme { public Color floor,dark,key,rim; public float exposure; }

    [MenuItem("Chess/Cosmetics/Capture High Quality Menu Artwork")]
    public static void Capture()
    {
        if(!Application.isBatchMode||!File.Exists("capture-project.txt"))
            throw new InvalidOperationException("Use tools/capture-cosmetic-cinematics.ps1. Photography runs in its own disposable project.");
        Debug.Log("[CinematicPhotography] Beginning native photography.");
        Directory.CreateDirectory(Output);
        catalog=AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/Game/Data/CosmeticCatalog.asset");
        ConfigureQuality();
        var report=new CaptureReport();
        bool preview=File.Exists("preview-quality.txt");
        string filter=File.Exists("capture-filter.txt")?File.ReadAllText("capture-filter.txt").Trim():"";
        foreach(var entry in catalog.pieceSkins.Concat(catalog.boards).Concat(catalog.environments))
        {
            string dataName=Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(entry.data.AssetGUID));
            if(filter.Length>0&&!filter.Split(',').Any(value=>dataName.IndexOf(value,StringComparison.OrdinalIgnoreCase)>=0))continue;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var data=Load<Object>(entry.data);
            theme=Palette(entry.id);
            Camera camera=Studio();
            AssetReferenceSprite artwork;
            string category;
            Vector3 target;
            if(data is PieceSkinData pieces)
            {
                artwork=pieces.preview;category="Pieces";
                // A dark, low contrast chess plinth separates the actual skin from its photographic backdrop.
                PhotographyPlinth();
                HeroPieces(pieces);
                target=new Vector3(0,.7f,0);
                Aim(camera,target,new Vector3(3.6f,3.6f,-8.3f),26);
                Focus(camera,target,3.8f,75);
            }
            else if(data is BoardSkinData boardData)
            {
                artwork=boardData.preview;category="Boards";
                var board=Board(boardData);
                if(entry.id.StartsWith("kingdom_rush"))Environment(catalog.FindEnvironment(entry.id),camera);
                else Floor(Geometry(board).min.y-.035f);
                var boardPieces=Load<PieceSkinData>(catalog.FindPiece(entry.id)??catalog.FindPiece("tazji_lowpoly"));
                Piece(boardPieces,PieceType.Knight,PieceTeam.White,new Vector3(-3.4f,0,-2.5f),.85f,175);
                Piece(boardPieces,PieceType.Pawn,PieceTeam.White,new Vector3(-2.5f,0,-1.5f),.55f,180);
                Piece(boardPieces,PieceType.King,PieceTeam.Black,new Vector3(2.5f,0,2.5f),1.05f,210);
                target=new Vector3(.15f,.10f,-.35f);
                Vector3 angle=entry.id=="Boards/ClassicMarble"?new Vector3(-7.6f,6.8f,-9.6f):
                    entry.id=="Boards/ForestGarden"?new Vector3(-8.4f,5.8f,-9):
                    entry.id=="Boards/Celestial"?new Vector3(-7.6f,6,-9.6f):
                    entry.id=="kingdom_rush_denas"?new Vector3(-7.6f,5.6f,-9.8f):new Vector3(7.4f,6.4f,-9.2f);
                Aim(camera,target,angle,42);
                Focus(camera,target,7,60);
                if(entry.id=="Boards/ClassicMarble")RenderSettings.reflectionIntensity=.22f;
            }
            else if(data is EnvironmentData environment)
            {
                artwork=environment.preview;category="Environments";
                if(environment.environmentPrefab!=null&&environment.environmentPrefab.RuntimeKeyIsValid())
                {
                    Environment(entry,camera);
                    Board(Load<BoardSkinData>(catalog.FindBoard(entry.id).data));
                    MatchPieces(Load<PieceSkinData>(catalog.FindPiece(entry.id).data));
                    target=new Vector3(0,.25f,2.1f);
                    Aim(camera,target,new Vector3(13.4f,8.5f,-17.7f),46);
                    // The arena and immediate surroundings, never the outer terrain boundary.
                    Focus(camera,new Vector3(0,.4f,0),16);
                    RenderSettings.fogColor=entry.id.Contains("veznan")?new Color(.15f,.10f,.19f):new Color(.38f,.47f,.52f);
                    RenderSettings.fogStartDistance=28;RenderSettings.fogEndDistance=95;
                }
                else
                {
                    var sky=new Material(environment.skybox);Temporary.Add(sky);
                    if(sky.HasProperty("_Rotation"))sky.SetFloat("_Rotation",-45);
                    if(sky.HasProperty("_Exposure"))sky.SetFloat("_Exposure",.8f);
                    RenderSettings.skybox=sky;RenderSettings.fog=false;
                    camera.clearFlags=CameraClearFlags.Skybox;
                    Board(Load<BoardSkinData>(catalog.FindBoard("Boards/Celestial").data));
                    MatchPieces(Load<PieceSkinData>(catalog.FindPiece("ChessSets/CrystalBlue").data));
                    target=new Vector3(0,.2f,.7f);
                    Aim(camera,target,new Vector3(10.2f,7.2f,-14.4f),49);
                    Focus(camera,new Vector3(0,.5f,0),16);
                    GalaxyBackdrop(camera,environment.skybox);
                }
            }
            else throw new InvalidOperationException("Unsupported catalog type: "+entry.id);
            string path=AssetDatabase.GUIDToAssetPath(artwork.AssetGUID);
            if(!path.EndsWith(".png",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Missing preview: "+entry.id);
            string name=category+"-"+dataName;
            string master=Output+"/"+name+"-4K.png";
            Write(camera,master,path,preview?1280:4096,preview?960:3072);
            var photo=new Photo{id=entry.id,category=category,previewPath=path,masterPath=master,cameraPosition=camera.transform.position,cameraTarget=target,fieldOfView=camera.fieldOfView,sourceWidth=preview?1280:4096,sourceHeight=preview?960:3072};
            report.images.Add(photo);
            File.WriteAllText(Output+"/captures.json",JsonUtility.ToJson(report,true));
            File.WriteAllLines(Output+"/progress.txt",report.images.Select(image=>image.category+": "+image.id));
            Debug.Log("[CinematicPhotography] "+name+" captured.");
            // All resources belong to this short-lived photography process.
            // Leave the previous render's resources alive until its scene has been closed.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            foreach(var item in Temporary)if(item)Object.DestroyImmediate(item);Temporary.Clear();
        }
        Debug.Log("[CinematicPhotography] Completed "+report.images.Count+" cinematic photographs.");
    }
    public static void Verify()
    {
        if(!Application.isBatchMode||!File.Exists("capture-project.txt"))throw new InvalidOperationException("Isolated photography project only.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report=JsonUtility.FromJson<CaptureReport>(File.ReadAllText(Output+"/captures.json"));
        var sourceCatalog=AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/Game/Data/CosmeticCatalog.asset");
        var entries=sourceCatalog.pieceSkins.Concat(sourceCatalog.boards).Concat(sourceCatalog.environments).ToArray();
        if(report.images.Count!=entries.Length)throw new InvalidOperationException("The capture report is incomplete.");
        foreach(var entry in entries)
        {
            var data=Load<Object>(entry.data);
            var preview=data is PieceSkinData p?p.preview:data is BoardSkinData b?b.preview:((EnvironmentData)data).preview;
            string path=AssetDatabase.GUIDToAssetPath(preview.AssetGUID);
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(!entry.HasArt||!sprite||sprite.texture.width!=Width||sprite.texture.height!=Height||entry.thumbnail!=sprite)
                throw new InvalidOperationException("Catalog / menu artwork mismatch: "+entry.id);
            var photo=report.images.Single(image=>image.id==entry.id&&image.previewPath==path);
            if(photo.sourceWidth!=4096||photo.sourceHeight!=3072)throw new InvalidOperationException("Not a 4K photograph: "+entry.id);
        }
        File.WriteAllText(Output+"/validation.txt","PASS "+entries.Length+" imported 2048x1536 sprites.\nPASS catalog thumbnails and data preview GUIDs match.\nPASS every preview has a native 4096x3072 source render.\n");
        Debug.Log("[CinematicPhotography] Menu Sprite / data references verified.");
    }
    private static T Load<T>(AssetReference reference) where T:Object => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(reference.AssetGUID));
    private static T Load<T>(CosmeticCatalogEntry entry) where T:Object => Load<T>(entry.data);
    private static void ConfigureQuality()
    {
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        QualitySettings.renderPipeline=pipeline;
        pipeline.renderScale=1;pipeline.msaaSampleCount=8;pipeline.mainLightShadowmapResolution=4096;
        pipeline.shadowDistance=60;pipeline.shadowCascadeCount=4;pipeline.cascade4Split=new Vector3(.12f,.3f,.6f);
        pipeline.shadowDepthBias=.08f;pipeline.shadowNormalBias=.1f;
        QualitySettings.shadows=UnityEngine.ShadowQuality.All;QualitySettings.shadowResolution=UnityEngine.ShadowResolution.VeryHigh;
        QualitySettings.shadowDistance=60;QualitySettings.lodBias=3;QualitySettings.maximumLODLevel=0;
        QualitySettings.anisotropicFiltering=AnisotropicFiltering.ForceEnable;
        ShaderUtil.allowAsyncCompilation=false;
    }
    private static Theme Palette(string id)
    {
        bool violet=id.Contains("veznan"),forest=id.Contains("Woodland")||id.Contains("Forest"),cosmic=id.Contains("Crystal")||id.Contains("Celestial")||id=="classic";
        return new Theme{
            exposure=violet?.28f:forest?.08f:.05f,
            floor=violet?new Color(.055f,.025f,.075f):forest?new Color(.045f,.075f,.05f):cosmic?new Color(.022f,.045f,.08f):new Color(.045f,.05f,.075f),
            dark=violet?new Color(.027f,.011f,.047f):forest?new Color(.016f,.035f,.025f):new Color(.009f,.018f,.038f),
            key=violet?new Color(1,.88f,.94f):forest?new Color(1,.92f,.75f):new Color(1,.91f,.77f),
            rim=violet?new Color(.73f,.45f,1):forest?new Color(.72f,.94f,.65f):cosmic?new Color(.34f,.78f,1):new Color(.58f,.76f,1)};
    }
    private static Camera Studio()
    {
        RenderSettings.skybox=null;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.30f,.34f,.42f);
        var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(new Color(.27f,.30f,.37f));
        sh.AddDirectionalLight(Vector3.up,theme.rim,.17f);RenderSettings.ambientProbe=sh;
        RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture=Reflection();RenderSettings.reflectionIntensity=.65f;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
        RenderSettings.fogColor=theme.dark;RenderSettings.fogStartDistance=22;RenderSettings.fogEndDistance=75;
        Lamp("Warm key",new Vector3(43,-35,0),2.35f,theme.key,true);
        Lamp("Soft bounce",new Vector3(25,135,0),.60f,new Color(.73f,.84f,1),false);
        Lamp("Edge light",new Vector3(28,190,0),1.15f,theme.rim,false);
        var camera=new GameObject("Cinematic product camera").AddComponent<Camera>();
        camera.orthographic=false;camera.aspect=Width/(float)Height;camera.nearClipPlane=.05f;camera.farClipPlane=200;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=theme.dark;
        camera.allowHDR=true;camera.allowMSAA=true;
        var additional=camera.GetUniversalAdditionalCameraData();additional.renderPostProcessing=true;additional.renderShadows=true;
        additional.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;additional.antialiasingQuality=AntialiasingQuality.High;
        var volume=new GameObject("Cinematic color grade").AddComponent<Volume>();volume.isGlobal=true;volume.priority=100;
        grade=ScriptableObject.CreateInstance<VolumeProfile>();Temporary.Add(grade);volume.sharedProfile=grade;
        grade.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
        var bloom=grade.Add<Bloom>(true);bloom.intensity.Override(.14f);bloom.threshold.Override(1.3f);bloom.scatter.Override(.55f);bloom.clamp.Override(3);bloom.highQualityFiltering.Override(true);
        var color=grade.Add<ColorAdjustments>(true);color.contrast.Override(9);color.saturation.Override(-5);color.postExposure.Override(theme.exposure);
        var vignette=grade.Add<Vignette>(true);vignette.intensity.Override(.19f);vignette.smoothness.Override(.72f);
        return camera;
    }
    private static Cubemap Reflection()
    {
        const int size=64;var cube=new Cubemap(size,TextureFormat.RGBAHalf,true);Temporary.Add(cube);
        for(int face=0;face<6;face++)
        {
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(x+.5f)/size*2-1,v=(y+.5f)/size*2-1;
                Vector3 d=face==0?new Vector3(1,-v,-u):face==1?new Vector3(-1,-v,u):face==2?new Vector3(u,1,v):face==3?new Vector3(u,-1,-v):face==4?new Vector3(u,-v,1):new Vector3(-u,-v,-1);d.Normalize();
                float key=Mathf.Pow(Mathf.Max(0,Vector3.Dot(d,new Vector3(-.4f,.8f,-.5f).normalized)),28);
                float rim=Mathf.Pow(Mathf.Max(0,Vector3.Dot(d,new Vector3(.6f,.4f,.7f).normalized)),36);
                pixels[y*size+x]=Color.Lerp(theme.floor,theme.rim*.32f,Mathf.Clamp01(d.y*.5f+.5f))+theme.key*key*3+theme.rim*rim*2;
            }
            cube.SetPixels(pixels,(CubemapFace)face);
        }
        cube.Apply(true,false);return cube;
    }
    private static void Lamp(string name,Vector3 rotation,float intensity,Color color,bool shadows)
    {
        var lamp=new GameObject(name).AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=intensity;lamp.color=color;
        lamp.transform.rotation=Quaternion.Euler(rotation);lamp.shadows=shadows?LightShadows.Soft:LightShadows.None;
        lamp.shadowStrength=.76f;lamp.shadowBias=.08f;lamp.shadowNormalBias=.1f;
        lamp.GetUniversalAdditionalLightData().usePipelineSettings=false;lamp.GetUniversalAdditionalLightData().softShadowQuality=SoftShadowQuality.High;
        if(shadows)RenderSettings.sun=lamp;
    }
    private static void Aim(Camera camera,Vector3 target,Vector3 offset,float fov)
    {camera.fieldOfView=fov;camera.transform.position=target+offset;camera.transform.LookAt(target);}
    private static void Focus(Camera camera,Vector3 target,float aperture,float focalLength=50)
    {
        var depth=grade.Add<DepthOfField>(true);depth.mode.Override(DepthOfFieldMode.Bokeh);
        depth.focusDistance.Override(Vector3.Distance(camera.transform.position,target));depth.focalLength.Override(focalLength);depth.aperture.Override(aperture);
    }
    private static GameObject Model(AssetReferenceGameObject reference)
    {
        var prefab=Load<GameObject>(reference);if(!prefab)throw new InvalidOperationException("Missing mesh "+reference.AssetGUID);
        var model=Object.Instantiate(prefab);model.name=prefab.name;
        foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))if(script)script.enabled=false;
        foreach(var camera in model.GetComponentsInChildren<Camera>(true))Object.DestroyImmediate(camera);
        foreach(var lamp in model.GetComponentsInChildren<Light>(true))Object.DestroyImmediate(lamp);
        foreach(var renderer in model.GetComponentsInChildren<Renderer>())
        {
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            if(renderer is MeshRenderer||renderer is SkinnedMeshRenderer)
                foreach(var material in renderer.sharedMaterials)
                    if(!material||!material.shader||material.shader.name.Contains("InternalError"))
                        throw new InvalidOperationException("Missing photography material: "+model.name+" / "+renderer.name);
        }
        foreach(var animator in model.GetComponentsInChildren<Animator>())
        {var clips=AnimationUtility.GetAnimationClips(animator.gameObject);if(clips.Length>0)clips[0].SampleAnimation(animator.gameObject,0);}
        return model;
    }
    private static GameObject Board(BoardSkinData data)
    {
        var model=Model(data.boardPrefab);model.transform.localRotation=Quaternion.Euler(data.localRotation);model.transform.localScale=data.localScale;
        if(data.boardId=="Boards/Celestial")foreach(var renderer in model.GetComponentsInChildren<Renderer>().Where(r=>r.name=="CL_Chess_Board"))
        {
            renderer.sharedMaterials=renderer.sharedMaterials.Select(source=>{var material=new Material(source);Temporary.Add(material);material.DisableKeyword("_EMISSION");if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",Color.black);return material;}).ToArray();
        }
        return model;
    }
    private static void Environment(CosmeticCatalogEntry entry,Camera camera)
    {
        if(entry==null)return;var data=Load<EnvironmentData>(entry.data);if(data.environmentPrefab==null||!data.environmentPrefab.RuntimeKeyIsValid())return;
        bool dark=entry.id.Contains("veznan");
        camera.backgroundColor=dark?new Color(.075f,.035f,.12f):new Color(.22f,.33f,.43f);
        RenderSettings.fogColor=dark?new Color(.15f,.10f,.19f):new Color(.38f,.47f,.52f);
        var root=Model(data.environmentPrefab);root.transform.localRotation=Quaternion.Euler(data.localRotation);root.transform.localScale=data.localScale;root.transform.localPosition=data.localPosition;
    }
    private static void GalaxyBackdrop(Camera camera,Material source)
    {
        // Use one real region of the equipped galaxy texture as a photographic backdrop,
        // avoiding the visible corner joins of the legacy six-sided skybox.
        var texture=source.GetTexture("_FrontTex");
        if(!texture)throw new InvalidOperationException("Missing native galaxy photograph backdrop.");
        RenderSettings.skybox=null;camera.clearFlags=CameraClearFlags.SolidColor;
        var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));Temporary.Add(material);
        material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",new Color(.8f,.8f,.9f));
        var backdrop=GameObject.CreatePrimitive(PrimitiveType.Quad);backdrop.name="Native galaxy photographic backdrop";
        const float distance=110;
        float height=distance*2*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
        backdrop.transform.position=camera.transform.position+camera.transform.forward*distance;
        backdrop.transform.rotation=camera.transform.rotation;
        backdrop.transform.localScale=new Vector3(height*camera.aspect*1.02f,height*1.02f,1);
        var renderer=backdrop.GetComponent<Renderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
    private static void HeroPieces(PieceSkinData data)
    {
        Piece(data,PieceType.King,PieceTeam.White,new Vector3(-.25f,0,.25f),1.95f,180);
        Piece(data,PieceType.Queen,PieceTeam.Black,new Vector3(1.3f,0,1f),1.8f,190);
        Piece(data,PieceType.Knight,PieceTeam.White,new Vector3(-1.7f,0,-.8f),1.55f,155);
        Piece(data,PieceType.Rook,PieceTeam.White,new Vector3(-2.1f,0,1.25f),1.4f,180);
        Piece(data,PieceType.Bishop,PieceTeam.Black,new Vector3(2.1f,0,-.15f),1.65f,185);
        Piece(data,PieceType.Pawn,PieceTeam.Black,new Vector3(.8f,0,-1.55f),1f,180);
    }
    private static GameObject Piece(PieceSkinData data,PieceType type,PieceTeam side,Vector3 position,float height,float yaw)
    {
        var model=Model(data.GetPrefab(type,side));model.transform.localRotation=Quaternion.Euler(data.GetTuning(type).RotationEuler+new Vector3(0,yaw,0))*model.transform.localRotation;
        var bounds=Geometry(model);float factor=Mathf.Min(height/bounds.size.y,height*.78f/Mathf.Max(bounds.size.x,bounds.size.z));model.transform.localScale*=factor;
        bounds=Geometry(model);model.transform.position+=position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)+Vector3.up*.008f;
        if(data.applyTeamColor)
        {
            var block=new MaterialPropertyBlock();var tint=side==PieceTeam.White?data.whiteColor:data.blackColor;block.SetColor("_BaseColor",tint);block.SetColor("_Color",tint);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.SetPropertyBlock(block);
        }
        return model;
    }
    private static void MatchPieces(PieceSkinData data)
    {
        var row=new[]{PieceType.Rook,PieceType.Knight,PieceType.Bishop,PieceType.Queen,PieceType.King,PieceType.Bishop,PieceType.Knight,PieceType.Rook};
        for(int side=0;side<2;side++)for(int x=0;x<8;x++)for(int rank=0;rank<2;rank++)
        {
            var type=rank==0?row[x]:PieceType.Pawn;
            Piece(data,type,(PieceTeam)side,new Vector3(x-3.5f,0,side==0?rank-3.5f:3.5f-rank),type==PieceType.Pawn?.55f:type==PieceType.King?1f:.83f,side==0?0:180);
        }
    }
    private static Bounds Geometry(GameObject root)
    {
        var renderers=root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&(r is MeshRenderer||r is SkinnedMeshRenderer)).ToArray();
        if(renderers.Length==0)throw new InvalidOperationException("No visible geometry: "+root.name);
        var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);return bounds;
    }
    private static void PhotographyPlinth()
    {
        Floor(-.17f);
        var baseMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));Temporary.Add(baseMaterial);
        baseMaterial.SetColor("_BaseColor",theme.floor*.8f);baseMaterial.SetFloat("_Smoothness",.35f);baseMaterial.SetFloat("_Metallic",.22f);
        var basePlate=GameObject.CreatePrimitive(PrimitiveType.Cube);basePlate.name="Studio slate plinth";
        basePlate.transform.position=new Vector3(0,-.1f,0);basePlate.transform.localScale=new Vector3(8.65f,.18f,6.5f);basePlate.GetComponent<Renderer>().sharedMaterial=baseMaterial;
        var cells=new Material[2];
        for(int i=0;i<2;i++)
        {
            cells[i]=new Material(baseMaterial);Temporary.Add(cells[i]);
            cells[i].SetColor("_BaseColor",Color.Lerp(theme.floor,new Color(.28f,.32f,.36f),i==0?.35f:.55f));
            cells[i].SetFloat("_Smoothness",.3f);
        }
        for(int x=0;x<8;x++)for(int z=0;z<6;z++)
        {
            var tile=GameObject.CreatePrimitive(PrimitiveType.Cube);tile.name="Inset slate photography tile";
            tile.transform.position=new Vector3((x-3.5f)*1.075f,-.02f,(z-2.5f)*1.075f);
            tile.transform.localScale=new Vector3(1.07f,.04f,1.07f);tile.GetComponent<Renderer>().sharedMaterial=cells[(x+z)%2];
        }
    }
    private static void Floor(float elevation)
    {
        var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Themed slate photography backdrop";ground.transform.position=Vector3.up*elevation;ground.transform.localScale=Vector3.one*40;
        var texture=new Texture2D(256,256,TextureFormat.RGB24,true);Temporary.Add(texture);texture.wrapMode=TextureWrapMode.Repeat;
        var pixels=new Color[256*256];for(int y=0;y<256;y++)for(int x=0;x<256;x++)pixels[y*256+x]=theme.floor*(.86f+Mathf.PerlinNoise(x*.04f,y*.04f)*.28f);
        texture.SetPixels(pixels);texture.Apply(true,false);
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));Temporary.Add(material);material.SetTexture("_BaseMap",texture);material.SetTextureScale("_BaseMap",Vector2.one*50);material.SetFloat("_Metallic",.08f);material.SetFloat("_Smoothness",.27f);
        ground.GetComponent<Renderer>().sharedMaterial=material;
    }
    private static void Write(Camera camera,string masterPath,string previewPath,int width,int height)
    {
        var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();
        var previous=RenderTexture.active;var request=new RenderPipeline.StandardRequest{destination=target};
        try
        {
            for(int i=0;i<6;i++)RenderPipeline.SubmitRenderRequest(camera,request);
            Save(target,masterPath);
            var reduced=new RenderTexture(Width,Height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);reduced.Create();
            try{target.filterMode=FilterMode.Bilinear;Graphics.Blit(target,reduced);Save(reduced,previewPath);}
            finally{reduced.Release();Object.DestroyImmediate(reduced);}
        }
        finally{RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);}
    }
    private static void Save(RenderTexture source,string path)
    {
        RenderTexture.active=source;var image=new Texture2D(source.width,source.height,TextureFormat.RGB24,false);
        try{image.ReadPixels(new Rect(0,0,source.width,source.height),0,0);image.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{Object.DestroyImmediate(image);}
    }
}
