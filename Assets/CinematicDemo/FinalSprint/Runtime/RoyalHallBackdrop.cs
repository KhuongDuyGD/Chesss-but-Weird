#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ChessButWeird.CinematicDemo.FinalSprint
{
    // Development scene decoration only. Never owns the reward, camera, RT or graph.
    [DisallowMultipleComponent, RequireComponent(typeof(FinalDemoRoot))]
    public sealed class RoyalHallBackdrop : MonoBehaviour
    {
        public Texture2D backdrop;
        public Shader backdropShader;
        public RawImage Image { get; private set; }
        public bool Applied { get; private set; }
        public Camera DecoratedCamera { get; private set; }
        public int SurfaceAllocations { get; private set; }
        public int SurfaceReleases { get; private set; }
        FinalDemoRoot root;
        GameObject surface;
        Mesh mesh;
        Material material;
        const string SurfaceName = "Royal hall camera background";
        void Awake() { root = GetComponent<FinalDemoRoot>(); }
        void OnEnable() { RenderPipelineManager.beginCameraRendering += BeforeCamera; }
        void LateUpdate()
        {
            if (!backdrop || !backdropShader || !root || !root.Canvas || !root.View || !root.View.ritual) return;
            if (!Applied)
            {
                var go = new GameObject("Royal hall external backdrop", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                go.transform.SetParent(root.Canvas.transform, false);
                go.transform.SetAsFirstSibling();
                Image = go.GetComponent<RawImage>(); Image.texture = backdrop;
                Image.raycastTarget = false;
                var fitter = go.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = (float)backdrop.width / backdrop.height;
                Applied = true;
            }
            var camera = root.Host?.Factory.Current?.Ritual ? root.Host.Factory.Current.Ritual.cinematicCamera : null;
            if (camera != DecoratedCamera || (DecoratedCamera && !surface))
            {
                ReleaseSurface();
                if (camera) CreateSurface(camera);
            }
            // Destroyed Unity objects compare equal to null; release our separate native resources too.
            if (!camera && (material || mesh || surface)) ReleaseSurface();
        }
        void CreateSurface(Camera camera)
        {
            DecoratedCamera = camera;
            surface = new GameObject(SurfaceName, typeof(MeshFilter), typeof(MeshRenderer));
            surface.hideFlags = HideFlags.DontSave;
            surface.layer = camera.gameObject.layer;
            surface.transform.SetParent(camera.transform, false);
            mesh = new Mesh { name = "Owned royal hall background quad", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0,2,1,0,3,2 }; mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            material = new Material(backdropShader) { name = "Owned royal hall background material", hideFlags = HideFlags.DontSave, mainTexture = backdrop };
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            SurfaceAllocations++;
            FitCamera(camera);
        }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        { if (camera == DecoratedCamera && surface) FitCamera(camera); }
        void FitCamera(Camera camera)
        {
            // Farther than the stage, inside the existing far clip. Opaque geometry occludes it normally.
            float depth = Mathf.Lerp(camera.nearClipPlane, camera.farClipPlane, .96f);
            float height = camera.orthographic ? 2 * camera.orthographicSize : 2 * depth * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
            surface.transform.localPosition = new Vector3(0,0,depth);
            surface.transform.localRotation = Quaternion.identity;
            surface.transform.localScale = new Vector3(height * camera.aspect, height, 1);
            float imageAspect = (float)backdrop.width / backdrop.height;
            Vector2 scale = camera.aspect > imageAspect ? new Vector2(1, imageAspect / camera.aspect) : new Vector2(camera.aspect / imageAspect, 1);
            material.mainTextureScale = scale; material.mainTextureOffset = (Vector2.one - scale) * .5f;
        }
        void ReleaseSurface()
        {
            bool owned = material || mesh || surface;
            if (surface) Destroy(surface);
            if (material) Destroy(material);
            if (mesh) Destroy(mesh);
            surface = null; material = null; mesh = null; DecoratedCamera = null;
            if (owned) SurfaceReleases++;
        }
        void ReleaseDecoration()
        {
            ReleaseSurface();
            if (Image) Destroy(Image.gameObject);
            Image = null; Applied = false;
        }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= BeforeCamera; ReleaseDecoration(); }
        void OnDestroy() { RenderPipelineManager.beginCameraRendering -= BeforeCamera; ReleaseDecoration(); }
    }
}
#endif
