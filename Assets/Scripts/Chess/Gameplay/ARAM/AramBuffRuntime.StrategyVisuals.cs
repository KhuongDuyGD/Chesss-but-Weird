using System.Collections.Generic;
using ChessButWeird.BuffVfx;
using UnityEngine;

public sealed partial class AramBuffRuntime
{
    private readonly List<BuffHudState> strategyHud=new List<BuffHudState>();
    private readonly List<Vector3> previewCells=new List<Vector3>(64);
    private readonly List<BuffPreviewPiece> formationGhosts=new List<BuffPreviewPiece>(32);
    private readonly Dictionary<ChessPiece,Vector2Int> formationDraft=new Dictionary<ChessPiece,Vector2Int>();
    private void VisualSelection(int id,PieceTeam team,IEnumerable<ChessPiece> pieces,string outcome)
    {
        var changes=new List<BuffVisualChange>();foreach(var piece in pieces)if(Live(piece))changes.Add(new BuffVisualChange{ActorId=piece.GetInstanceID(),Actor=piece.transform,Team=team,From=game.AramTile(piece.BoardPosition),To=game.AramTile(piece.BoardPosition),FromKind=piece.Type.ToString(),ToKind=piece.Type.ToString()});
        if(changes.Count>0)VisualDetails(id,team,changes[0].From,changes[0].To,outcome,changes.ToArray());
    }
    private void VisualPair(PieceTeam team,string outcome)=>VisualSelection(3,team,new[]{GetState(team).SwappedKnight,GetState(team).SwappedBishop},outcome);
    private List<BuffVisualChange> CaptureVisualArmy(PieceTeam team)
    {
        var result=new List<BuffVisualChange>();foreach(var p in game.GetActivePiecesForAram(team))result.Add(new BuffVisualChange{ActorId=p.GetInstanceID(),Actor=p.transform,Team=team,From=game.AramTile(p.BoardPosition),To=game.AramTile(p.BoardPosition),FromKind=p.Type.ToString(),ToKind=p.Type.ToString()});return result;
    }
    private void VisualArmy(int id,PieceTeam team,List<BuffVisualChange> before,string outcome)
    {
        var after=CaptureVisualArmy(team);var result=new List<BuffVisualChange>();var used=new HashSet<int>();
        foreach(var old in before)
        {
            int index=after.FindIndex(n=>n.ActorId==old.ActorId);if(index<0)index=after.FindIndex(n=>(n.To-old.From).sqrMagnitude<.0001f&&!used.Contains(n.ActorId));
            if(index>=0){var n=after[index];used.Add(n.ActorId);if(old.ActorId==n.ActorId&&old.FromKind==n.ToKind&&old.From==n.To)continue;n.From=old.From;n.FromKind=old.FromKind;result.Add(n);}
            else{var n=old;n.ToKind=null;n.Actor=null;result.Add(n);}
        }
        foreach(var n in after)if(!used.Contains(n.ActorId)&&before.TrueForAll(o=>o.ActorId!=n.ActorId)){var change=n;change.FromKind=null;result.Add(change);}
        var at=after.Count>0?after[0].To:Vector3.zero;VisualDetails(id,team,at,at,outcome,result.ToArray());
    }
    private void VisualDetails(int id,PieceTeam team,Vector3 from,Vector3 to,string outcome,BuffVisualChange[] changes=null,int? amount=null,ChessPiece actor=null)
    {
        if(id==3&&team!=(game.IsBotGame?game.PlayerTeam:game.CurrentTurn))return;
        var fx=EnsureVisuals();if(!fx)return;fx.Play(new BuffVisualEvent{Id="practice:"+(++visualSequence),BuffId=id,Team=team,Source=from,Target=to,Actor=actor?actor.transform:null,ActorId=actor?actor.GetInstanceID():0,Outcome=outcome,Changes=changes,Amount=amount,ViewerAuthorized=true,BoardRotation=Quaternion.LookRotation(game.AramTile(Vector2Int.up)-game.AramTile(Vector2Int.zero),Vector3.up)});
    }
    private void VisualResult(int id,ChessPiece piece,Vector2Int from,string outcome,string previous=null,int? amount=null)
    {
        if(!piece)return;var change=new BuffVisualChange{ActorId=piece.GetInstanceID(),Actor=piece.transform,Team=piece.Team,From=game.AramTile(from),To=game.AramTile(piece.BoardPosition),FromKind=previous,ToKind=piece.Type.ToString()};VisualDetails(id,piece.Team,change.From,change.To,outcome,new[]{change},amount,piece);
    }
    private void RefreshStrategyVisuals()
    {
        var fx=EnsureVisuals();if(!fx)return;strategyHud.Clear();var team=game.IsBotGame?game.PlayerTeam:game.CurrentTurn;int side=Side(team);
        if(!game.GameOver)
        {
            if(HasBuff(team,AramBuffId.KingIsPoorPiece))strategyHud.Add(new BuffHudState{BuffId=32,Team=team,Text="KINGLESS",Current=Mathf.Max(0,25-completedTurns[side]),Target=25,Unit="owner turns"});
            if(HasBuff(team,AramBuffId.EveryManForHimself))
            {
                int mask=questPromoted[side]?32:0;var types=new[]{PieceType.Pawn,PieceType.Knight,PieceType.Bishop,PieceType.Rook,PieceType.Queen};int count=questPromoted[side]?1:0;
                for(int i=0;i<5;i++)if(questCaptures[side].Contains(types[i])){mask|=1<<i;count++;}
                strategyHud.Add(new BuffHudState{BuffId=26,Team=team,Text=questRewarded[side]?"Quest reward earned":"Capture each type + promote",Current=count,Target=6,Mask=mask});
            }
            if(HasBuff(team,AramBuffId.CreditCard))strategyHud.Add(new BuffHudState{BuffId=31,Team=team,Text=bought[side]?"Purchase used this turn":credit[side]<0?"Purchases blocked: in debt":"Purchase available",Current=credit[side],Unit="points"});
            if(HasBuff(team,AramBuffId.GamblingLeadsToMisery))strategyHud.Add(new BuffHudState{BuffId=8,Team=team,Text="Extra moves used",Current=extraUses[side],Target=3});
            if(HasBuff(team,AramBuffId.PeaceTShirt))strategyHud.Add(new BuffHudState{BuffId=10,Team=team,Text=peaceFailed[side]?"Recruitment failed":peaceUsed[side]?"Recruitment used":combinedPlies>=14?"Recruitment ready":"Avoid check",Current=Mathf.Max(0,14-combinedPlies),Unit="combined moves"});
        }
        fx.SyncHud(strategyHud);RefreshInteractionVisuals();
    }
    private void ResetFormationPreview()
    {formationDraft.Clear();foreach(var pair in formation)formationDraft[pair.Key]=pair.Value;formationPiece=null;buffVfx?.ClearPreview();}
    private void RefreshInteractionVisuals()
    {
        var fx=EnsureVisuals();if(!fx)return;
        if(game.PauseLocked||game.GameOver||IsSelectingSetupTargets){fx.ClearPreview();return;}
        if(customizing)
        {
            formationGhosts.Clear();foreach(var pair in formationDraft)if(pair.Key)formationGhosts.Add(new BuffPreviewPiece(pair.Key.transform,game.AramTile(pair.Value),pair.Key.Team));fx.SyncFormation(formationGhosts);
            previewCells.Clear();if(formationPiece)for(int x=collapsedFiles;x<8-collapsedFiles;x++)for(int y=0;y<8;y++){var p=new Vector2Int(x,y);if(InHome(p,formationTeam)&&(!At(p)||At(p).Team==formationTeam))previewCells.Add(game.AramTile(p));}
            Vector3? hover=null;if(game.AramReadPointer(out var square))hover=game.AramTile(square);
            fx.SyncPreview(23,formationPiece?game.AramTile(formationDraft[formationPiece]):Vector3.zero,previewCells,hover,hover.HasValue&&previewCells.Contains(hover.Value));return;
        }
        fx.SyncFormation(System.Array.Empty<BuffPreviewPiece>());
        var selected=game.AramVisualSelected;var owner=game.IsBotGame?game.PlayerTeam:game.CurrentTurn;
        if(!selected||selected.Team!=owner||selected.Team!=game.CurrentTurn||targetAction!=null){fx.ClearPreview();return;}
        var state=GetState(selected.Team);int id=state.FreestyleKnight==selected?2:state.CommandantPawns.Contains(selected)?0:selected.Type==PieceType.King&&HasBuff(selected.Team,AramBuffId.StrongFortress)?1:-1;
        if(id<0){fx.ClearPreview();return;}previewCells.Clear();foreach(var p in game.AramVisualLegalMoves(selected))if(id!=1||p.y==selected.BoardPosition.y&&(p.x==2||p.x==6)&&Mathf.Abs(p.x-selected.BoardPosition.x)>=2)previewCells.Add(game.AramTile(p));
        Vector3? hovered=null;if(game.AramReadPointer(out var sq))hovered=game.AramTile(sq);bool valid=hovered.HasValue&&previewCells.Contains(hovered.Value);fx.SyncPreview(id,game.AramTile(selected.BoardPosition),previewCells,valid?hovered:null,valid);
    }
}
