using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared pixel-coverage material for menu geometry, independent of camera MSAA/post processing.</summary>
public abstract class AntialiasedUIGraphic : MaskableGraphic
{
    private static Material sharedMaterial;

    internal static Material SharedEdgeMaterial
    {
        get
        {
            if (!sharedMaterial)
            {
                var shader = Resources.Load<Shader>("UI/MenuEdgeAntialiasing");
                if (!shader) return null;
                sharedMaterial = new Material(shader) { name = "Menu Edge Antialiasing", hideFlags = HideFlags.HideAndDontSave };
            }
            return sharedMaterial;
        }
    }

    public override Material defaultMaterial => SharedEdgeMaterial ? SharedEdgeMaterial : base.defaultMaterial;

    protected override void OnEnable()
    {
        base.OnEnable();
        EnableChannels();
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        EnableChannels();
    }

    protected override void OnTransformParentChanged()
    {
        base.OnTransformParentChanged();
        EnableChannels();
    }

    public override void Rebuild(CanvasUpdate update)
    {
        // Runtime menu builders add the Graphic before parenting it. Its OnEnable
        // therefore has no canvas, and SetParent raises OnTransformParentChanged
        // rather than reliably raising OnCanvasHierarchyChanged. Prepare the
        // colour streams before UGUI submits the very first mesh as well.
        if (update == CanvasUpdate.PreRender) EnableChannels();
        base.Rebuild(update);
    }

    private void EnableChannels()
    {
        EnableCanvasChannels(canvas);
    }

    internal static void EnableCanvasChannels(Canvas target)
    {
        if (!target) return;
        const AdditionalCanvasShaderChannels required = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
        var channels = target.additionalShaderChannels;
        if ((channels & required) != required) target.additionalShaderChannels = channels | required;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetMaterial()
    {
        if (!sharedMaterial) return;
        if (Application.isPlaying) Destroy(sharedMaterial);
        else DestroyImmediate(sharedMaterial);
        sharedMaterial = null;
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterCleanup()
    {
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ResetMaterial;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ResetMaterial;
    }
    private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
    { if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) ResetMaterial(); }
#endif
}
