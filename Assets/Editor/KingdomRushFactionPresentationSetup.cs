using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Shared by repeatable catalog registration; dependencies travel inside each environment bundle.
public static class KingdomRushFactionPresentationSetup
{
    private const string Root=KingdomRushGameSetup.Root;
    internal static void Configure(string faction)
    {
        bool dark=faction=="Veznan";
        string path=Root+"/Prefabs/KR_"+faction+"_Environment.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var presentation=root.GetComponent<KingdomRushEnvironmentPresentation>();
            if(!presentation)presentation=root.AddComponent<KingdomRushEnvironmentPresentation>();
            presentation.darkDominion=dark;
            var reflections=new GameObject("Local soft sky reflections");reflections.transform.SetParent(root.transform,false);
            var probe=reflections.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Custom;
            probe.customBakedTexture=Reflection(faction,dark);probe.size=new Vector3(70,35,70);
            probe.center=new Vector3(0,5,0);probe.boxProjection=false;probe.intensity=.75f;probe.blendDistance=6;
            var material=ParticleMaterial();
            foreach(var p in new[]{new Vector3(-9,-.35f,-3),new Vector3(9,-.35f,3),new Vector3(-5,-.35f,-10),new Vector3(5,-.35f,10)})
                Motes(root.transform,p,dark,material);
            KingdomRushLivingWorldSetup.ConfigureEnvironment(root,dark);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }
    private static Cubemap Reflection(string faction,bool dark)
    {
        string path=Root+"/Textures/FactionsHQ/"+faction+"_SoftSky.asset";
        var map=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
        if(!map){map=new Cubemap(64,TextureFormat.RGBAHalf,true);AssetDatabase.CreateAsset(map,path);}
        for(int f=0;f<6;f++)
        {
            var pixels=new Color[64*64];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float u=(x+.5f)/32-1,v=(y+.5f)/32-1;Vector3 d;
                switch((CubemapFace)f)
                {
                    case CubemapFace.PositiveX:d=new Vector3(1,-v,-u);break;
                    case CubemapFace.NegativeX:d=new Vector3(-1,-v,u);break;
                    case CubemapFace.PositiveY:d=new Vector3(u,1,v);break;
                    case CubemapFace.NegativeY:d=new Vector3(u,-1,-v);break;
                    case CubemapFace.PositiveZ:d=new Vector3(u,-v,1);break;
                    default:d=new Vector3(-u,-v,-1);break;
                }
                d.Normalize();
                Color low=dark?new Color(.08f,.065f,.09f):new Color(.18f,.16f,.11f);
                Color high=dark?new Color(.24f,.27f,.38f):new Color(.52f,.65f,.80f);
                var color=Color.Lerp(low,high,Mathf.SmoothStep(0,1,d.y*.5f+.5f));
                float sun=Mathf.Pow(Mathf.Max(0,Vector3.Dot(d,new Vector3(-.4f,.85f,-.35f).normalized)),16);
                color+=(dark?new Color(.16f,.18f,.24f):new Color(.8f,.65f,.42f))*sun;
                pixels[y*64+x]=color;
            }
            map.SetPixels(pixels,(CubemapFace)f);
        }
        map.Apply(true,false);map.wrapMode=TextureWrapMode.Clamp;map.filterMode=FilterMode.Trilinear;
        EditorUtility.SetDirty(map);return map;
    }
    private static Material ParticleMaterial()
    {
        string folder=Root+"/Textures/FactionsHQ", texturePath=folder+"/SoftMote.png";
        Directory.CreateDirectory(folder);
        if(!File.Exists(texturePath))
        {
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float r=new Vector2((x+.5f)/32-1,(y+.5f)/32-1).magnitude;
                pixels[y*64+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r*r),3));
            }
            texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(texturePath,texture.EncodeToPNG());
            Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(texturePath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.alphaIsTransparency=true;
            importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.SaveAndReimport();
        }
        string materialPath=Root+"/Materials/KRF_SoftMotes.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(material,materialPath);}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);
        material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);material.renderQueue=3000;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetShaderPassEnabled("ShadowCaster",false);
        EditorUtility.SetDirty(material);return material;
    }
    private static void Motes(Transform parent,Vector3 position,bool dark,Material material)
    {
        var go=new GameObject(dark?"Low drifting soul embers":"Quiet garden light motes");
        go.transform.SetParent(parent,false);go.transform.localPosition=position;
        var particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;main.duration=8;main.loop=true;main.startLifetime=new ParticleSystem.MinMaxCurve(3,5);
        main.startSize=new ParticleSystem.MinMaxCurve(.025f,.055f);main.startSpeed=new ParticleSystem.MinMaxCurve(.05f,.13f);
        main.maxParticles=28;main.simulationSpace=ParticleSystemSimulationSpace.Local;
        main.startColor=dark?new Color(.58f,.78f,.27f,.34f):new Color(1,.88f,.55f,.26f);
        var emission=particles.emission;emission.rateOverTime=dark?3:1.5f;
        var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(1.3f,.08f,1.3f);
        var velocity=particles.velocityOverLifetime;velocity.enabled=false;velocity.space=ParticleSystemSimulationSpace.Local;
        // Unity requires matching XYZ curve modes, even for axes with zero velocity.
        velocity.x=new ParticleSystem.MinMaxCurve(0f,0f);
        velocity.y=new ParticleSystem.MinMaxCurve(.07f,.15f);
        velocity.z=new ParticleSystem.MinMaxCurve(0f,0f);velocity.enabled=true;
        var color=particles.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.25f),new GradientAlphaKey(.5f,.7f),new GradientAlphaKey(0,1)});
        color.color=gradient;
        var size=particles.sizeOverLifetime;size.enabled=true;
        size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.35f,1,0));
        var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
        renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        particles.Play();
    }
}
