using UnityEngine;

namespace ChessButWeird.BuffVfx
{
    // One pooled visual sequence. It never holds a ChessGame, board array, rule engine or capture callback.
    internal sealed class BuffEffectInstance
    {
        internal BuffVisualRecipe Recipe {get;}
        private readonly GameObject root,prop,projectile;
        private readonly LineRenderer beam,wave,flash;
        private readonly LineRenderer teamStamp;
        private readonly GameObject echo;
        private readonly MeshFilter[] echoMeshes=new MeshFilter[16];
        private readonly MeshRenderer[] echoRenderers=new MeshRenderer[16];
        private int echoCount;
        private readonly LineRenderer[] tiles=new LineRenderer[9];
        private readonly ParticleSystem particles;
        private readonly GameObject sigil;
        private readonly MeshFilter sigilMesh;
        private readonly MeshRenderer sigilRenderer;
        private readonly BuffVisualAssets assets;
        private readonly BuffTraversalSequence traversal;
        private readonly BuffStrategySequence strategy;
        private readonly MaterialPropertyBlock sigilColor=new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock echoColor=new MaterialPropertyBlock();
        private BuffVisualEvent cue;
        private float elapsed,size,duration;
        private bool reduced,emitted;
        private int quality;
        private Vector3 muzzle;
        internal BuffEffectInstance(Transform parent,BuffVisualRecipe recipe,BuffVisualAssets assets)
        {
            this.assets=assets;
            Recipe=recipe;root=new GameObject("Buff sequence "+recipe.key);root.transform.SetParent(parent,false);
            if(recipe.group==7||recipe.group==8){strategy=new BuffStrategySequence(root,recipe,assets);return;}
            if(recipe.group==5||recipe.group==6){traversal=new BuffTraversalSequence(root,recipe,assets);root.SetActive(false);return;}
            prop=assets.Prop(recipe.prop,root.transform);
            sigil=new GameObject("Resolve sigil",typeof(MeshFilter),typeof(MeshRenderer));sigil.transform.SetParent(root.transform,false);
            sigilMesh=sigil.GetComponent<MeshFilter>();sigilRenderer=sigil.GetComponent<MeshRenderer>();sigilRenderer.sharedMaterial=assets.Ink;
            sigilRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            beam=assets.Line(root.transform,"Confirmed trajectory",.025f,2);beam.useWorldSpace=true;
            wave=assets.Line(root.transform,"Resolve wave",.045f,40,true);wave.useWorldSpace=true;
            flash=assets.Line(root.transform,"Muzzle ink star",.04f,8,true);flash.useWorldSpace=true;
            teamStamp=assets.Line(root.transform,"Caster team stamp",.025f,16,true);teamStamp.useWorldSpace=true;
            echo=new GameObject("Recorded source silhouette");echo.transform.SetParent(root.transform,false);echo.SetActive(false);
            for(int i=0;i<9;i++){tiles[i]=assets.Line(root.transform,"Resolved tile "+i,.025f,4,true);tiles[i].useWorldSpace=true;}
            projectile=new GameObject("Artillery shell",typeof(MeshFilter),typeof(MeshRenderer));projectile.transform.SetParent(root.transform,false);
            projectile.GetComponent<MeshFilter>().sharedMesh=assets.Shard;projectile.GetComponent<MeshRenderer>().sharedMaterial=assets.PropMaterials[2];
            var debris=new GameObject("Pooled ink debris",typeof(ParticleSystem));debris.transform.SetParent(root.transform,false);
            particles=debris.GetComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.loop=false;main.maxParticles=32;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=.5f;main.startSpeed=0;main.useUnscaledTime=true;
            var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
            var particleRenderer=particles.GetComponent<ParticleSystemRenderer>();particleRenderer.renderMode=ParticleSystemRenderMode.Mesh;particleRenderer.mesh=assets.Shard;particleRenderer.sharedMaterial=assets.Ink;
            var over=particles.colorOverLifetime;over.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.25f,.22f,.27f),1)},new[]{new GradientAlphaKey(.8f,0),new GradientAlphaKey(0,1)});over.color=gradient;
            var scale=particles.sizeOverLifetime;scale.enabled=true;scale.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,1),new Keyframe(1,.1f)));
            BuffVisualAssets.IgnoreRaycasts(root);root.SetActive(false);
        }
        internal void Begin(BuffVisualEvent payload,float tile,bool reduce,int effects)
        {
            if(traversal!=null){traversal.Begin(payload,tile,reduce,effects);return;}
            if(strategy!=null){strategy.Begin(payload,tile,reduce,effects);return;}
            cue=payload;elapsed=0;size=tile;reduced=reduce;quality=effects;emitted=false;
            duration=reduce?.32f:Recipe.duration;root.SetActive(true);root.transform.position=payload.Source;
            root.transform.rotation=Quaternion.identity;root.transform.localScale=Vector3.one;
            if(prop){prop.SetActive(true);prop.transform.localScale=Vector3.one*size;}
            projectile.SetActive(false);particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed=false;particles.randomSeed=(uint)(payload.Id.GetHashCode()&0x7fffffff)+1;
            for(int i=0;i<tiles.Length;i++)tiles[i].gameObject.SetActive(false);
            echoCount=0;echo.SetActive(false);
            // A moving Rook can already be at its destination when the confirmed snapshot arrives.
            // Its faint render-only source echo keeps the cannon mounted and preserves the actual trajectory.
            if(Recipe.prop=="Cannon"&&payload.Actor)
            {
                foreach(var mesh in payload.Actor.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer=mesh.GetComponent<Renderer>();if(!mesh.sharedMesh||!renderer||!renderer.enabled||echoCount==16)continue;
                    if(!echoMeshes[echoCount])
                    {
                        var part=new GameObject("Source mesh "+echoCount,typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(echo.transform,false);
                        echoMeshes[echoCount]=part.GetComponent<MeshFilter>();echoRenderers[echoCount]=part.GetComponent<MeshRenderer>();
                        echoRenderers[echoCount].sharedMaterial=assets.Ink;echoRenderers[echoCount].shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                        BuffVisualAssets.IgnoreRaycasts(part);
                    }
                    var copy=echoMeshes[echoCount];copy.sharedMesh=mesh.sharedMesh;
                    copy.transform.SetPositionAndRotation(mesh.transform.position+payload.Source-Vector3.up*tile*.62f-payload.Actor.position,mesh.transform.rotation);
                    copy.transform.localScale=mesh.transform.lossyScale;echoCount++;
                }
                for(int i=0;i<16;i++)if(echoMeshes[i])echoMeshes[i].gameObject.SetActive(i<echoCount);
            }
            Tick(0);
        }
        internal void Playback(bool paused,float speed)
        {
            if(traversal!=null){traversal.Playback(paused,speed);return;}
            if(strategy!=null){strategy.Playback(paused,speed);return;}
            var main=particles.main;main.simulationSpeed=Mathf.Max(0,speed);
            if(paused&&particles.isPlaying)particles.Pause();else if(!paused&&particles.isPaused)particles.Play();
        }
        private static Color Alpha(Color color,float opacity){color.a=Mathf.Clamp01(opacity);return color;}
        private static void Tint(LineRenderer line,Color color){line.startColor=line.endColor=color;}
        internal bool Tick(float dt)
        {
            if(traversal!=null)return traversal.Tick(dt);
            if(strategy!=null)return strategy.Tick(dt);
            elapsed+=dt;float t=Mathf.Clamp01(elapsed/duration),shot=reduced?.1f:.28f,impact=reduced?.16f:.58f;
            bool gun=prop;var direction=cue.Target-cue.Source;direction.y=0;if(direction.sqrMagnitude<.0001f)direction=Vector3.forward;
            var rotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
            Vector3 source=cue.Source;
            int stampPoints=cue.Team==PieceTeam.White?16:4;teamStamp.positionCount=stampPoints;
            for(int i=0;i<stampPoints;i++){float a=i*Mathf.PI*2/stampPoints;teamStamp.SetPosition(i,new Vector3(cue.Source.x+Mathf.Cos(a)*size*.24f,cue.Source.y-size*.58f,cue.Source.z+Mathf.Sin(a)*size*.24f));}
            Tint(teamStamp,Alpha(cue.Team==PieceTeam.White?new Color(.96f,.85f,.51f):new Color(.33f,.62f,.92f),(1-t)*.7f));teamStamp.widthMultiplier=size*.025f;
            if(gun)
            {
                // A saddle, support arms and authored weapon are one mount. No free-floating gun or modification of the piece mesh.
                Vector3 anchor=Recipe.prop=="Cannon"?cue.Source:cue.Actor&&cue.Actor.gameObject.activeInHierarchy?cue.Actor.position+Vector3.up*size*.62f:cue.Source;
                echo.SetActive(echoCount>0&&cue.Actor&&(cue.Actor.position+Vector3.up*size*.62f-cue.Source).sqrMagnitude>size*size*.04f);
                if(echo.activeSelf){echoColor.SetColor("_Color",new Color(.74f,.72f,.65f,(1-t)*.24f));for(int i=0;i<echoCount;i++)echoRenderers[i].SetPropertyBlock(echoColor);}
                float ready=reduced?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(t/shot));
                float recoil=t>shot&&t<shot+.14f?Mathf.Sin((t-shot)/.14f*Mathf.PI)*.075f*size:0;
                prop.transform.SetPositionAndRotation(anchor+rotation*new Vector3(.12f*size,-(1-ready)*size*.32f,-recoil),rotation*Quaternion.Euler((1-ready)*45,0,0));
                prop.transform.localScale=Vector3.one*size*(t>.85f?Mathf.Max(0,(1-t)/.15f):1);
                muzzle=prop.transform.TransformPoint(new Vector3(0,.13f,Recipe.prop=="Sniper"?.84f:Recipe.prop=="Cannon"?.75f:.61f));
                source=muzzle;
            }
            else muzzle=source;
            sigil.SetActive(Recipe.glyph=="fracture"||Recipe.glyph=="fang"||(Recipe.glyph=="reticle"||Recipe.glyph=="bomb")&&t<shot);
            if(sigil.activeSelf)
            {
                bool transferred=cue.Outcome=="Transferred";
                sigilMesh.sharedMesh=assets.Glyph(transferred?"crown":Recipe.glyph);
                sigil.transform.position=cue.Target+Vector3.up*size*(Recipe.glyph=="fracture"?.75f:.18f);
                var camera=Camera.main;sigil.transform.rotation=(camera?camera.transform.rotation:Quaternion.identity)*Quaternion.Euler(0,reduced?0:t*180,0);
                sigil.transform.localScale=Vector3.one*size*.22f;
                var color=transferred?(cue.Team==PieceTeam.White?new Color(.96f,.85f,.51f):new Color(.33f,.62f,.92f)):Recipe.color;
                sigilColor.SetColor("_Color",Alpha(color,t>.75f?(1-t)*4:1));sigilRenderer.SetPropertyBlock(sigilColor);
            }
            bool resolving=cue.Phase==BuffVisualPhase.Resolve;
            bool firing=resolving&&t>=shot&&t<impact;
            bool aim=Recipe.glyph=="reticle"&&t<shot;
            beam.gameObject.SetActive((firing||cue.Phase==BuffVisualPhase.Preview||aim)&&Recipe.prop!="Cannon");beam.widthMultiplier=size*(Recipe.id==6?.07f:aim?.012f:.026f);
            beam.SetPosition(0,source);beam.SetPosition(1,cue.Target);Tint(beam,Alpha(Recipe.color,aim?.22f:Recipe.id==6?.85f:.9f));
            bool canFlash=UserSettings.Presentation.Flash>0&&!reduced;
            bool bombFlash=resolving&&Recipe.glyph=="bomb"&&t>=impact&&t<impact+.08f&&canFlash;
            flash.gameObject.SetActive(bombFlash||gun&&firing&&t<shot+.1f&&canFlash);
            var flashRotation=bombFlash&&Camera.main?Camera.main.transform.rotation:rotation;
            var flashCenter=bombFlash?cue.Target+Vector3.up*size*.3f:muzzle;flash.widthMultiplier=size*(bombFlash?.1f:.04f);
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4,r=(i%2==0?(bombFlash?.42f:.16f):.05f)*size;flash.SetPosition(i,flashCenter+flashRotation*new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0));}Tint(flash,Alpha(new Color(1,.81f,.38f),UserSettings.Presentation.Flash));
            projectile.SetActive(Recipe.prop=="Cannon"&&firing);
            if(projectile.activeSelf){float f=Mathf.InverseLerp(shot,impact,t);projectile.transform.position=Vector3.Lerp(source,cue.Target,f)+Vector3.up*Mathf.Sin(f*Mathf.PI)*size*.75f;projectile.transform.localScale=new Vector3(.065f,.1f,.065f)*size;}
            float pulse=Mathf.Clamp01((t-impact)/(1-impact));bool showImpact=resolving&&t>=impact;
            bool miss=cue.Outcome=="Miss";
            wave.gameObject.SetActive(showImpact&&!miss&&Recipe.glyph!="bomb");wave.widthMultiplier=size*.04f;
            float radius=size*(Recipe.glyph=="bomb"?1.42f:.26f)*(reduced?1:Mathf.Lerp(.15f,1,pulse));
            for(int i=0;i<40;i++){float a=i*Mathf.PI*2/40;wave.SetPosition(i,cue.Target+new Vector3(Mathf.Cos(a)*radius,size*.045f,Mathf.Sin(a)*radius));}Tint(wave,Alpha(Recipe.color,(1-pulse)*.8f));
            if(cue.Tiles!=null)for(int i=0;i<Mathf.Min(9,cue.Tiles.Length);i++)
            {
                var line=tiles[i];line.gameObject.SetActive(true);float r=size*(showImpact&&!reduced?Mathf.Lerp(.14f,.46f,Mathf.Clamp01(pulse*3)):.46f);
                line.SetPosition(0,cue.Tiles[i]+new Vector3(-r,size*.045f,-r));line.SetPosition(1,cue.Tiles[i]+new Vector3(-r,size*.045f,r));line.SetPosition(2,cue.Tiles[i]+new Vector3(r,size*.045f,r));line.SetPosition(3,cue.Tiles[i]+new Vector3(r,size*.045f,-r));line.widthMultiplier=size*.027f;Tint(line,Alpha(Recipe.color,showImpact?(1-pulse)*.75f:.32f));
            }
            if(showImpact&&!emitted)
            {
                emitted=true;
                if(!reduced&&quality>0&&!miss)
                {
                    particles.Play();int count=Recipe.glyph=="bomb"?(quality==2?24:12):(quality==2?9:5);
                    for(int i=0;i<count;i++)
                    {
                        float a=i*2.399963f,r=.5f+(i%5)*.18f;bool bomb=Recipe.glyph=="bomb";
                        Vector3 position=cue.Target;
                        if(bomb&&cue.Tiles!=null&&cue.Tiles.Length>0)position=cue.Tiles[i%cue.Tiles.Length]+new Vector3(Mathf.Cos(a)*.12f,0,Mathf.Sin(a)*.12f)*size;
                        var emit=new ParticleSystem.EmitParams {position=position+Vector3.up*size*.08f,
                            velocity=new Vector3(Mathf.Cos(a)*(bomb?.3f:r),bomb?1.5f+(i%3)*.4f:.5f+(i%3)*.45f,Mathf.Sin(a)*(bomb?.3f:r))*size,
                            startColor=bomb?Color.Lerp(Recipe.color,new Color(.3f,.25f,.24f,.55f),(i%3)*.3f):Recipe.color,
                            startSize=size*(bomb?(i%3==0?.26f:.17f):i%3==0?.065f:.035f),startLifetime=duration*(1-impact)};particles.Emit(emit,1);
                    }
                }
            }
            if(!gun&&!bombFlash)flash.gameObject.SetActive(false);
            return elapsed<duration;
        }
        internal void Release(){if(traversal!=null){traversal.Release();return;}if(strategy!=null){strategy.Release();return;}particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);root.SetActive(false);cue=null;}
    }
}
