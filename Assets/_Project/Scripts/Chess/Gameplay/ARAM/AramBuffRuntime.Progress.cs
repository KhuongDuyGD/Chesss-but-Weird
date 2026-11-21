using System.Collections.Generic;
using ChessButWeird.Domain;
using UnityEngine;

public sealed partial class AramBuffRuntime
{
    public string BuffHudStatus(PieceTeam team,AramBuffId id)
    {
        if(!HasBuff(team,id)||!game)return "";
        int side=Side(team);var state=GetState(team);
        switch(id)
        {
            case AramBuffId.FlyingThunderGod:
                if(!Live(state.OriginalQueen)||state.GetQueenTeleportUses()>=5)return "Expired";
                return state.GetQueenTeleportCooldown()>0?"Cooldown: "+state.GetQueenTeleportCooldown()+" owner turns":"Ready · "+(5-state.GetQueenTeleportUses())+" uses";
            case AramBuffId.Doppelganger:return doppelDisabled[side]?"Expired":HudCooldown(swapReady[side]-combinedPlies,"moves");
            case AramBuffId.HidingKing:return HudCooldown(hidingReady[side]-combinedPlies,"moves");
            case AramBuffId.OneManArmy:return HudCooldown(rifleReady[side]-completedTurns[side],"owner turns");
            case AramBuffId.PeaceTShirt:return peaceFailed[side]||peaceUsed[side]?"Expired":HudCooldown(AramBalanceRules.PeaceGoal-combinedPlies,"moves");
            case AramBuffId.SacUrQueen:return queenRefundUsed[side]?"Expired":HudCooldown(10-completedTurns[side],"owner turns");
            case AramBuffId.SuicideBomber:return state.SuicideBomberUsed||!Live(state.OriginalQueen)?"Expired":"Armed";
            case AramBuffId.SubstituteNinjutsu:return substituteUsed[side]?"Expired":"Ready";
            case AramBuffId.GachaBanner:return tickets[side]+" tickets · "+(rollsThisTurn[side]>=1?"Next turn":"Ready");
            case AramBuffId.GamblingLeadsToMisery:return extraUses[side]>=3?"Expired":(3-extraUses[side])+" uses left";
            case AramBuffId.IFrameRoll:return escapeUses[side]>=3?"Expired":(3-escapeUses[side])+" escapes left";
            default:return BuffProgress(team,id);
        }
    }
    private static string HudCooldown(int remaining,string unit)=>remaining>0?"Cooldown: "+remaining+" "+unit:"Ready";
    /// <summary>Caller must apply the match's buff-visibility policy before exposing this text.</summary>
    public string BuffProgress(PieceTeam team,AramBuffId id)
    {
        if(!HasBuff(team,id)||!game)return "";
        var state=GetState(team);int side=Side(team);
        if(id==AramBuffId.FlyingThunderGod)
            return !Live(state.OriginalQueen)?"Original Queen lost.":$"Teleports: {state.GetQueenTeleportUses()}/5 used · Recharge: {state.GetQueenTeleportCooldown()} owner turns";
        // V1 sends only its authoritative six-buff state. Never display local
        // extended counters as if the server had implemented the new rules.
        switch(id)
        {
            case AramBuffId.CommandantPawn:return $"Selected Pawns alive: {CountLive(state.CommandantPawns,team)}/3";
            case AramBuffId.FreestyleLeap:return Live(state.FreestyleKnight)?"Your selected Knight is alive.":"Your selected Knight was lost.";
            case AramBuffId.NobleSacrifice:return $"Bloodthirsty Pawns alive: {CountLive(bloodthirsty,team)}";
            case AramBuffId.AbsoluteSniper:return $"Charged Bishops: {CountLive(sniperUntil.Keys,team)} · {NextExpiry(sniperUntil,team)}";
            case AramBuffId.GamblingLeadsToMisery:return $"Extra moves used: {extraUses[side]}/3";
            case AramBuffId.LootBox:return combinedPlies>=50?"Both scheduled crates have dropped.":$"Next drop in {(combinedPlies<25?25:50)-combinedPlies} combined moves";
            case AramBuffId.StrongFortress:return $"Kingside: {((castledWings[side]&1)==0?"available":"used")} · Queenside: {((castledWings[side]&2)==0?"available":"used")}";
            case AramBuffId.SuicideBomber:return state.SuicideBomberUsed?"Explosion used.":Live(state.OriginalQueen)?"Original Queen's explosion is armed.":"Original Queen lost.";
            case AramBuffId.Doppelganger:return doppelDisabled[side]?"Exchange ended; normal movement restored.":$"Movement: {(swapActive[side]?"exchanged":"normal")} · {CombinedCooldown(swapReady[side]-combinedPlies)}";
            case AramBuffId.PeaceTShirt:return peaceFailed[side]?"Condition failed: your King was checked.":peaceUsed[side]?"Recruitment used.":combinedPlies>=14?"Recruitment ready.":$"Survive {14-combinedPlies} more combined moves without check";
            case AramBuffId.RiseOfPawn:return $"Your active mines: {CountOwnedMines(team)}";
            case AramBuffId.MobileFortress:return $"Loaded carriers: {CountLive(passengers.Keys,team)}";
            case AramBuffId.HidingKing:return CombinedCooldown(hidingReady[side]-combinedPlies);
            case AramBuffId.GachaBanner:return $"Tickets: {tickets[side]} · Drop this turn: {rollsThisTurn[side]}/1";
            case AramBuffId.SubstituteNinjutsu:return substituteUsed[side]?"Substitute used.":"Substitute ready for your first check.";
            case AramBuffId.HighTechEra:return $"Cannons active: {CountLive(cannonTurns.Keys,team)}/2 · {NextExpiry(cannonTurns,team)}";
            case AramBuffId.QueensBetrayal:
                foreach(var queen in unstableQueens)if(Live(queen)&&queen.Team==team)
                    return $"Next betrayal chance: {Mathf.RoundToInt(100*AramBalanceRules.BetrayalChance(betrayalChecks.TryGetValue(queen,out var n)?n:0))}%";
                return "The unstable Queen is no longer in your army.";
            case AramBuffId.DefinitionOfAram:return $"Board: {8-2*collapsedFiles} × 8 · "+(collapsedFiles==0?$"First collapse after {Mathf.Max(0,10-completedTurns[side])} owner turns":collapsedFiles==1?$"Next collapse in {Mathf.Max(0,collapseSecondAt-combinedPlies)} combined moves":"Final size reached.");
            case AramBuffId.IFrameRoll:return $"Escapes used: {escapeUses[side]}/3";
            case AramBuffId.PlagueTown:return $"Infected enemies alive: {CountLive(infectedUntil.Keys,team==PieceTeam.White?PieceTeam.Black:PieceTeam.White)}";
            case AramBuffId.CustomizeArmy:return customizing&&formationTeam==team?$"Formation time left: {Mathf.CeilToInt(Mathf.Max(0,formationDeadline-Time.unscaledTime))}s":"Formation prepared.";
            case AramBuffId.PawnsRevolution:return $"Pawns alive: {CountPieces(team,PieceType.Pawn)}";
            case AramBuffId.OneManArmy:return "Rifle: "+CooldownLabel(rifleReady[side]-completedTurns[side]);
            case AramBuffId.EveryManForHimself:
                var rows=new List<string>();foreach(var type in new[]{PieceType.Pawn,PieceType.Knight,PieceType.Bishop,PieceType.Rook,PieceType.Queen})
                    rows.Add(type+": "+(questCaptures[side].Contains(type)?"done":"pending"));
                return string.Join(" · ",rows)+$"\nPawn promotion: {(questPromoted[side]?"done":"pending")} · Reward: {(questRewarded[side]?"earned":"pending")}";
            case AramBuffId.SacUrQueen:return queenRefundUsed[side]?"Queen refund used.":CooldownLabel(10-completedTurns[side]);
            case AramBuffId.UltimateQuest:return $"Queen mastery pieces alive: {CountLive(queenMastery,team)}";
            case AramBuffId.CreditCard:return $"Balance: {credit[side]} points · Purchase this turn: {(bought[side]?"used":"available")}";
            case AramBuffId.KingIsPoorPiece:return $"Checkmate deadline: {Mathf.Max(0,25-completedTurns[side])} owner turns remaining";
            case AramBuffId.DoubleEdgedTrap:return $"Trapped enemy Pawns alive: {CountLive(trapPawns,team==PieceTeam.White?PieceTeam.Black:PieceTeam.White)}";
            default:return "";
        }
    }
    private int CountLive(IEnumerable<ChessPiece> source,PieceTeam team)
    {int count=0;foreach(var piece in source)if(Live(piece)&&piece.Team==team)count++;return count;}
    private int CountOwnedMines(PieceTeam team){int count=0;foreach(var owner in mines.Values)if(owner==team)count++;return count;}
    private string NextExpiry(Dictionary<ChessPiece,int> source,PieceTeam team)
    {
        int earliest=int.MaxValue;foreach(var pair in source)if(Live(pair.Key)&&pair.Key.Team==team)earliest=Mathf.Min(earliest,pair.Value-combinedPlies);
        return earliest==int.MaxValue?"No active charge":"Next expiry in "+Mathf.Max(0,earliest)+" combined moves";
    }
}
