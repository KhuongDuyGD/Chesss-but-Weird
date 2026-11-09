using ChessButWeird.BuffVfx;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal sealed partial class OnlineAramPresenter
{
    private BuffVfxOrchestrator buffVfx;
    private BuffVfxOnlineAdapter visualAdapter;
    private GameObject rifleViewModel;
    private bool visualUnavailable;
    private float nextPreview;
    private readonly List<Vector3> legalPreview=new List<Vector3>(64);
    private readonly List<BuffPreviewPiece> ghostPreview=new List<BuffPreviewPiece>(32);
    private void RefreshInteractionVisuals()
    {
        if(!buffVfx||!game.Board)return;
        if(!Visible||!owner.CanSendOnlineCommand||aiming){buffVfx.ClearPreview();return;}
        if(Time.unscaledTime<nextPreview)return;nextPreview=Time.unscaledTime+.1f;
        legalPreview.Clear();ghostPreview.Clear();
        if(action=="ConfirmFormation")
        {
            foreach(var pair in formation){var p=game.OnlinePieceView(pair.Key);if(p)ghostPreview.Add(new BuffPreviewPiece(p.transform,game.Board.GetTileCenterWorld(ChessGame.ParseOnlineSquare(pair.Value)),p.Team));}
            buffVfx.SyncFormation(ghostPreview);
            if(formationPiece.HasValue)for(int x=0;x<8;x++)for(int y=0;y<8;y++){var sq=new Vector2Int(x,y);if(FormationCell(sq))legalPreview.Add(game.Board.GetTileCenterWorld(sq));}
            Vector3? hover=null;if(game.AramReadPointer(out var square))hover=game.Board.GetTileCenterWorld(square);
            var from=formationPiece.HasValue?game.Board.GetTileCenterWorld(ChessGame.ParseOnlineSquare(formation[formationPiece.Value])):Vector3.zero;
            buffVfx.SyncPreview(23,from,legalPreview,hover,hover.HasValue&&legalPreview.Contains(hover.Value));return;
        }
        buffVfx.SyncFormation(ghostPreview);
        var selected=game.AramVisualSelected;
        if(!selected||state.turn!=team||!string.IsNullOrEmpty(action)||selected.Team.ToString()!=team||(string)state.chaos?["phase"]!="Playing"){buffVfx.ClearPreview();return;}
        var data=state.board.FirstOrDefault(p=>game.OnlinePieceView(p.id)==selected);if(data==null){buffVfx.ClearPreview();return;}
        int buff=(int?)side?["buffId"]??-1;
        bool eligible=buff==0&&side?["commandantPawns"]?.Any(v=>(int)v==data.id)==true||buff==2&&(int?)side?["freestyleKnight"]==data.id||buff==1&&selected.Type==PieceType.King;
        if(!eligible){buffVfx.ClearPreview();return;}
        var origin=ChessGame.ParseOnlineSquare(data.square);
        foreach(var move in state.chaos?["legalMoves"]??new JArray())if((string)move["from"]==data.square)
        {var destination=ChessGame.ParseOnlineSquare((string)move["to"]);if(buff!=1||destination.y==origin.y&&(destination.x==2||destination.x==6)&&Mathf.Abs(destination.x-origin.x)>=2)legalPreview.Add(game.Board.GetTileCenterWorld(destination));}
        Vector3? hovered=null;if(game.AramReadPointer(out var cell))hovered=game.Board.GetTileCenterWorld(cell);bool valid=hovered.HasValue&&legalPreview.Contains(hovered.Value);
        buffVfx.SyncPreview(buff,game.Board.GetTileCenterWorld(origin),legalPreview,valid?hovered:null,valid);
    }
    private bool FormationCell(Vector2Int sq)
    {
        if(sq.x<0||sq.x>=8||sq.y<0||sq.y>=8)return false;
        int collapsed=(int?)state.chaos?["collapsedFiles"]??0;int home=(int?)side?["homeRank"]??(team=="White"?0:7);
        string value=((char)('a'+sq.x)).ToString()+(sq.y+1);
        return sq.x>=collapsed&&sq.x<8-collapsed&&(home==0?sq.y<4:sq.y>=4)&&!state.board.Any(p=>p.square==value&&p.team!=team);
    }
    private void PresentVisuals()
    {
        if(!game.Board||visualUnavailable)return;
        if(!buffVfx)
        {
            var host=new GameObject("Online CHAOS buff VFX");host.transform.SetParent(uiHost.transform,false);
            buffVfx=host.AddComponent<BuffVfxOrchestrator>();
            buffVfx.Initialize(Vector3.Distance(game.Board.GetTileCenterWorld(Vector2Int.zero),game.Board.GetTileCenterWorld(Vector2Int.right)));
            if(!buffVfx.Ready){visualUnavailable=true;return;}
            visualAdapter=new BuffVfxOnlineAdapter(buffVfx,game.Board.GetTileCenterWorld,id=>{var p=game.OnlinePieceView(id);return p?p.transform:null;});
        }
        buffVfx.Paused=game.PauseLocked;
        visualAdapter.Apply(state.matchId,state.stateVersion,state.chaos,!state.IsActive,state.result?.reason);
    }
}
