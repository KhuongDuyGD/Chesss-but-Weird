using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChessButWeird.Domain;
using ChessButWeird.Application;

public enum BotMoveQuality { Ordinary, Great, Brilliant, Blunder }

/// <summary>Conservative local reactions, not an Elo system or Chess.com's proprietary grading.</summary>
public static class BotMoveAssessment
{
    private static readonly StockfishDifficultyProfile ReviewProfile=new StockfishDifficultyProfile(
        StockfishDifficulty.Medium,"Move review",string.Empty,8,8,0,1,0,0);
    private static readonly StockfishDifficultyProfile ReplyProfile=new StockfishDifficultyProfile(
        StockfishDifficulty.Medium,"Move reply",string.Empty,8,1,0,1,0,0);

    public static Task<BotMoveQuality> AssessAsync(StockfishUciClient engine,string beforeFen,string afterFen,Move played,CancellationToken cancellation)
        => AssessAsync(engine,StockfishPosition.FromFen(beforeFen),StockfishPosition.FromFen(afterFen),played,cancellation);

    public static async Task<BotMoveQuality> AssessAsync(StockfishUciClient engine,StockfishPosition beforePosition,StockfishPosition afterPosition,Move played,CancellationToken cancellation)
    {
        string beforeFen=beforePosition.Fen,afterFen=afterPosition.Fen;
        var before=FenCodec.Parse(beforeFen);
        if(!ClassicRules.TryApply(before,played,out var applied)||FenCodec.Write(applied.State)!=afterFen)return BotMoveQuality.Ordinary;
        var choices=await engine.FindCandidatesAsync(beforePosition,ReviewProfile,cancellation);
        var replies=await engine.FindCandidatesAsync(afterPosition,ReplyProfile,cancellation);
        if(choices.Count<2||replies.Count==0)return BotMoveQuality.Ordinary;
        string uci=played.From.ToString()+played.To.ToString();
        if(played.Promotion.HasValue)uci+="kqrbnp"[(int)played.Promotion.Value];
        // Prefer the played root's score at the same depth; otherwise reverse the opponent's best reply score.
        var candidate=choices.FirstOrDefault(c=>c.Move==uci);
        int playedScore=candidate!=null?candidate.ScoreCentipawns:-replies[0].ScoreCentipawns;
        bool sacrifice=IsMaterialSacrifice(before,applied.State,played);
        return Classify(choices,uci,playedScore,sacrifice,ClassicRules.LegalMoves(before).Count);
    }

    public static BotMoveQuality Classify(IReadOnlyList<StockfishCandidate> choices,string played,int playedScore,bool sacrifice,int legalMoveCount)
    {
        if(choices==null||choices.Count<2||legalMoveCount<2)return BotMoveQuality.Ordinary;
        var ranked=choices.Where(c=>c!=null&&c.Depth>=7).GroupBy(c=>c.Move)
            .Select(group=>group.OrderByDescending(c=>c.Depth).First()).OrderByDescending(c=>c.ScoreCentipawns).ToArray();
        if(ranked.Length<2)return BotMoveQuality.Ordinary;
        int best=ranked[0].ScoreCentipawns;
        // Different mate distances with the same outcome are not centipawn mistakes.
        int loss=Math.Abs(best)>=90000&&Math.Abs(playedScore)>=90000&&Math.Sign(best)==Math.Sign(playedScore)?0:
            Math.Max(0,best-playedScore);
        if(loss>=250)return BotMoveQuality.Blunder;
        bool bestMove=ranked[0].Move==played&&loss<=35;
        if(bestMove&&sacrifice&&best>=-50)return BotMoveQuality.Brilliant;
        if(bestMove&&best-ranked[1].ScoreCentipawns>=120)return BotMoveQuality.Great;
        return BotMoveQuality.Ordinary;
    }

    public static bool IsMaterialSacrifice(MatchState before,MatchState after,Move played)
    {
        var offered=before.Board.GetPiece(played.From);
        if(offered.Kind==PieceKind.Pawn||offered.Kind==PieceKind.King||played.Promotion.HasValue)return false;
        int gained=Value(before.Board.GetPiece(played.To).Kind,before.Board.GetPiece(played.To).IsEmpty);
        foreach(var reply in ClassicRules.LegalMoves(after))
        {
            if(reply.To!=played.To)continue;
            if(!ClassicRules.TryApply(after,reply,out var capture))continue;
            int recoverable=ClassicRules.LegalMoves(capture.State).Any(move=>move.To==played.To)
                ?Value(after.Board.GetPiece(reply.From).Kind,false):0;
            if(Value(offered.Kind,false)-gained-recoverable>=2)return true;
        }
        return false;
    }
    private static int Value(PieceKind kind,bool empty)=>empty?0:kind==PieceKind.Queen?9:kind==PieceKind.Rook?5:
        kind==PieceKind.Bishop||kind==PieceKind.Knight?3:kind==PieceKind.Pawn?1:0;
}
