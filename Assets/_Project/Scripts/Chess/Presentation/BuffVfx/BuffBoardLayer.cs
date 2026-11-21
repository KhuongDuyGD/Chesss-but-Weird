using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessButWeird.BuffVfx
{
    internal sealed class BuffBoardLayer
    {
        internal const int Budget=192;
        private readonly Transform parent;
        private readonly BuffVisualAssets assets;
        private readonly float tile;
        private readonly Dictionary<int,TileProp> active=new Dictionary<int,TileProp>();
        private readonly Dictionary<string,Stack<TileProp>> pools=new Dictionary<string,Stack<TileProp>>();
        private readonly HashSet<int> desired=new HashSet<int>();
        private readonly List<int> removals=new List<int>();
        private int created;
        internal int ActiveCount=>active.Count;
        internal BuffBoardLayer(Transform parent,BuffVisualAssets assets,float tile){this.parent=parent;this.assets=assets;this.tile=tile;}
        internal void Sync(IReadOnlyList<BuffBoardVisual> states)
        {
            desired.Clear();foreach(var state in states)
            {
                if(state.Cell<0||state.Cell>63||!Valid(state)||!float.IsFinite(state.Position.x)||!float.IsFinite(state.Position.y)||!float.IsFinite(state.Position.z)||!desired.Add(state.Key))continue;
                if(!active.TryGetValue(state.Key,out var visual))
                {
                    if(!pools.TryGetValue(state.Kind,out var pool)){pool=new Stack<TileProp>();pools[state.Kind]=pool;}
                    if(pool.Count>0)visual=pool.Pop();else{if(created>=Budget)continue;visual=new TileProp(parent,assets,state.Kind,tile);created++;}
                    active.Add(state.Key,visual);
                }
                visual.Apply(state);
            }
            removals.Clear();foreach(var item in active)if(!desired.Contains(item.Key))removals.Add(item.Key);
            foreach(int key in removals){var p=active[key];p.Hide();pools[p.Kind].Push(p);active.Remove(key);}
        }
        private static bool Valid(BuffBoardVisual state)=>(state.BuffId==9&&state.Kind=="Crate")||(state.BuffId==11&&state.Kind=="Mine")||(state.BuffId==18&&(state.Kind=="Barrier"||state.Kind=="Warning"));
        internal void Resolve(BuffVisualEvent cue,float seconds)
        {
            if(cue.BuffId==9&&cue.Outcome=="Drop")foreach(var p in active.Values)if(p.Kind=="Crate"&&(p.Position-cue.Target).sqrMagnitude<tile*tile*.01f)p.Delay(seconds);
            if(cue.BuffId==11&&cue.Outcome=="Arm")foreach(var p in active.Values)if(p.Kind=="Mine"&&(p.Position-cue.Target).sqrMagnitude<tile*tile*.01f)p.Delay(seconds);
            if(cue.BuffId==18&&cue.Tiles!=null)foreach(var p in active.Values)if(p.Kind=="Barrier")foreach(var cell in cue.Tiles)if((p.Position-cell).sqrMagnitude<tile*tile*.01f)p.Delay(seconds*.55f);
        }
        internal void Tick(float dt){foreach(var p in active.Values)p.Tick(dt);}
        internal void Clear(){foreach(var p in active.Values){p.Hide();pools[p.Kind].Push(p);}active.Clear();}
        private sealed class TileProp
        {
            private readonly GameObject root,prop,plate;
            private readonly LineRenderer border,crack;
            private readonly BuffVisualAssets assets;
            private readonly float tile;
            private float delay,age;
            private BuffBoardVisual state;
            private readonly MaterialPropertyBlock tint=new MaterialPropertyBlock();
            internal string Kind {get;}
            internal Vector3 Position=>state.Position;
            internal TileProp(Transform parent,BuffVisualAssets assets,string kind,float tile)
            {
                Kind=kind;this.assets=assets;this.tile=tile;root=new GameObject("Buff board "+kind);root.transform.SetParent(parent,false);
                string model=kind=="Crate"?"SupplyCrate":kind=="Mine"?"Landmine":kind=="Barrier"?"ConstructionBarrier":null;
                prop=assets.Prop(model,root.transform);
                if(prop)prop.transform.localScale=Vector3.one*tile;
                border=assets.Line(root.transform,"Tile footprint",tile*.018f,4,true);
                for(int i=0;i<4;i++){float a=(45+i*90)*Mathf.Deg2Rad;border.SetPosition(i,new Vector3(Mathf.Cos(a)*.64f,.035f,Mathf.Sin(a)*.64f)*tile);}
                crack=assets.Line(root.transform,"Confirmed perimeter cracks",tile*.025f,7);crack.gameObject.SetActive(kind=="Warning");
                for(int i=0;i<7;i++)crack.SetPosition(i,new Vector3(-.4f+i*.13f,.04f,(i%2==0?-.13f:.14f))*tile);
                plate=new GameObject("Collapsed cell void inlay",typeof(MeshFilter),typeof(MeshRenderer));plate.transform.SetParent(root.transform,false);
                plate.GetComponent<MeshFilter>().sharedMesh=assets.Glyph("tilePlate");plate.GetComponent<MeshRenderer>().sharedMaterial=assets.Ink;
                plate.transform.localRotation=Quaternion.Euler(90,0,0);plate.transform.localPosition=Vector3.up*tile*.018f;plate.transform.localScale=Vector3.one*tile;
                tint.SetColor("_Color",new Color(.09f,.09f,.13f,.98f));plate.GetComponent<MeshRenderer>().SetPropertyBlock(tint);plate.SetActive(kind=="Barrier");
                BuffVisualAssets.IgnoreRaycasts(root);root.SetActive(false);
            }
            internal void Apply(BuffBoardVisual value){if(!root.activeSelf){delay=age=0;}state=value;root.SetActive(true);root.transform.SetPositionAndRotation(value.Position,value.Rotation);Tick(0);}
            internal void Delay(float seconds){delay=Mathf.Max(delay,seconds);Tick(0);}
            internal void Tick(float dt)
            {
                age+=dt;delay=Mathf.Max(0,delay-dt);if(prop)prop.SetActive(delay<=0);
                var color=Kind=="Mine"?new Color(.86f,.43f,.26f):Kind=="Warning"||Kind=="Barrier"?new Color(.96f,.65f,.22f):state.Team==PieceTeam.White?new Color(.94f,.82f,.55f):new Color(.35f,.65f,.9f);
                color.a=Kind=="Warning"?UserSettings.Presentation.ReducedMotion||UserSettings.Presentation.Flash==0?.65f:.55f+.15f*Mathf.Sin(age*3):.7f;
                border.startColor=border.endColor=crack.startColor=crack.endColor=color;
                if(prop&&Kind=="Crate")prop.transform.localPosition=Vector3.up*tile*(UserSettings.Presentation.ReducedMotion?0:.012f*Mathf.Sin(age*2));
            }
            internal void Hide(){root.SetActive(false);delay=age=0;}
        }
    }
}
