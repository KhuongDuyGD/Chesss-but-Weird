using UnityEngine;
using UnityEngine.UI;

namespace ChessButWeird.BuffVfx
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BuffHourglassGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;
            void Triangle(float ax,float ay,float bx,float by,float cx,float cy)
            {
                int n=mesh.currentVertCount;
                mesh.AddVert(new Vector3(r.xMin+ax*r.width,r.yMin+ay*r.height),color,Vector2.zero);
                mesh.AddVert(new Vector3(r.xMin+bx*r.width,r.yMin+by*r.height),color,Vector2.zero);
                mesh.AddVert(new Vector3(r.xMin+cx*r.width,r.yMin+cy*r.height),color,Vector2.zero);
                mesh.AddTriangle(n,n+1,n+2);
            }
            Triangle(0,0,1,0,1,.08f);Triangle(0,0,1,.08f,0,.08f);
            Triangle(0,.92f,1,.92f,1,1);Triangle(0,.92f,1,1,0,1);
            Triangle(.12f,.87f,.88f,.87f,.5f,.5f);Triangle(.5f,.5f,.88f,.13f,.12f,.13f);
        }
    }
}
