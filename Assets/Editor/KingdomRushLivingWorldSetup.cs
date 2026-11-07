using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Author once into addressable prefabs. Runtime only animates bounded, reusable presentation assets.
public static class KingdomRushLivingWorldSetup
{
    private const string Root=KingdomRushGameSetup.Root;
    private const string Folder=Root+"/Presentation/Advanced";
    internal static void ConfigureBoard(string faction)
    {
        string path=Root+"/Prefabs/KR_"+faction+"_Board.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            bool dark=faction=="Veznan";var ambience=root.AddComponent<KingdomRushBattlefieldAmbience>();ambience.darkDominion=dark;
            var flames=new List<Renderer>();
            foreach(float x in new[]{-4.63f,4.63f})foreach(float z in new[]{-5.33f,5.33f})
                flames.Add(Flame(root.transform,new Vector3(x,.45f,z),.30f,dark?"Soul":"Azure"));
            ambience.flames=flames.ToArray();PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    internal static void ConfigureEnvironment(GameObject root,bool dark)
    {
        var ambience=root.AddComponent<KingdomRushBattlefieldAmbience>();ambience.darkDominion=dark;ambience.drivesWind=true;
        ambience.motes=root.GetComponentsInChildren<ParticleSystem>();
        foreach(var particles in ambience.motes)Budget(particles,particles.emission.rateOverTime.constant);
        var extras=new List<ParticleSystem>();var mist=new List<ParticleSystem>();var flames=new List<Renderer>();
        if(dark)
        {
            foreach(var p in new[]{new Vector3(-9,0,-3),new Vector3(9,0,3),new Vector3(-5,0,-10),new Vector3(5,0,10)})
            {
                float y=Ground(p.x,p.z);flames.Add(Flame(root.transform,new Vector3(p.x,y+.18f,p.z+.12f),.40f,"Cinder"));
                mist.Add(Particles(root.transform,"Local ruin mist",new Vector3(p.x,y+.40f,p.z),.30f,6,9,1.6f,2.5f,new Color(.48f,.42f,.58f,.035f),"Mist"));
            }
            foreach(float side in new[]{-1f,1f})
                extras.Add(Particles(root.transform,"Slow drifting ash",new Vector3(side*9,-.3f,side*3),1.2f,4,7,.018f,.040f,new Color(.77f,.58f,.35f,.32f),"Ash"));
        }
        else
        {
            foreach(float side in new[]{-1f,1f})
                extras.Add(Particles(root.transform,"Gentle falling leaves",new Vector3(side*8.7f,.50f,-side*7),.55f,5,8,.065f,.11f,new Color(.72f,.80f,.28f,.85f),"Leaf"));
            var birds=new List<Transform>();var left=new List<Transform>();var right=new List<Transform>();
            for(int i=0;i<2;i++)
            {
                var bird=new GameObject("Occasional Linirea swallow");bird.transform.SetParent(root.transform,false);
                var material=BirdMaterial();Primitive(bird.transform,"Smooth bird body",Vector3.zero,new Vector3(.075f,.05f,.16f),material);
                Primitive(bird.transform,"Smooth bird head",new Vector3(0,.025f,.075f),Vector3.one*.05f,material);
                Primitive(bird.transform,"Ivory fantasy swallow breast",new Vector3(0,-.015f,.025f),new Vector3(.055f,.035f,.11f),BirdMaterial(true));
                var l=new GameObject("Left wing hinge").transform;l.SetParent(bird.transform,false);l.localPosition=new Vector3(-.025f,0,0);
                var r=new GameObject("Right wing hinge").transform;r.SetParent(bird.transform,false);r.localPosition=new Vector3(.025f,0,0);
                MeshObject(l,"Curved wing",WingMesh(),material);var wing=MeshObject(r,"Curved wing",WingMesh(),material);wing.transform.localScale=new Vector3(-1,1,1);
                birds.Add(bird.transform);left.Add(l);right.Add(r);bird.SetActive(false);
            }
            ambience.birds=birds.ToArray();ambience.leftWings=left.ToArray();ambience.rightWings=right.ToArray();
        }
        ambience.accents=extras.ToArray();ambience.mist=mist.ToArray();ambience.flames=flames.ToArray();
    }
    private static float Ground(float x,float z)
    {
        float ramp=Mathf.Clamp01((new Vector2(x,z).magnitude-8)/15);
        return -1.10f+ramp*(.65f+.6f*Mathf.Sin(x*.19f)*Mathf.Cos(z*.21f)+.30f*Mathf.Sin(z*.36f+x*.17f));
    }
    internal static Renderer Flame(Transform parent,Vector3 position,float height,string palette)
    {
        var go=MeshObject(parent,"Volumetric "+palette+" flame",FlameMesh(),FlameMaterial(palette));
        go.transform.localPosition=position;go.transform.localScale=Vector3.one*height;
        var renderer=go.GetComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;
    }
    private static Material FlameMaterial(string palette)
    {
        string path=Folder+"/Fire_"+palette+".mat";Directory.CreateDirectory(Folder);
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("ChessButWeird/KingdomRush/ElementalFlame"));AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",palette=="Soul"?new Color(.24f,.85f,.055f):palette=="Azure"?new Color(.06f,.48f,1):new Color(1,.25f,.025f));
        material.SetFloat("_MotionAmount",1);material.enableInstancing=true;EditorUtility.SetDirty(material);return material;
    }
    private static GameObject MeshObject(Transform parent,string name,Mesh mesh,Material material)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial=material;return go;
    }
    private static void Primitive(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
    }
    private static Mesh SaveMesh(string name,List<Vector3> vertices,List<int> triangles,List<Vector2> uv)
    {
        Directory.CreateDirectory(Folder);string path=Folder+"/"+name+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool create=!mesh;
        if(create)mesh=new Mesh{name=name};else mesh.Clear();
        mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);return mesh;
    }
    private static Mesh FlameMesh()
    {
        var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();const int rings=12,segments=24;
        for(int lobe=0;lobe<3;lobe++)
        {
            int start=v.Count;
            for(int j=0;j<=rings;j++)for(int k=0;k<=segments;k++)
            {
                float y=j/(float)rings,a=k*2*Mathf.PI/segments;float radius=.13f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(y*Mathf.PI)),.78f)*(lobe==0?1:.65f);
                float offset=lobe==0?0:(lobe==1?-.10f:.10f)*(1-y);
                v.Add(new Vector3(Mathf.Cos(a)*radius+offset+.06f*y*y, y*(lobe==0?1:.78f), Mathf.Sin(a)*radius));uv.Add(new Vector2(k/(float)segments,y));
                if(j==rings||k==segments)continue;int q=start+j*(segments+1)+k;
                t.AddRange(new[]{q,q+segments+1,q+segments+2,q,q+segments+2,q+1});
            }
        }
        return SaveMesh("Smooth elemental wisps",v,t,uv);
    }
    private static Mesh WingMesh()
    {
        var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();const int length=14,width=6;
        for(int x=0;x<=length;x++)for(int z=0;z<=width;z++)
        {
            float u=x/(float)length,w=z/(float)width;
            v.Add(new Vector3(-u*.21f,.025f*Mathf.Sin(u*Mathf.PI)-Mathf.Pow(2*w-1,2)*.007f,(2*w-1)*.045f*Mathf.Sin(u*Mathf.PI)-u*.025f));
            uv.Add(new Vector2(u,w));if(x==length||z==width)continue;int q=x*(width+1)+z;
            t.AddRange(new[]{q,q+1,q+width+2,q,q+width+2,q+width+1});
        }
        return SaveMesh("Curved swallow wing",v,t,uv);
    }
    private static Mesh LeafMesh()
    {
        var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();const int length=14,width=6;
        for(int x=0;x<=length;x++)for(int z=0;z<=width;z++)
        {
            float u=x/(float)length,w=z/(float)width;
            v.Add(new Vector3((u-.5f)*1.5f,.17f*Mathf.Sin(u*Mathf.PI)+.06f*Mathf.Pow(2*w-1,2),(2*w-1)*.36f*Mathf.Sin(u*Mathf.PI)));
            uv.Add(new Vector2(u,w));if(x==length||z==width)continue;int q=x*(width+1)+z;
            t.AddRange(new[]{q,q+1,q+width+2,q,q+width+2,q+width+1});
        }
        return SaveMesh("Curved floating leaf",v,t,uv);
    }
    private static Material BirdMaterial(bool breast=false)
    {
        Directory.CreateDirectory(Folder);string path=Folder+(breast?"/Swallow_Breast.mat":"/Swallow.mat");
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.color=breast?new Color(.84f,.86f,.72f):new Color(.16f,.32f,.48f);material.SetFloat("_Smoothness",.08f);material.SetFloat("_Cull",0);EditorUtility.SetDirty(material);return material;
    }
    private static Material ParticleMaterial(string kind)
    {
        Directory.CreateDirectory(Folder);string path=Folder+"/"+kind+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material)
        {
            material=new Material(Shader.Find(kind=="Leaf"?"Universal Render Pipeline/Particles/Lit":"Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material,path);
        }
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/FactionsHQ/SoftMote.png"));
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);
        material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);material.SetFloat("_Smoothness",.08f);material.renderQueue=3000;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetShaderPassEnabled("ShadowCaster",false);
        EditorUtility.SetDirty(material);return material;
    }
    private static void Budget(ParticleSystem particles,float rate)
    {
        var budget=particles.gameObject.GetComponent<KingdomRushParticleBudget>();
        if(!budget)budget=particles.gameObject.AddComponent<KingdomRushParticleBudget>();budget.authoredRate=rate;
    }
    private static ParticleSystem Particles(Transform parent,string name,Vector3 p,float rate,float lifetimeLow,float lifetimeHigh,float sizeLow,float sizeHigh,Color tint,string kind)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=p;
        var particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        bool leaf=kind=="Leaf",fog=kind=="Mist";
        var main=particles.main;main.loop=true;main.duration=10;main.startLifetime=new ParticleSystem.MinMaxCurve(lifetimeLow,lifetimeHigh);
        main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(sizeLow,sizeHigh);main.startColor=tint;
        main.maxParticles=fog?6:16;main.simulationSpace=ParticleSystemSimulationSpace.Local;
        main.startRotation3D=leaf;main.startRotationZ=new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);
        if(leaf){main.startRotationX=new ParticleSystem.MinMaxCurve(-.3f,.3f);main.startRotationY=new ParticleSystem.MinMaxCurve(-.4f,.4f);}
        var emission=particles.emission;emission.rateOverTime=rate;Budget(particles,rate);
        var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=fog?new Vector3(2.5f,.1f,3):new Vector3(1.5f,.6f,2);
        var velocity=particles.velocityOverLifetime;velocity.enabled=false;velocity.space=ParticleSystemSimulationSpace.Local;
        velocity.x=new ParticleSystem.MinMaxCurve(leaf?.08f:.015f,leaf?.16f:.035f);
        velocity.y=new ParticleSystem.MinMaxCurve(leaf?-.10f:.018f,leaf?-.06f:.045f);
        velocity.z=new ParticleSystem.MinMaxCurve(.025f,.065f);velocity.enabled=true;
        var noise=particles.noise;noise.enabled=true;noise.strength=leaf?.08f:.025f;noise.frequency=.22f;noise.scrollSpeed=.07f;noise.octaveCount=1;
        if(leaf)
        {
            var rotation=particles.rotationOverLifetime;rotation.separateAxes=true;
            rotation.x=new ParticleSystem.MinMaxCurve(.02f,.08f);rotation.y=new ParticleSystem.MinMaxCurve(.03f,.09f);rotation.z=new ParticleSystem.MinMaxCurve(.02f,.07f);rotation.enabled=true;
        }
        var color=particles.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.25f),new GradientAlphaKey(.6f,.75f),new GradientAlphaKey(0,1)});color.color=gradient;
        var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=ParticleMaterial(kind);
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        if(leaf){renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=LeafMesh();}
        particles.Play();return particles;
    }
}
