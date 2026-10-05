using System;
using System.Collections.Generic;
using ChessButWeird.Domain;
using UnityEngine;

public sealed partial class AramBuffRuntime
{
    private int combinedPlies, collapseSecondAt=-1;
    private readonly int[] homeRanks={0,7}, credit=new int[2];
    private readonly bool[] bought=new bool[2], queenRefundUsed=new bool[2], questPromoted=new bool[2], questRewarded=new bool[2], doppelDisabled=new bool[2];
    private readonly int[] castledWings=new int[2];
    private readonly HashSet<PieceType>[] questCaptures={new HashSet<PieceType>(),new HashSet<PieceType>()};
    private readonly HashSet<ChessPiece> queenMastery=new HashSet<ChessPiece>(), trapPawns=new HashSet<ChessPiece>(), promotedQueens=new HashSet<ChessPiece>();
    private readonly Dictionary<ChessPiece,int> passengerReady=new Dictionary<ChessPiece,int>(), betrayalChecks=new Dictionary<ChessPiece,int>();
    private readonly Dictionary<ChessPiece,PassengerTraits> passengerTraits=new Dictionary<ChessPiece,PassengerTraits>();
    internal int ForwardFor(PieceTeam team) => Home(team)==0?1:-1;
    internal bool IsKingless(PieceTeam team) => active&&!networkMatch&&HasBuff(team,AramBuffId.KingIsPoorPiece);
    internal bool FortressWingAvailable(PieceTeam team,int wing) => (castledWings[Side(team)]&(wing==6?1:2))==0;
    private void ResetDocumentState()
    {
        combinedPlies=0;collapseSecondAt=-1;homeRanks[0]=0;homeRanks[1]=7;
        Array.Clear(credit,0,2);Array.Clear(bought,0,2);Array.Clear(queenRefundUsed,0,2);
        Array.Clear(questPromoted,0,2);Array.Clear(questRewarded,0,2);Array.Clear(doppelDisabled,0,2);Array.Clear(castledWings,0,2);
        questCaptures[0].Clear();questCaptures[1].Clear();queenMastery.Clear();trapPawns.Clear();promotedQueens.Clear();
        passengerReady.Clear();passengerTraits.Clear();betrayalChecks.Clear();
    }
    private void ApplyDocumentInitialEffects(PieceTeam team)
    {
        if(HasBuff(team,AramBuffId.AutoCastling))
        {
            var king=game.AramKing(team);var rook=At(new Vector2Int(7,Home(team)));
            if(king&&rook&&rook.Team==team&&rook.Type==PieceType.Rook)
            {
                game.AramRelocate(king,new Vector2Int(6,Home(team)),false,true);
                game.AramRelocate(rook,new Vector2Int(5,Home(team)),false,true);
                castledWings[Side(team)]|=1;
            }
        }
        if(IsKingless(team))
        {
            var king=game.AramKing(team);if(king)game.AramReplace(king,PieceType.Queen,team);
        }
        if(HasBuff(team,AramBuffId.SacUrQueen))decisions.Enqueue(()=>SelectQueenReplacement(team));
        if(HasBuff(team,AramBuffId.HidingKing))decisions.Enqueue(()=>HideKing(team,true));
    }
    private void SelectQueenReplacement(PieceTeam team)
    {
        var queen=GetState(team).OriginalQueen;if(!Live(queen))return;
        Choose($"{team}: choose a Rook, Bishop or Knight on the board as the type replacing your Queen.",p=>
        {
            var sample=At(p);if(!sample||sample.Team!=team||(sample.Type!=PieceType.Rook&&sample.Type!=PieceType.Bishop&&sample.Type!=PieceType.Knight))return false;
            var replacement=game.AramReplace(queen,sample.Type,team);GetState(team).OriginalQueen=null;
            return replacement;
        },false);
    }
    private void CaptureReward(PieceTeam team,ChessPiece captured)
    {
        if(!captured||captured.Team==team)return;
        if(HasBuff(team,AramBuffId.GachaBanner)&&captured.Type!=PieceType.Pawn)tickets[Side(team)]++;
        if(HasBuff(team,AramBuffId.CreditCard))credit[Side(team)]+=AramBalanceRules.CapturePoints((PieceKind)captured.Type);
    }
    private void UpdateDoppelganger(PieceTeam team)
    {
        if(!HasBuff(team,AramBuffId.Doppelganger)||doppelDisabled[Side(team)])return;
        if(CountPieces(team,PieceType.Knight)>0&&CountPieces(team,PieceType.Bishop)>0)return;
        doppelDisabled[Side(team)]=true;GetState(team).SwappedKnight=null;GetState(team).SwappedBishop=null;
        swapActive[Side(team)]=false;game.ClearSelection();Say("Doppelganger ended: a required piece type is gone.");
    }
    private void AdvanceCombinedPly()
    {
        combinedPlies++;
        foreach(var p in new List<ChessPiece>(sniperUntil.Keys))
            if(!Live(p)||combinedPlies>=sniperUntil[p]){sniperUntil.Remove(p);RemoveMarker(p);}
        foreach(var p in new List<ChessPiece>(cannonTurns.Keys))
            if(!Live(p)||combinedPlies>=cannonTurns[p])cannonTurns.Remove(p);
        foreach(var p in new List<ChessPiece>(infectedUntil.Keys))
            if(!Live(p))infectedUntil.Remove(p);
            else if(combinedPlies>=infectedUntil[p]){infectedUntil.Remove(p);game.AramRemove(p);Say("An infected piece died from Plague.");}
        foreach(var team in new[]{PieceTeam.White,PieceTeam.Black})
        {
            if(combinedPlies<=AramBalanceRules.PeaceGoal&&game.AramInCheck(team))peaceFailed[Side(team)]=true;
            UpdateDoppelganger(team);
            TryQueueQuestReward(team);
        }
        foreach(var queen in new List<ChessPiece>(unstableQueens))
        {
            if(!Live(queen)){unstableQueens.Remove(queen);betrayalChecks.Remove(queen);continue;}
            int checks=betrayalChecks.TryGetValue(queen,out var n)?n:0;
            if(UnityEngine.Random.value<AramBalanceRules.BetrayalChance(checks))
            {
                var receiver=queen.Team==PieceTeam.White?PieceTeam.Black:PieceTeam.White;
                unstableQueens.Remove(queen);betrayalChecks.Remove(queen);game.AramRemove(queen);
                QueueDeployment(receiver,PieceType.Queen,null,3);Say("An unstable Queen betrayed her army.");
            }
            else betrayalChecks[queen]=checks+1;
        }
        foreach(int milestone in new[]{25,50})
            if(combinedPlies>=milestone&&lootRounds.Add(milestone))
                foreach(var team in new[]{PieceTeam.White,PieceTeam.Black})if(HasBuff(team,AramBuffId.LootBox))DropCrate(team,milestone);
        CheckCollapse();game.AramFinishIfKingMissing();
    }
    internal void ObserveCheckConditions()
    {
        if(!active||networkMatch||combinedPlies>=AramBalanceRules.PeaceGoal)return;
        foreach(var side in new[]{PieceTeam.White,PieceTeam.Black})if(game.AramInCheck(side))peaceFailed[Side(side)]=true;
    }
    private void DropCrate(PieceTeam team,int milestone)
    {
        var eligible=new List<Vector2Int>();
        for(int x=collapsedFiles;x<8-collapsedFiles;x++)for(int y=0;y<8;y++)
        {
            var square=new Vector2Int(x,y);var occupant=At(square);
            if(!crates.ContainsKey(square)&&(milestone==50||!occupant||occupant.Type!=PieceType.King))eligible.Add(square);
        }
        if(eligible.Count==0)return;
        var selected=Pick(eligible);var victim=At(selected);
        if(victim){game.AramAbilityCapture(team,victim,$"Loot crate captured {victim.Type} on {selected}");CaptureReward(team,victim);game.AramRemove(victim);}
        crates[selected]=new SupplyCrate(team,milestone==25?PieceType.Rook:PieceType.Queen);RefreshBoardMarkers();
    }
    private void CheckCollapse()
    {
        if(collapsedFiles==0)
            foreach(var team in new[]{PieceTeam.White,PieceTeam.Black})
                if(HasBuff(team,AramBuffId.DefinitionOfAram)&&completedTurns[Side(team)]>=10)
                {collapsedFiles=1;collapseSecondAt=combinedPlies+5;CollapsePieces();break;}
        if(collapsedFiles==1&&combinedPlies>=collapseSecondAt){collapsedFiles=2;CollapsePieces();}
    }
    private void CollapsePieces()
    {
        foreach(var team in new[]{PieceTeam.White,PieceTeam.Black})
            foreach(var p in game.GetActivePiecesForAram(team))if(!SquareAvailable(p.BoardPosition))game.AramRemove(p);
        foreach(var p in new List<Vector2Int>(crates.Keys))if(!SquareAvailable(p))crates.Remove(p);
        foreach(var p in new List<Vector2Int>(mines.Keys))if(!SquareAvailable(p))mines.Remove(p);
        RefreshBoardMarkers();Say("The outer files collapsed.");
    }
    internal bool TryFinishTurnDeadline()
    {
        foreach(var team in new[]{PieceTeam.White,PieceTeam.Black})
            if(IsKingless(team)&&completedTurns[Side(team)]>=AramBalanceRules.PoorKingDeadline)
            {game.AramWin(team==PieceTeam.White?PieceTeam.Black:PieceTeam.White);return true;}
        return false;
    }
    internal bool DrawIsLoss(PieceTeam team) => !networkMatch&&(HasBuff(team,AramBuffId.EveryManForHimself)||IsKingless(team));
    internal bool BlocksPromotion(ChessPiece pawn) => !networkMatch&&pawn&&pawn.Type==PieceType.Pawn&&
        HasBuff(pawn.Team==PieceTeam.White?PieceTeam.Black:PieceTeam.White,AramBuffId.DoubleEdgedTrap);
    internal void RegisterSpawn(ChessPiece piece)
    {if(BlocksPromotion(piece)&&piece.BoardPosition.y==(Home(piece.Team)==0?7:0))trapPawns.Add(piece);}
    internal void OtherQueenRemoved(ChessPiece piece,Vector2Int square)
    {if(!networkMatch&&WouldQueenExplode(piece))DetonateRemovedQueen(piece,square,null);}
    internal void PawnPromoted(ChessPiece oldPawn,ChessPiece promoted)
    {
        if(networkMatch||!promoted)return;
        questPromoted[Side(promoted.Team)]=true;if(promoted.Type==PieceType.Queen)promotedQueens.Add(promoted);
        if(queenMastery.Remove(oldPawn))queenMastery.Add(promoted);
        TransferInfection(oldPawn,promoted);
        TryQueueQuestReward(promoted.Team);
        if(HasBuff(promoted.Team,AramBuffId.Paratrooper))
            decisions.Enqueue(()=>
            {
                if(!Live(promoted))return;
                var allowed=EmptySquares();allowed.Add(promoted.BoardPosition);
                allowed.RemoveAll(p=>p!=promoted.BoardPosition&&!game.AramSafeDestination(promoted,p));
                Choose($"{promoted.Team}: choose a highlighted square for your promoted {promoted.Type}.",p=>
                {if(!allowed.Contains(p))return false;return p==promoted.BoardPosition||game.AramRelocate(promoted,p);},false);
                game.AramHighlight(allowed);
            });
    }
    private void TransferInfection(ChessPiece original,ChessPiece replacement)
    {if(original&&replacement&&infectedUntil.TryGetValue(original,out var expiry)){infectedUntil.Remove(original);infectedUntil[replacement]=expiry;}}
    private bool QuestComplete(PieceTeam team) => questPromoted[Side(team)]&&questCaptures[Side(team)].Count>=5;
    private void TryQueueQuestReward(PieceTeam team)
    {
        int side=Side(team);if(!HasBuff(team,AramBuffId.EveryManForHimself)||questRewarded[side]||!QuestComplete(team))return;
        questRewarded[side]=true;
        decisions.Enqueue(()=>
        {
            if(CountPieces(team,PieceType.Pawn)<2){QueueDeployment(team,PieceType.Queen,null,0);return;}
            int remaining=2;
            Func<Vector2Int,bool> select=null;select=p=>
            {
                var pawn=At(p);if(!pawn||pawn.Team!=team||pawn.Type!=PieceType.Pawn)return false;
                var replacement=game.AramReplace(pawn,PieceType.Queen,team);promotedQueens.Add(replacement);
                if(--remaining>0){Choose($"{team}: select the second Pawn to become a Queen.",select,false);return false;}
                return true;
            };
            Choose($"{team}: quest complete! Select 2 Pawns to become Queens.",select,false);
        });
    }
    private void RefundQueen(PieceTeam team)
    {
        int side=Side(team);if(queenRefundUsed[side]||completedTurns[side]<10)return;
        Choose("Choose an ally other than a King or promoted Queen to become a Queen.",p=>
        {
            var piece=At(p);if(!piece||piece.Team!=team||piece.Type==PieceType.King||promotedQueens.Contains(piece))return false;
            if(!game.AramCanPlace(PieceType.Queen,team,p,false,piece))return false;
            game.AramReplace(piece,PieceType.Queen,team);queenRefundUsed[side]=true;return true;
        });
    }
    private void BuyPiece(PieceTeam team,PieceType type)
    {
        int side=Side(team);if(!AramBalanceRules.CanPurchase(credit[side],bought[side],(PieceKind)type))return;
        Choose($"{team}: place your {type} in an empty home square. Price: {AramBalanceRules.PurchasePrice((PieceKind)type)}.",p=>
        {
            if(At(p)||!InHome(p,team)||!game.AramCanPlace(type,team,p))return false;
            if(!game.AramSpawn(type,team,p,true))return false;
            credit[side]-=AramBalanceRules.PurchasePrice((PieceKind)type);bought[side]=true;return true;
        });
    }
    private void ReselectDoppelganger(PieceTeam team)
    {
        var state=GetState(team);var oldKnight=state.SwappedKnight;var oldBishop=state.SwappedBishop;bool oldActive=swapActive[Side(team)];
        Choose("Choose the Knight for the new exchange pair.",p=>
        {
            var knight=At(p);if(!knight||knight.Team!=team||knight.Type!=PieceType.Knight)return false;
            Choose("Choose the Bishop for the new exchange pair.",q=>
            {
                var bishop=At(q);if(!Live(knight)||!bishop||bishop.Team!=team||bishop.Type!=PieceType.Bishop)return false;
                state.SwappedKnight=knight;state.SwappedBishop=bishop;swapActive[Side(team)]=true;
                if(game.AramInCheck(team)){state.SwappedKnight=oldKnight;state.SwappedBishop=oldBishop;swapActive[Side(team)]=oldActive;return false;}
                swapReady[Side(team)]=combinedPlies+4;ApplyAllMarkers();return true;
            });return true;
        });
    }
    private void UnloadPassenger(PieceTeam team)
    {
        Choose("Choose a Rook whose passenger is ready to unload.",p=>
        {
            var rook=At(p);if(!rook||rook.Team!=team||!passengers.ContainsKey(rook)||completedTurns[Side(team)]<passengerReady[rook])return false;
            Choose("Choose an empty square in the Rook's surrounding 3x3 area.",q=>
            {
                if(!Live(rook)||At(q)||!SquareAvailable(q)||Mathf.Abs(q.x-rook.BoardPosition.x)>1||Mathf.Abs(q.y-rook.BoardPosition.y)>1)return false;
                var kind=q.y==(Home(team)==0?7:0)&&!HasBuff(team==PieceTeam.White?PieceTeam.Black:PieceTeam.White,AramBuffId.DoubleEdgedTrap)?PieceType.Queen:PieceType.Pawn;
                if(!game.AramCanPlace(kind,team,q))return false;
                var traits=passengerTraits[rook];passengers.Remove(rook);passengerReady.Remove(rook);passengerTraits.Remove(rook);RemoveMarker(rook);
                RestorePassenger(team,kind,q,traits);return true;
            });return true;
        });
    }
    private void RestorePassenger(PieceTeam team,PieceType kind,Vector2Int square,PassengerTraits traits)
    {
        var piece=game.AramSpawn(kind,team,square,true);if(!piece)return;
        if(traits.Mastery)queenMastery.Add(piece);
        if(kind==PieceType.Pawn)
        {
            if(traits.Bloodthirsty)bloodthirsty.Add(piece);if(traits.Commandant)GetState(team).CommandantPawns.Add(piece);
            if(traits.Trapped||BlocksPromotion(piece)&&square.y==(Home(team)==0?7:0))trapPawns.Add(piece);
        }
        else PawnPromoted(null,piece);
    }
    private void RemoveMarker(ChessPiece piece)
    {
        if(!piece)return;var marker=piece.GetComponent<AramBuffPieceMarker>();if(marker){activeMarkers.Remove(marker);marker.enabled=false;}
    }
    private readonly struct PassengerTraits
    {
        public readonly bool Bloodthirsty,Commandant,Mastery,Trapped;
        public PassengerTraits(bool blood,bool commandant,bool mastery,bool trapped)
        {Bloodthirsty=blood;Commandant=commandant;Mastery=mastery;Trapped=trapped;}
    }
}
