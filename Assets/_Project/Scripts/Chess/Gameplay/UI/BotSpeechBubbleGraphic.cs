using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A sketchbook speech balloon with a continuous outline and a tail pointing at the avatar.</summary>
[RequireComponent(typeof(RectTransform),typeof(CanvasRenderer))]
public sealed class BotSpeechBubbleGraphic : AntialiasedUIGraphic
{
    public const float TailHeight=26;
    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();var bounds=GetPixelAdjustedRect();
        var body=Rect.MinMaxRect(bounds.xMin+4,bounds.yMin+TailHeight,bounds.xMax-4,bounds.yMax-4);
        const float radius=20;
        var rounded=new List<Vector2>();
        Arc(rounded,new Vector2(body.xMax-radius,body.yMin+radius),radius,-90);
        Arc(rounded,new Vector2(body.xMax-radius,body.yMax-radius),radius,0);
        Arc(rounded,new Vector2(body.xMin+radius,body.yMax-radius),radius,90);
        Arc(rounded,new Vector2(body.xMin+radius,body.yMin+radius),radius,180);
        var tip=new Vector2(body.xMax-66,bounds.yMin+4);
        var left=new Vector2(tip.x-19,body.yMin);
        var right=new Vector2(tip.x+25,body.yMin);
        // Separate convex fills avoid a triangle fan spilling across the tail's concave junctions.
        UIEdgeMesh.Fill(helper,new[]{tip,right+Vector2.up*2,left+Vector2.up*2},MatchHudStyle.Paper);
        UIEdgeMesh.Fill(helper,rounded,MatchHudStyle.Paper);
        var outline=new List<Vector2>{tip,right};outline.AddRange(rounded);outline.Add(left);
        UIEdgeMesh.Stroke(helper,outline,MatchHudStyle.Ink,2.1f,true);
    }
    private static void Arc(List<Vector2> points,Vector2 center,float radius,float start)
    {
        for(int step=0;step<=8;step++)
        {
            float angle=(start+step*90/8f)*Mathf.Deg2Rad;
            var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            float wobble=.55f*Mathf.Sin((points.Count+1)*2.4f);
            points.Add(center+direction*(radius+wobble));
        }
    }
}
