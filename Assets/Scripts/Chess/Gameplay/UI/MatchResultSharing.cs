using System.Text;
using ChessButWeird.Application;
using UnityEngine;

/// <summary>A public result summary that players can paste into their preferred chat.</summary>
public static class MatchResultSharing
{
    public static bool CanShare(ChessGame game) => MatchExportPolicy.CanShare(game, game && game.GameOver);

    public static string BuildText(ChessGame game)
    {
        if (!CanShare(game)) return string.Empty;
        bool draw = game.Status == ChessGame.ChessGameStatus.Draw;
        string score = draw ? "1/2-1/2" : game.WinningTeam == PieceTeam.White ? "1-0" : "0-1";
        var text = new StringBuilder("Chess but Weird\n");
        text.AppendLine("Mode: " + game.StatisticsMode);
        text.AppendLine("White: " + SingleLine(game.StatisticsPlayerName(PieceTeam.White)));
        text.AppendLine("Black: " + SingleLine(game.StatisticsPlayerName(PieceTeam.Black)));
        text.AppendLine("Result: " + score + " - " + (draw ? "Draw" : game.WinningTeam + " wins"));
        if (!string.IsNullOrWhiteSpace(game.ResultTerminationReason))
            text.AppendLine("Reason: " + SingleLine(game.ResultTerminationReason));
        int seconds = Mathf.Max(0, Mathf.FloorToInt(game.MatchElapsedSeconds));
        text.AppendLine(game.StatisticsTimeKnown ? $"Time played: {seconds / 60:00}:{seconds % 60:00}" : "Time played: unavailable");
        if (game.ResultMoveCount.HasValue)
            text.Append("Total moves played: ").Append(game.ResultMoveCount.Value);
        return text.ToString();
    }

    private static string SingleLine(string value) => StandardChessNotation.SingleLine(value);
}
