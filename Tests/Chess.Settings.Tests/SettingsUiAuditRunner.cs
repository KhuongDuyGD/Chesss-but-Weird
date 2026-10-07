using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChessButWeird.Settings;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static class SettingsUiAuditRunner
{
    private static int checks,ticks;
    private static string directory;
    private static readonly List<string> report=new List<string>();
    public static void Run()
    {
        if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/settings-audit/"))throw new Exception("Requires isolated settings audit project");
        directory=Path.GetDirectoryName(Application.dataPath);
        PlayerSettings.companyName="CBW Isolated Settings Tests";PlayerSettings.productName=Path.GetFileName(directory);
        var playerSettings=new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
        var input=playerSettings.FindProperty("activeInputHandler");if(input!=null){input.intValue=2;playerSettings.ApplyModifiedPropertiesWithoutUndo();}
        foreach(var definition in SettingsRegistry.Create())PlayerPrefs.DeleteKey(PlayerPrefsSettingsStore.Prefix+definition.Id);
        PlayerPrefs.SetInt("cbw_settings_music_volume",43);PlayerPrefs.SetInt("cbw_settings_sound_volume",71);
        PlayerPrefs.SetInt("cbw_settings_graphics_auto_v2",0);PlayerPrefs.SetInt("cbw_settings_graphics_preset_v2",1);
        PlayerPrefs.SetInt("cbw_settings_fps_auto_v2",0);PlayerPrefs.SetInt("cbw_settings_fps",90);
        PlayerPrefs.SetInt("cbw_settings_antialiasing_v1",5);PlayerPrefs.SetInt("cbw_settings_shadows_v1",1);
        PlayerPrefs.SetString("audit_progress","preserved");PlayerPrefs.Save();
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        EditorApplication.update+=Wait;EditorApplication.EnterPlaymode();
    }
    private static void Wait()
    {
        if(!EditorApplication.isPlaying||++ticks<16)return;EditorApplication.update-=Wait;
        try{Audit();report.Add("PASS "+checks+" Unity settings assertions. Raster previews included; no full gameplay or server coverage.");Finish(0);}
        catch(Exception e){report.Add("FAIL "+e);Finish(1);}
    }
    private static void Finish(int code){File.WriteAllLines(Path.Combine(directory,"audit-result.txt"),report);Debug.Log(string.Join("\n",report));EditorApplication.Exit(code);}
    private static void Check(bool value,string detail){checks++;if(!value)throw new Exception(detail);}
    private static T Field<T>(object source,string name)=>(T)source.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(source);
    private static void Call(object source,string name,params object[] args)=>source.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(source,args.Length==0?null:args);
    private static Rect Bounds(RectTransform rect,RectTransform relative)
    {var points=new Vector3[4];rect.GetWorldCorners(points);var a=relative.InverseTransformPoint(points[0]);var b=relative.InverseTransformPoint(points[2]);return Rect.MinMaxRect(a.x,a.y,b.x,b.y);}
    private static void Within(RectTransform child,RectTransform parent,string detail)
    {var a=Bounds(child,parent);var b=parent.rect;Check(a.xMin>=b.xMin-1&&a.xMax<=b.xMax+1&&a.yMin>=b.yMin-1&&a.yMax<=b.yMax+1,detail+" outside bounds: "+a+" vs "+b);}
    private static void Audit()
    {
        var manager=UserSettings.Manager;
        Check(manager.Get("music")==43&&manager.Get("sfx")==71,"Legacy volume migration");
        Check(manager.Get("graphics")==2&&manager.Get("fps")==90&&manager.Get("aa")==5&&manager.Get("shadows")==2,"Legacy graphics migration");
        manager.Set("music",36);manager.Flush();new PlayerPrefsSettingsStore();
        Check(PlayerPrefs.GetString(PlayerPrefsSettingsStore.Prefix+"music")=="36","Migration overwrote a new preference");
        var restored=new SettingsManager(SettingsRegistry.Create(),new PlayerPrefsSettingsStore());
        Check(restored.Get("music")==36,"PlayerPrefs reload");
        manager.ResetAll();Check(PlayerPrefs.GetString("audit_progress")=="preserved","Reset touched player progress");
        manager.Set("master",35);Check(Mathf.Abs(AudioListener.volume-.35f)<.01f,"Master volume did not apply immediately");
        manager.Set("mute_unfocused",1);var runtime=UnityEngine.Object.FindAnyObjectByType<SettingsRuntime>();
        Call(runtime,"OnApplicationFocus",false);Check(AudioListener.volume==0,"Unfocused audio did not mute");Call(runtime,"OnApplicationFocus",true);Check(Mathf.Abs(AudioListener.volume-.35f)<.01f,"Focus did not restore volume");
        manager.Set("music",41);Check(GameMusicManager.RefreshCount>0,"Music adapter not notified");
        var source=new GameObject("SFX audit",typeof(AudioSource),typeof(SettingsSfxVolume)).GetComponent<AudioSource>();
        manager.Set("sfx",25);Check(Mathf.Abs(source.volume-.25f)<.01f,"SFX volume did not update the active AudioSource");
        manager.ResetAll();
        var eventObject=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        var host=new GameObject("Settings fixture canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
        var canvas=host.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
        var root=(RectTransform)host.transform;root.sizeDelta=new Vector2(1440,900);
        var menu=host.AddComponent<SettingsMenuController>();int closes=0;menu.Initialize(root,()=>closes++);menu.Open();
        Check(menu.IsOpen,"Menu failed to open");
        Check(Field<List<Button>>(menu,"tabs").Count==6,"Wrong navigation tabs");
        foreach(var resolution in new[]{new Vector2(1280,720),new Vector2(1920,1080),new Vector2(2560,1440),new Vector2(1920,1200)})
        foreach(var scale in new[]{80,100,150})
        foreach(bool large in new[]{false,true})
        {
            root.sizeDelta=resolution;manager.Set("ui_scale",scale);manager.Set("large_text",large?1:0);
            foreach(SettingsCategory tab in Enum.GetValues(typeof(SettingsCategory)))
            {
                menu.SelectCategory(tab);Canvas.ForceUpdateCanvases();
                var frame=Field<RectTransform>(menu,"frame");Call(frame.GetComponent<MenuDesignFrame>(),"Apply");Canvas.ForceUpdateCanvases();Within(frame,root,"Frame "+resolution);
                var viewport=Field<ScrollRect>(menu,"scroll").viewport;Within(viewport,frame,"Viewport");
                foreach(var button in frame.GetComponentsInChildren<Button>())
                    if(!button.transform.IsChildOf(menu.Content))Within((RectTransform)button.transform,frame,"Shell button "+button.name);
                foreach(var text in menu.Content.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    text.ForceMeshUpdate(true,true);
                    if(string.IsNullOrEmpty(text.text))continue;
                    Check(!text.isTextOverflowing,resolution+" "+scale+" "+large+" "+tab+" clipped: "+text.text+" rect "+text.rectTransform.rect);
                }
                var rows=menu.Content.Cast<Transform>().Where(t=>t.gameObject.activeSelf&&t.name.EndsWith(" row")).ToList();
                foreach(var row in rows)
                {
                    foreach(var control in row.GetComponentsInChildren<Selectable>())Within((RectTransform)control.transform,(RectTransform)row,"Row control");
                }
                var scroll=Field<ScrollRect>(menu,"scroll");Check(scroll.content.rect.height>=scroll.viewport.rect.height,"Invalid scroll content");
                if(rows.Count>0)
                {
                    var last=rows.Last().GetComponentsInChildren<Selectable>().First();last.Select();
                    Within((RectTransform)last.transform,scroll.viewport,"Keyboard navigation must reveal focused control");
                    scroll.verticalNormalizedPosition=1;
                }
            }
            report.Add("PASS layout "+resolution+", scale "+scale+"%, large text "+large+", all six tabs");
        }
        manager.Set("language",1);manager.Set("ui_scale",150);manager.Set("large_text",1);menu.SelectCategory(SettingsCategory.General);
        var translated=menu.Content.Cast<Transform>().Single(t=>t.gameObject.activeSelf&&t.name=="language row").GetComponentsInChildren<TextMeshProUGUI>();
        Check(UserSettings.LanguageCode=="vi"&&translated.Any(t=>t.text=="Language and international text preferences"),"Language table did not apply");
        foreach(var text in translated.Where(t=>t.name=="Setting name"||t.name=="Description")){text.ForceMeshUpdate(true,true);Check(!text.isTextOverflowing,"Long translated text clipped");}
        manager.Set("language",0);
        manager.Set("ui_scale",100);manager.Set("large_text",0);root.sizeDelta=new Vector2(1920,1080);menu.SelectCategory(SettingsCategory.Graphics);
        var anchor=(RectTransform)menu.Content.GetComponentsInChildren<Button>().First(b=>b.name=="fps control").transform;
        Call(menu,"ShowChoices",anchor,manager.Definition("fps").Labels.ToArray(),0,(Action<int>)(_=>{}));Canvas.ForceUpdateCanvases();Within(Field<RectTransform>(menu,"popup"),Field<RectTransform>(menu,"frame"),"Dropdown visible bounds");
        menu.Confirm("Reset all settings to default?\nControls and accessibility preferences will also reset.",()=>{},"Reset All");Canvas.ForceUpdateCanvases();Within(Field<RectTransform>(menu,"popup"),Field<RectTransform>(menu,"frame"),"Confirmation visible bounds");
        Call(menu,"HidePopup");
        // Verify real URP values and renderer bridge; the authoring pipeline must remain untouched.
        var originalOverride=QualitySettings.renderPipeline;var pipeline=UniversalRenderPipelineAsset.Create();QualitySettings.renderPipeline=pipeline;
        int originalAtlas=pipeline.mainLightShadowmapResolution;
        foreach(var tier in new[]{1,2,3,0})
        {
            manager.Set("shadows",tier);GameRuntimeSettings.ApplySaved();
            var applied=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            Check(applied.shadowDistance==(tier==0?0:tier==1?25:tier==2?50:80),"Shadow distance tier");
            Check(applied.mainLightShadowmapResolution==(tier==1?512:tier==2?1024:2048),"Shadow atlas tier");
            Check(ChessModelRendering.Shadows==(tier>0),"Renderer shadow bridge");
            Check(pipeline.mainLightShadowmapResolution==originalAtlas,"Runtime modified authoring URP asset");
        }
        GameRuntimeSettings.ReleaseRuntimePipelines();QualitySettings.renderPipeline=originalOverride;GraphicsSettings.defaultRenderPipeline=null;
        manager.Set("aa",2);GameRuntimeSettings.ApplySaved();var camera=new GameObject("Settings screenshot camera",typeof(Camera)).GetComponent<Camera>();camera.orthographic=true;camera.transform.position=new Vector3(0,0,-100);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=SketchbookUI.Paper;canvas.worldCamera=camera;
        foreach(var tab in new[]{SettingsCategory.General,SettingsCategory.Graphics,SettingsCategory.Accessibility})
        {
            manager.ResetAll();root.sizeDelta=new Vector2(1920,1080);menu.SelectCategory(tab);Capture(camera,canvas,menu,1920,1080,tab+"-1920x1080");
        }
        manager.Set("ui_scale",150);manager.Set("large_text",1);root.sizeDelta=new Vector2(1280,720);menu.SelectCategory(SettingsCategory.General);Capture(camera,canvas,menu,1280,720,"General-1280x720-scale150-large");
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard=InputSystem.AddDevice<Keyboard>();
        Call(menu,"BeginRebind","key_orbit");
        Press(keyboard,Key.F10);Call(menu,"Update");
        Check(manager.Get("key_orbit")==Array.IndexOf(SettingsRegistry.BindingKeys,"Y")&&Field<TextMeshProUGUI>(menu,"confirmationText").text.Contains("already assigned"),
            "Keyboard conflict warning / existing binding preservation: open="+menu.IsOpen+", current="+(Keyboard.current==keyboard)+", pressed="+keyboard.f10Key.wasPressedThisFrame+", rebind="+Field<string>(menu,"rebindId")+", key="+manager.Get("key_orbit")+", text="+Field<TextMeshProUGUI>(menu,"confirmationText").text);
        Press(keyboard,Key.Q);Call(menu,"Update");
        Check(manager.Get("key_orbit")==Array.IndexOf(SettingsRegistry.BindingKeys,"Q")&&!Field<RectTransform>(menu,"popup"),"Keyboard capture did not save and close");
        var activeAnchor=(RectTransform)menu.Content.GetComponentsInChildren<Button>().First(b=>b.name=="language control").transform;
        Call(menu,"ShowChoices",activeAnchor,manager.Definition("fps").Labels.ToArray(),0,(Action<int>)(_=>{}));
        Press(keyboard,Key.Escape);Call(menu,"Update");Check(menu.IsOpen&&!Field<RectTransform>(menu,"popup"),"ESC should back out of a dropdown first");
        Press(keyboard,Key.Escape);Call(menu,"Update");Check(closes==1&&!menu.IsOpen,"ESC close callback lifecycle");
        InputSystem.RemoveDevice(keyboard);
        Check(PlayerPrefs.GetString("audit_progress")=="preserved","Settings damaged progression");
    }
    private static void Press(Keyboard keyboard,Key key)
    {InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));InputSystem.Update();}
    private static void Capture(Camera camera,Canvas canvas,SettingsMenuController menu,int width,int height,string name)
    {
        camera.orthographicSize=height/2f;camera.aspect=(float)width/height;Canvas.ForceUpdateCanvases();Call(Field<RectTransform>(menu,"frame").GetComponent<MenuDesignFrame>(),"Apply");Canvas.ForceUpdateCanvases();
        var target=new RenderTexture(width,height,24);camera.targetTexture=target;camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
        File.WriteAllBytes(Path.Combine(directory,name+".png"),texture.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(texture);
    }
}
