using TMPro;
using UnityEngine;

namespace ChessButWeird.BuffVfx
{
    // Resolves recorded transformations and placements. Mesh echoes never change a real piece.
    internal sealed class BuffStrategySequence
    {
        private readonly GameObject root,prop;
        private readonly BuffVisualRecipe recipe;
        private readonly LineRenderer[] rings=new LineRenderer[32],trails=new LineRenderer[8];
        private readonly MeshRenderer[] glyphs=new MeshRenderer[32];
        private readonly MeshFilter[] symbols=new MeshFilter[32];
        private readonly BuffMeshEcho[] echoes=new BuffMeshEcho[8];
        private readonly BuffVisualAssets assets;
        private readonly MaterialPropertyBlock tint=new MaterialPropertyBlock();
        private readonly ParticleSystem particles;
        private readonly TextMeshPro receipt;
        private BuffVisualEvent cue;
        private float age,duration,tile;
        private bool reduced,emitted;
        private int quality,count;
        private readonly int[] indices=new int[32];
        internal BuffStrategySequence(GameObject root,BuffVisualRecipe recipe,BuffVisualAssets assets)
        {
            this.root=root;this.recipe=recipe;this.assets=assets;
            for(int i=0;i<32;i++)
            {
                rings[i]=assets.Line(root.transform,"Resolved deployment circle "+i,.022f,32,true);rings[i].useWorldSpace=true;
                var glyph=new GameObject("Recorded origin/result "+i,typeof(MeshFilter),typeof(MeshRenderer));glyph.transform.SetParent(root.transform,false);
                symbols[i]=glyph.GetComponent<MeshFilter>();glyphs[i]=glyph.GetComponent<MeshRenderer>();glyphs[i].sharedMaterial=assets.Ink;glyphs[i].shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            for(int i=0;i<8;i++){trails[i]=assets.Line(root.transform,"Coordinated resolved path "+i,.02f,24);trails[i].useWorldSpace=true;echoes[i]=new BuffMeshEcho(root.transform,assets,"Resolved unit silhouette "+i);}
            prop=assets.Prop(recipe.id==34||recipe.id==12?"Parachute":recipe.id==31?"CreditCard":recipe.id==29?"CrownHalo":null,root.transform);
            var text=new GameObject("Confirmed cost",typeof(TextMeshPro));text.transform.SetParent(root.transform,false);receipt=text.GetComponent<TextMeshPro>();receipt.font=ChessFontCatalog.TmpFont;receipt.fontSize=3;receipt.alignment=TextAlignmentOptions.Center;receipt.richText=false;receipt.rectTransform.sizeDelta=new Vector2(3,1);
            var debris=new GameObject("Bounded summoning sparks",typeof(ParticleSystem));debris.transform.SetParent(root.transform,false);particles=debris.GetComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.loop=false;main.maxParticles=32;main.startLifetime=.35f;main.startSpeed=0;main.simulationSpace=ParticleSystemSimulationSpace.World;main.useUnscaledTime=true;
            var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
            var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=assets.Shard;renderer.sharedMaterial=assets.Ink;
            BuffVisualAssets.IgnoreRaycasts(root);root.SetActive(false);
        }
        internal void Begin(BuffVisualEvent value,float size,bool reduce,int effects)
        {
            cue=value;tile=size;reduced=reduce;quality=effects;age=0;emitted=false;duration=reduce?.32f:recipe.duration;
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one;root.SetActive(true);
            count=0;
            if(cue.Changes!=null&&cue.Changes.Length>0)
            {
                // Keep every resulting deployment visible on a full home half; removals use the remaining budget.
                for(int i=0;i<cue.Changes.Length&&count<32;i++)if(cue.Changes[i].ToKind!=null)indices[count++]=i;
                for(int i=0;i<cue.Changes.Length&&count<32;i++)if(cue.Changes[i].ToKind==null)indices[count++]=i;
            }
            else {count=1;indices[0]=-1;}
            for(int i=0;i<32;i++){rings[i].gameObject.SetActive(i<count);glyphs[i].gameObject.SetActive(i<count);}
            for(int i=0;i<8;i++){echoes[i].Hide();trails[i].gameObject.SetActive(false);if(i<count)echoes[i].Capture(Change(i).Actor);}
            receipt.text=recipe.id==31&&cue.Amount.HasValue?"-"+cue.Amount.Value+" points":recipe.id==8&&cue.Amount==1?"+1 extra move":"";receipt.gameObject.SetActive(receipt.text.Length>0);
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);particles.useAutoRandomSeed=false;particles.randomSeed=(uint)(cue.Id.GetHashCode()&0x7fffffff)+1;Tick(0);
        }
        private BuffVisualChange Change(int i)=>indices[i]>=0?cue.Changes[indices[i]]:new BuffVisualChange{ActorId=cue.ActorId,Actor=cue.Actor,Team=cue.Team,From=cue.Source,To=cue.Target};
        internal bool Tick(float dt)
        {
            age+=dt;float t=Mathf.Clamp01(age/duration),fade=1-t;var camera=Camera.main;var color=recipe.color;
            for(int i=0;i<count;i++)
            {
                var change=Change(i);bool removed=change.ToKind==null&&cue.Changes!=null;
                var at=removed?change.From:change.To;float stagger=reduced?0:(i%4)*.025f;float progress=Mathf.Clamp01((t-stagger)/(1-stagger));
                var line=rings[i];line.widthMultiplier=tile*.022f;var ink=color;ink.a=fade*.8f;line.startColor=line.endColor=ink;
                for(int j=0;j<32;j++){float a=j*Mathf.PI/16;float r=(j%4==0?.34f:.29f)*tile*(reduced?1:.65f+progress*.5f);line.SetPosition(j,at+new Vector3(Mathf.Cos(a)*r,tile*.045f,Mathf.Sin(a)*r));}
                string symbol=recipe.glyph;
                if(recipe.id==19){bool origin=!reduced&&progress<.55f&&Mathf.FloorToInt(progress*8)%2==0;symbol="unit"+(origin?change.FromKind:change.ToKind);}
                else if(recipe.id==24||recipe.id==32)symbol=progress<.35f?"unit"+change.FromKind:"unit"+change.ToKind;
                else if(recipe.id==27)symbol=progress<.28f?"unit"+change.FromKind:progress<.55f?cue.Outcome=="Restored"?"crown":"fracture":"unit"+change.ToKind;
                else if(recipe.id==33)symbol="arrows";
                symbols[i].sharedMesh=assets.Glyph(symbol);var glyph=glyphs[i];glyph.transform.position=Vector3.Lerp(change.From,at,progress)+Vector3.up*tile*(.28f+Mathf.Sin(progress*Mathf.PI)*.25f);
                glyph.transform.rotation=camera?camera.transform.rotation:Quaternion.identity;glyph.transform.localScale=Vector3.one*tile*.14f;tint.SetColor("_Color",new Color(color.r,color.g,color.b,fade*.85f));glyph.SetPropertyBlock(tint);
                if(i<8)
                {
                    bool travel=recipe.id==1||recipe.id==28||recipe.id==2||recipe.id==12||recipe.id==23;
                    var path=trails[i];path.gameObject.SetActive(travel);path.widthMultiplier=tile*.024f;path.startColor=path.endColor=ink;
                    float arc=reduced?0:recipe.id==2?.65f:recipe.id==1||recipe.id==28?change.FromKind=="King"?.3f:change.FromKind=="Rook"?.08f:.18f:.2f;
                    for(int j=0;j<24;j++){float f=j/23f;path.SetPosition(j,Vector3.Lerp(change.From,at,f)+Vector3.up*tile*(.1f+Mathf.Sin(f*Mathf.PI)*arc));}
                    var echoTint=color;echoTint.a=reduced?0:fade*.22f;
                    var location=travel?Vector3.Lerp(change.From,at,progress)+Vector3.up*tile*Mathf.Sin(progress*Mathf.PI)*arc:at+Vector3.up*tile*(1-progress)*.3f;
                    echoes[i].Pose(location,change.Actor?change.Actor.rotation:Quaternion.identity,echoTint);
                }
            }
            if(prop)
            {
                bool parachute=recipe.id==34||recipe.id==12;bool show=!parachute||recipe.id==34||cue.Outcome=="Parachuted";prop.SetActive(show);
                var at=count>0?Change(0).To:cue.Target;
                float lift=tile*(parachute?reduced?1:1.65f-t*.75f:recipe.id==29?.9f:.65f);
                if(parachute&&cue.Actor)lift=Mathf.Max(lift,cue.Actor.position.y-at.y+tile*1.1f);
                prop.transform.SetPositionAndRotation(at+Vector3.up*lift,cue.BoardRotation);
                prop.transform.localScale=Vector3.one*tile*(t>.75f?(1-t)*4:1);
            }
            if(receipt.gameObject.activeSelf){receipt.transform.position=cue.Target+Vector3.up*tile*1.25f;receipt.transform.rotation=camera?camera.transform.rotation:Quaternion.identity;receipt.transform.localScale=Vector3.one*tile;receipt.color=new Color(.96f,.85f,.51f,fade);}
            if(!emitted&&t>=.45f)
            {
                emitted=true;if(quality>0&&!reduced){particles.Play();int n=quality==1?6:16;for(int i=0;i<n;i++){float a=i*2.399963f;var p=new ParticleSystem.EmitParams{position=(count>0?Change(i%count).To:cue.Target)+Vector3.up*tile*.12f,velocity=new Vector3(Mathf.Cos(a),.8f,Mathf.Sin(a))*tile*.4f,startColor=color,startSize=tile*.05f,startLifetime=.35f};particles.Emit(p,1);}}
            }
            return age<duration;
        }
        internal void Playback(bool paused,float speed){var m=particles.main;m.simulationSpeed=Mathf.Max(0,speed);if(paused&&particles.isPlaying)particles.Pause();else if(!paused&&particles.isPaused)particles.Play();}
        internal void Release(){particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);root.SetActive(false);foreach(var echo in echoes)echo.Hide();cue=null;}
    }
}
