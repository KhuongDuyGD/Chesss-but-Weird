using System;
using UnityEngine;

/// <summary>Menu bitmap sampling, also used by file-loaded fallbacks that bypass Unity's texture importer.</summary>
public static class MenuTextureSampling
{
    private static readonly float[] LinearChannels = CreateLinearChannels();
    private static readonly string[] MenuFolders =
    {
        "Assets/Materials/Main_Menu/", "Assets/Materials/Gacha_menu/", "Assets/Materials/LANUI/",
        "Assets/Materials/MultiplayerUI/", "Assets/Materials/PlayerProfile/", "Assets/Materials/Result_Menu/",
        "Assets/Materials/SettingsMenu/", "Assets/Resources/Main_Menu/", "Assets/Resources/MainMenu/"
    };

    public static bool IsMenuArtwork(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        path = path.Replace('\\', '/');
        foreach (string folder in MenuFolders)
            if (path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    public static void Configure(Texture2D texture)
    {
        if (!texture) return;
        texture.filterMode = FilterMode.Trilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.anisoLevel = 1;
        texture.mipMapBias = 0f;
    }

    /// <summary>Generate alpha-weighted mip colours once during loading, then release the CPU copy.</summary>
    public static void FinishRuntimeTexture(Texture2D texture)
    {
        Configure(texture);
        if (!texture || !texture.isReadable) return;
        var pixels = texture.GetPixels32();
        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear && texture.isDataSRGB;
        int width = texture.width, height = texture.height;
        for (int level = 0; level < texture.mipmapCount; level++)
        {
            // Bilinear sampling may read a transparent neighbour. Give it the adjacent ink colour, never black.
            var upload = DilateTransparentColour(pixels, width, height, linear);
            texture.SetPixels32(upload, level);
            if (level == texture.mipmapCount - 1) break;
            int nextWidth = Mathf.Max(1, width / 2), nextHeight = Mathf.Max(1, height / 2);
            var next = new Color32[nextWidth * nextHeight];
            for (int y = 0; y < nextHeight; y++) for (int x = 0; x < nextWidth; x++)
            {
                int alpha = 0, samples = 0;
                float red = 0, green = 0, blue = 0;
                // Area bounds include the last row/column in non-power-of-two artwork.
                int x0 = x * width / nextWidth, x1 = (x + 1) * width / nextWidth;
                int y0 = y * height / nextHeight, y1 = (y + 1) * height / nextHeight;
                for (int sy = y0; sy < y1; sy++) for (int sx = x0; sx < x1; sx++)
                {
                    var p = pixels[sy * width + sx];
                    alpha += p.a; red += Decode(p.r,linear) * p.a; green += Decode(p.g,linear) * p.a; blue += Decode(p.b,linear) * p.a; samples++;
                }
                next[y * nextWidth + x] = alpha > 0
                    ? new Color32(Encode(red / alpha,linear), Encode(green / alpha,linear), Encode(blue / alpha,linear), (byte)((alpha + samples / 2) / samples))
                    : new Color32(0, 0, 0, 0);
            }
            pixels = next; width = nextWidth; height = nextHeight;
        }
        texture.Apply(false, true);
    }

    private static Color32[] DilateTransparentColour(Color32[] pixels, int width, int height, bool linear)
    {
        var output = (Color32[])pixels.Clone();
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int index = y * width + x;
            if (pixels[index].a != 0) continue;
            int weight = 0;
            float red = 0, green = 0, blue = 0;
            Accumulate(x > 0 ? index - 1 : index);
            Accumulate(x + 1 < width ? index + 1 : index);
            Accumulate(y > 0 ? index - width : index);
            Accumulate(y + 1 < height ? index + width : index);
            if (weight > 0) output[index] = new Color32(Encode(red / weight,linear), Encode(green / weight,linear), Encode(blue / weight,linear), 0);

            void Accumulate(int neighbour)
            {
                var p = pixels[neighbour];
                weight += p.a; red += Decode(p.r,linear) * p.a; green += Decode(p.g,linear) * p.a; blue += Decode(p.b,linear) * p.a;
            }
        }
        return output;
    }

    private static float Decode(byte channel,bool linear) => linear ? LinearChannels[channel] : channel / 255f;
    private static byte Encode(float channel,bool linear) => (byte)Mathf.Clamp(Mathf.RoundToInt((linear ? Mathf.LinearToGammaSpace(channel) : channel) * 255),0,255);
    private static float[] CreateLinearChannels()
    {
        var channels=new float[256];
        for(int i=0;i<channels.Length;i++) channels[i]=Mathf.GammaToLinearSpace(i/255f);
        return channels;
    }
}
