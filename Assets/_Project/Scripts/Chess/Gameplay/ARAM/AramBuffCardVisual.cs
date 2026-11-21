using UnityEngine;
using UnityEngine.UI;

// Geometry stays sharp at every UI scale and ships with both draft entry points.
public static class AramBuffCardTheme
{
    public static Color Accent(AramBuffTier tier) => tier == AramBuffTier.Silver ? new Color(.70f,.83f,.92f) :
        tier == AramBuffTier.Gold ? new Color(1f,.76f,.34f) :
        tier == AramBuffTier.Diamond ? new Color(.35f,.94f,.91f) : new Color(.86f,.61f,1f);
    public static Color Ink(AramBuffTier tier) => Color.Lerp(new Color(.08f,.09f,.15f), Accent(tier), .10f);
    public static Color Paper(AramBuffTier tier) => Color.Lerp(new Color(.96f,.94f,.89f), Accent(tier), .10f);
}

/// <summary>Rarity seal, halo and buff-family emblem. No textures, particles or flashing.</summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class AramBuffCardVisual : MaskableGraphic
{
    private AramBuffId buff;
    private AramBuffTier tier;
    private float phase;
    private int drawnBucket = -1;
    private bool frameOnly;
    public void ConfigureFrame(AramBuffTier rarity)
    {
        frameOnly=true;tier=rarity;color=AramBuffCardTheme.Accent(tier);
        raycastTarget=false;SetVerticesDirty();
    }
    public void Configure(AramBuffDefinition definition)
    {
        buff = definition ? definition.Id : AramBuffId.CommandantPawn;
        tier = definition ? definition.Tier : AramBuffTier.Silver;
        color = AramBuffCardTheme.Accent(tier);
        raycastTarget = false;
        SetVerticesDirty();
    }
    private void Update()
    {
        if(frameOnly)return;
        bool animate = !UserSettings.Presentation.ReducedMotion && UserSettings.Presentation.AnimationSpeed > 0 && UserSettings.Presentation.Effects > 0;
        int bucket = animate ? (int)(Time.unscaledTime * 24) : 0;
        if (bucket == drawnBucket) return;
        drawnBucket = bucket;
        phase = animate ? bucket / 24f * .14f : 0;
        SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if(frameOnly){DrawFrame(mesh);return;}
        Vector2 center = rectTransform.rect.center;
        float scale = Mathf.Min(rectTransform.rect.width / 300, rectTransform.rect.height / 92);
        if (scale <= 0) return;
        Color accent = color;
        // A quiet radial ink wash, bounded inside the illustration region.
        for (int i = 0; i < 48; i++)
        {
            float a = i * Mathf.PI * 2 / 48, b = (i + 1) * Mathf.PI * 2 / 48;
            Triangle(mesh, center, center + new Vector2(Mathf.Cos(a)*110, Mathf.Sin(a)*43)*scale,
                center + new Vector2(Mathf.Cos(b)*110, Mathf.Sin(b)*43)*scale,
                new Color(accent.r,accent.g,accent.b,.16f), new Color(accent.r,accent.g,accent.b,0));
        }
        void Line(Vector2 a, Vector2 b, float width = 1.6f, float alpha = 1) =>
            Stroke(mesh, center+a*scale, center+b*scale, width*scale, new Color(accent.r,accent.g,accent.b,alpha));
        void Ring(float radius, float width, float alpha, int segments = 48)
        {
            for (int i=0;i<segments;i++)
            {
                float a = i*Mathf.PI*2/segments, b=(i+1)*Mathf.PI*2/segments;
                Line(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,alpha);
            }
        }
        Ring(37, 1, .35f);
        int facets = tier == AramBuffTier.Silver ? 4 : tier == AramBuffTier.Gold ? 6 : tier == AramBuffTier.Diamond ? 8 : 12;
        for (int i=0;i<facets;i++)
        {
            float a = i*Mathf.PI*2/facets+phase;
            Vector2 direction = new Vector2(Mathf.Cos(a),Mathf.Sin(a));
            Line(direction*40,direction*44,1,.55f);
        }
        // Distinct silhouettes plus a written rarity label support color vision differences.
        float gem = tier == AramBuffTier.Legendary ? 8 : tier == AramBuffTier.Diamond ? 6 : 4;
        foreach (int side in new[]{-1,1})
        {
            float x=side*85;
            Line(new Vector2(x-gem,0),new Vector2(x,gem)); Line(new Vector2(x,gem),new Vector2(x+gem,0));
            Line(new Vector2(x+gem,0),new Vector2(x,-gem)); Line(new Vector2(x,-gem),new Vector2(x-gem,0));
            Line(new Vector2(side*102,0),new Vector2(side*140,0),1,.25f);
            if (tier >= AramBuffTier.Diamond) { Line(new Vector2(x,gem),new Vector2(x,-gem),1,.4f); }
        }
        Vector2 P(float x,float y)=>new Vector2(x,y);
        void Path(params Vector2[] points) { for(int i=1;i<points.Length;i++)Line(points[i-1],points[i],2.2f); }
        void Circle(Vector2 c,float r)
        {
            for(int i=0;i<24;i++) { float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;Line(c+P(Mathf.Cos(a),Mathf.Sin(a))*r,c+P(Mathf.Cos(b),Mathf.Sin(b))*r,2.2f); }
        }
        int id=(int)buff;
        if (id==0||id==6||id==11||id==24||id==26)
        {
            Circle(P(0,13),8); Path(P(-5,5),P(-8,-12),P(8,-12),P(5,5));
            Path(P(-14,-19),P(-12,-13),P(12,-13),P(14,-19),P(-14,-19));
            if(id==24) {Line(P(-25,-12),P(-25,7));Line(P(25,-12),P(25,7));}
            else {Path(P(19,-5),P(19,19),P(14,13));Line(P(19,19),P(24,13));}
        }
        else if(id==1||id==12||id==16||id==28||id==30)
        {
            Path(P(-17,19),P(-17,9),P(-13,9),P(-11,-14),P(11,-14),P(13,9),P(17,9),P(17,19),P(8,19),P(8,12),P(-8,12),P(-8,19),P(-17,19));
            Path(P(-17,-20),P(-14,-14),P(14,-14),P(17,-20),P(-17,-20));
        }
        else if(id==3||id==10||id==13||id==15||id==34)
        {
            Path(P(-22,9),P(19,9),P(11,17));Line(P(19,9),P(11,1));
            Path(P(22,-9),P(-19,-9),P(-11,-17));Line(P(-19,-9),P(-11,-1));
        }
        else if(id==7||id==20||id==25)
        {
            Ring(19,2.2f,1); Circle(Vector2.zero,5);
            Line(P(-27,0),P(-13,0),2.2f);Line(P(13,0),P(27,0),2.2f);
            Line(P(0,-27),P(0,-13),2.2f);Line(P(0,13),P(0,27),2.2f);
        }
        else if(id==8||id==14||id==19||id==31)
        {
            Path(P(-20,-20),P(20,-20),P(20,20),P(-20,20),P(-20,-20));
            foreach(var dot in new[]{P(-10,10),P(10,10),P(0,0),P(-10,-10),P(10,-10)}) Circle(dot,2.3f);
        }
        else if(id==9||id==23)
        {
            Path(P(-21,-16),P(0,-25),P(21,-16),P(21,14),P(0,24),P(-21,14),P(-21,-16));
            Path(P(-21,14),P(0,4),P(21,14));Line(P(0,4),P(0,-25));Line(P(-10,19),P(10,9));
        }
        else if(id==17||id==27||id==29||id==32)
        {
            Path(P(-23,16),P(-17,-12),P(17,-12),P(23,16),P(9,5),P(0,23),P(-9,5),P(-23,16));
            Line(P(-17,-20),P(17,-20),2.2f);
        }
        else if(id==21||id==22)
        {
            Path(P(-20,-21),P(-20,10),P(-14,21),P(0,26),P(14,21),P(20,10),P(20,-21),P(10,-15),P(0,-21),P(-10,-15),P(-20,-21));
            Circle(P(-7,5),2.5f);Circle(P(7,5),2.5f);
        }
        else if(id==2)
        {
            Path(P(-18,-21),P(17,-21),P(11,-10),P(10,15),P(0,25),P(-17,11),P(-9,4),P(-3,9),P(-3,-3),P(-18,-21));Circle(P(3,14),1.5f);
        }
        else
        {
            // Explosion, teleport, collapse and traps share an angular energy mark.
            Path(P(5,27),P(-17,-2),P(-3,-2),P(-8,-27),P(18,7),P(4,7),P(5,27));
            if(id==4||id==18||id==33) { Line(P(-26,18),P(-20,12));Line(P(26,-18),P(20,-12)); }
        }
    }
    private void DrawFrame(VertexHelper mesh)
    {
        Vector2 center=rectTransform.rect.center;
        float x=rectTransform.rect.width*.5f-12, y=rectTransform.rect.height*.5f-12;
        Color tint=new Color(color.r,color.g,color.b,.5f);
        void Line(Vector2 a,Vector2 b,float width=1)=>Stroke(mesh,center+a,center+b,width,tint);
        foreach(int sx in new[]{-1,1})foreach(int sy in new[]{-1,1})
        {
            Vector2 P(float a,float b)=>new Vector2(sx*(x-a),sy*(y-b));
            float length=tier==AramBuffTier.Silver?13:tier==AramBuffTier.Gold?24:tier==AramBuffTier.Diamond?30:38;
            Line(P(length,0),P(0,0));Line(P(0,0),P(0,length));
            if(tier==AramBuffTier.Gold)
            {
                Line(P(21,5),P(6,5));Line(P(6,5),P(6,21));
                Line(P(10,10),P(14,14));Line(P(14,14),P(10,18));Line(P(10,18),P(6,14));Line(P(6,14),P(10,10));
            }
            else if(tier==AramBuffTier.Diamond)
            {
                Line(P(28,5),P(11,5));Line(P(11,5),P(5,11));Line(P(5,11),P(5,28));
                Line(P(12,12),P(20,16));Line(P(20,16),P(16,20));Line(P(16,20),P(12,12));
            }
            else if(tier==AramBuffTier.Legendary)
            {
                Line(P(34,5),P(15,5));Line(P(15,5),P(5,15));Line(P(5,15),P(5,34));
                // Four-point ink stars, stronger than the lower-tier corner marks.
                Vector2 c=P(16,16);
                var star=new[]{new Vector2(0,8),new Vector2(2,2),new Vector2(8,0),new Vector2(2,-2),
                    new Vector2(0,-8),new Vector2(-2,-2),new Vector2(-8,0),new Vector2(-2,2),new Vector2(0,8)};
                for(int i=1;i<star.Length;i++)Line(c+star[i-1],c+star[i]);
            }
        }
    }
    private static void Triangle(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Color inner,Color outer)
    {
        int n=mesh.currentVertCount;mesh.AddVert(a,inner,Vector2.zero);mesh.AddVert(b,outer,Vector2.zero);mesh.AddVert(c,outer,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);
    }
    private static void Stroke(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color tint)
    {
        Vector2 d=b-a;if(d.sqrMagnitude<.001f)return;Vector2 normal=new Vector2(-d.y,d.x).normalized*(width*.5f);
        int n=mesh.currentVertCount;mesh.AddVert(a-normal,tint,Vector2.zero);mesh.AddVert(a+normal,tint,Vector2.zero);
        mesh.AddVert(b+normal,tint,Vector2.zero);mesh.AddVert(b-normal,tint,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);
    }
}
