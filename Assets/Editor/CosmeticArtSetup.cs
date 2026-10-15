using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CosmeticArtSetup
{
    private const string Celestial = "Assets/Skins/Tazji'sCelestialBoard/Celestial_Legendary_Board_Animated.fbx";

    [MenuItem("Chess/Update Cosmetic Art and Animated Board")]
    public static void Setup()
    {
        var importer = AssetImporter.GetAtPath(Celestial) as ModelImporter;
        if (!importer) throw new InvalidOperationException("Celestial FBX is missing.");
        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Legacy;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips) { clip.loopTime = true; clip.wrapMode = WrapMode.Loop; }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        DefaultLowPolyCosmeticsSetup.Setup();
        GachaSkinAssetsSetup.Setup();
        // Halloween has art but has no backend unityAssetKey yet. Import the
        // thumbnails now; register its models only when that contract exists.
        foreach (string path in Directory.GetFiles("Assets/Skins/Tazji'sHalloween", "*.png", SearchOption.AllDirectories))
        {
            var texture = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!texture) continue;
            texture.textureType = TextureImporterType.Sprite;
            texture.spriteImportMode = SpriteImportMode.Single;
            texture.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        Inspect();
    }

    public static void Inspect()
    {
        Directory.CreateDirectory("Logs/skin-update");
        using (var output = new StreamWriter("Logs/skin-update/import.txt"))
        foreach (string path in new[] { "Assets/Skins/Tazji'sLowPoly/Chess_Arena/Chess_Arena.fbx", Celestial })
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!model) throw new InvalidOperationException("Model failed to import: " + path);
            output.WriteLine(path);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                output.WriteLine(renderer.name + " bounds=" + renderer.bounds);
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(clip => !clip.name.StartsWith("__preview__")))
                output.WriteLine("Animation: " + clip.name + " length=" + clip.length + " legacy=" + clip.legacy + " wrap=" + clip.wrapMode);
        }
        Debug.Log("[CosmeticArt] Import details: Logs/skin-update/import.txt");
    }
}
