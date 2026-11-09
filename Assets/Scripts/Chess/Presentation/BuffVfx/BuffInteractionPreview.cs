using System.Collections.Generic;
using UnityEngine;

namespace ChessButWeird.BuffVfx
{
    internal sealed class BuffInteractionPreview
    {
        private readonly Transform owner;
        private readonly BuffVisualAssets assets;
        private readonly float tile;
        private readonly LineRenderer[] targets=new LineRenderer[64];
        private readonly BuffMeshEcho[] pieces=new BuffMeshEcho[32];
        private readonly LineRenderer arc;
        internal int Targets {get;private set;}
        internal int Ghosts {get;private set;}
        internal BuffInteractionPreview(Transform owner,BuffVisualAssets assets,float tile)
        {this.owner=owner;this.assets=assets;this.tile=tile;arc=assets.Line(owner,"Authorized interaction arc",tile*.018f,24);arc.useWorldSpace=true;arc.gameObject.SetActive(false);}
        internal void Sync(int buff,Vector3 source,IReadOnlyList<Vector3> allowed,Vector3? hovered,bool valid)
        {
            Targets=Mathf.Min(64,allowed.Count);
            for(int i=0;i<64;i++)
            {
                if(i>=Targets){if(targets[i])targets[i].gameObject.SetActive(false);continue;}
                if(!targets[i]){targets[i]=assets.Line(owner,"Authorized target "+i,tile*.018f,4,true);targets[i].useWorldSpace=true;}
                var line=targets[i];line.gameObject.SetActive(true);var at=allowed[i];var color=new Color(.65f,.85f,.82f,.65f);line.startColor=line.endColor=color;
                for(int j=0;j<4;j++){float a=(45+j*90)*Mathf.Deg2Rad;line.SetPosition(j,at+new Vector3(Mathf.Cos(a)*tile*.47f,tile*.055f,Mathf.Sin(a)*tile*.47f));}
            }
            bool show=hovered.HasValue&&allowed.Count>0;arc.gameObject.SetActive(show);if(!show)return;
            var ink=valid?new Color(.67f,.87f,.82f,.75f):new Color(.9f,.38f,.29f,.7f);arc.startColor=arc.endColor=ink;
            for(int i=0;i<24;i++){float t=i/23f;arc.SetPosition(i,Vector3.Lerp(source,hovered.Value,t)+Vector3.up*tile*(.1f+Mathf.Sin(t*Mathf.PI)*(buff==2?.7f:.18f)));}
        }
        internal void Formation(IReadOnlyList<BuffPreviewPiece> states)
        {
            Ghosts=Mathf.Min(32,states.Count);for(int i=0;i<32;i++)
            {
                if(i>=Ghosts){pieces[i]?.Hide();continue;}
                if(pieces[i]==null)pieces[i]=new BuffMeshEcho(owner,assets,"Uncommitted formation ghost "+i);
                var state=states[i];pieces[i].Capture(state.Actor);pieces[i].Pose(state.Position,state.Actor?state.Actor.rotation:Quaternion.identity,state.Team==PieceTeam.White?new Color(.95f,.83f,.57f,.3f):new Color(.4f,.69f,.94f,.3f),1.035f);
            }
        }
        internal void Clear(){Targets=Ghosts=0;foreach(var t in targets)if(t)t.gameObject.SetActive(false);foreach(var p in pieces)p?.Hide();arc.gameObject.SetActive(false);}
    }
}
