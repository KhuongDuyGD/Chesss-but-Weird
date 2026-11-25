using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Shared render capture used by the current cosmetic and HUD verification tools.
public static class KingdomRushArtReviewCapture
{
    public static void Write(Camera camera, string path, int width, int height)
    {
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        target.Create();
        var previous = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            // Batch captures have no player frames between scene import and the
            // first request. Complete shader compilation before reading pixels.
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var request = new RenderPipeline.StandardRequest { destination = target };
            try
            {
                for (int frame = 0; frame < 4; frame++) RenderPipeline.SubmitRenderRequest(camera, request);
            }
            finally { ShaderUtil.allowAsyncCompilation = previousAsync; }
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
