using UnityEngine;
using UnityEngine.Rendering;

namespace ChessButWeird.BuffVfx
{
    // Render-only additive displacement, restored before the next input/raycast update.
    public sealed class CameraCueCoordinator : MonoBehaviour
    {
        private float remaining,amplitude;
        private Camera displaced;
        private Vector3 original;
        public void Pulse(float worldAmplitude){amplitude=Mathf.Max(amplitude,worldAmplitude);remaining=.18f;}
        public void Cancel(){Restore();remaining=amplitude=0;}
        private void OnEnable(){RenderPipelineManager.beginCameraRendering+=Begin;RenderPipelineManager.endCameraRendering+=End;Camera.onPreCull+=BuiltinBegin;Camera.onPostRender+=BuiltinEnd;}
        private void OnDisable(){RenderPipelineManager.beginCameraRendering-=Begin;RenderPipelineManager.endCameraRendering-=End;Camera.onPreCull-=BuiltinBegin;Camera.onPostRender-=BuiltinEnd;Restore();remaining=amplitude=0;}
        private void Update(){remaining=Mathf.Max(0,remaining-Time.unscaledDeltaTime);if(remaining==0)amplitude=0;}
        private void Begin(ScriptableRenderContext context,Camera camera)=>Apply(camera);
        private void End(ScriptableRenderContext context,Camera camera){if(camera==displaced)Restore();}
        private void BuiltinBegin(Camera camera){if(!GraphicsSettings.currentRenderPipeline)Apply(camera);}
        private void BuiltinEnd(Camera camera){if(!GraphicsSettings.currentRenderPipeline&&camera==displaced)Restore();}
        private void Apply(Camera camera)
        {
            if(displaced||camera!=Camera.main||remaining<=0||UserSettings.Presentation.Shake<=0||UserSettings.Presentation.ReducedMotion)return;
            displaced=camera;original=camera.transform.position;
            float amount=amplitude*UserSettings.Presentation.Shake*(remaining/.18f)*Mathf.Sin(remaining*65);
            camera.transform.position=original+camera.transform.up*amount;
        }
        private void Restore(){if(displaced)displaced.transform.position=original;displaced=null;}
    }
}
