using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Render the actual imported Unity meshes and materials from several angles.
public static class KingdomRushArtReviewCapture
{
    private const string Root = "Assets/Skins/KingdomRushCollab";
    private const string Output = "Logs/KingdomRush/ArtReview";

    public static void PrepareAndCapture()
    {
        KingdomRushArtImport.Prepare();
        Capture();
    }

    [MenuItem("Chess/Kingdom Rush/Capture Imported Multiangle Review")]
    public static void Capture()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene(Root + "/Scenes/KR_ArtReview.unity");
        var camera = Camera.main;
        camera.orthographicSize = 8.4f;
        for (int i = 0; i < 4; i++)
        {
            float angle = (i * 90f + 35f) * Mathf.Deg2Rad;
            camera.transform.position = new Vector3(Mathf.Sin(angle) * 18, 17, -Mathf.Cos(angle) * 18);
            camera.transform.LookAt(Vector3.zero);
            Write(camera, Output + "/Board_" + i + ".png", 1280, 960);
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.62f, .62f, .62f);
        var sun = new GameObject("Studio key").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 1.25f; sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(40, -35, 0);
        var fill = new GameObject("Studio fill").AddComponent<Light>();
        fill.type = LightType.Directional; fill.intensity = .4f;
        fill.transform.rotation = Quaternion.Euler(25, 145, 0);
        camera = new GameObject("Imported piece review").AddComponent<Camera>();
        camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.27f, .29f, .32f); camera.nearClipPlane = .02f;
        foreach (string file in Directory.GetFiles(Root + "/Models", "0*.fbx").OrderBy(p => p))
        {
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/')));
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var marker = model.GetComponentsInChildren<Transform>().Single(t => t.name == "KR_Forward");
            var forward = (marker.position - model.transform.position).normalized;
            float h = bounds.size.y;
            camera.orthographicSize = h * .68f;
            string folder = Output + "/" + Path.GetFileNameWithoutExtension(file); Directory.CreateDirectory(folder);
            for (int i = 0; i < 5; i++)
            {
                var direction = Quaternion.AngleAxis(i * 90f, Vector3.up) * forward;
                float elevation = i == 4 ? 2.8f : .65f;
                camera.transform.position = bounds.center + direction * h * (i == 4 ? 1.2f : 3f) + Vector3.up * h * elevation;
                camera.transform.LookAt(bounds.center);
                Write(camera, folder + "/" + i + ".png", 640, 640);
            }
            UnityEngine.Object.DestroyImmediate(model);
        }
        EditorSceneManager.OpenScene(Root + "/Scenes/KR_ArtReview.unity");
        Debug.Log("[KingdomRushArt] Imported PBR multiangle review captured: 12 models x 5 views + 4 board views.");
    }

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
