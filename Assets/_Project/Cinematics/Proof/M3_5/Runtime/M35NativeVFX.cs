#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using UnityEngine;
namespace ChessButWeird.CinematicProof.M35
{
    // Two slender opaque ribbon strokes and a solid ink shockwave; no extra camera or distortion.
    public sealed class M35NativeVFX:MonoBehaviour
    {
        public LineRenderer[] strokes;public Transform shockwave;public Renderer shockwaveRenderer;
        private readonly Vector3[] points=new Vector3[32];private Material ink;private static readonly int Accent=Shader.PropertyToID("_Accent"),Strength=Shader.PropertyToID("_Strength");
        private void Awake(){ink=new Material(strokes[0].sharedMaterial){name="M35_OwnedStroke",hideFlags=HideFlags.DontSave};foreach(var line in strokes)line.sharedMaterial=ink;shockwaveRenderer.sharedMaterial=ink;Clear();}
        public void Apply(float t,Color colour,int rarity,bool motion,bool flash,bool reduced)
        {
            ink.SetColor(Accent,colour);ink.SetFloat(Strength,flash?.10f:.28f);
            float charge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.45f,3.25f,t));bool visible=rarity>=2&&!motion&&t>1.45f&&t<4.35f;
            for(int s=0;s<strokes.Length;s++)
            {
                var line=strokes[s];line.enabled=visible&&(!reduced||s==0);if(!line.enabled)continue;
                float length=charge*3.1f;for(int i=0;i<points.Length;i++){float p=i/(float)(points.Length-1),a=t*.7f+(s*Mathf.PI)+p*length;float radius=1.35f+Mathf.Sin(p*Mathf.PI)*.10f;points[i]=new Vector3(Mathf.Cos(a)*radius,.6f+p*1.75f,Mathf.Sin(a)*radius);}
                line.widthMultiplier=(reduced?.014f:.023f)*Mathf.Clamp01((4.35f-t)*3);line.SetPositions(points);
            }
            bool wave=t>=3.55f&&t<4.28f&&rarity>=1&&!flash&&!motion;shockwaveRenderer.enabled=wave;
            if(wave){float p=Mathf.InverseLerp(3.55f,4.28f,t);shockwave.localScale=Vector3.one*Mathf.Lerp(.7f,2.50f,1-Mathf.Pow(1-p,3));shockwave.localPosition=new Vector3(0,.40f-p*.02f,0);}
        }
        public void Clear(){foreach(var line in strokes)line.enabled=false;if(shockwaveRenderer)shockwaveRenderer.enabled=false;}
        private void OnDisable()=>Clear();
        private void OnDestroy(){if(ink)Destroy(ink);}
    }
}

#endif
