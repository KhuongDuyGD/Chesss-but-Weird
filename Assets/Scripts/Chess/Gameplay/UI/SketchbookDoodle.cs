using UnityEngine;
using UnityEngine.UI;

// Small vector doodles share one mesh per icon; no textures or per-frame animation.
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class SketchbookDoodle : AntialiasedUIGraphic
{
    public enum Shape { Star, Pawn, Board, Coin, Diamond, Ticket, Scribble, Paper, Check, Queen, Rook, Bishop, Knight, Trophy }
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
            case Shape.Queen:
                Silhouette(vh, new[] {new Vector2(18,22),new Vector2(32,41),new Vector2(39,18),new Vector2(50,39),
                    new Vector2(64,18),new Vector2(70,42),new Vector2(84,22),new Vector2(73,59),new Vector2(28,59)});
                Path(vh,new[]{new Vector2(32,67),new Vector2(68,67),new Vector2(73,80),new Vector2(27,80)},true,color);
                PieceBase(vh); break;
            case Shape.Rook:
                Silhouette(vh,new[]{new Vector2(20,16),new Vector2(34,16),new Vector2(34,29),new Vector2(44,29),
                    new Vector2(44,16),new Vector2(58,16),new Vector2(58,29),new Vector2(68,29),new Vector2(68,16),
                    new Vector2(82,16),new Vector2(78,44),new Vector2(68,50),new Vector2(70,78),new Vector2(30,78),new Vector2(33,50),new Vector2(23,44)});
                Path(vh,new[]{new Vector2(34,50),new Vector2(67,50)},false,color); PieceBase(vh); break;
            case Shape.Bishop:
                Silhouette(vh,new[]{new Vector2(50,9),new Vector2(68,30),new Vector2(71,42),new Vector2(62,54),
                    new Vector2(58,60),new Vector2(69,78),new Vector2(31,78),new Vector2(42,60),new Vector2(31,46),new Vector2(30,35)});
                Path(vh,new[]{new Vector2(58,25),new Vector2(43,43)},false,color,5); PieceBase(vh); break;
            case Shape.Knight:
                Silhouette(vh,new[]{new Vector2(28,78),new Vector2(37,57),new Vector2(24,61),new Vector2(13,48),
                    new Vector2(39,20),new Vector2(43,10),new Vector2(54,18),new Vector2(68,15),new Vector2(80,35),
                    new Vector2(79,58),new Vector2(72,78)});
                Circle(vh,new Vector2(46,33),3,color);
                Path(vh,new[]{new Vector2(63,32),new Vector2(65,48),new Vector2(56,64)},false,color); PieceBase(vh); break;
            case Shape.Trophy:
                Path(vh,new[]{new Vector2(23,23),new Vector2(10,23),new Vector2(12,44),new Vector2(30,53)},false,color,5);
                Path(vh,new[]{new Vector2(77,23),new Vector2(90,23),new Vector2(88,44),new Vector2(70,53)},false,color,5);
                Silhouette(vh,new[]{new Vector2(23,13),new Vector2(77,13),new Vector2(72,48),new Vector2(61,61),new Vector2(39,61),new Vector2(28,48)});
                Path(vh,new[]{new Vector2(50,63),new Vector2(50,79)},false,color,6); PieceBase(vh); break;
            case Shape.Check:
                Path(vh, new[] { new Vector2(15, 51), new Vector2(40, 77), new Vector2(88, 20) }, false, color, 12f); break;
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

    private void Silhouette(VertexHelper vh, Vector2[] points)
    { Polygon(vh, points, SketchbookUI.White); Path(vh, points, true, color); }
    private void PieceBase(VertexHelper vh)
    {
        Silhouette(vh,new[]{new Vector2(29,81),new Vector2(71,81),new Vector2(79,93),new Vector2(21,93)});
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
