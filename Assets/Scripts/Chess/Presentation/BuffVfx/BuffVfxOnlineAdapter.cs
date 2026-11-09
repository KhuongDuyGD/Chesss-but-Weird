using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ChessButWeird.BuffVfx
{
    public sealed class BuffVfxOnlineAdapter
    {
        private readonly BuffVfxOrchestrator fx;
        private readonly Func<Vector2Int,Vector3> tile;
        private readonly Func<int,Transform> actor;
        private readonly List<BuffVisualStatus> statuses=new List<BuffVisualStatus>(64);
        private readonly List<BuffBoardVisual> board=new List<BuffBoardVisual>(96);
        private readonly List<BuffHudState> hud=new List<BuffHudState>();
        private string match;
        private long version=-1,sequence;
        private bool terminalPresented;
        public long LastSequence=>sequence;
        public BuffVfxOnlineAdapter(BuffVfxOrchestrator fx,Func<Vector2Int,Vector3> tile,Func<int,Transform> actor)
        {this.fx=fx;this.tile=tile;this.actor=actor;}
        public Vector3 BoardPoint(Vector3 canonical)
        {
            var origin=tile(Vector2Int.zero);var x=tile(Vector2Int.right)-origin;var z=tile(Vector2Int.up)-origin;
            return origin+x*canonical.x+z*canonical.z+Vector3.Cross(z,x).normalized*fx.TileSize*canonical.y;
        }
        public static bool TrySquare(string text,out Vector2Int square)
        {square=default;if(text==null||text.Length!=2||text[0]<'a'||text[0]>'h'||text[1]<'1'||text[1]>'8')return false;square=new Vector2Int(text[0]-'a',text[1]-'1');return true;}
        private static bool Point(JToken token,out Vector3 point)
        {
            point=default;if(!(token is JArray a)||a.Count!=3)return false;
            for(int i=0;i<3;i++)if(a[i].Type!=JTokenType.Float&&a[i].Type!=JTokenType.Integer)return false;
            point=new Vector3((float)a[0],(float)a[1],(float)a[2]);
            return float.IsFinite(point.x)&&float.IsFinite(point.y)&&float.IsFinite(point.z)&&Mathf.Abs(point.x)<=32&&Mathf.Abs(point.y)<=32&&Mathf.Abs(point.z)<=32;
        }
        public void Apply(string matchId,long stateVersion,JObject chaos,bool finished,string resultReason=null)
        {
            bool first=version<0||match!=matchId;
            if(first){match=matchId;version=-1;terminalPresented=false;hud.Clear();sequence=(long?)chaos?["visualSequence"]??0;fx.ClearVisuals(true);}
            if(stateVersion<=version)return;version=stateVersion;
            if(finished||chaos==null){bool show=!first&&!terminalPresented&&resultReason=="PoorKingDeadline"&&hud.Exists(h=>h.BuffId==32);fx.ClearVisuals();if(show)fx.FinishKingless();terminalPresented=true;return;}
            statuses.Clear();
            foreach(var item in chaos["visualStatuses"]??new JArray())
            {
                if(!Enum.TryParse((string)item["team"],out PieceTeam team)||!Enum.IsDefined(typeof(PieceTeam),team))continue;
                int id=(int?)item["buffId"]??-1,piece=(int?)item["actorId"]??0;
                if(fx.Registry.Find(id)?.implemented!=true)continue;
                statuses.Add(new BuffVisualStatus(id,piece,actor(piece),team,(int?)item["remaining"]??-1,(int?)item["percent"]??-1));
            }
            fx.SyncStatuses(statuses);
            board.Clear();
            if(chaos["visualBoard"] is JArray boardStates)foreach(var item in boardStates)
            {
                if(board.Count>=192)break;
                if(!TrySquare((string)item["square"],out var p)||!Enum.TryParse((string)item["team"],out PieceTeam team)||!Enum.IsDefined(typeof(PieceTeam),team))continue;
                board.Add(new BuffBoardVisual((int?)item["buffId"]??-1,p.x+p.y*8,tile(p),(string)item["kind"],team,(int?)item["remaining"]??-1,Quaternion.LookRotation(tile(Vector2Int.up)-tile(Vector2Int.zero),Vector3.up)));
            }
            fx.SyncBoard(board);
            hud.Clear();if(chaos["visualHud"] is JArray objectives)foreach(var h in objectives)
            {
                if(!Enum.TryParse((string)h["team"],out PieceTeam team))continue;
                hud.Add(new BuffHudState{BuffId=(int?)h["buffId"]??-1,Team=team,Text=(string)h["text"],Unit=(string)h["unit"],Current=(int?)h["current"]??0,Target=(int?)h["target"]??0,Mask=(int?)h["mask"]??0});
            }
            fx.SyncHud(hud);
            if(first)return; // Reconnect/first load restores statuses; it never replays old explosions or gunfire.
            foreach(var item in chaos["visualCues"]??new JArray())
            {
                long seq=(long?)item["sequence"]??0;if(seq<=sequence)continue;
                sequence=seq;
                int id=(int?)item["buffId"]??-1,piece=(int?)item["actorId"]??0;
                if(fx.Registry.Find(id)?.implemented!=true||(string)item["phase"]!="Resolve"||
                    !TrySquare((string)item["from"],out var from)||!TrySquare((string)item["to"],out var to)||
                    !Enum.TryParse((string)item["team"],out PieceTeam team)||!Enum.IsDefined(typeof(PieceTeam),team))continue;
                bool ground=fx.Registry.Find(id).group>=6;
                Vector3 source=tile(from)+Vector3.up*fx.TileSize*(ground?0:.62f),target=tile(to)+Vector3.up*fx.TileSize*(id==4||ground?0:.5f);
                if(item["origin"] is JArray){if(!Point(item["origin"],out var p))continue;source=BoardPoint(p);}
                if(item["impact"] is JArray){if(!Point(item["impact"],out var p))continue;target=BoardPoint(p);}
                Vector3[] cells=null;
                if(item["tiles"] is JArray tiles&&tiles.Count>0)
                {
                    if(tiles.Count>(id==18?32:9))continue;cells=new Vector3[tiles.Count];bool valid=true;
                    for(int i=0;i<cells.Length;i++){if(!TrySquare((string)tiles[i],out var p)){valid=false;break;}cells[i]=tile(p);}if(!valid)continue;
                }
                BuffVisualChange[] changes=null;
                if(item["changes"] is JArray batch&&batch.Count>0)
                {
                    if(batch.Count>64)continue;changes=new BuffVisualChange[batch.Count];bool valid=true;
                    for(int i=0;i<batch.Count;i++)
                    {
                        var c=batch[i];if(!TrySquare((string)c["from"],out var f)||!TrySquare((string)c["to"],out var t)||!Enum.TryParse((string)c["team"],out PieceTeam ct)||!Enum.IsDefined(typeof(PieceTeam),ct)){valid=false;break;}
                        int cid=(int?)c["actorId"]??0;changes[i]=new BuffVisualChange{ActorId=cid,Team=ct,From=tile(f),To=tile(t),FromKind=(string)c["fromKind"],ToKind=(string)c["toKind"],Actor=actor(cid)};
                    }
                    if(!valid)continue;
                }
                fx.Play(new BuffVisualEvent{Id=match+":"+seq,BuffId=id,ActorId=piece,Actor=actor(piece),Team=team,Amount=(int?)item["amount"],Changes=changes,
                    BoardRotation=Quaternion.LookRotation(tile(Vector2Int.up)-tile(Vector2Int.zero),Vector3.up),
                    Source=source,Target=target,Tiles=cells,Outcome=(string)item["outcome"]??"Resolved",ViewerAuthorized=true});
            }
        }
    }
}
