using System;
using System.Collections.Generic;
using System.Linq;
using ChessButWeird.BuffVfx;
using UnityEngine;

public static partial class AramAuditRunner
{
    private static void TestStrategyVisualHooks()
    {
        var game=Match(AramBuffId.CustomizeArmy);var runtime=game.Runtime;var fx=(BuffVfxOrchestrator)Call(runtime,"EnsureVisuals");BeginDecision(runtime);
        var pawn=game.Board[0,1];var original=game.GetActivePiecesForAram(PieceTeam.White).ToDictionary(p=>p,p=>p.BoardPosition);int revision=game.Statistics.Revision;
        Check(!Select(runtime,0,1)&&!Select(runtime,0,2),"Formation staging keeps decision pending");
        Check(game.Board[0,1]==pawn&&!game.Board[0,2]&&pawn.BoardPosition==new Vector2Int(0,1),"Formation ghost never mutates actual board or piece coordinate");
        Check(Field<Dictionary<ChessPiece,Vector2Int>>(runtime,"formationDraft")[pawn]==new Vector2Int(0,2),"Draft contains actual proposed placement");
        Check(game.Statistics.Revision==revision&&fx.PreviewGhosts==16,"No economy/statistics mutation before commit; sixteen owned mesh ghosts");
        Select(runtime,0,2);Select(runtime,0,4);Check(Field<Dictionary<ChessPiece,Vector2Int>>(runtime,"formationDraft")[pawn]==new Vector2Int(0,2),"Invalid half rejected without changing draft");
        Call(runtime,"ResetFormationPreview");Check(original.All(p=>p.Key.BoardPosition==p.Value)&&fx.PreviewGhosts==0,"Reset removes all previews and preserves original board");
        Select(runtime,0,1);Select(runtime,0,2);Call(runtime,"FinishFormation");
        Check(game.Board[0,2]==pawn&&!game.Board[0,1]&&fx.ActiveCount==1&&fx.PreviewGhosts==0,"Only confirmed formation changes real board and plays feedback");End(game);

        game=Match(AramBuffId.CustomizeArmy);runtime=game.Runtime;fx=(BuffVfxOrchestrator)Call(runtime,"EnsureVisuals");BeginDecision(runtime);game.Add(PieceTeam.Black,PieceType.Rook,4,4);pawn=game.Board[4,1];
        Select(runtime,4,1);Select(runtime,3,2);Check(game.Board[4,1]==pawn&&!game.Board[3,2],"Unsafe draft still leaves real King shield in place");Call(runtime,"FinishFormation");
        Check(game.Board[4,1]==pawn&&!game.Board[3,2]&&!(bool)Call(game,"AramInCheck",PieceTeam.White),"Confirming an unsafe draft restores actual King-safe formation");
        Check(fx.PreviewGhosts==0&&fx.PreviewTargets==0&&fx.ActiveCount==1,"Unsafe rollback cleans ghosts and publishes only final refund feedback");End(game);

        foreach(var buff in new[]{AramBuffId.RngFiesta,AramBuffId.PawnsRevolution,AramBuffId.AutoCastling,AramBuffId.KingIsPoorPiece})
        {
            game=Match(buff);runtime=game.Runtime;fx=(BuffVfxOrchestrator)Call(runtime,"EnsureVisuals");Check(fx.ActiveCount==1,"Actual initialization emits one bounded army batch: "+buff);
            Call(runtime,"LateUpdate");if(buff==AramBuffId.KingIsPoorPiece){Check(fx.HudRows==1,"Kingless HUD restored from actual owner clock");Field<int[]>(runtime,"completedTurns")[0]=23;Call(runtime,"RefreshStrategyVisuals");Check(Field<List<BuffHudState>>(runtime,"strategyHud")[0].Current==2,"Kingless uses owner turns, not wall time");}
            End(game);
        }
        game=Match(AramBuffId.CreditCard);runtime=game.Runtime;fx=(BuffVfxOrchestrator)Call(runtime,"EnsureVisuals");
        Field<int[]>(runtime,"credit")[0]=-1;Call(runtime,"BuyPiece",PieceTeam.White,PieceType.Queen);Check(Field<Func<Vector2Int,bool>>(runtime,"targetAction")==null&&fx.ActiveCount==0,"Debt rejects purchase with no success feedback");
        Field<int[]>(runtime,"credit")[0]=0;Call(runtime,"BuyPiece",PieceTeam.White,PieceType.Bishop);Check(!Select(runtime,0,0)&&fx.ActiveCount==0,"Occupied purchase target never publishes success");
        Check(Select(runtime,0,2)&&fx.ActiveCount==1&&Field<int[]>(runtime,"credit")[0]==-4,"Actual purchase on credit deducts cost once and emits receipt");End(game);

        game=Match(AramBuffId.MobileFortress);runtime=game.Runtime;fx=(BuffVfxOrchestrator)Call(runtime,"EnsureVisuals");Call(runtime,"LoadPassenger",PieceTeam.White);Select(runtime,0,0);Select(runtime,0,1);Call(runtime,"LateUpdate");
        Check(fx.ActiveCount==1&&Field<List<BuffVisualStatus>>(runtime,"visualStatuses").Exists(s=>s.BuffId==12&&s.Remaining==1),"Actual single passenger indicator and embark");
        fx.ClearVisuals();Call(runtime,"CompleteOwnerTurn",PieceTeam.White);Call(runtime,"UnloadPassenger",PieceTeam.White);Select(runtime,0,0);Select(runtime,0,1);Check(fx.ActiveCount==1,"Only actual unload emits exit cue");End(game);

        game=Match(AramBuffId.Doppelganger);runtime=game.Runtime;fx=(BuffVfxOrchestrator)Call(runtime,"EnsureVisuals");Call(runtime,"LateUpdate");Check(Field<List<BuffVisualStatus>>(runtime,"visualStatuses").Count(s=>s.BuffId==3)==2,"Owner sees exact paired movement insignia");
        game.CurrentTurn=PieceTeam.Black;Call(runtime,"LateUpdate");Check(!Field<List<BuffVisualStatus>>(runtime,"visualStatuses").Exists(s=>s.BuffId==3)&&fx.ActiveCount==0,"Hotseat viewer switch immediately clears private cues without expiry fade");End(game);

        game=Match(AramBuffId.CommandantPawn);runtime=game.Runtime;fx=(BuffVfxOrchestrator)Call(runtime,"EnsureVisuals");Call(runtime,"LateUpdate");Check(Field<List<BuffVisualStatus>>(runtime,"visualStatuses").Count(s=>s.BuffId==0)==3,"Three confirmed Commandant selections share one batch and three distinct emblems");
        Set(game,"selectedPiece",game.Board[0,1]);Call(runtime,"RefreshInteractionVisuals");Check(fx.PreviewTargets>0,"Selected commander reads actual safe legal move service");
        var allowed=(List<Vector2Int>)Call(game,"GetSafeLegalMoves",game.Board[0,1]);Check(fx.PreviewTargets==allowed.Count,"Preview matches real legal targets exactly");
        Set(game,"selectedPiece",null);Call(runtime,"RefreshInteractionVisuals");Check(fx.PreviewTargets==0,"Deselection clears interaction preview");End(game);

        game=Match(AramBuffId.DoubleEdgedTrap);runtime=game.Runtime;fx=(BuffVfxOrchestrator)Call(runtime,"EnsureVisuals");Call(runtime,"LateUpdate");Check(Field<List<BuffVisualStatus>>(runtime,"visualStatuses").Count(s=>s.BuffId==33)==8,"Promotion locks belong to actual enemy Pawns");End(game);
        game=Match(AramBuffId.PeaceTShirt);runtime=game.Runtime;fx=(BuffVfxOrchestrator)Call(runtime,"EnsureVisuals");Set(runtime,"combinedPlies",14);Call(runtime,"LateUpdate");Check(Field<List<BuffVisualStatus>>(runtime,"visualStatuses").Exists(s=>s.BuffId==10),"Ready recruitment insignia at actual fourteen combined plies");
        Field<bool[]>(runtime,"peaceUsed")[0]=true;Set(runtime,"nextVisualRefresh",0f);Call(runtime,"LateUpdate");Check(!Field<List<BuffVisualStatus>>(runtime,"visualStatuses").Exists(s=>s.BuffId==10),"Consumed readiness retires insignia");End(game);
        report.Add("PASS strategy hooks: actual army batches, owner HUD, debt/invalid purchase, passenger lifecycle, private Doppelganger, exact legal previews and uncommitted/reset/confirmed formations.");
    }
}
