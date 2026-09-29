using UnityEngine;
using UnityEngine.UI;

// Small vector doodles share one mesh per icon; no textures or per-frame animation.
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class SketchbookDoodle : AntialiasedUIGraphic
{
    public enum Shape { Star, Pawn, Board, Coin, Diamond, Ticket, Scribble, Paper }
    private Shape shape;

    public void Configure(Shape value, Color ink) { shape = value; color = ink; SetVerticesDirty(); }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (shape == Shape.Paper)
        {
            for (float y = 32; y < rectTransform.rect.height; y += 44)
                Line(vh, new Vector2(0, y), new Vector2(rectTransform.rect.width, y + 1.2f), 1f, color, false);
            return;
        }
        switch (shape)
        {
            case Shape.Star:
                var star = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = (-90 + i * 36) * Mathf.Deg2Rad;
                    float r = i % 2 == 0 ? 46 : 21;
                    star[i] = new Vector2(50 + Mathf.Cos(a) * r, 50 + Mathf.Sin(a) * r);
                }
                Polygon(vh, star, color); Path(vh, star, true, SketchbookUI.Ink); break;
            case Shape.Pawn:
                Circle(vh, new Vector2(50, 22), 15, SketchbookUI.White);
                Path(vh, new[] { new Vector2(38, 42), new Vector2(62, 42), new Vector2(60, 51),
                    new Vector2(68, 72), new Vector2(32, 72), new Vector2(40, 51) }, true, color);
                Path(vh, new[] { new Vector2(31, 79), new Vector2(69, 78), new Vector2(77, 91),
                    new Vector2(23, 92), new Vector2(31, 79) }, false, color); break;
            case Shape.Board:
                for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++)
                    if ((x + y) % 2 == 0) Polygon(vh, new[] { new Vector2(9+x*21,9+y*21),new Vector2(30+x*21,9+y*21),
                        new Vector2(30+x*21,30+y*21),new Vector2(9+x*21,30+y*21) }, color);
                Path(vh, new[] {new Vector2(8,8),new Vector2(93,9),new Vector2(92,93),new Vector2(9,92)},true,color); break;
            case Shape.Coin:
                Circle(vh, new Vector2(50, 50), 41, SketchbookUI.Yellow);
                Circle(vh, new Vector2(50, 50), 29, Color.clear);
                Path(vh, new[] {new Vector2(58,31),new Vector2(37,36),new Vector2(38,51),new Vector2(62,54),
                    new Vector2(62,67),new Vector2(39,72)},false,color); break;
            case Shape.Diamond:
                Polygon(vh, new[] {new Vector2(25,17),new Vector2(75,17),new Vector2(94,40),new Vector2(50,92),new Vector2(7,40)}, SketchbookUI.Blue);
                Path(vh, new[] {new Vector2(25,17),new Vector2(75,17),new Vector2(94,40),new Vector2(50,92),new Vector2(7,40)},true,color);
                Path(vh,new[]{new Vector2(7,40),new Vector2(94,40)},false,color);
                Path(vh,new[]{new Vector2(25,17),new Vector2(39,40),new Vector2(50,92),new Vector2(62,40),new Vector2(75,17)},false,color); break;
            case Shape.Ticket:
                Path(vh,new[]{new Vector2(7,22),new Vector2(92,22),new Vector2(92,40),new Vector2(82,50),new Vector2(93,62),
                    new Vector2(93,81),new Vector2(7,80),new Vector2(7,61),new Vector2(17,50),new Vector2(7,39)},true,color);
                for(int y=29;y<80;y+=13) Line(vh,new Vector2(66,y),new Vector2(66,y+6),3,color);
                Path(vh,new[]{new Vector2(28,39),new Vector2(48,38),new Vector2(42,64)},false,color); break;
            case Shape.Scribble:
                for(int i=0;i<5;i++) Path(vh,new[]{new Vector2(3+i*2,22+i*12),new Vector2(35,17+i*12),
                    new Vector2(67,24+i*12),new Vector2(98-i,18+i*12)},false,color,7f); break;
        }
    }

    private Vector2 Point(Vector2 p, bool normalize = true)
    {
        Rect r = rectTransform.rect;
        return new Vector2(r.xMin + p.x * (normalize ? r.width / 100 : 1), r.yMax - p.y * (normalize ? r.height / 100 : 1));
    }
    private void Path(VertexHelper vh, Vector2[] points, bool close, Color ink, float width = 3.6f)
    {
        var localPoints = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++) localPoints[i] = Point(points[i]);
        float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / 100f;
        UIEdgeMesh.Stroke(vh, localPoints, ink, width * scale, close);
    }
    private void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color ink, bool normalize = true)
    {
        a = Point(a, normalize); b = Point(b, normalize);
        float scale = normalize ? Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / 100f : 1;
        UIEdgeMesh.Stroke(vh, new[] { a, b }, ink, width * scale);
    }
    private void Polygon(VertexHelper vh, Vector2[] points, Color fill)
    {
        var localPoints = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++) localPoints[i] = Point(points[i]);
        UIEdgeMesh.Fill(vh, localPoints, fill);
    }
    private void Circle(VertexHelper vh, Vector2 center, float radius, Color fill)
    {
        var points=new Vector2[32];
        for(int i=0;i<points.Length;i++)
        {
            float a=i*Mathf.PI*2/points.Length;
            float r=radius+Mathf.Sin(i*2.3f)*.5f;
            points[i]=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;
        }
        Polygon(vh,points,fill); Path(vh,points,true,color);
    }
}
