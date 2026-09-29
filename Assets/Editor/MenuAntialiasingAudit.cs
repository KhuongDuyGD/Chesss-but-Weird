using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Actual GPU coverage checks, including AA disabled, tiny/rotated icons, fades and clipping.</summary>
public static class MenuAntialiasingAudit
{
    private static string Folder => Environment.GetEnvironmentVariable("CHESS_MENU_AA_AUDIT_OUTPUT") ?? "Documentation/UI/MenuAntialiasing";
    private static readonly List<string> Checks = new List<string>();
    private const int Width = 1024, Height = 512;

    [MenuItem("Chess/UI Audit/Verify Menu Edge Antialiasing")]
    public static void Capture()
    {
        Directory.CreateDirectory(Folder);
        Checks.Clear();
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Menu Edge Audit", typeof(RectTransform), typeof(Canvas));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)root.transform).sizeDelta = new Vector2(Width, Height);
        var cameraObject = new GameObject("Menu Edge Audit Camera", typeof(Camera));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.GetComponent<Camera>();
        camera.scene = scene; camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = Height * .5f;
        camera.transform.position = new Vector3(0,0,-100);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
        camera.nearClipPlane = .1f; camera.farClipPlane = 200; camera.cullingMask = 1 << 31;
        canvas.worldCamera = camera;
        var owned = new List<UnityEngine.Object>();
        int previousAA = QualitySettings.antiAliasing;
        try
        {
            var shader = Resources.Load<Shader>("UI/MenuEdgeAntialiasing");
            Require(shader && shader.isSupported, "The menu coverage shader is packaged and supported on the GPU");
            Require(!ShaderUtil.GetShaderMessages(shader).Any(message => message.severity == ShaderCompilerMessageSeverity.Error), "The UI shader compiles without errors");
            Require(!MenuTextureImportSettings.IsMenuArtwork("Assets/Materials/GameplayUI/Move.png") &&
                !MenuTextureImportSettings.IsMenuArtwork("Assets/Skins/Piece.png"), "Menu importer excludes gameplay HUD and chess skins");
            Require(MenuTextureImportSettings.IsMenuArtwork("Assets/Resources/MainMenu/MainMenuLogoOriginal.png"), "Original menu logo receives the sampling policy");
            VerifyRuntimeBitmaps(owned);
            Sprite logo = MainMenuLogoOriginal.LoadSprite(); owned.Add(logo);
            Require(logo && logo.texture.mipmapCount > 1 && logo.texture.filterMode == FilterMode.Trilinear,
                "Preserved logo has a mip chain and Trilinear filtering");

            for (int i=0;i<8;i++)
            {
                var icon = Child<ModeMenuDoodleGraphic>(root.transform, "Mode "+i, new Vector2(-448+i*128,134), new Vector2(92,92));
                icon.Configure((ModeMenuDoodleGraphic.Drawing)i);
                var doodle = Child<SketchbookDoodle>(root.transform, "Sketchbook "+i, new Vector2(-448+i*128,0), new Vector2(54,54));
                doodle.Configure((SketchbookDoodle.Shape)i, new Color(.15f,.145f,.17f));
                doodle.rectTransform.localEulerAngles = new Vector3(0,0,11.5f);
            }
            var panel = Child<HandDrawnRoundedGraphic>(root.transform,"Rounded Button",new Vector2(-278,-148),new Vector2(278,74));
            panel.Configure(new Color(1,.85f,.39f),new Color(.15f,.145f,.17f),18,3,1.2f,5);
            var ink = Child<MainMenuInkGraphic>(root.transform,"Small Gear",new Vector2(-66,-148),new Vector2(36,36));
            ink.Configure(MainMenuInkGraphic.Drawing.Gear,new Color(.15f,.145f,.17f),1.5f);
            var image = Child<Image>(root.transform,"Preserved Logo",new Vector2(126,-140),new Vector2(172,95));
            image.sprite = logo; image.preserveAspect = true;
            var nestedRoot = Child<Canvas>(root.transform,"Nested Canvas",new Vector2(324,-148),new Vector2(72,72));
            var nestedIcon = Child<ModeMenuDoodleGraphic>(nestedRoot.transform,"Nested Diamond",Vector2.zero,new Vector2(32,32));
            nestedIcon.Configure(ModeMenuDoodleGraphic.Drawing.Gacha);
            nestedIcon.rectTransform.localEulerAngles = new Vector3(0,0,17.3f);
            SetLayer(root.transform);
            Canvas.ForceUpdateCanvases();
            Require((canvas.additionalShaderChannels & (AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2)) ==
                (AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2), "Root canvas retains both UI colour channels");
            Require((nestedRoot.additionalShaderChannels & AdditionalCanvasShaderChannels.TexCoord2) != 0, "Nested animated canvases retain the coverage colour channels");

            QualitySettings.antiAliasing = 0;
            camera.allowMSAA = false;
            var off = Render(camera,1,"MenuEdges-AAOff.png",owned);
            QualitySettings.antiAliasing = 4;
            camera.allowMSAA = true;
            var on = Render(camera,4,"MenuEdges-MSAA4x.png",owned);
            double mean = off.Zip(on,(a,b)=>(Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b))/3d).Average();
            Require(mean < 2, "Turning MSAA on/off keeps menu edges stable (mean RGB byte difference: "+mean.ToString("F3")+")");

            // A transparent one-colour silhouette must have partial coverage even with zero multisampling.
            foreach (Transform child in root.transform) child.gameObject.SetActive(false);
            camera.allowMSAA = false; QualitySettings.antiAliasing = 0;
            var silhouette = Child<HandDrawnRoundedGraphic>(root.transform,"Coverage Silhouette",Vector2.zero,new Vector2(118,58));
            silhouette.Configure(Color.black,Color.clear,19,0,0,0);
            silhouette.rectTransform.localEulerAngles = new Vector3(0,0,13.7f);
            SetLayer(root.transform);
            var coverage = Render(camera,1,null,owned);
            int partial = coverage.Count(pixel=>pixel.r>4 && pixel.r<250);
            Require(partial > 130 && partial < 1100, "AA-off silhouette has a narrow, partially covered edge (pixels: "+partial+")");

            foreach (float scale in new[] { .25f,.5f,1f,1.375f,2f })
            {
                silhouette.rectTransform.localScale = Vector3.one*scale;
                var small = Render(camera,1,null,owned);
                Require(small.Count(pixel=>pixel.r>4 && pixel.r<250)>20, "Coverage survives fractional/low UI scale "+scale);
            }
            silhouette.rectTransform.localScale = Vector3.one;
            silhouette.rectTransform.localEulerAngles = Vector3.zero;
            silhouette.Configure(new Color(.22f,.47f,.71f),Color.clear,15,0,0,0);
            var reference = Child<Image>(root.transform,"Default UI Colour Reference",new Vector2(200,0),new Vector2(80,60));
            reference.color = silhouette.color;
            SetLayer(root.transform);
            var colours = Render(camera,1,null,owned);
            Require(Difference(Pixel(colours,0,0),Pixel(colours,200,0))<=2, "Smoothed shapes preserve the standard UI colour in linear colour space");
            var group = silhouette.gameObject.AddComponent<CanvasGroup>();
            group.alpha = .5f;
            var faded = Render(camera,1,null,owned);
            Require(Pixel(faded,0,0).r>Pixel(colours,0,0).r+30, "CanvasGroup fading affects fill and edge coverage");
            group.alpha = 1;
            silhouette.canvasRenderer.SetColor(new Color(.5f,1,1,1));
            var tinted = Render(camera,1,null,owned);
            Require(Pixel(tinted,0,0).r<Pixel(colours,0,0).r-10, "Button CanvasRenderer tint affects the smoothed graphic");
            silhouette.canvasRenderer.SetColor(Color.white);
            reference.gameObject.SetActive(false);

            silhouette.gameObject.SetActive(false);
            var rectangle=Child<AntialiasedMenuImage>(root.transform,"Settings Chevron Stroke",Vector2.zero,new Vector2(28,3));
            rectangle.color=Color.black;rectangle.rectTransform.localEulerAngles=new Vector3(0,0,40);
            SetLayer(root.transform);
            var rectangularCoverage=Render(camera,1,null,owned);
            Require(rectangularCoverage.Count(pixel=>pixel.r>4 && pixel.r<250)>20,"Plain menu rectangles and diagonal chevrons have pixel coverage with AA off");
            rectangle.sprite=logo;
            Render(camera,1,null,owned);
            Require(rectangle.canvasRenderer.GetMaterial(0).shader.name=="UI/Default","Bitmap Images retain the standard UI texture shader");
            rectangle.sprite=null; rectangle.color=Color.clear; rectangle.raycastTarget=true;
            Render(camera,1,null,owned);
            Require(rectangle.canvasRenderer.GetMesh().vertexCount>0 && rectangle.depth>=0,
                "Transparent slider hit areas retain mesh depth for pointer raycasting");
            rectangle.gameObject.SetActive(false);silhouette.gameObject.SetActive(true);

            var maskRoot = Child<RectMask2D>(root.transform,"Rectangle Clip",Vector2.zero,new Vector2(60,42));
            silhouette.transform.SetParent(maskRoot.transform,false);
            SetLayer(root.transform);
            var clipped = Render(camera,1,null,owned);
            Require(Pixel(clipped,0,0).b<245 && Pixel(clipped,42,0).r==255, "RectMask2D clips the antialiased menu shape");
            maskRoot.softness = new Vector2Int(12,8);
            var softClip = Render(camera,1,null,owned);
            Require(Pixel(softClip,27,0).r>Pixel(clipped,27,0).r+10, "RectMask2D softness retains its smooth fade");
            silhouette.transform.SetParent(root.transform,false);
            maskRoot.gameObject.SetActive(false);
            var stencil = Child<Image>(root.transform,"Stencil Clip",Vector2.zero,new Vector2(60,42));
            stencil.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            silhouette.transform.SetParent(stencil.transform,false);
            SetLayer(root.transform);
            var stencilPixels = Render(camera,1,null,owned);
            Require(Pixel(stencilPixels,0,0).b<245 && Pixel(stencilPixels,42,0).r==255, "UGUI stencil masks clip the antialiased menu shape");
            Require(!ShaderUtil.GetShaderMessages(shader).Any(message=>message.severity==ShaderCompilerMessageSeverity.Error), "Rendered shader variants compile without errors");
            Debug.Log("[MenuAntialiasingAudit] PASS "+Checks.Count+" checks. Captures: "+Path.GetFullPath(Folder));
        }
        finally
        {
            QualitySettings.antiAliasing=previousAA;
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            foreach(var item in owned) if(item) UnityEngine.Object.DestroyImmediate(item);
            EditorSceneManager.ClosePreviewScene(scene);
            File.WriteAllLines(Path.Combine(Folder,"MenuAntialiasingAuditChecks.txt"),Checks);
        }
    }

    private static void VerifyRuntimeBitmaps(List<UnityEngine.Object> owned)
    {
        var source = new Texture2D(7,5,TextureFormat.RGBA32,false);
        var pixels = new Color32[35];
        for(int y=0;y<5;y++) pixels[y*7+6]=new Color32(255,80,20,255);
        source.SetPixels32(pixels); source.Apply(); owned.Add(source);
        var loaded = new Texture2D(2,2,TextureFormat.RGBA32,true); owned.Add(loaded);
        Require(loaded.LoadImage(source.EncodeToPNG()), "File fallback decodes a menu PNG with mipmaps requested");
        MenuTextureSampling.FinishRuntimeTexture(loaded);
        Require(loaded.mipmapCount==3 && !loaded.isReadable && loaded.filterMode==FilterMode.Trilinear && loaded.wrapMode==TextureWrapMode.Clamp,
            "Runtime PNG path keeps the mip chain and releases CPU texture memory");
        var mip=ReadMip(loaded,1);
        Require(mip.Length==6,"Alpha-weighted mipmap can be read back from the GPU");
        Require(mip[2].r>=253 && mip[2].g>=78 && mip[2].a>=83 && mip[2].a<=87,
            "NPOT mipmap includes the last column without darkening transparent ink edges (actual: "+mip[2]+")");
        var basePixels=ReadMip(loaded,0);
        Require(basePixels[5].a==0 && basePixels[5].r==255,
            "Transparent neighbour carries ink RGB while its alpha and silhouette stay unchanged");

        var contrast=new Texture2D(2,2,TextureFormat.RGBA32,true);owned.Add(contrast);
        contrast.SetPixels32(new[] { new Color32(255,255,255,255),new Color32(0,0,0,255),new Color32(255,255,255,255),new Color32(0,0,0,255) });
        MenuTextureSampling.FinishRuntimeTexture(contrast);
        var average=ReadMip(contrast,1)[0];
        int expected=QualitySettings.activeColorSpace==ColorSpace.Linear ? 188 : 128;
        Require(Math.Abs(average.r-expected)<=2,"High-contrast ink mipmaps average in the project's colour space (actual: "+average.r+")");
    }

    private static Color32[] ReadMip(Texture2D texture,int level)
    {
        int width=Mathf.Max(1,texture.width>>level),height=Mathf.Max(1,texture.height>>level);
        var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/MenuArtworkMipAudit.shader");
        var material=new Material(shader);material.SetFloat("_MipLevel",level);
        var target=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var capture=new Texture2D(width,height,TextureFormat.RGBA32,false);
        var previous=RenderTexture.active;bool previousWrite=GL.sRGBWrite;
        var previousFilter=texture.filterMode;
        try
        {
            texture.filterMode=FilterMode.Point;
            GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;
            Graphics.Blit(texture,target,material);
            RenderTexture.active=target;
            capture.ReadPixels(new Rect(0,0,width,height),0,0);capture.Apply();
            return capture.GetPixels32();
        }
        finally
        {
            texture.filterMode=previousFilter;GL.sRGBWrite=previousWrite;RenderTexture.active=previous;
            UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(capture);
        }
    }

    private static T Child<T>(Transform parent,string name,Vector2 position,Vector2 size) where T:Component
    {
        var child=new GameObject(name,typeof(RectTransform),typeof(T));
        child.transform.SetParent(parent,false);
        var rect=(RectTransform)child.transform; rect.anchoredPosition=position;rect.sizeDelta=size;
        return child.GetComponent<T>();
    }
    private static void SetLayer(Transform root)
    { foreach(var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=31; }
    private static Color32[] Render(Camera camera,int samples,string name,List<UnityEngine.Object> owned)
    {
        Canvas.ForceUpdateCanvases();
        var target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32) { antiAliasing=samples };
        var capture=new Texture2D(Width,Height,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try
        {
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            capture.ReadPixels(new Rect(0,0,Width,Height),0,0);capture.Apply();
            if(name!=null) File.WriteAllBytes(Path.Combine(Folder,name),capture.EncodeToPNG());
            return capture.GetPixels32();
        }
        finally
        {
            camera.targetTexture=null;RenderTexture.active=previous;
            UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(capture);
        }
    }
    private static Color32 Pixel(Color32[] pixels,int x,int y)=>pixels[(Height/2+y)*Width+Width/2+x];
    private static int Difference(Color32 a,Color32 b)=>Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)));
    private static void Require(bool condition,string description)
    { Checks.Add((condition?"PASS ":"FAIL ")+description);if(!condition)throw new InvalidOperationException(description); }
}
