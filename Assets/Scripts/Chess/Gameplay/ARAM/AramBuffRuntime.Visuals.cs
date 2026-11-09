using System.Collections.Generic;
using ChessButWeird.BuffVfx;
using ChessButWeird.Domain;
using UnityEngine;

public sealed partial class AramBuffRuntime
{
    private BuffVfxOrchestrator buffVfx;
    private long visualSequence;
    private float nextVisualRefresh;
    private bool visualUnavailable;
    private PieceTeam? visualViewer;
    private readonly List<BuffVisualStatus> visualStatuses=new List<BuffVisualStatus>(64);
    private readonly List<BuffBoardVisual> visualBoard=new List<BuffBoardVisual>(96);
    private readonly HashSet<int> endedDecoyVisuals=new HashSet<int>();
    private BuffVfxOrchestrator EnsureVisuals()
    {
        if(visualUnavailable)return null;
        if(buffVfx)return buffVfx.Ready?buffVfx:null;
        if(!game)return null;
        var host=new GameObject("Practice CHAOS buff VFX");host.transform.SetParent(game.transform,false);
        buffVfx=host.AddComponent<BuffVfxOrchestrator>();buffVfx.Initialize(Vector3.Distance(game.AramTile(Vector2Int.zero),game.AramTile(Vector2Int.right)));
        visualUnavailable=!buffVfx.Ready;return visualUnavailable?null:buffVfx;
    }
    private void ResetVisuals()
    {
        if(buffVfx){buffVfx.ClearVisuals(true);buffVfx.gameObject.SetActive(false);Destroy(buffVfx.gameObject);}
        buffVfx=null;visualUnavailable=false;visualViewer=null;visualSequence=0;nextVisualRefresh=0;visualStatuses.Clear();visualBoard.Clear();endedDecoyVisuals.Clear();
    }
    private void Visual(AramBuffId id,ChessPiece actor,Vector2Int from,Vector2Int to,string outcome="Resolved",Vector3[] tiles=null,Vector3? source=null,Vector3? target=null)
    {
        var fx=EnsureVisuals();if(!fx||!actor)return;
        bool ground=fx.Registry.Find((int)id)?.group>=6;
        fx.Play(new BuffVisualEvent {Id="practice:"+(++visualSequence),BuffId=(int)id,ActorId=actor.GetInstanceID(),Actor=actor.transform,
            Team=actor.Team,Source=source??game.AramTile(from)+Vector3.up*fx.TileSize*(ground?0:.62f),
            Target=target??game.AramTile(to)+Vector3.up*fx.TileSize*(id==AramBuffId.SuicideBomber||ground?0:.5f),
            BoardRotation=Quaternion.LookRotation(game.AramTile(Vector2Int.up)-game.AramTile(Vector2Int.zero),Vector3.up),
            Outcome=outcome,Tiles=tiles,ViewerAuthorized=true});
    }
    private void LateUpdate()
    {
        if(!active||!game)return;
        var fx=EnsureVisuals();if(!fx)return;fx.Paused=game.PauseLocked;
        var viewer=game.IsBotGame?game.PlayerTeam:game.CurrentTurn;
        if(visualViewer.HasValue&&visualViewer!=viewer&&(HasBuff(PieceTeam.White,AramBuffId.Doppelganger)||HasBuff(PieceTeam.Black,AramBuffId.Doppelganger))){fx.ClearVisuals();nextVisualRefresh=0;}
        visualViewer=viewer;
        if(Time.unscaledTime<nextVisualRefresh)return;nextVisualRefresh=Time.unscaledTime+.1f;
        visualStatuses.Clear();
        if(!game.GameOver)for(int x=0;x<8;x++)for(int y=0;y<8;y++)
        {
            var p=game.AramBoard[x,y];if(!p)continue;
            void Add(AramBuffId buff,int remaining=-1,int percent=-1)=>visualStatuses.Add(new BuffVisualStatus((int)buff,p.GetInstanceID(),p.transform,p.Team,remaining,percent));
            if(bloodthirsty.Contains(p))Add(AramBuffId.NobleSacrifice);
            if(sniperUntil.TryGetValue(p,out int s)&&s>combinedPlies)Add(AramBuffId.AbsoluteSniper,s-combinedPlies);
            if(cannonTurns.TryGetValue(p,out int c)&&c>combinedPlies)Add(AramBuffId.HighTechEra,c-combinedPlies);
            if(unstableQueens.Contains(p))Add(AramBuffId.QueensBetrayal,percent:Mathf.RoundToInt(100*AramBalanceRules.BetrayalChance(betrayalChecks.TryGetValue(p,out int n)?n:0)));
            var side=GetState(p.Team);
            bool owner=p.Team==(game.IsBotGame?game.PlayerTeam:game.CurrentTurn);
            if(owner&&side.HasBuff(AramBuffId.FlyingThunderGod)&&side.OriginalQueen==p&&side.GetQueenTeleportUses()<5)Add(AramBuffId.FlyingThunderGod,side.GetQueenTeleportCooldown());
            if(owner&&side.HasBuff(AramBuffId.HidingKing)&&p.Type==PieceType.King&&!decoys.Contains(p))Add(AramBuffId.HidingKing,Mathf.Max(0,hidingReady[Side(p.Team)]-combinedPlies));
            if(owner&&side.HasBuff(AramBuffId.IFrameRoll)&&p.Type==PieceType.King&&!decoys.Contains(p)&&escapeUses[Side(p.Team)]<3)Add(AramBuffId.IFrameRoll,3-escapeUses[Side(p.Team)]);
            if(decoys.Contains(p))Add(AramBuffId.SubstituteNinjutsu);
            if(side.HasBuff(AramBuffId.GhostArmy))Add(AramBuffId.GhostArmy);
            if(side.HasBuff(AramBuffId.MonsterTruck)&&p.Type==PieceType.Rook)Add(AramBuffId.MonsterTruck);
            if(infectedUntil.TryGetValue(p,out int infection)&&infection>combinedPlies)Add(AramBuffId.PlagueTown,infection-combinedPlies);
            if(side.CommandantPawns.Contains(p))Add(AramBuffId.CommandantPawn);
            if(side.FreestyleKnight==p)Add(AramBuffId.FreestyleLeap);
            if(owner&&side.HasBuff(AramBuffId.Doppelganger)&&!doppelDisabled[Side(p.Team)]&&(side.SwappedKnight==p||side.SwappedBishop==p))Add(AramBuffId.Doppelganger,swapActive[Side(p.Team)]?1:0);
            if(queenMastery.Contains(p))Add(AramBuffId.UltimateQuest);
            if(passengers.ContainsKey(p))Add(AramBuffId.MobileFortress,1);
            if(BlocksPromotion(p))Add(AramBuffId.DoubleEdgedTrap,trapPawns.Contains(p)?1:0);
            if(owner&&p.Type==PieceType.King&&side.HasBuff(AramBuffId.PeaceTShirt)&&combinedPlies>=14&&!peaceFailed[Side(p.Team)]&&!peaceUsed[Side(p.Team)])Add(AramBuffId.PeaceTShirt);
            if(side.HasBuff(AramBuffId.SuicideBomber)&&side.OriginalQueen==p&&!side.SuicideBomberUsed)Add(AramBuffId.SuicideBomber);
            if(p.Type==PieceType.King&&side.HasBuff(AramBuffId.OneManArmy)&&!decoys.Contains(p)&&(!game.IsBotGame||p.Team==game.PlayerTeam))Add(AramBuffId.OneManArmy,Mathf.Max(0,rifleReady[Side(p.Team)]-completedTurns[Side(p.Team)]));
        }
        fx.SyncStatuses(visualStatuses);
        RefreshVisualBoard();
        RefreshStrategyVisuals();
    }
    private void VisualTile(int buff,Vector2Int square,PieceTeam team,string outcome,Vector3[] cells=null)
    {
        var fx=EnsureVisuals();if(!fx)return;
        fx.Play(new BuffVisualEvent{Id="practice:"+(++visualSequence),BuffId=buff,Team=team,Source=game.AramTile(square),Target=game.AramTile(square),Tiles=cells,Outcome=outcome,ViewerAuthorized=true,BoardRotation=Quaternion.LookRotation(game.AramTile(Vector2Int.up)-game.AramTile(Vector2Int.zero),Vector3.up)});
    }
    private void VisualDecoyEnd(ChessPiece piece)
    {if(piece&&endedDecoyVisuals.Add(piece.GetInstanceID()))Visual(AramBuffId.SubstituteNinjutsu,piece,piece.BoardPosition,piece.BoardPosition,"DecoyEnd");}
    private void RecordTraversalVisual(ChessPiece moving,Vector2Int from,Vector2Int to)
    {
        bool ghost=HasBuff(moving.Team,AramBuffId.GhostArmy),truck=moving.Type==PieceType.Rook&&HasBuff(moving.Team,AramBuffId.MonsterTruck);
        if(!ghost&&!truck)return;
        var delta=to-from;if(delta==Vector2Int.zero||delta.x!=0&&delta.y!=0&&Mathf.Abs(delta.x)!=Mathf.Abs(delta.y))return;
        var step=new Vector2Int(System.Math.Sign(delta.x),System.Math.Sign(delta.y));var cells=new List<Vector3>();
        for(var p=from+step;p!=to;p+=step){var crossed=At(p);if(crossed&&crossed.Team==moving.Team)cells.Add(game.AramTile(p));}
        if(cells.Count>0)Visual(ghost?AramBuffId.GhostArmy:AramBuffId.MonsterTruck,moving,from,to,"Phase",cells.ToArray());
    }
    private void RefreshVisualBoard()
    {
        var fx=EnsureVisuals();if(!fx)return;visualBoard.Clear();
        var rotation=Quaternion.LookRotation(game.AramTile(Vector2Int.up)-game.AramTile(Vector2Int.zero),Vector3.up);
        void Add(int id,Vector2Int p,PieceTeam team,string kind,int remaining=-1)=>visualBoard.Add(new BuffBoardVisual(id,p.x+p.y*8,game.AramTile(p),kind,team,remaining,rotation));
        foreach(var mine in mines)Add(11,mine.Key,mine.Value,"Mine");
        foreach(var crate in crates)Add(9,crate.Key,crate.Value.Team,"Crate");
        for(int x=0;x<8;x++)if(x<collapsedFiles||x>=8-collapsedFiles)for(int y=0;y<8;y++)Add(18,new Vector2Int(x,y),PieceTeam.White,"Barrier");
        int left=collapsedFiles==1?collapseSecondAt-combinedPlies:99;
        if(collapsedFiles==0)foreach(var team in new[]{PieceTeam.White,PieceTeam.Black})if(HasBuff(team,AramBuffId.DefinitionOfAram))left=Mathf.Min(left,10-completedTurns[Side(team)]);
        if(collapsedFiles<2&&left>0&&left<=2)for(int y=0;y<8;y++){Add(18,new Vector2Int(collapsedFiles,y),PieceTeam.White,"Warning",left);Add(18,new Vector2Int(7-collapsedFiles,y),PieceTeam.White,"Warning",left);}
        fx.SyncBoard(visualBoard);
    }
}
