using TMPro;
using UnityEngine;

namespace ChessButWeird.BuffVfx
{
    internal sealed class PieceStatusVisual
    {
        private readonly GameObject root;
        private readonly MeshFilter icon;
        private readonly MeshRenderer renderer;
        private readonly LineRenderer frame;
        private readonly TextMeshPro counter;
        private readonly MeshRenderer counterPlate;
        private readonly BuffVisualAssets assets;
        private readonly MaterialPropertyBlock color = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock plateColor = new MaterialPropertyBlock();
        private BuffVisualStatus state;
        private Color accent;
        private float size, height, fade = 1;
        private int slot,columns;
        private string value;
        private bool expiring;
        private BuffMeshEcho ghost;
        private GameObject decoy;
        private GameObject crown;
        private LineRenderer[] wisps;
        private float age,tile;
        internal PieceStatusVisual(Transform parent, BuffVisualAssets assets)
        {
            this.assets=assets;
            root=new GameObject("Buff persistent sigil");root.transform.SetParent(parent,false);
            var symbol=new GameObject("Distinct buff glyph",typeof(MeshFilter),typeof(MeshRenderer));symbol.transform.SetParent(root.transform,false);
            icon=symbol.GetComponent<MeshFilter>();renderer=symbol.GetComponent<MeshRenderer>();renderer.sharedMaterial=assets.Ink;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            frame=assets.Line(root.transform,"Team frame",.08f,16,true);
            var text=new GameObject("Confirmed remaining moves",typeof(TextMeshPro));text.transform.SetParent(root.transform,false);
            counter=text.GetComponent<TextMeshPro>();counter.font=ChessFontCatalog.TmpFont;counter.fontSize=16;counter.richText=false;
            counter.alignment=TextAlignmentOptions.Center;counter.enableAutoSizing=false;
            counter.rectTransform.sizeDelta=new Vector2(5,2);counter.transform.localPosition=new Vector3(0,-1.8f,0);
            counter.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            var plate=new GameObject("Counter ink backing",typeof(MeshFilter),typeof(MeshRenderer));plate.transform.SetParent(root.transform,false);plate.transform.localPosition=new Vector3(0,-1.8f,.02f);
            plate.GetComponent<MeshFilter>().sharedMesh=assets.Glyph("counterPlate");counterPlate=plate.GetComponent<MeshRenderer>();counterPlate.sharedMaterial=assets.Ink;counterPlate.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            BuffVisualAssets.IgnoreRaycasts(root);root.SetActive(false);
        }
        internal void Apply(BuffVisualStatus snapshot,BuffVisualRecipe recipe,float tile,int stack,int total)
        {
            this.tile=tile;
            if(state.Actor!=snapshot.Actor)
            {
                var collider=snapshot.Actor.GetComponentInChildren<Collider>();
                height=collider?Mathf.Max(tile*.55f,collider.bounds.max.y-snapshot.Actor.position.y):tile*.9f;
            }
            state=snapshot;slot=stack;columns=Mathf.Min(3,total-stack/3*3);size=tile*.105f;accent=recipe.color;icon.sharedMesh=assets.Glyph(recipe.glyph);
            if(snapshot.BuffId==21){if(ghost==null)ghost=new BuffMeshEcho(root.transform.parent,assets,"Ghost phase silhouette");ghost.Capture(snapshot.Actor);}else ghost?.Hide();
            if(snapshot.BuffId==15&&!decoy)decoy=assets.Prop("DecoyLog",root.transform.parent);
            if(decoy)decoy.SetActive(snapshot.BuffId==15);
            if(snapshot.BuffId==29&&!crown)crown=assets.Prop("CrownHalo",root.transform.parent);
            if(crown)crown.SetActive(snapshot.BuffId==29);
            if(snapshot.BuffId==33)icon.sharedMesh=assets.Glyph(snapshot.Remaining>0?"arrows":"lock");
            if((snapshot.BuffId==5||snapshot.BuffId==22)&&wisps==null){wisps=new LineRenderer[3];for(int i=0;i<3;i++){wisps[i]=assets.Line(root.transform.parent,"Status vapor or ready arc",tile*.015f,8);wisps[i].useWorldSpace=true;}}
            expiring=false;fade=1;root.SetActive(true);root.transform.localScale=Vector3.one*size;
            string next=snapshot.BuffId==33||snapshot.BuffId==3?"":snapshot.Percent>=0?snapshot.Percent+"%":snapshot.Remaining>=0?snapshot.Remaining.ToString():"";
            if(next!=value){value=next;counter.text=next;}
            counter.gameObject.SetActive(next.Length>0);counterPlate.gameObject.SetActive(next.Length>0);frame.widthMultiplier=tile*.012f;
            frame.positionCount=snapshot.Team==PieceTeam.White?16:4;
            for(int i=0;i<frame.positionCount;i++){float a=i*Mathf.PI*2/frame.positionCount;frame.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a),.01f)*1.08f);}
            Paint();Tick(0);
        }
        internal bool Tick(float dt)
        {
            if(!state.Actor||!state.Actor.gameObject.activeInHierarchy)return false;
            if(expiring){fade=Mathf.Max(0,fade-dt/.18f);Paint();if(fade<=0)return false;}
            var camera=Camera.main;
            var right=camera?camera.transform.right:Vector3.right;
            root.transform.position=state.Actor.position+Vector3.up*(height+size*(3f+(slot/3)*4.2f))+right*((slot%3-(columns-1)*.5f)*size*6f);
            if(camera)root.transform.rotation=camera.transform.rotation;
            age+=dt;
            if(state.BuffId==21&&ghost!=null){var ink=state.Team==PieceTeam.White?new Color(.73f,.91f,.91f,.16f*fade):new Color(.39f,.68f,.93f,.2f*fade);ghost.Pose(state.Actor.position,state.Actor.rotation,ink,1.035f);}
            if(decoy&&state.BuffId==15){decoy.transform.SetPositionAndRotation(state.Actor.position+Vector3.up*tile*.04f,state.Actor.rotation);decoy.transform.localScale=Vector3.one*tile*.85f*fade;}
            if(crown&&state.BuffId==29){crown.transform.SetPositionAndRotation(state.Actor.position+Vector3.up*(height+tile*.12f),state.Actor.rotation);crown.transform.localScale=Vector3.one*tile*.65f*fade;}
            if(wisps!=null)for(int j=0;j<3;j++)
            {
                bool show=state.BuffId==22||state.BuffId==5&&state.Remaining==0;
                var line=wisps[j];line.gameObject.SetActive(show);if(!show)continue;
                var ink=state.BuffId==22&&j==1?new Color(.59f,.41f,.71f):accent;ink.a=.4f*fade;line.startColor=line.endColor=ink;
                float phase=UserSettings.Presentation.ReducedMotion?0:age*.8f;
                for(int i=0;i<8;i++){float f=i/7f,a=j*Mathf.PI*2/3+f*2+phase;line.SetPosition(i,state.Actor.position+new Vector3(Mathf.Cos(a)*tile*.23f,tile*(.08f+f*.45f),Mathf.Sin(a)*tile*.23f));}
            }
            return true;
        }
        private void Paint()
        {
            var ink=accent;ink.a=fade;color.SetColor("_Color",ink);renderer.SetPropertyBlock(color);
            var team=state.Team==PieceTeam.White?new Color(.96f,.86f,.65f,fade):new Color(.33f,.62f,.92f,fade);
            frame.startColor=frame.endColor=team;counter.color=new Color(.96f,.94f,.87f,fade);
            plateColor.SetColor("_Color",new Color(.1f,.11f,.14f,.85f*fade));counterPlate.SetPropertyBlock(plateColor);
        }
        internal void Expire(){expiring=true;}
        internal void Release(){root.SetActive(false);state.Actor=null;ghost?.Hide();if(decoy)decoy.SetActive(false);if(crown)crown.SetActive(false);if(wisps!=null)foreach(var w in wisps)w.gameObject.SetActive(false);age=0;}
    }
}
