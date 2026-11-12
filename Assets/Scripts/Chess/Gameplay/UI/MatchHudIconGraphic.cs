using UnityEngine;
using UnityEngine.UI;

/// <summary>Small vector controls, independent of the font's available glyphs.</summary>
[RequireComponent(typeof(RectTransform),typeof(CanvasRenderer))]
public sealed class MatchHudIconGraphic : AntialiasedUIGraphic
{
    public enum Symbol { Power, Locked, Unlocked, Focus, Journal, Turn, Thinking, Played, Waiting, Camera }
    private Symbol symbol;
    public void Configure(Symbol value, Color ink)
    {
        if (symbol == value && color == ink) return;
        symbol = value; color = ink; raycastTarget = false; SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        switch (symbol)
        {
            case Symbol.Power:
                Arc(mesh, .5f, .47f, .35f, 135, 405);
                Line(mesh, .5f, .94f, .5f, .49f); break;
            case Symbol.Camera:
                Path(mesh,new Vector2(.12f,.25f),new Vector2(.64f,.25f),new Vector2(.64f,.75f),new Vector2(.12f,.75f),new Vector2(.12f,.25f));
                Path(mesh,new Vector2(.64f,.59f),new Vector2(.9f,.77f),new Vector2(.9f,.23f),new Vector2(.64f,.41f)); break;
            case Symbol.Locked:
            case Symbol.Unlocked:
                Path(mesh, new Vector2(.23f,.17f), new Vector2(.77f,.17f), new Vector2(.77f,.57f), new Vector2(.23f,.57f), new Vector2(.23f,.17f));
                Arc(mesh, symbol == Symbol.Locked ? .5f : .7f, .58f, .22f, 0, 180);
                Line(mesh,.5f,.3f,.5f,.43f); break;
            case Symbol.Focus:
                Path(mesh,new Vector2(.15f,.62f),new Vector2(.15f,.85f),new Vector2(.38f,.85f));
                Path(mesh,new Vector2(.62f,.85f),new Vector2(.85f,.85f),new Vector2(.85f,.62f));
                Path(mesh,new Vector2(.85f,.38f),new Vector2(.85f,.15f),new Vector2(.62f,.15f));
                Path(mesh,new Vector2(.38f,.15f),new Vector2(.15f,.15f),new Vector2(.15f,.38f)); break;
            case Symbol.Journal:
                Path(mesh,new Vector2(.2f,.15f),new Vector2(.8f,.15f),new Vector2(.8f,.85f),new Vector2(.2f,.85f),new Vector2(.2f,.15f));
                Line(mesh,.35f,.66f,.66f,.66f); Line(mesh,.35f,.48f,.66f,.48f); Line(mesh,.35f,.3f,.56f,.3f); break;
            case Symbol.Turn:
                Line(mesh,.15f,.5f,.82f,.5f);
                Path(mesh,new Vector2(.53f,.79f),new Vector2(.82f,.5f),new Vector2(.53f,.21f)); break;
            case Symbol.Thinking:
                Path(mesh,new Vector2(.24f,.85f),new Vector2(.76f,.85f),new Vector2(.7f,.65f),new Vector2(.3f,.35f),new Vector2(.24f,.15f),new Vector2(.76f,.15f),new Vector2(.7f,.35f),new Vector2(.3f,.65f),new Vector2(.24f,.85f)); break;
            case Symbol.Played:
                Path(mesh,new Vector2(.17f,.48f),new Vector2(.4f,.25f),new Vector2(.84f,.77f)); break;
            case Symbol.Waiting:
                Arc(mesh,.5f,.5f,.26f,0,360); break;
        }
    }
    private Vector2 Point(Vector2 point)
    { var r=rectTransform.rect; return new Vector2(r.xMin+point.x*r.width,r.yMin+point.y*r.height); }
    private void Path(VertexHelper mesh, params Vector2[] points)
    {
        for(int i=0;i<points.Length;i++) points[i]=Point(points[i]);
        UIEdgeMesh.Stroke(mesh,points,color,Mathf.Max(1.5f,Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.075f));
    }
    private void Line(VertexHelper mesh,float x,float y,float x2,float y2)=>Path(mesh,new Vector2(x,y),new Vector2(x2,y2));
    private void Arc(VertexHelper mesh,float x,float y,float radius,float from,float to)
    {
        var points=new Vector2[25];
        for(int i=0;i<points.Length;i++) {float angle=Mathf.Lerp(from,to,i/24f)*Mathf.Deg2Rad;points[i]=new Vector2(x+Mathf.Cos(angle)*radius,y+Mathf.Sin(angle)*radius);}
        Path(mesh,points);
    }
}
