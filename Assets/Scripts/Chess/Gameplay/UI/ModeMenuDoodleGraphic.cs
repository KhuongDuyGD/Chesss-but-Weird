using UnityEngine;
using UnityEngine.UI;

/// <summary>Small, static ink meshes. Utility drawings preserve the chest, diamond, silly avatar and gear.</summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class ModeMenuDoodleGraphic : AntialiasedUIGraphic
{
    public enum Drawing { Local, Online, Aram, Shop, Inventory, Gacha, Profile, Settings }
    private Drawing drawing;
    private static readonly Color Ink = new Color(.15f, .145f, .17f);
    private static readonly Color White = new Color(1f, .994f, .964f);
    private static readonly Color Blue = new Color(.40f, .61f, .94f);
    private static readonly Color Gold = new Color(1f, .78f, .25f);

    public Drawing Illustration => drawing;

    public void Configure(Drawing value)
    {
        drawing = value;
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        switch (drawing)
        {
            case Drawing.Local:
                Ground(mesh, 50, 88, 43);
                Pawn(mesh, 4, 16, 45, 72, White, false);
                Pawn(mesh, 51, 22, 43, 67, Blue, true);
                break;
            case Drawing.Online:
                Ground(mesh, 50, 90, 45);
                Board(mesh, 21, 44, 58);
                Pawn(mesh, 1, 24, 32, 57, White, false);
                Pawn(mesh, 67, 24, 32, 57, Blue, false);
                Path(mesh, new[] { V(34,16), V(42,9), V(50,7), V(59,9), V(66,16) }, Ink, 2.2f);
                Path(mesh, new[] { V(41,22), V(47,17), V(53,17), V(59,22) }, Ink, 2.2f);
                Disc(mesh, V(50,27), 2.4f, Ink);
                break;
            case Drawing.Aram:
                Ground(mesh, 51, 93, 39);
                Pawn(mesh, 17, 21, 59, 72, White, true);
                FillPath(mesh, new[] { V(30,25), V(27,7), V(42,16), V(52,2), V(61,16), V(76,7), V(72,27) }, Gold);
                Path(mesh, new[] { V(37,26), V(66,27) }, new Color(.89f,.44f,.18f), 2.2f);
                FillPath(mesh, new[] { V(66,59), V(94,63), V(89,92), V(61,87) }, White);
                Disc(mesh,V(74,69),2.3f,Ink); Disc(mesh,V(83,82),2.3f,Ink); Disc(mesh,V(72,80),2.3f,Ink);
                Path(mesh, new[] { V(87,13), V(85,32) }, new Color(.85f,.28f,.26f), 3.2f);
                Disc(mesh, V(84,40), 2f, new Color(.85f,.28f,.26f));
                break;
            case Drawing.Shop:
                Ground(mesh, 50, 91, 40);
                FillPath(mesh, new[] { V(16,29), V(84,26), V(90,85), V(13,88) }, new Color(.63f,.78f,.96f));
                Path(mesh, new[] { V(34,35), V(34,21), V(38,12), V(48,9), V(60,12), V(65,21), V(65,33) }, Ink, 2.9f);
                Pawn(mesh, 34, 41, 32, 42, White, false);
                FillPath(mesh, new[] { V(78,20), V(92,29), V(85,43), V(73,33) }, Gold);
                Disc(mesh, V(80,27), 1.8f, Ink);
                break;
            case Drawing.Inventory: Chest(mesh); break;
            case Drawing.Gacha: Diamond(mesh); break;
            case Drawing.Profile: Portrait(mesh); break;
            case Drawing.Settings: Gear(mesh); break;
        }
    }

    private void Chest(VertexHelper mesh)
    {
        Ground(mesh, 50, 90, 39);
        FillPath(mesh, new[] { V(14,43), V(87,41), V(85,87), V(14,88) }, new Color(.77f,.52f,.32f));
        FillPath(mesh, new[] { V(13,18), V(86,16), V(88,42), V(12,45) }, new Color(.88f,.64f,.40f));
        FillPath(mesh, new[] { V(19,20), V(25,20), V(24,41), V(18,42) }, new Color(.99f,.86f,.62f), false);
        FillPath(mesh, new[] { V(74,18), V(80,18), V(81,41), V(75,41) }, new Color(.99f,.86f,.62f), false);
        var grain = new Color(.40f,.25f,.14f,.32f);
        Path(mesh, new[] { V(30,29), V(44,26), V(68,28) }, grain, 1.1f);
        Path(mesh, new[] { V(31,34), V(49,33), V(67,35) }, grain, 1.1f);
        Path(mesh, new[] { V(23,65), V(33,63), V(40,64) }, grain, 1.1f);
        Path(mesh, new[] { V(60,72), V(78,70) }, grain, 1.1f);
        Path(mesh, new[] { V(22,80), V(45,78), V(58,80) }, grain, 1.1f);
        FillPath(mesh, new[] { V(41,40), V(60,40), V(59,64), V(42,64) }, new Color(.96f,.81f,.48f));
        Disc(mesh, V(51,49), 3.2f, Ink);
        Path(mesh, new[] { V(51,49), V(51,56) }, Ink, 2.5f);
        Path(mesh, new[] { V(18,23), V(36,22), V(53,22) }, new Color(1f,1f,1f,.36f), 1.5f);
    }

    private void Diamond(VertexHelper mesh)
    {
        Ground(mesh, 50, 91, 24);
        Fill(mesh, new[] { V(50,10), V(82,49), V(50,87), V(18,49) }, new Color(1f,.89f,.56f));
        Fill(mesh, new[] { V(18,49), V(50,49), V(50,87) }, new Color(1f,.70f,.28f));
        Fill(mesh, new[] { V(50,49), V(82,49), V(50,87) }, new Color(.97f,.58f,.26f));
        Fill(mesh, new[] { V(50,10), V(18,49), V(40,42) }, White);
        Path(mesh, new[] { V(50,10), V(82,49), V(50,87), V(18,49), V(50,10) }, Ink, 2.9f);
        Path(mesh, new[] { V(50,10), V(40,49), V(50,87), V(62,49), V(50,10) }, new Color(.67f,.40f,.14f,.65f), 1.6f);
        Path(mesh, new[] { V(18,49), V(82,49) }, Ink, 1.8f);
        Path(mesh, new[] { V(5,27), V(14,32) }, Gold, 2.8f);
        Path(mesh, new[] { V(5,62), V(14,59) }, Gold, 2.8f);
        Path(mesh, new[] { V(87,26), V(95,21) }, Gold, 2.8f);
        Path(mesh, new[] { V(87,65), V(96,70) }, Gold, 2.8f);
    }

    private void Portrait(VertexHelper mesh)
    {
        Disc(mesh, V(51,53), 42, new Color(.29f,.24f,.18f,.08f));
        Circle(mesh, V(50,49), 42, Blue);
        FillPath(mesh, new[] { V(23,82), V(27,73), V(37,65), V(63,65), V(74,74), V(78,82), V(64,89), V(39,89) }, White);
        Circle(mesh, V(50,41), 24, White);
        Path(mesh, new[] { V(35,39), V(40,34), V(45,39) }, Ink, 2.5f);
        Path(mesh, new[] { V(55,39), V(60,34), V(65,39) }, Ink, 2.5f);
        Path(mesh, new[] { V(37,48), V(44,54), V(53,55), V(64,48) }, Ink, 2.5f);
        FillPath(mesh, new[] { V(47,53), V(59,53), V(58,66), V(54,71), V(49,69), V(46,64), V(47,53) }, new Color(.99f,.57f,.66f));
        Path(mesh, new[] { V(53,56), V(53,65) }, new Color(.74f,.29f,.40f), 1.4f);
        Path(mesh, new[] { V(15,28), V(18,23), V(23,19) }, new Color(1f,1f,1f,.50f), 1.8f);
    }

    private void Gear(VertexHelper mesh)
    {
        Disc(mesh, V(52,54), 39, new Color(.29f,.24f,.18f,.08f));
        var outline = new Vector2[33];
        for (int i = 0; i < outline.Length; i++)
        {
            float angle = i * Mathf.PI / 16f;
            float radius = i % 4 < 2 ? 42 : 33;
            outline[i] = V(50 + Mathf.Cos(angle)*radius, 49 + Mathf.Sin(angle)*radius);
        }
        FillPath(mesh, outline, new Color(.88f,.88f,.85f));
        Circle(mesh, V(50,49), 16, White);
        Path(mesh, new[] { V(28,32), V(33,27), V(39,25) }, new Color(1f,1f,1f,.70f), 2.1f);
    }

    private void Pawn(VertexHelper mesh, float x, float y, float width, float height, Color fill, bool angry)
    {
        Vector2 A(float px, float py) => V(x + px * width, y + py * height);
        FillPath(mesh, new[] { A(.36f,.36f), A(.64f,.36f), A(.61f,.54f), A(.79f,.79f), A(.20f,.79f), A(.39f,.54f) }, fill);
        FillPath(mesh, new[] { A(.20f,.80f), A(.79f,.80f), A(.89f,.95f), A(.11f,.95f) }, fill);
        Circle(mesh, A(.5f,.2f), width*.235f, fill);
        float eyeRadius = Mathf.Max(.9f, width*.025f);
        Disc(mesh, A(.41f,.18f), eyeRadius, Ink); Disc(mesh, A(.61f,.18f), eyeRadius, Ink);
        if (angry)
        {
            Path(mesh, new[] { A(.34f,.10f), A(.46f,.14f) }, Ink, 1.5f);
            Path(mesh, new[] { A(.57f,.14f), A(.69f,.10f) }, Ink, 1.5f);
            Path(mesh, new[] { A(.43f,.29f), A(.51f,.26f), A(.60f,.29f) }, Ink, 1.3f);
        }
        else Path(mesh, new[] { A(.41f,.27f), A(.49f,.30f), A(.58f,.27f) }, Ink, 1.3f);
        Path(mesh, new[] { A(.28f,.88f), A(.73f,.87f) }, new Color(.26f,.22f,.18f,.28f), 1f);
    }

    private void Board(VertexHelper mesh, float x, float y, float size)
    {
        FillPath(mesh, new[] { V(x,y), V(x+size,y), V(x+size,y+size*.66f), V(x,y+size*.66f) }, White);
        for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++)
        {
            if ((row + column) % 2 != 0) continue;
            float left = x+column*size/4, top = y+row*size*.66f/4;
            Fill(mesh, new[] { V(left,top), V(left+size/4,top), V(left+size/4,top+size*.66f/4), V(left,top+size*.66f/4) }, new Color(.38f,.40f,.43f,.38f));
        }
    }

    private void Ground(VertexHelper mesh, float x, float y, float radius)
    {
        Ellipse(mesh, V(x,y), radius, 3.2f, new Color(.29f,.24f,.18f,.035f));
        Ellipse(mesh, V(x,y), radius*.83f, 2.2f, new Color(.29f,.24f,.18f,.055f));
    }

    private static Vector2 V(float x, float y) => new Vector2(x,y);
    private Vector2 P(Vector2 point)
    {
        Rect rect = rectTransform.rect;
        return new Vector2(rect.xMin+point.x*rect.width/100f, rect.yMax-point.y*rect.height/100f);
    }

    private void FillPath(VertexHelper mesh, Vector2[] points, Color fill, bool outline = true)
    {
        Fill(mesh, points, fill);
        if (outline) Path(mesh, points, Ink, 2.3f, true);
    }

    private void Fill(VertexHelper mesh, Vector2[] points, Color fill)
    {
        var localPoints = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++) localPoints[i] = P(points[i]);
        UIEdgeMesh.Fill(mesh, localPoints, fill);
    }

    private void Circle(VertexHelper mesh, Vector2 center, float radius, Color fill)
    {
        var points = Ring(center, radius, radius, 28);
        FillPath(mesh, points, fill);
    }
    private void Disc(VertexHelper mesh, Vector2 center, float radius, Color fill) => Ellipse(mesh, center, radius, radius, fill, 10);
    private void Ellipse(VertexHelper mesh, Vector2 center, float rx, float ry, Color fill, int steps = 24) => Fill(mesh, Ring(center,rx,ry,steps), fill);
    private static Vector2[] Ring(Vector2 center, float rx, float ry, int steps)
    {
        var points = new Vector2[steps];
        for (int i = 0; i < steps; i++)
        {
            float angle = i*Mathf.PI*2/steps;
            float wobble = 1f + Mathf.Sin(i*2.3f)*.005f;
            points[i] = center + new Vector2(Mathf.Cos(angle)*rx, Mathf.Sin(angle)*ry)*wobble;
        }
        return points;
    }

    private void Path(VertexHelper mesh, Vector2[] points, Color ink, float width, bool closed = false)
    {
        var localPoints = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++) localPoints[i] = P(points[i]);
        float pixelWidth = width*Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/100;
        UIEdgeMesh.Stroke(mesh, localPoints, ink, pixelWidth, closed);
    }
}
