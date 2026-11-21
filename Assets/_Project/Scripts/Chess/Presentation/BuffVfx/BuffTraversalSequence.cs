using UnityEngine;

namespace ChessButWeird.BuffVfx
{
    // Confirmed presentation only: no gameplay component, piece movement or board mutation.
    internal sealed class BuffTraversalSequence
    {
        private readonly GameObject root,prop;
        private readonly Transform lid;
        private readonly BuffVisualRecipe recipe;
        private readonly LineRenderer[] paths=new LineRenderer[3],anchors=new LineRenderer[9];
        private readonly BuffMeshEcho[] echoes=new BuffMeshEcho[3];
        private readonly MeshRenderer[] slabs=new MeshRenderer[16];
        private readonly ParticleSystem particles;
        private readonly MaterialPropertyBlock tint=new MaterialPropertyBlock();
        private BuffVisualEvent cue;
        private float age,duration,tile;
        private bool reduced,emitted;
        private int quality;
        internal BuffTraversalSequence(GameObject root,BuffVisualRecipe recipe,BuffVisualAssets assets)
        {
            this.root=root;this.recipe=recipe;
            for(int i=0;i<3;i++){paths[i]=assets.Line(root.transform,"Confirmed movement trail "+i,.025f,16);paths[i].useWorldSpace=true;}
            for(int i=0;i<9;i++){anchors[i]=assets.Line(root.transform,"Resolved contact "+i,.025f,24,true);anchors[i].useWorldSpace=true;}
            if(recipe.group==5)for(int i=0;i<3;i++)echoes[i]=new BuffMeshEcho(root.transform,assets,"Movement afterimage "+i);
            prop=assets.Prop(recipe.id==9?"SupplyCrate":recipe.id==11?"Landmine":null,root.transform);
            if(prop)foreach(var t in prop.GetComponentsInChildren<Transform>())if(t.name.StartsWith("CrateLid")){lid=t;break;}
            if(recipe.id==18)for(int i=0;i<16;i++)
            {
                var slab=new GameObject("Cosmetic falling cell "+i,typeof(MeshFilter),typeof(MeshRenderer));slab.transform.SetParent(root.transform,false);
                slab.GetComponent<MeshFilter>().sharedMesh=assets.Glyph("tilePlate");slabs[i]=slab.GetComponent<MeshRenderer>();slabs[i].sharedMaterial=assets.Ink;
                slabs[i].shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var debris=new GameObject("Pooled stylized resolve flecks",typeof(ParticleSystem));debris.transform.SetParent(root.transform,false);
            particles=debris.GetComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.loop=false;main.maxParticles=32;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=.4f;main.startSpeed=0;main.useUnscaledTime=true;
            var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
            var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=assets.Shard;renderer.sharedMaterial=assets.Ink;
            var fade=particles.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.7f,0),new GradientAlphaKey(0,1)});fade.color=gradient;
            BuffVisualAssets.IgnoreRaycasts(root);
        }
        internal void Begin(BuffVisualEvent value,float size,bool reduce,int effects)
        {
            cue=value;tile=size;reduced=reduce;quality=effects;age=0;emitted=false;duration=reduce?.32f:recipe.duration;
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one;root.SetActive(true);
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);particles.useAutoRandomSeed=false;particles.randomSeed=(uint)(value.Id.GetHashCode()&0x7fffffff)+1;
            foreach(var echo in echoes)if(echo!=null)echo.Capture(value.Actor);
            if(lid)lid.localRotation=Quaternion.identity;
            Tick(0);
        }
        internal void Playback(bool paused,float speed){var main=particles.main;main.simulationSpeed=Mathf.Max(0,speed);if(paused&&particles.isPlaying)particles.Pause();else if(!paused&&particles.isPaused)particles.Play();}
        private void Ring(int index,Vector3 center,float radius,Color color,float alpha)
        {
            var line=anchors[index];line.gameObject.SetActive(alpha>.01f);line.widthMultiplier=tile*.027f;color.a=Mathf.Clamp01(alpha);line.startColor=line.endColor=color;
            int count=cue.Team==PieceTeam.White?24:4;line.positionCount=count;
            for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;line.SetPosition(i,center+new Vector3(Mathf.Cos(a)*radius,tile*.04f,Mathf.Sin(a)*radius));}
        }
        internal bool Tick(float dt)
        {
            age+=dt;float t=Mathf.Clamp01(age/duration),fade=1-t;var color=recipe.color;
            bool mobility=recipe.group==5;
            Vector3 from=cue.Source-(mobility?Vector3.up*tile*.62f:Vector3.zero),to=cue.Target-(mobility?Vector3.up*tile*.5f:Vector3.zero);
            for(int i=0;i<9;i++)anchors[i].gameObject.SetActive(false);
            for(int i=0;i<3;i++)paths[i].gameObject.SetActive(false);
            Ring(0,from,tile*(reduced?.3f:.24f+.16f*t),color,fade*.7f);Ring(1,to,tile*(reduced?.3f:.12f+.3f*t),color,fade*.85f);
            if(mobility)
            {
                var direction=to-from;var side=Vector3.Cross(Vector3.up,direction).normalized;
                bool trail=recipe.id==5||recipe.id==13||recipe.id==20||recipe.id==21||recipe.id==30;
                int count=recipe.id==13?2:recipe.id==30?3:1;
                if(trail)for(int j=0;j<count;j++)
                {
                    var line=paths[j];line.gameObject.SetActive(true);line.widthMultiplier=tile*(recipe.id==5?.028f:.018f);var ink=color;ink.a=fade*(reduced?.3f:.65f);line.startColor=line.endColor=ink;
                    for(int i=0;i<16;i++)
                    {
                        float f=i/15f;
                        if(recipe.id==30&&!reduced){float head=Mathf.Clamp01(t*1.5f-j*.08f);f=Mathf.Lerp(Mathf.Max(0,head-.5f),head,f);}
                        var point=Vector3.Lerp(from,to,f)+Vector3.up*tile*.2f;
                        if(!reduced&&recipe.id==5)point+=side*tile*(i%2==0?.09f:-.09f)*Mathf.Sin(f*Mathf.PI);
                        if(recipe.id==13)point+=side*tile*(j==0?1:-1)*Mathf.Sin(f*Mathf.PI)*.24f;
                        if(recipe.id==30)point+=side*tile*(j-1)*.16f;
                        line.SetPosition(i,point);
                    }
                }
                if(cue.Tiles!=null)for(int i=0;i<Mathf.Min(7,cue.Tiles.Length);i++)Ring(i+2,cue.Tiles[i],tile*.26f,color,fade*.5f);
                for(int i=0;i<3;i++)
                {
                    bool show=recipe.id==5||recipe.id==20||recipe.id==21||recipe.id==30;
                    if(!show||reduced){echoes[i].Hide();continue;}
                    float f=Mathf.Clamp01(t*1.4f-i*.15f);var ink=color;ink.a=fade*(.19f-i*.045f);
                    echoes[i].Pose(Vector3.Lerp(from,to,f),cue.Actor?cue.Actor.rotation:Quaternion.identity,ink);
                }
                if(recipe.id==20)Ring(2,from,tile*.13f,color,fade*.8f);
                if(recipe.id==13&&!reduced){Ring(2,Vector3.Lerp(from,to,t),tile*.13f,color,fade*.8f);Ring(3,Vector3.Lerp(to,from,t),tile*.13f,color,fade*.8f);}
            }
            if(prop)
            {
                float drop=cue.Outcome=="Drop"&&!reduced?Mathf.Max(0,1-t*2.5f):0;
                float bounce=cue.Outcome=="Drop"&&!reduced&&t>.4f?Mathf.Sin(Mathf.Clamp01((t-.4f)/.3f)*Mathf.PI)*.08f:0;
                prop.transform.position=to+Vector3.up*tile*(drop*drop*1.7f+bounce);
                prop.transform.rotation=cue.BoardRotation;
                float scale=cue.Outcome=="Drop"||cue.Outcome=="Arm"?1:Mathf.Clamp01((1-t)*2);
                prop.transform.localScale=Vector3.one*tile*scale;
                if(lid)lid.localRotation=Quaternion.Euler(cue.Outcome=="Drop"?0:-Mathf.SmoothStep(0,105,t*2),0,0);
            }
            if(recipe.id==18)for(int i=0;i<16;i++)
            {
                var slab=slabs[i];bool show=cue.Tiles!=null&&i<cue.Tiles.Length;slab.gameObject.SetActive(show);
                if(!show)continue;
                slab.transform.position=cue.Tiles[i]+Vector3.up*tile*(.035f-(reduced?0:t*t*.9f));
                slab.transform.rotation=cue.BoardRotation*Quaternion.Euler(90+(reduced?0:t*30),0,0);slab.transform.localScale=Vector3.one*tile;
                tint.SetColor("_Color",new Color(.34f,.29f,.27f,fade));slab.SetPropertyBlock(tint);
            }
            if(!emitted&&t>=.4f)
            {
                emitted=true;
                if(quality>0&&!reduced)
                {
                    int count=quality==1?6:16;
                    for(int i=0;i<count;i++)
                    {
                        float a=i*2.399963f;var origin=recipe.id==18&&cue.Tiles!=null&&cue.Tiles.Length>0?cue.Tiles[i%cue.Tiles.Length]:recipe.id==15&&cue.Outcome=="Substitute"?from:to;
                        var smoke=recipe.id==15;
                        var offset=smoke?new Vector3(Mathf.Cos(a)*.29f,.25f+(i%3)*.08f,Mathf.Sin(a)*.29f):Vector3.up*.12f;
                        var p=new ParticleSystem.EmitParams{position=origin+offset*tile,velocity=new Vector3(Mathf.Cos(a),.7f+(i%3)*.2f,Mathf.Sin(a))*tile*(smoke?.3f:.5f),startSize=tile*(smoke?.22f:.05f),startLifetime=.4f,startColor=smoke?new Color(.57f,.65f,.71f,.65f):color};particles.Emit(p,1);
                    }
                    particles.Play();
                }
            }
            return age<duration;
        }
        internal void Release(){particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);foreach(var echo in echoes)if(echo!=null)echo.Hide();root.SetActive(false);}
    }
}
