using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEngine.AddressableAssets;

namespace CBWRelease
{
    // Read-only import/reference checks. No content build, network, scene save or economic calls.
    public static class CosmeticReleaseVerification
    {
        [Serializable] sealed class Check { public string name; public bool passed; }
        [Serializable] sealed class Result { public string utc; public string unity; public bool passed; public string scope="Editor import and static Addressables checks; not Player certification"; public List<Check> checks=new List<Check>(); public string error; }
        public static void Run()
        {
            var result=new Result{utc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion};
            string output=Environment.GetCommandLineArgs().FirstOrDefault(x=>x.StartsWith("-cbwEvidence=",StringComparison.Ordinal))?.Substring(13);
            if(string.IsNullOrEmpty(output))throw new ArgumentException("Explicit -cbwEvidence=<external result path> required");
            Action<bool,string> need=(ok,name)=>{result.checks.Add(new Check{name=name,passed=ok});if(!ok)throw new InvalidOperationException(name);};
            try
            {
                need(Application.isBatchMode,"Explicit batch verification");
                var catalog=AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/Game/Data/CosmeticCatalog.asset");
                var settings=AddressableAssetSettingsDefaultObject.Settings;
                need(catalog&&settings,"Catalog and Addressables settings import");
                need(catalog.pieceSkins.Count==3&&catalog.boards.Count==4,"Three piece sets and four boards");
                need(catalog.defaultPieceSkinId=="tazji_lowpoly"&&catalog.defaultBoardId=="tazji_lowpoly","Category-scoped default IDs preserved");
                foreach(var entry in catalog.pieceSkins.Concat(catalog.boards))
                {
                    string key=entry.data.AssetGUID;string path=AssetDatabase.GUIDToAssetPath(key);
                    need(!string.IsNullOrEmpty(path)&&settings.FindAssetEntry(key)!=null,entry.id+" data GUID registered");
                    need(entry.thumbnail&&entry.HasArt,entry.id+" exact thumbnail imports");
                    var thumbPath=AssetDatabase.GetAssetPath(entry.thumbnail);
                    var importer=AssetImporter.GetAtPath(thumbPath) as TextureImporter;
                    need(importer&&importer.textureType==TextureImporterType.Sprite&&importer.spriteImportMode==SpriteImportMode.Single,entry.id+" single Sprite importer");
                    need(settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(thumbPath))!=null,entry.id+" thumbnail registered");
                    var piece=AssetDatabase.LoadAssetAtPath<PieceSkinData>(path);
                    var board=AssetDatabase.LoadAssetAtPath<BoardSkinData>(path);
                    need(catalog.pieceSkins.Contains(entry)?piece!=null:board!=null,entry.id+" category separation");
                    var refs=piece?new[]{piece.pawn,piece.rook,piece.knight,piece.bishop,piece.queen,piece.king,piece.blackPawn,piece.blackRook,piece.blackKnight,piece.blackBishop,piece.blackQueen,piece.blackKing}:new[]{board.boardPrefab};
                    foreach(AssetReferenceGameObject reference in refs)
                    {
                        if(reference==null||!reference.RuntimeKeyIsValid())continue; // optional black overrides
                        var modelPath=AssetDatabase.GUIDToAssetPath(reference.AssetGUID);
                        var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                        need(model&&model.GetComponentsInChildren<Renderer>(true).Length>0,entry.id+" model "+reference.AssetGUID);
                        need(settings.FindAssetEntry(reference.AssetGUID)!=null,entry.id+" model address registered "+reference.AssetGUID);
                    }
                    if(board&&board.loopAnimation)
                    {
                        string modelPath=AssetDatabase.GUIDToAssetPath(board.boardPrefab.AssetGUID);
                        var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                        var animation=model.GetComponent<Animation>();
                        need(animation&&animation.clip&&animation.clip.legacy,entry.id+" Legacy Animation compatible with BoardSkinManager");
                        need(animation.clip.length>0&&AnimationUtility.GetAnimationClipSettings(animation.clip).loopTime,entry.id+" non-empty looping clip");
                        var instance=UnityEngine.Object.Instantiate(model);
                        try
                        {
                            var a=instance.GetComponent<Animation>();a.wrapMode=WrapMode.Loop;a.clip.SampleAnimation(instance,a.clip.length*.5f);
                            need(instance.GetComponentsInChildren<Transform>(true).All(t=>float.IsFinite(t.localPosition.x)),entry.id+" sampled animation finite");
                        }
                        finally{UnityEngine.Object.DestroyImmediate(instance);}
                    }
                }
                result.passed=true;
            }
            catch(Exception ex){result.error=ex.ToString();Debug.LogException(ex);}
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(result,true));
            if(!result.passed)throw new InvalidOperationException("Cosmetic verification failed: "+result.error);
            Debug.Log("CBW_RELEASE_COSMETIC_PASS checks="+result.checks.Count);
        }
    }
}
