using UnityEngine;
using UnityEngine.UI;

// Vector shield and star: stays crisp at every resolution, with no font-symbol dependency.
[DisallowMultipleComponent]
public sealed class RankBadgeGraphic : MaskableGraphic
{
    private Color accent = new Color(.45f, .49f, .55f);
    private bool placed;
    public void Configure(Color value, bool ranked)
    {
        raycastTarget = false;
        if (accent == value && placed == ranked) return;
        accent = value; placed = ranked; SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = GetPixelAdjustedRect();
        float size = Mathf.Min(rect.width, rect.height);
        Vector2 center = rect.center;
        Vector2[] shield = { new Vector2(0, .49f), new Vector2(.31f, .36f), new Vector2(.46f, 0),
            new Vector2(.31f, -.34f), new Vector2(0, -.49f), new Vector2(-.31f, -.34f), new Vector2(-.46f, 0), new Vector2(-.31f, .36f) };
        Polygon(mesh, center, shield, size, accent);
        Polygon(mesh, center, shield, size * .91f, new Color(.10f, .12f, .16f));
        Polygon(mesh, center, shield, size * .75f, Color.Lerp(new Color(.12f, .15f, .18f), accent, .38f));
        Vector2[] star = new Vector2[10];
        for (int i = 0; i < star.Length; i++)
        {
            float angle = Mathf.PI * .5f - i * Mathf.PI / 5;
            float radius = i % 2 == 0 ? .31f : .15f;
            star[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        Polygon(mesh, center, star, size, placed ? accent : new Color(.45f, .48f, .52f));
    }

    private static void Polygon(VertexHelper mesh, Vector2 center, Vector2[] points, float scale, Color fill)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(center, fill, Vector2.zero);
        foreach (var point in points) mesh.AddVert(center + point * scale, fill, Vector2.zero);
        for (int i = 0; i < points.Length; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % points.Length);
    }
}
