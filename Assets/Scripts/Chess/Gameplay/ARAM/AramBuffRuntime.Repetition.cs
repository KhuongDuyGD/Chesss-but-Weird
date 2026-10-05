using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed partial class AramBuffRuntime
{
    /// <summary>Canonical rules state for threefold repetition, separate from wire FEN.
    /// Mature cooldowns clamp to zero; an irrelevant ever-increasing turn counter
    /// must not prevent repetition for permanent passive buffs.</summary>
    internal string RepetitionState
    {
        get
        {
            var parts=new List<string>();
            foreach(var team in new[]{PieceTeam.White,PieceTeam.Black})
            {
                int side=Side(team);var state=GetState(team);string prefix=side+":";
                foreach(var buff in state.Buffs)
                {
                    if(!buff)continue;
                    string value="";
                    switch(buff.Id)
                    {
                        case AramBuffId.CommandantPawn:value=PiecesKey(state.CommandantPawns);break;
                        case AramBuffId.FreestyleLeap:value=PieceKey(state.FreestyleKnight);break;
                        case AramBuffId.StrongFortress:
                            value=castledWings[side].ToString();foreach(var p in game.GetActivePiecesForAram(team))if(p.Type==PieceType.Rook)value+="/"+PieceKey(p)+":"+p.HasMoved;break;
                        case AramBuffId.Doppelganger:value=$"{PieceKey(state.SwappedKnight)}/{PieceKey(state.SwappedBishop)}/{swapActive[side]}/{doppelDisabled[side]}/{Remaining(swapReady[side],combinedPlies)}";break;
                        case AramBuffId.SuicideBomber:value=PieceKey(state.OriginalQueen)+":"+state.SuicideBomberUsed;break;
                        case AramBuffId.FlyingThunderGod:value=$"{PieceKey(state.OriginalQueen)}/{state.GetQueenTeleportUsesForSync()}/{state.GetQueenTeleportCooldownForSync()}";break;
                        case AramBuffId.GamblingLeadsToMisery:value=extraUses[side].ToString();break;
                        case AramBuffId.LootBox:value=Remaining(50,combinedPlies).ToString();break;
                        case AramBuffId.PeaceTShirt:value=$"{peaceFailed[side]}/{peaceUsed[side]}/{Remaining(14,combinedPlies)}";break;
                        case AramBuffId.HidingKing:value=Remaining(hidingReady[side],combinedPlies).ToString();break;
                        case AramBuffId.GachaBanner:value=tickets[side].ToString();break;
                        case AramBuffId.SubstituteNinjutsu:value=substituteUsed[side].ToString();break;
                        case AramBuffId.QueensBetrayal:
                            foreach(var p in unstableQueens)if(Live(p)&&p.Team==team)value=PieceKey(p)+":"+(betrayalChecks.TryGetValue(p,out var count)?count:0);break;
                        case AramBuffId.DefinitionOfAram:value=$"{Remaining(10,completedTurns[side])}/{collapsedFiles}/{(collapsedFiles==1?Remaining(collapseSecondAt,combinedPlies):0)}";break;
                        case AramBuffId.IFrameRoll:value=escapeUses[side].ToString();break;
                        case AramBuffId.OneManArmy:value=Remaining(rifleReady[side],completedTurns[side]).ToString();break;
                        case AramBuffId.EveryManForHimself:
                            int mask=0;foreach(var kind in questCaptures[side])mask|=1<<(int)kind;value=$"{mask}/{questPromoted[side]}/{questRewarded[side]}";break;
                        case AramBuffId.SacUrQueen:value=$"{queenRefundUsed[side]}/{Remaining(10,completedTurns[side])}";break;
                        case AramBuffId.CreditCard:value=credit[side].ToString();break;
                        case AramBuffId.KingIsPoorPiece:value=Remaining(25,completedTurns[side]).ToString();break;
                    }
                    parts.Add(prefix+buff.Id+":"+value);
                }
            }
            parts.Add("blood:"+PiecesKey(bloodthirsty));parts.Add("mastery:"+PiecesKey(queenMastery));parts.Add("trap:"+PiecesKey(trapPawns));
            parts.Add("promoted-queens:"+PiecesKey(promotedQueens));parts.Add("decoys:"+PiecesKey(decoys));parts.Add("forced:"+PieceKey(forcedPawn));
            AddExpiry(parts,"sniper",sniperUntil);AddExpiry(parts,"cannon",cannonTurns);AddExpiry(parts,"plague",infectedUntil);
            foreach(var pair in mines)parts.Add("mine:"+SquareKey(pair.Key)+":"+pair.Value);
            foreach(var pair in crates)parts.Add("crate:"+SquareKey(pair.Key)+":"+pair.Value.Team+":"+pair.Value.Kind);
            foreach(var pair in passengers)if(Live(pair.Key))
            {
                var traits=passengerTraits[pair.Key];parts.Add($"cargo:{PieceKey(pair.Key)}:{pair.Value}:{Remaining(passengerReady[pair.Key],completedTurns[Side(pair.Value)])}:{traits.Bloodthirsty}:{traits.Commandant}:{traits.Mastery}:{traits.Trapped}");
            }
            parts.Add("home:"+homeRanks[0]+":"+homeRanks[1]);parts.Sort(System.StringComparer.Ordinal);return string.Join(";",parts);
        }
    }
    private static int Remaining(int ready,int clock)=>Mathf.Max(0,ready-clock);
    private static string SquareKey(Vector2Int p)=>p.x+","+p.y;
    private string PieceKey(ChessPiece p)=>Live(p)?SquareKey(p.BoardPosition):"-";
    private string PiecesKey(IEnumerable<ChessPiece> pieces)
    {var values=new List<string>();foreach(var p in pieces)if(Live(p))values.Add(SquareKey(p.BoardPosition));values.Sort(System.StringComparer.Ordinal);return string.Join("/",values);}
    private void AddExpiry(List<string> parts,string name,Dictionary<ChessPiece,int> values)
    {foreach(var pair in values)if(Live(pair.Key))parts.Add(name+":"+PieceKey(pair.Key)+":"+Remaining(pair.Value,combinedPlies));}
}
