using UnityEngine;
using UnityEngine.UI;

/// <summary>Intentional, reusable ink doodles for the first screen. All geometry is static.</summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class MainMenuInkGraphic : AntialiasedUIGraphic
{
    public enum Drawing { Crown, Spark, Arrow, Gear, Heart, Corner, Underline }
    private Drawing drawing;
    private float lineWidth = 2.5f;
    public void Configure(Drawing value, Color ink, float width = 2.5f)
    {
        drawing = value; color = ink; lineWidth = width; raycastTarget = false; SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        switch (drawing)
        {
            case Drawing.Crown:
                Path(mesh, new[] { P(15,70), P(8,27), P(32,44), P(48,8), P(65,44), P(91,24), P(84,72), P(15,70) });
                Path(mesh, new[] { P(20,83), P(49,85), P(79,82) });
                Path(mesh, new[] { P(32,57), P(39,61), P(53,62), P(62,57) });
                break;
            case Drawing.Spark:
                Segment(mesh, P(50,6), P(50,29)); Segment(mesh, P(50,71), P(50,94));
                Segment(mesh, P(5,50), P(28,50)); Segment(mesh, P(72,50), P(96,50));
                Segment(mesh, P(17,17), P(33,33)); Segment(mesh, P(68,68), P(84,84));
                break;
            case Drawing.Arrow:
                Path(mesh, new[] { P(5,51), P(45,49), P(84,52) });
                Path(mesh, new[] { P(60,26), P(87,52), P(62,78) });
                break;
            case Drawing.Gear:
                var gear = new Vector2[33];
                for (int i = 0; i < gear.Length; i++)
                {
                    float angle = i * Mathf.PI / 16f;
                    float radius = i % 4 < 2 ? 42f : 33f;
                    gear[i] = P(50 + Mathf.Cos(angle) * radius, 50 + Mathf.Sin(angle) * radius);
                }
                Path(mesh, gear); Circle(mesh, 50, 50, 16);
                break;
            case Drawing.Heart:
                Path(mesh, new[] { P(50,88), P(16,53), P(9,33), P(15,17), P(31,13), P(49,30),
                    P(65,12), P(83,16), P(91,32), P(85,51), P(50,88) });
                break;
            case Drawing.Corner:
                Path(mesh, new[] { P(5,90), P(6,7), P(93,5) });
                break;
            case Drawing.Underline:
                Path(mesh, new[] { P(2,46), P(27,40), P(55,43), P(81,38), P(97,43) });
                Path(mesh, new[] { P(10,63), P(38,56), P(69,59), P(91,54) });
                break;
        }
    }

    private Vector2 P(float x, float y)
    {
        Rect r = rectTransform.rect;
        return new Vector2(r.xMin + x * r.width / 100f, r.yMax - y * r.height / 100f);
    }
    private void Path(VertexHelper mesh, Vector2[] points)
    { UIEdgeMesh.Stroke(mesh, points, color, lineWidth); }
    private void Circle(VertexHelper mesh, float x, float y, float radius)
    {
        var points = new Vector2[24];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i * Mathf.PI / 12f;
            points[i] = P(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
        }
        UIEdgeMesh.Stroke(mesh, points, color, lineWidth, true);
    }
    private void Segment(VertexHelper mesh, Vector2 a, Vector2 b)
    {
        UIEdgeMesh.Stroke(mesh, new[] { a, b }, color, lineWidth);
    }
}
