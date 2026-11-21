using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Coverage bands around UI silhouettes. Fragment derivatives keep their smoothing one pixel wide.</summary>
public static class UIEdgeMesh
{
    private const float Fringe = 4f;

    public static void Fill(VertexHelper mesh, IList<Vector2> points, Color fill, bool includeTransparent = false)
    {
        if (points.Count < 3 || (fill.a <= 0f && !includeTransparent)) return;
        if ((points[0] - points[points.Count - 1]).sqrMagnitude < .00001f)
        {
            var contour = new Vector2[points.Count - 1];
            for (int i = 0; i < contour.Length; i++) contour[i] = points[i];
            points = contour;
            if (points.Count < 3) return;
        }
        Vector2 center = Center(points);
        float distance = InnerDistance(points, center);
        var outside = Offset(points, Fringe);
        int first = mesh.currentVertCount;
        Add(mesh,center,distance,0,fill,fill);
        for (int i=0;i<points.Count;i++) Add(mesh,points[i],0,0,fill,fill);
        for (int i=0;i<points.Count;i++) Add(mesh,outside[i],-Fringe,0,fill,fill);
        for (int i=0;i<points.Count;i++)
        {
            int next=(i+1)%points.Count;
            mesh.AddTriangle(first,first+1+i,first+1+next);
            Quad(mesh,first+1+i,first+1+next,first+1+points.Count+next,first+1+points.Count+i);
        }
    }

    public static void Panel(VertexHelper mesh, IList<Vector2> outer, IList<Vector2> inner, Color fill, Color ink, float width)
    {
        if (width <= 0f || ink.a <= 0f) { Fill(mesh,outer,fill); return; }
        var outside=Offset(outer,Fringe);
        Vector2 center=Center(inner);
        float distance=InnerDistance(inner,center);
        float inset=Mathf.Min(Fringe,distance*.5f);
        var inside=Offset(inner,-inset);
        int count=outer.Count, first=mesh.currentVertCount;
        for (int i=0;i<count;i++) Add(mesh,outside[i],-Fringe,-width-Fringe,ink,fill);
        for (int i=0;i<count;i++) Add(mesh,outer[i],0,-width,ink,fill);
        for (int i=0;i<count;i++) Add(mesh,inner[i],width,0,ink,fill);
        for (int i=0;i<count;i++) Add(mesh,inside[i],width+inset,inset,ink,fill);
        int middle=mesh.currentVertCount;
        Add(mesh,center,distance+width,distance,ink,fill);
        for (int i=0;i<count;i++)
        {
            int next=(i+1)%count;
            for (int band=0;band<3;band++)
                Quad(mesh,first+band*count+i,first+band*count+next,first+(band+1)*count+next,first+(band+1)*count+i);
            mesh.AddTriangle(middle,first+3*count+i,first+3*count+next);
        }
    }

    public static void Stroke(VertexHelper mesh, IList<Vector2> source, Color ink, float width, bool closed=false)
    {
        if (source.Count<2 || width<=0f || ink.a<=0f) return;
        int count=source.Count;
        if ((source[0]-source[count-1]).sqrMagnitude<.00001f) { closed=true;count--; }
        if(count<2)return;
        float radius=width*.5f;
        int first=mesh.currentVertCount;
        for(int i=0;i<count;i++)
        {
            Vector2 before=source[i]-source[i==0?(closed?count-1:0):i-1];
            Vector2 after=source[i==count-1?(closed?0:i):i+1]-source[i];
            if(before.sqrMagnitude<.00001f)before=after;
            if(after.sqrMagnitude<.00001f)after=before;
            Vector2 normal=Left(before.normalized)+Left(after.normalized);
            normal=normal.sqrMagnitude>.00001f?normal.normalized:Left(after.normalized);
            float projection=Mathf.Max(.34f,Mathf.Abs(Vector2.Dot(normal,Left(after.normalized))));
            float edge=radius/projection, fringe=Fringe/projection;
            Add(mesh,source[i]+normal*(edge+fringe),-Fringe,0,ink,ink);
            Add(mesh,source[i]+normal*edge,0,0,ink,ink);
            Add(mesh,source[i],radius,0,ink,ink);
            Add(mesh,source[i]-normal*edge,0,0,ink,ink);
            Add(mesh,source[i]-normal*(edge+fringe),-Fringe,0,ink,ink);
        }
        for(int i=0;i<(closed?count:count-1);i++)
        {
            int next=(i+1)%count;
            for(int band=0;band<4;band++) Quad(mesh,first+i*5+band,first+next*5+band,first+next*5+band+1,first+i*5+band+1);
        }
        if(!closed)
        {
            Cap(mesh,source[0],radius,(source[0]-source[1]).normalized,ink);
            Cap(mesh,source[count-1],radius,(source[count-1]-source[count-2]).normalized,ink);
        }
    }

    private static void Cap(VertexHelper mesh,Vector2 center,float radius,Vector2 direction,Color ink)
    {
        const int steps=12;
        int first=mesh.currentVertCount;
        Add(mesh,center,radius,0,ink,ink);
        float start=Mathf.Atan2(direction.y,direction.x)-Mathf.PI*.5f;
        for(int band=0;band<2;band++) for(int i=0;i<=steps;i++)
        {
            float angle=start+i*Mathf.PI/steps;
            Add(mesh,center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(radius+band*Fringe),-band*Fringe,0,ink,ink);
        }
        for(int i=0;i<steps;i++)
        {
            mesh.AddTriangle(first,first+1+i,first+2+i);
            Quad(mesh,first+1+i,first+2+i,first+2+steps+1+i,first+1+steps+1+i);
        }
    }

    private static Vector2[] Offset(IList<Vector2> points,float amount)
    {
        int count=points.Count;
        float area=0f;
        for(int i=0;i<count;i++) { var next=points[(i+1)%count];area+=points[i].x*next.y-next.x*points[i].y; }
        float direction=area>=0?-1f:1f;
        var result=new Vector2[count];
        for(int i=0;i<count;i++)
        {
            var incoming=(points[i]-points[(i+count-1)%count]).normalized;
            var outgoing=(points[(i+1)%count]-points[i]).normalized;
            Vector2 normal=(Left(incoming)+Left(outgoing))*direction;
            normal=normal.sqrMagnitude>.00001f?normal.normalized:Left(outgoing)*direction;
            float projection=Mathf.Max(.34f,Mathf.Abs(Vector2.Dot(normal,Left(outgoing)*direction)));
            result[i]=points[i]+normal*(amount/projection);
        }
        return result;
    }
    private static Vector2 Center(IList<Vector2> points)
    { Vector2 center=Vector2.zero;foreach(var point in points)center+=point;return center/points.Count; }
    private static float InnerDistance(IList<Vector2> points,Vector2 center)
    {
        float distance=float.MaxValue;
        for(int i=0;i<points.Count;i++)
            distance=Mathf.Min(distance,Mathf.Abs(Vector2.Dot(center-points[i],Left((points[(i+1)%points.Count]-points[i]).normalized))));
        return Mathf.Max(.001f,distance);
    }
    private static Vector2 Left(Vector2 direction)=>new Vector2(-direction.y,direction.x);
    private static void Add(VertexHelper mesh,Vector2 point,float edge,float blend,Color primary,Color secondary)
    {
        var vertex=UIVertex.simpleVert;
        vertex.position=point;vertex.color=Color.white;vertex.uv0=new Vector2(edge,blend);
        vertex.uv1=new Vector4(primary.r,primary.g,primary.b,primary.a);
        vertex.uv2=new Vector4(secondary.r,secondary.g,secondary.b,secondary.a);
        mesh.AddVert(vertex);
    }
    private static void Quad(VertexHelper mesh,int a,int b,int c,int d)
    { mesh.AddTriangle(a,b,c);mesh.AddTriangle(a,c,d); }
}
