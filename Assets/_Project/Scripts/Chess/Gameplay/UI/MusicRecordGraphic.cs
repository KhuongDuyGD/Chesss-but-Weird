using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class MusicRecordGraphic : AntialiasedUIGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = rectTransform.rect;
        Vector2 center = r.center;
        float radius = Mathf.Min(r.width, r.height) * .46f;
        Ring(mesh, center, radius, SketchbookUI.Ink, true);
        for (int i = 0; i < 4; i++)
            Ring(mesh, center, radius * (.57f + i * .09f), new Color(.52f, .50f, .55f, .6f), false);
        Ring(mesh, center, radius * .35f, SketchbookUI.Pink, true);
        Ring(mesh, center, radius * .07f, SketchbookUI.White, true);
        UIEdgeMesh.Stroke(mesh, new[] { center + new Vector2(-radius * .14f, radius * .21f),
            center + new Vector2(radius * .10f, radius * .20f) }, SketchbookUI.Ink, 1.8f);
    }

    private static void Ring(VertexHelper mesh, Vector2 center, float radius, Color color, bool fill)
    {
        var points = new Vector2[96];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i * Mathf.PI * 2 / points.Length;
            points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        if (fill) UIEdgeMesh.Fill(mesh, points, color);
        else UIEdgeMesh.Stroke(mesh, points, color, 1f, true);
    }
}
