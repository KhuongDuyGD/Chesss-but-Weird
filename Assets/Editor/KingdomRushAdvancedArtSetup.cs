using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Non-destructive, repeatable presentation derivatives; approved FBX and source maps stay intact.
public static class KingdomRushAdvancedArtSetup
{
    private const string Root=KingdomRushGameSetup.Root;
    [Serializable] private sealed class Manifest { public Surface[] models; }
    [Serializable] private sealed class Surface
    {
        public string name,albedo,emission;
        public float[] magicAnchor;
        public float emissiveCoverage;
    }
    private static Manifest surfaces;
    internal static void Prepare()
    {
        surfaces=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/advanced_surface_manifest.json"));
        foreach(var spec in surfaces.models)
        {
            Texture(spec.albedo,true,4096);
            if(!string.IsNullOrEmpty(spec.emission))Texture(spec.emission,true,1024);
            string path=Root+"/Materials/KRA_"+spec.name+".mat";
            var original=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/KR_Art_"+spec.name+".mat");
            if(!original)throw new InvalidOperationException("Missing approved character material "+spec.name);
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(original);AssetDatabase.CreateAsset(material,path);}
            else material.CopyPropertiesFromMaterial(original);
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(spec.albedo));
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.82f);
            material.enableInstancing=true;
            if(spec.emissiveCoverage>.001f)
            {
                material.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(spec.emission));
                material.SetColor("_EmissionColor",Color.white*1.65f);material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            else {material.DisableKeyword("_EMISSION");material.SetColor("_EmissionColor",Color.black);}
            EditorUtility.SetDirty(material);
        }
    }
    internal static void ConfigurePiece(GameObject root,string name)
    {
        var spec=surfaces.models.Single(s=>s.name==name);
        var surface=root.GetComponentsInChildren<MeshRenderer>().Single();
        surface.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/KRA_"+name+".mat");
        if(spec.emissiveCoverage>.001f)
        {
            var magic=root.AddComponent<KingdomRushPieceMagic>();magic.surface=surface;
            if(spec.magicAnchor!=null&&spec.magicAnchor.Length==3)
            {
                // Sample actual imported vertices/UVs so FBX axis conversion cannot mirror the anchor.
                var mesh=surface.GetComponent<MeshFilter>().sharedMesh;var points=mesh.vertices;var uv=mesh.uv;
                var map=new Texture2D(2,2);map.LoadImage(File.ReadAllBytes(spec.emission));
                Vector3 sum=Vector3.zero;float weights=0,height=KingdomRushGameSetup.BoundsOf(root).size.y;
                try
                {
                    for(int i=0;i<points.Length;i++)
                    {
                        var p=root.transform.InverseTransformPoint(surface.transform.TransformPoint(points[i]));
                        if(p.y<height*.80f||Mathf.Abs(p.x)<height*.15f)continue;
                        var color=map.GetPixelBilinear(uv[i].x,uv[i].y);float weight=color.g-Mathf.Max(color.r*.85f,color.b);
                        if(weight<.08f)continue;sum+=p*weight;weights+=weight;
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(map);}
                if(weights<=0)throw new InvalidOperationException("Existing staff magic anchor not found: "+name);
                var anchor=sum/weights-Vector3.up*.025f;
                magic.staffFlame=KingdomRushLivingWorldSetup.Flame(root.transform,anchor,height*.15f,"Soul");
            }
        }
    }
    internal static void RefineEnvironmentMaterials()
    {
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}))
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if(!material.name.StartsWith("KRF_KR_HQ_",StringComparison.Ordinal))continue;
            string name=material.name;
            if(name.Contains("New_leaf")||name.Contains("Deep_leaf")||name.Contains("Leaf_highlights"))
            {
                material.shader=Shader.Find("ChessButWeird/KingdomRush/Foliage");
                if(!material.shader)throw new InvalidOperationException("Foliage shader did not import");
            }
            material.SetFloat("_BumpScale",name.Contains("tile")?.12f:.16f);
            if(name.Contains("Ash_tile"))material.SetColor("_BaseColor",new Color(1.06f,1.06f,1.08f));
            if(name.Contains("River"))
            {
                material.SetFloat("_EnvironmentReflections",0);material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            EditorUtility.SetDirty(material);
        }
    }
    private static void Texture(string path,bool srgb,int size)
    {
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(!importer)throw new InvalidOperationException("Missing advanced surface "+path);
        if(importer.maxTextureSize==size&&importer.mipmapEnabled&&importer.anisoLevel==16&&importer.sRGBTexture==srgb&&importer.filterMode==FilterMode.Trilinear&&importer.textureCompression==TextureImporterCompression.CompressedHQ)return;
        importer.textureType=TextureImporterType.Default;importer.maxTextureSize=size;importer.sRGBTexture=srgb;
        importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=16;
        importer.wrapMode=TextureWrapMode.Clamp;importer.alphaIsTransparency=false;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.compressionQuality=100;
        importer.SaveAndReimport();
    }
}
