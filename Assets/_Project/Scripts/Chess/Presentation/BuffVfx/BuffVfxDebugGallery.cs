#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessButWeird.BuffVfx
{
    // Synthetic presentation events in a developer-only scene; never attached to a ChessGame.
    public sealed class BuffVfxDebugGallery : MonoBehaviour
    {
        public GameObject[] PieceModels;
        private static readonly int[] Cases={6,17,4,7,16,25,5,13,15,20,21,30,22,9,11,18,12,14,19,24,27,29,31,32,33,34,0,1,2,3,8,10,23,26,28};
        private int page;
        private BuffVfxOrchestrator fx;
        private GameObject actors;
        private Transform caster,target;
        private Transform decoy;
        private Vector3 recordedFrom,recordedTo;
        private Vector3[] crossedAllies;
        private int selected;
        private PieceTeam selectedTeam;
        private long sequence;
        private Material lightTile,dark,pieceWhite,pieceBlack;
        private Mesh tileMesh;
        private readonly List<BuffVisualStatus> statuses=new List<BuffVisualStatus>();
        public BuffVfxOrchestrator Effects=>fx;
        public Transform Caster=>caster;
        public Transform Target=>target;
        private void Start(){if(!fx)Build();}
        public void Build()
        {
            var camera=new GameObject("VFX gallery camera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(transform,false);camera.tag="MainCamera";
            camera.transform.position=new Vector3(10,9,-9);camera.transform.LookAt(new Vector3(3.5f,0,3.5f));camera.orthographic=true;camera.orthographicSize=6.5f;camera.backgroundColor=new Color(.11f,.13f,.18f);camera.clearFlags=CameraClearFlags.SolidColor;
            var sun=new GameObject("Gallery key light",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1;sun.transform.SetParent(transform,false);sun.transform.rotation=Quaternion.Euler(50,-30,0);
            var shader=Resources.Load<Shader>("BuffVfx/PropInk");
            lightTile=new Material(shader){color=new Color(.78f,.73f,.61f)};dark=new Material(shader){color=new Color(.29f,.35f,.38f)};
            pieceWhite=new Material(shader){color=new Color(.92f,.88f,.72f)};pieceBlack=new Material(shader){color=new Color(.25f,.4f,.57f)};
            tileMesh=new Mesh {name="Developer board tile"};tileMesh.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)};tileMesh.triangles=new[]{0,1,2,0,2,3};tileMesh.RecalculateNormals();
            for(int x=0;x<8;x++)for(int y=0;y<8;y++){var tile=new GameObject("Tile "+x+","+y,typeof(MeshFilter),typeof(MeshRenderer));tile.transform.SetParent(transform,false);tile.transform.position=new Vector3(x,0,y);tile.GetComponent<MeshFilter>().sharedMesh=tileMesh;tile.GetComponent<MeshRenderer>().sharedMaterial=(x+y)%2==0?lightTile:dark;}
            fx=gameObject.AddComponent<BuffVfxOrchestrator>();fx.Initialize(1);Select(0);
        }
        private Transform Piece(int kind,PieceTeam team,Vector2Int square)
        {
            if(PieceModels==null||kind>=PieceModels.Length||!PieceModels[kind])throw new InvalidOperationException("Assign the six existing low-poly chess models to the gallery.");
            var go=Instantiate(PieceModels[kind],actors.transform);go.name=team+" gallery piece";
            var renderers=go.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            go.transform.localScale*=.95f/Mathf.Max(.001f,bounds.size.y);
            bounds=renderers[0].bounds;foreach(var r in renderers){r.sharedMaterial=team==PieceTeam.White?pieceWhite:pieceBlack;bounds.Encapsulate(r.bounds);}
            go.transform.position+=new Vector3(square.x-bounds.center.x,-bounds.min.y,square.y-bounds.center.z);
            BuffVisualAssets.IgnoreRaycasts(go);return go.transform;
        }
        public void Select(int index,bool edge=false,PieceTeam team=PieceTeam.White)
        {
            selected=index;selectedTeam=team;fx.ClearVisuals(true);if(actors){actors.SetActive(false);Destroy(actors);}actors=new GameObject("Gallery actors");actors.transform.SetParent(transform,false);
            int id=Cases[selected],kind=id==7||id==21?3:id==16||id==30?2:id==25||id==13||id==15||id==20?0:id==17||id==4||id==5?1:5;
            if(selected>=16)kind=id==1||id==28?0:id==32||id==26?1:id==31||id==19?3:id==27||id==2||id==3||id==29||id==10?4:id==34||id==14||id==23?2:5;
            var from=edge?Vector2Int.zero:new Vector2Int(2,2);var to=edge?Vector2Int.zero:new Vector2Int(5,5);
            if(id==5)to=from+new Vector2Int(3,2);
            if(id==13){from=new Vector2Int(edge?0:2,0);to=new Vector2Int(edge?3:5,0);}
            if(id==20)to=from+new Vector2Int(2,1);
            if(id==21){from=edge?Vector2Int.zero:Vector2Int.one;to=from+new Vector2Int(4,4);}
            if(id==30){from=edge?Vector2Int.zero:new Vector2Int(2,1);to=from+new Vector2Int(0,4);}
            if(id==1||id==28){from=new Vector2Int(4,team==PieceTeam.White?0:7);to=new Vector2Int(6,from.y);}
            if(id==2)to=from+new Vector2Int(0,2);
            if(id==0){from=new Vector2Int(edge?0:2,team==PieceTeam.White?1:6);to=from+new Vector2Int(0,team==PieceTeam.White?2:-2);}
            if(id==12)to=from+Vector2Int.one;
            if(id==8)to=from+Vector2Int.one;
            if(id==29)to=from+new Vector2Int(1,2);
            if(id==23){from=new Vector2Int(2,team==PieceTeam.White?1:6);to=new Vector2Int(5,team==PieceTeam.White?2:5);}
            if(id==24){from=new Vector2Int(0,team==PieceTeam.White?0:7);to=from;}
            if(id==19||id==27||id==32){from=new Vector2Int(id==19?1:id==27?3:4,team==PieceTeam.White?0:7);to=from;}
            if(id==33){from=new Vector2Int(edge?0:2,team==PieceTeam.White?6:1);to=from+new Vector2Int(0,team==PieceTeam.White?1:-1);}
            if(id==34){from=new Vector2Int(edge?0:2,team==PieceTeam.White?7:0);to=new Vector2Int(5,team==PieceTeam.White?4:3);}
            recordedFrom=new Vector3(from.x,0,from.y);recordedTo=new Vector3(to.x,0,to.y);decoy=null;crossedAllies=null;
            caster=Piece(kind,team,id==4?to:from);target=Piece(id==17?1:id==3?3:2,id==17||id==13||id==3||id==1||id==28||id==12?team:team==PieceTeam.White?PieceTeam.Black:PieceTeam.White,to);
            if(selected>=16)
            {
                target.gameObject.SetActive(false);caster.position=recordedTo;
                if(id==1||id==28){target.gameObject.SetActive(true);target.position=new Vector3(5,0,to.y);}
                if(id==3){target.gameObject.SetActive(true);target.position=new Vector3(4,0,4);}
                if(id==12){target.gameObject.SetActive(true);target.position=recordedFrom;caster.gameObject.SetActive(false);}
            }
            if(id==17)target.gameObject.SetActive(false);
            if(id==9||id==11||id==18){caster.gameObject.SetActive(false);target.gameObject.SetActive(false);}
            if(id==5||id==15||id==20||id==21||id==30)target.gameObject.SetActive(false);
            if(id==21||id==30)
            {
                crossedAllies=new Vector3[3];var step=(to-from)/4;
                for(int i=0;i<3;i++){var p=from+step*(i+1);Piece(5,team,p);crossedAllies[i]=new Vector3(p.x,0,p.y);}
            }
            statuses.Clear();statuses.Add(new BuffVisualStatus(id,caster.GetInstanceID(),caster,team,id==7?4:id==16?10:id==25?0:-1,id==17?20:-1));fx.SyncStatuses(statuses);
            if(id>=5&&id!=7&&id!=16&&id!=17&&id!=25)fx.SyncStatuses(id==9||id==11||id==18?Array.Empty<BuffVisualStatus>():new[]{new BuffVisualStatus(id,caster.GetInstanceID(),caster,team,id==5?0:id==13?10:id==20?3:id==22?4:-1)});
            if(id==15)fx.SyncStatuses(Array.Empty<BuffVisualStatus>());
            var board=new List<BuffBoardVisual>();
            if(id==9||id==11)board.Add(new BuffBoardVisual(id,to.x+to.y*8,new Vector3(to.x,0,to.y),id==9?"Crate":"Mine",team));
            if(id==18)for(int y=0;y<8;y++)foreach(int x in new[]{0,7})board.Add(new BuffBoardVisual(18,x+y*8,new Vector3(x,0,y),"Warning",team,1));
            fx.SyncBoard(board);
            if(selected>=16)
            {
                var marked=id==12?target:caster;
                fx.SyncStatuses(id==0||id==2||id==3||id==29||id==33||id==12?new[]{new BuffVisualStatus(id,marked.GetInstanceID(),marked,team,id==12?1:id==33?0:-1)}:Array.Empty<BuffVisualStatus>());
                fx.SyncHud(id==32?new[]{new BuffHudState{BuffId=32,Team=team,Text="KINGLESS",Current=2,Target=25,Unit="owner turns"}}:id==26?new[]{new BuffHudState{BuffId=26,Team=team,Text="Capture each type + promote",Current=4,Target=6,Mask=15}}:id==31?new[]{new BuffHudState{BuffId=31,Team=team,Text="Purchase available",Current=8,Unit="points"}}:id==10?new[]{new BuffHudState{BuffId=10,Team=team,Text="Recruitment ready",Current=0,Unit="combined moves"}}:Array.Empty<BuffHudState>());
                if(id==23){fx.SyncFormation(new[]{new BuffPreviewPiece(caster,recordedFrom,team)});fx.SyncPreview(23,recordedTo,new[]{recordedFrom,recordedTo},recordedFrom,true);}
                if(id==0||id==1||id==2)fx.SyncPreview(id,recordedFrom,new[]{recordedTo},recordedTo,true);
            }
        }
        public bool PlaySelected(bool edge=false,string outcome="Resolved")
        {
            int id=Cases[selected];var source=caster.position+Vector3.up*.62f;var destination=target.position+Vector3.up*.5f;
            if(selected>=16)
            {
                var kind=id==1||id==28?"King":id==32||id==26?"Queen":id==31||id==19?"Bishop":id==27||id==2||id==3||id==29||id==10?"Knight":id==34||id==14||id==23?"Rook":"Pawn";
                var before=id==14||id==31||id==12?null:id==32?"King":id==27?"Queen":id==19||id==26?"Pawn":id==24?"Rook":kind;
                if(outcome=="Resolved")outcome=id==8?"ExtraMove":id==12?"Parachuted":id==31?"Purchased":id==34?"Landed":id==26?"QuestReward":id==3?"Exchanged":"Resolved";
                var changes=new List<BuffVisualChange>{new BuffVisualChange{ActorId=caster.GetInstanceID(),Actor=caster,Team=selectedTeam,From=recordedFrom,To=recordedTo,FromKind=before,ToKind=kind}};
                if(id==1||id==28)changes.Add(new BuffVisualChange{ActorId=target.GetInstanceID(),Actor=target,Team=selectedTeam,From=new Vector3(7,0,recordedTo.z),To=new Vector3(5,0,recordedTo.z),FromKind="Rook",ToKind="Rook"});
                if(id==3)changes.Add(new BuffVisualChange{ActorId=target.GetInstanceID(),Actor=target,Team=selectedTeam,From=target.position,To=target.position,FromKind="Bishop",ToKind="Bishop"});
                if(id==12){target.gameObject.SetActive(false);caster.gameObject.SetActive(true);fx.SyncStatuses(Array.Empty<BuffVisualStatus>());}
                fx.ClearPreview();if(id==33)fx.SyncStatuses(new[]{new BuffVisualStatus(33,caster.GetInstanceID(),caster,selectedTeam,1)});
                return fx.Play(new BuffVisualEvent{Id="gallery:"+(++sequence),BuffId=id,ActorId=caster.GetInstanceID(),Actor=caster,Team=selectedTeam,Source=recordedFrom,Target=recordedTo,Changes=changes.ToArray(),Amount=id==31?4:id==8?(int?)1:null,ViewerAuthorized=true,Outcome=outcome});
            }
            if(fx.Registry.Find(id).group==5){source=recordedFrom+Vector3.up*.62f;destination=recordedTo+Vector3.up*.5f;caster.position=recordedTo;if(id==13)target.position=recordedFrom;}
            if(fx.Registry.Find(id).group==6){source=caster.position;destination=target.position;}
            Vector3[] tiles=null;if(id==4){var center=edge?Vector2Int.zero:new Vector2Int(5,5);var cells=new List<Vector3>();for(int x=center.x-1;x<=center.x+1;x++)for(int y=center.y-1;y<=center.y+1;y++)if(x>=0&&x<8&&y>=0&&y<8)cells.Add(new Vector3(x,0,y));tiles=cells.ToArray();destination=new Vector3(center.x,0,center.y);}
            if(id==4){caster.gameObject.SetActive(false);target.gameObject.SetActive(false);}
            if(id==17){caster.gameObject.SetActive(false);target.gameObject.SetActive(outcome=="Transferred");if(outcome=="Resolved")outcome="Betrayal";}
            if(id==7)fx.SyncStatuses(Array.Empty<BuffVisualStatus>());
            if(id==25)fx.SyncStatuses(new[]{new BuffVisualStatus(id,caster.GetInstanceID(),caster,selectedTeam,3)});
            if(id==5)fx.SyncStatuses(new[]{new BuffVisualStatus(id,caster.GetInstanceID(),caster,selectedTeam,5)});
            if(id==21||id==30)tiles=crossedAllies;
            if(id==9&&outcome=="Resolved")outcome="Drop";
            if(id==11){if(outcome=="Resolved")outcome="Trigger";fx.SyncBoard(Array.Empty<BuffBoardVisual>());}
            if(id==15&&outcome=="Resolved")outcome="Substitute";
            if(id==15)
            {
                if(!decoy)decoy=Piece(0,selectedTeam,new Vector2Int(Mathf.RoundToInt(recordedFrom.x),Mathf.RoundToInt(recordedFrom.z)));
                if(outcome=="DecoyEnd"){decoy.gameObject.SetActive(false);fx.SyncStatuses(Array.Empty<BuffVisualStatus>());}
                else fx.SyncStatuses(new[]{new BuffVisualStatus(15,decoy.GetInstanceID(),decoy,selectedTeam)});
            }
            if(id==18)
            {
                var cells=new List<Vector3>();var props=new List<BuffBoardVisual>();
                for(int y=0;y<8;y++)foreach(int x in new[]{0,7}){var p=new Vector3(x,0,y);cells.Add(p);props.Add(new BuffBoardVisual(18,x+y*8,p,"Barrier",selectedTeam));}
                tiles=cells.ToArray();source=destination=Vector3.zero;outcome="Collapse";fx.SyncBoard(props);
            }
            return fx.Play(new BuffVisualEvent{Id="gallery:"+(++sequence),BuffId=id,ActorId=caster.GetInstanceID(),Actor=id==17&&outcome=="Transferred"?target:caster,Team=selectedTeam,Source=source,Target=destination,Tiles=tiles,ViewerAuthorized=true,Outcome=outcome});
        }
        private void OnGUI()
        {
            if(!fx)return;GUI.skin.font=ChessFontCatalog.RuntimeFont;
            GUI.Box(new Rect(15,15,760,230),"BUFF VFX / DEVELOPER GALLERY / SYNTHETIC EVENTS");
            for(int i=0;i<12;i++){int index=page*12+i;if(index<Cases.Length&&GUI.Button(new Rect(25+(i%4)*185,45+(i/4)*35,180,30),fx.Registry.Find(Cases[index]).key))Select(index,false,selectedTeam);}
            if(GUI.Button(new Rect(25,154,160,28),"PAGE "+(page+1)+" / 3"))page=(page+1)%3;
            if(GUI.Button(new Rect(25,195,105,28),"PLAY"))PlaySelected();
            if(GUI.Button(new Rect(140,195,105,28),"EDGE CASE")){Select(selected,true,selectedTeam);PlaySelected(true);}
            if(GUI.Button(new Rect(255,195,105,28),"CLEAR / SKIP"))fx.ClearVisuals();
            if(GUI.Button(new Rect(370,195,105,28),selectedTeam.ToString()))Select(selected,false,selectedTeam==PieceTeam.White?PieceTeam.Black:PieceTeam.White);
            GUI.Label(new Rect(485,195,285,30),"Active: "+fx.ActiveCount+" / Status: "+fx.ActiveStatuses+" / Board: "+fx.ActiveBoardProps);
        }
        private void OnDestroy(){if(lightTile)Destroy(lightTile);if(dark)Destroy(dark);if(pieceWhite)Destroy(pieceWhite);if(pieceBlack)Destroy(pieceBlack);if(tileMesh)Destroy(tileMesh);}
    }
}
#endif
