using UnityEditor;
using UnityEngine;

/// <summary>Keep menu artwork smooth at its displayed size without changing board, piece or HUD textures.</summary>
public sealed class MenuTextureImportSettings : AssetPostprocessor
{
    public override uint GetVersion() => 1;

    private void OnPreprocessTexture()
    {
        if (!IsMenuArtwork(assetPath)) return;
        Apply((TextureImporter)assetImporter);
    }

    public static bool IsMenuArtwork(string path) => MenuTextureSampling.IsMenuArtwork(path);

    public static void Apply(TextureImporter importer)
    {
        importer.filterMode = FilterMode.Trilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.mipmapFilter = TextureImporterMipFilter.BoxFilter;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.anisoLevel = 1;
        importer.mipMapBias = 0;
        // Retain GPU compression and existing size budgets, using its highest quality for thin ink lines.
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.compressionQuality = 100;
        importer.crunchedCompression = false;
        foreach (string target in new[] { "Standalone", "Android", "iPhone", "WebGL" })
        {
            var platform = importer.GetPlatformTextureSettings(target);
            if (!platform.overridden) continue;
            platform.textureCompression = TextureImporterCompression.CompressedHQ;
            platform.compressionQuality = 100;
            platform.crunchedCompression = false;
            platform.format = TextureImporterFormat.Automatic;
            importer.SetPlatformTextureSettings(platform);
        }
    }
}
