using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

// Registers faction-specific content using the existing catalog/loader contracts.
// The archived mixed arena stays addressable for a future mode, outside the public catalog.
public static class KingdomRushFactionSetup
{
    public static readonly string[] Ids = { CosmeticSelection.KingdomRushDenasId, CosmeticSelection.KingdomRushVeznanId };
    public static readonly string[] Factions = { "Denas", "Veznan" };
    private const string Root = KingdomRushGameSetup.Root;
    [Serializable] private sealed class Palette { public Spec[] materials; }
    [Serializable] private sealed class Spec { public string name, albedo, normal, metalSmooth; public float[] color; public float metallic, roughness, emissionStrength; public float[] emission; }

    [MenuItem("Chess/Kingdom Rush/Register Faction Collections")]
    public static void Setup() => KingdomRushGameSetup.Setup();

    internal static void Register(AddressableAssetSettings settings, CosmeticCatalog catalog)
    {
        string palettePath = Root + "/faction_material_palette.json";
        if (!File.Exists(palettePath)) throw new InvalidOperationException("Build faction Blender assets before registering the collection.");
        PrepareMaterials(palettePath);
        KingdomRushAdvancedArtSetup.RefineEnvironmentMaterials();
        for (int f = 0; f < Factions.Length; f++)
        {
            string faction = Factions[f], id = Ids[f], label = f == 0 ? "Denas — Royal Guard" : "Vez'Nan — Dark Dominion";
            string group = "PieceSkin_KR_" + faction;
            var pieces = KingdomRushGameSetup.LoadOrCreate<PieceSkinData>("Assets/Game/Data/PieceSkins/KingdomRush" + faction + ".asset");
            pieces.skinId = id; pieces.displayName = "Kingdom Rush — " + label;
            pieces.applyTeamColor = false; pieces.useAuthoredBoardScale = true; pieces.faceOpponent = true; pieces.tuning.Clear();
            pieces.preview = KingdomRushGameSetup.Preview(settings, Root + "/Previews/06_Quan_vua_" + faction + ".png", faction + "Pieces");
            for (int kind = 0; kind < KingdomRushGameSetup.Prefixes.Length; kind++)
            for (int side = 0; side < 2; side++)
            {
                string sculpt = KingdomRushGameSetup.Prefixes[kind] + "_" + faction;
                var original = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + sculpt + ".prefab");
                if (!original) throw new InvalidOperationException("Missing approved sculpt " + sculpt);
                var node = (GameObject)PrefabUtility.InstantiatePrefab(original);
                string name = sculpt + (side == 0 ? "_White" : "_Black"); node.name = name;
                try
                {
                    // Team identity is on the stone foot, preserving every painted character surface.
                    var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder); band.name = "Team identity foot rim";
                    UnityEngine.Object.DestroyImmediate(band.GetComponent<Collider>());
                    band.transform.SetParent(node.transform, false); band.transform.localPosition = new Vector3(0,.017f,0);
                    band.transform.localScale = new Vector3(.78f,.017f,.78f);
                    band.GetComponent<Renderer>().sharedMaterial = TeamMaterial(faction, side);
                    string path = Root + "/Prefabs/" + name + ".prefab";
                    var prefab = PrefabUtility.SaveAsPrefabAsset(node,path);
                    KingdomRushGameSetup.Register(settings,prefab,group,"Pieces/" + id + "/" + name);
                    Assign(pieces,kind,side,new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(path)));
                }
                finally { UnityEngine.Object.DestroyImmediate(node); }
            }
            KingdomRushGameSetup.Register(settings,pieces,group,"PieceSkin/" + id);
            KingdomRushGameSetup.Upsert(catalog.pieceSkins,pieces,id,pieces.displayName,pieces.preview,"LEGENDARY");
            var board = KingdomRushGameSetup.LoadOrCreate<BoardSkinData>("Assets/Game/Data/Boards/KingdomRush" + faction + ".asset");
            board.boardId=id; board.displayName="Kingdom Rush — " + (f == 0 ? "Denas Royal Citadel" : "Vez'Nan Ruin Throne");
            board.boardPrefab=KingdomRushGameSetup.ModelPrefab(settings,"KR_"+faction+"_Board","Board_KR_"+faction,"Boards/"+id);
            KingdomRushLivingWorldSetup.ConfigureBoard(faction);
            board.preview=KingdomRushGameSetup.Preview(settings,Root+"/Previews/KR_"+faction+"_Board.png",faction+"Board");
            board.localPosition=board.localRotation=Vector3.zero;board.localScale=Vector3.one;
            board.prisonLayoutYaw=-90;board.loopAnimation=false;board.frameFullModel=true;
            board.cameraFramePadding=1.04f;
            KingdomRushGameSetup.Register(settings,board,"Board_KR_"+faction,"Board/"+id);
            KingdomRushGameSetup.Upsert(catalog.boards,board,id,board.displayName,board.preview,"LEGENDARY");
            var env=KingdomRushGameSetup.LoadOrCreate<EnvironmentData>("Assets/Game/Data/Environments/KingdomRush"+faction+".asset");
            env.environmentId=id;env.displayName="Kingdom Rush — "+(f==0?"Linirea Royal Heartlands":"Vez'Nan Scorched Dominion");
            env.environmentPrefab=KingdomRushGameSetup.ModelPrefab(settings,"KR_"+faction+"_Environment","Environment_KR_"+faction,"Environments/"+id);
            KingdomRushFactionPresentationSetup.Configure(faction);
            env.preview=KingdomRushGameSetup.Preview(settings,Root+"/Previews/KR_"+faction+"_Environment.png",faction+"Background");
            env.localPosition=env.localRotation=Vector3.zero;env.localScale=Vector3.one;env.useBoardScale=true;
            env.horizonColor=f==0?new Color(.34f,.48f,.18f,1f):new Color(.115f,.105f,.15f,1f);
            KingdomRushGameSetup.Register(settings,env,"Environment_KR_"+faction,"Environment/"+id);
            KingdomRushGameSetup.Upsert(catalog.environments,env,id,env.displayName,env.preview,"LEGENDARY");
            foreach(var asset in new UnityEngine.Object[]{pieces,board,env})EditorUtility.SetDirty(asset);
        }
        catalog.pieceSkins.RemoveAll(e=>e!=null&&e.id==CosmeticSelection.KingdomRushId);
        catalog.boards.RemoveAll(e=>e!=null&&e.id==CosmeticSelection.KingdomRushId);
        EditorUtility.SetDirty(catalog);
    }
    private static Material TeamMaterial(string faction,int side)
    {
        string path=Root+"/Materials/KRF_Team_"+faction+"_"+side+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        mat.color=side==0?new Color(.86f,.86f,.77f):new Color(.075f,.095f,.13f);mat.SetFloat("_Smoothness",.28f);EditorUtility.SetDirty(mat);return mat;
    }
    private static void Assign(PieceSkinData p,int k,int side,AssetReferenceGameObject value)
    {
        string[] fields={"pawn","rook","knight","bishop","queen","king"};
        string field=side==0?fields[k]:"black"+char.ToUpperInvariant(fields[k][0])+fields[k].Substring(1);
        typeof(PieceSkinData).GetField(field).SetValue(p,value);
    }
    private static void PrepareMaterials(string path)
    {
        var palette=JsonUtility.FromJson<Palette>(File.ReadAllText(path));
        var mats=new System.Collections.Generic.Dictionary<string,Material>();
        foreach(var s in palette.materials)
        {
            string name="KRF_"+Regex.Replace(s.name,"[^A-Za-z0-9]+","_").Trim('_');
            string target=Root+"/Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(target);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,target);}
            mat.SetColor("_BaseColor",new Color(s.color[0],s.color[1],s.color[2],1));
            mat.SetFloat("_Metallic",s.metallic);mat.SetFloat("_Smoothness",1-s.roughness);mat.SetFloat("_Cull",2);
            if(s.emissionStrength>0)
            {
                mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(s.emission[0],s.emission[1],s.emission[2])*s.emissionStrength);
                mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            else {mat.DisableKeyword("_EMISSION");mat.SetColor("_EmissionColor",Color.black);mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;}
            if(!string.IsNullOrEmpty(s.albedo))
            {
                ConfigureTexture(s.albedo,true);ConfigureTexture(s.normal,false,true);ConfigureTexture(s.metalSmooth,false);
                mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(s.albedo));
                mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(s.normal));
                mat.SetFloat("_BumpScale",.48f);mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(s.metalSmooth));
                mat.SetFloat("_Metallic",1);mat.SetFloat("_Smoothness",1);mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            }
            else if(new[]{"tile","stone","earth","rock","meadow"}.Any(k=>s.name.ToLowerInvariant().Contains(k)))
            {
                mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/KR_Stone_Normal.png"));
                mat.SetFloat("_BumpScale",.22f);mat.EnableKeyword("_NORMALMAP");
            }
            mat.enableInstancing=true;EditorUtility.SetDirty(mat);mats[s.name]=mat;
        }
        foreach(string faction in Factions)foreach(string suffix in new[]{"Board","Environment"})
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/Models/KR_"+faction+"_"+suffix+".fbx");
            if(!importer)throw new InvalidOperationException("Missing faction model");
            importer.globalScale=1;importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
            importer.isReadable=true;importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            foreach(var pair in mats)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),pair.Key),pair.Value);
            importer.SaveAndReimport();
        }
    }
    private static void ConfigureTexture(string path,bool srgb,bool normal=false)
    {
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(!importer)throw new InvalidOperationException("Missing HQ surface texture "+path);
        var type=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        if(importer.textureType==type&&importer.sRGBTexture==srgb&&importer.mipmapEnabled&&importer.anisoLevel==16&&importer.wrapMode==TextureWrapMode.Repeat&&importer.filterMode==FilterMode.Trilinear&&importer.textureCompression==TextureImporterCompression.CompressedHQ)return;
        importer.textureType=type;importer.sRGBTexture=srgb;importer.mipmapEnabled=true;importer.anisoLevel=16;
        importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.maxTextureSize=2048;
        importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.compressionQuality=100;importer.SaveAndReimport();
    }
}
