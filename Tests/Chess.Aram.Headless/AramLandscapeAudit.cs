using System;
using System.Collections.Generic;
using ChessButWeird.BuffVfx;
using UnityEngine;

public static partial class AramAuditRunner
{
    private static void TestLandscapeVisualHooks()
    {
        var game=Match(AramBuffId.FlyingThunderGod);var runtime=game.Runtime;var fx=(BuffVfxOrchestrator)Call(runtime,"EnsureVisuals");
        var queen=game.Board[3,0];ClearArmy(game,queen,game.Board[4,0],game.Board[4,7]);
        var from=queen.BoardPosition;var to=new Vector2Int(2,2);Check(runtime.IsLegalAramMove(queen,from,to,game.Board),"Teleport test uses a legal real extension");
        Call(game,"AramRelocate",queen,to,false,false,true);runtime.OnMoveAccepted(queen,from,to,game.Board);
        Check(fx.ActiveCount==1,"Accepted local teleport emits its resolve cue");Call(runtime,"LateUpdate");
        Check(Field<List<BuffVisualStatus>>(runtime,"visualStatuses").Exists(s=>s.BuffId==5&&s.Remaining==5),"Local actual teleport cooldown projected");
        game.CurrentTurn=PieceTeam.Black;Set(runtime,"nextVisualRefresh",0f);Call(runtime,"LateUpdate");
        Check(!Field<List<BuffVisualStatus>>(runtime,"visualStatuses").Exists(s=>s.BuffId==5),"Hotseat opponent cannot see readiness");End(game);

        foreach(var buff in new[]{AramBuffId.GhostArmy,AramBuffId.MonsterTruck})
        {
            game=Match(buff);runtime=game.Runtime;fx=(BuffVfxOrchestrator)Call(runtime,"EnsureVisuals");var rook=game.Board[0,0];
            ClearArmy(game,rook,game.Board[4,0],game.Board[4,7]);Call(game,"AramRelocate",rook,new Vector2Int(1,1),false,false,true);
            for(int y=2;y<=4;y++)game.Add(PieceTeam.White,PieceType.Pawn,1,y);
            to=new Vector2Int(1,5);Check(runtime.IsLegalAramMove(rook,rook.BoardPosition,to,game.Board),"Real traversal through three allies: "+buff);
            runtime.BeforeNormalMove(rook,null,rook.BoardPosition,to);Check(fx.ActiveCount==1,"Local confirmed traversal cue: "+buff);
            Check(game.Board[1,2]&&game.Board[1,3]&&game.Board[1,4],"Traversal visualization cannot push/remove allies");End(game);
        }
        game=Match(AramBuffId.SubstituteNinjutsu);runtime=game.Runtime;fx=(BuffVfxOrchestrator)Call(runtime,"EnsureVisuals");
        var decoy=game.Add(PieceTeam.White,PieceType.King,2,3);Field<HashSet<ChessPiece>>(runtime,"decoys").Add(decoy);Call(runtime,"LateUpdate");
        Check(Field<List<BuffVisualStatus>>(runtime,"visualStatuses").Exists(s=>s.BuffId==15&&s.ActorId==decoy.GetInstanceID()),"Real decoy instance ID projected");
        Call(game,"AramRemove",decoy,false);runtime.OnPieceRemoved(decoy);Check(fx.ActiveCount==1,"Decoy removal cue emitted once even with duplicate cleanup callbacks");End(game);

        game=Match(AramBuffId.RiseOfPawn);runtime=game.Runtime;fx=(BuffVfxOrchestrator)Call(runtime,"EnsureVisuals");var pawn=game.Board[2,1];Call(game,"AramRemove",game.Board[2,0],false);
        from=pawn.BoardPosition;to=new Vector2Int(2,0);Call(game,"AramRelocate",pawn,to,false,false,true);runtime.AfterNormalMove(pawn,null,from,to);Call(runtime,"RefreshVisualBoard");
        Check(fx.ActiveCount==1&&fx.ActiveBoardProps==1&&!game.Board[2,0],"Actual retreat creates a single public mine prop");
        var enemy=game.Board[2,7];Call(game,"AramRelocate",enemy,to,false,false,true);Call(runtime,"ResolveLanding",enemy,to);Call(runtime,"RefreshVisualBoard");
        Check(fx.ActiveCount==2&&fx.ActiveBoardProps==0&&!game.Board[2,0],"Actual mine trigger removes victim and persistent mine");End(game);

        game=Match(AramBuffId.PlagueTown);runtime=game.Runtime;fx=(BuffVfxOrchestrator)Call(runtime,"EnsureVisuals");var infected=game.Board[2,6];
        Field<Dictionary<ChessPiece,int>>(runtime,"infectedUntil")[infected]=2;Call(runtime,"LateUpdate");Check(Field<List<BuffVisualStatus>>(runtime,"visualStatuses").Exists(s=>s.BuffId==22&&s.Remaining==2),"Actual combined-ply poison lifetime projected");
        Ply(runtime);Ply(runtime);Check(fx.ActiveCount==1&&!game.Board[2,6],"Actual plague expiry publishes dissolve after rule removal");End(game);

        game=Match(AramBuffId.DefinitionOfAram);runtime=game.Runtime;fx=(BuffVfxOrchestrator)Call(runtime,"EnsureVisuals");Field<int[]>(runtime,"completedTurns")[0]=8;Call(runtime,"RefreshVisualBoard");
        Check(fx.ActiveBoardProps==16&&Field<int>(runtime,"collapsedFiles")==0,"Exact two edge files warned before collapse");
        Field<int[]>(runtime,"completedTurns")[0]=10;Call(runtime,"CheckCollapse");Call(runtime,"RefreshVisualBoard");
        Check(fx.ActiveBoardProps==16&&fx.ActiveCount==1&&Field<int>(runtime,"collapsedFiles")==1,"Actual first collapse publishes sixteen falling cells and barriers");
        Set(runtime,"combinedPlies",5);Call(runtime,"CheckCollapse");Call(runtime,"RefreshVisualBoard");Check(fx.ActiveBoardProps==32&&fx.ActiveCount==2,"Second actual collapse extends barriers exactly one file per side");
        runtime.EndMatch();Check(fx.ActiveBoardProps==0&&fx.ActiveStatuses==0,"Local restart clears hazards and statuses");UnityEngine.Object.DestroyImmediate(game.gameObject);
        report.Add("PASS mobility/environment local hooks: teleport owner cooldown, three-ally traversal, decoy identity/cleanup, mine lifecycle, plague plies and both collapse stages");
    }
}
