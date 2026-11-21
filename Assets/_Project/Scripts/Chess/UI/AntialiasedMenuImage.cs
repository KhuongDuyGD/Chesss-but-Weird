using UnityEngine;
using UnityEngine.UI;

/// <summary>Image-compatible menu primitive. Sprite artwork keeps its standard textured rendering.</summary>
[AddComponentMenu("UI/Antialiased Menu Image")]
public sealed class AntialiasedMenuImage : Image
{
    private bool IsSolidRectangle => !overrideSprite && type == Type.Simple;

    public override Material defaultMaterial => IsSolidRectangle && AntialiasedUIGraphic.SharedEdgeMaterial
        ? AntialiasedUIGraphic.SharedEdgeMaterial : base.defaultMaterial;

    protected override void OnEnable()
    {
        base.OnEnable();
        AntialiasedUIGraphic.EnableCanvasChannels(canvas);
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        AntialiasedUIGraphic.EnableCanvasChannels(canvas);
    }

    protected override void OnTransformParentChanged()
    {
        base.OnTransformParentChanged();
        AntialiasedUIGraphic.EnableCanvasChannels(canvas);
    }

    public override void Rebuild(CanvasUpdate update)
    {
        if (update == CanvasUpdate.PreRender && IsSolidRectangle)
            AntialiasedUIGraphic.EnableCanvasChannels(canvas);
        base.Rebuild(update);
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        if (!IsSolidRectangle) { base.OnPopulateMesh(mesh); return; }
        mesh.Clear();
        var rect = GetPixelAdjustedRect();
        if (rect.width <= 0 || rect.height <= 0) return;
        UIEdgeMesh.Fill(mesh, new[]
        {
            new Vector2(rect.xMin,rect.yMin), new Vector2(rect.xMax,rect.yMin),
            new Vector2(rect.xMax,rect.yMax), new Vector2(rect.xMin,rect.yMax)
        }, color, true); // Fully transparent slider hit areas still need a registered mesh for raycasting.
    }
}
