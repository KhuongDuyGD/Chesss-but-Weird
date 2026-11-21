using UnityEngine;

// Owned by the selected environment, including older backgrounds without KR lighting.
// Suppresses the original scene's sky artwork at map edges; leaves reflection/ambient
// settings and all HUD graphics alone. Restores the camera when the environment unloads.
[DisallowMultipleComponent]
public sealed class CosmeticEnvironmentBackdrop : MonoBehaviour
{
    private Camera view;
    private CameraClearFlags previousClearFlags;
    private Color previousColor, horizon;
    private bool configured;
    private Material skybox, previousSkybox;

    public void ConfigureSkybox(Material material)
    {
        skybox = material; previousSkybox = RenderSettings.skybox;
        RenderSettings.skybox = skybox;
        configured = true; Apply();
    }

    public void Configure(Color color)
    {
        horizon = color; horizon.a = 1f;
        configured = true;
        Apply();
    }

    private void OnEnable()
    {
        if (!configured) return;
        if (skybox) { previousSkybox = RenderSettings.skybox; RenderSettings.skybox = skybox; }
        Apply();
    }
    private void LateUpdate() { if (configured) Apply(); }

    private void Apply()
    {
        var current = Camera.main;
        if (view != current)
        {
            Restore();
            view = current;
            if (view) { previousClearFlags = view.clearFlags; previousColor = view.backgroundColor; }
        }
        if (!view) return;
        view.clearFlags = skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        if (!skybox) view.backgroundColor = horizon;
    }

    private void OnDisable()
    {
        Restore();
        if (skybox && RenderSettings.skybox == skybox) RenderSettings.skybox = previousSkybox;
    }
    private void Restore()
    {
        if (view) { view.clearFlags = previousClearFlags; view.backgroundColor = previousColor; }
        view = null;
    }
}
