using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ChessButWeird.Domain;

namespace ChessButWeird.Application
{
    public enum MatchNotationRules { Unsupported, Standard, Chaos }
    public enum ChessExportKind { Pgn, Fen }

    /// <summary>Detached data at the export boundary. Unknown metadata stays unknown.</summary>
    public sealed class MatchExportRecord
    {
        public MatchNotationRules Rules { get; }
        public string ModeId { get; }
        public bool Completed { get; }
        public string InitialFen { get; }
        public string FinalFen { get; }
        public IReadOnlyList<Move> Moves { get; }
        public bool HistoryComplete { get; }
        public string White { get; }
        public string Black { get; }
        public string Result { get; }
        public DateTime? StartedAt { get; }

        public MatchExportRecord(MatchNotationRules rules, string modeId, bool completed,
            string initialFen, string finalFen, IEnumerable<Move> moves, bool historyComplete,
            string white, string black, string result, DateTime? startedAt = null)
        {
            Rules = rules; ModeId = modeId; Completed = completed;
            InitialFen = initialFen; FinalFen = finalFen;
            Moves = moves == null ? null : new List<Move>(moves).AsReadOnly();
            HistoryComplete = historyComplete; White = white; Black = black;
            Result = result; StartedAt = startedAt;
        }
    }

    public sealed class ChessExportResult
    {
        public bool Success { get; }
        public string Text { get; }
        public string Error { get; }
        private ChessExportResult(bool success, string text, string error)
        { Success = success; Text = text; Error = error; }
        public static ChessExportResult Ok(string text) => new ChessExportResult(true, text, null);
        public static ChessExportResult Fail(string error) => new ChessExportResult(false, null, error);
    }

    /// <summary>Single availability policy shared by the UI and every notation export call.</summary>
    public static class MatchExportPolicy
    {
        public static bool IsChaos(string mode) => string.Equals(mode, "Chaos", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "Aram", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "ChaosNormal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "ChaosRanked", StringComparison.OrdinalIgnoreCase);
        public static bool ShowNotation(MatchExportRecord record) => record != null &&
            record.Rules == MatchNotationRules.Standard && !IsChaos(record.ModeId);
        public static bool CanShare(bool sessionExists, bool completed) => sessionExists && completed;
        public static string Rejection(MatchExportRecord record)
        {
            if (record == null) return "The match session is unavailable.";
            if (record.Rules == MatchNotationRules.Chaos || IsChaos(record.ModeId)) return "Chess notation export is unavailable in Chaos Mode.";
            if (!ShowNotation(record)) return "This mode does not provide a standard chess record.";
            if (!record.Completed) return "Finish the match before exporting its result.";
            return null;
        }
    }

    public static class MatchExportService
    {
        public static ChessExportResult Export(MatchExportRecord record, ChessExportKind kind)
        {
            string rejection = MatchExportPolicy.Rejection(record);
            if (rejection != null) return ChessExportResult.Fail(rejection);
            try
            {
                if (kind != ChessExportKind.Pgn && kind != ChessExportKind.Fen)
                    return ChessExportResult.Fail("Unknown export format.");
                MatchState final = StandardChessNotation.ParsePosition(record.FinalFen);
                if (kind == ChessExportKind.Fen) return ChessExportResult.Ok(FenCodec.Write(final));
                if (!record.HistoryComplete || record.Moves == null || string.IsNullOrWhiteSpace(record.InitialFen))
                    return ChessExportResult.Fail("The complete move record is unavailable. FEN may still be exported.");
                if (record.Result != "1-0" && record.Result != "0-1" && record.Result != "1/2-1/2")
                    return ChessExportResult.Fail("The completed match has no valid result.");

                MatchState state = StandardChessNotation.ParsePosition(record.InitialFen);
                string initialFen = FenCodec.Write(state);
                var tokens = new List<string>();
                for (int i = 0; i < record.Moves.Count; i++)
                {
                    Move move = record.Moves[i];
                    string san = StandardChessNotation.WriteSan(state, move, out MatchState next);
                    if (state.Turn == Team.White) tokens.Add(state.FullMoveNumber.ToString(CultureInfo.InvariantCulture) + ".");
                    else if (i == 0) tokens.Add(state.FullMoveNumber.ToString(CultureInfo.InvariantCulture) + "...");
                    tokens.Add(san); state = next;
                }
                if (FenCodec.Write(state) != FenCodec.Write(final))
                    return ChessExportResult.Fail("The move record does not reconstruct the final position and counters.");
                bool noMoves = ClassicRules.LegalMoves(final).Count == 0;
                bool check = ClassicRules.IsInCheck(final.Board, final.Turn);
                string forcedResult = check ? (final.Turn == Team.White ? "0-1" : "1-0") : "1/2-1/2";
                if (noMoves && forcedResult != record.Result)
                    return ChessExportResult.Fail("The recorded result conflicts with the final position.");

                var pgn = new StringBuilder();
                Tag(pgn, "Event", "Chess but Weird"); Tag(pgn, "Site", "?");
                Tag(pgn, "Date", record.StartedAt.HasValue ? record.StartedAt.Value.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture) : "????.??.??");
                Tag(pgn, "Round", "?"); Tag(pgn, "White", record.White); Tag(pgn, "Black", record.Black); Tag(pgn, "Result", record.Result);
                if (initialFen != FenCodec.InitialPosition) { Tag(pgn, "SetUp", "1"); Tag(pgn, "FEN", initialFen); }
                pgn.Append('\n'); tokens.Add(record.Result);
                int column = 0;
                foreach (string token in tokens)
                {
                    if (column > 0 && column + token.Length + 1 > 79) { pgn.Append('\n'); column = 0; }
                    if (column > 0) { pgn.Append(' '); column++; }
                    pgn.Append(token); column += token.Length;
                }
                pgn.Append("\n\n");
                return ChessExportResult.Ok(pgn.ToString());
            }
            catch (Exception error) when (error is FormatException || error is ArgumentException || error is OverflowException)
            { return ChessExportResult.Fail("Cannot export: " + error.Message); }
        }

        private static void Tag(StringBuilder text, string name, string value)
        {
            string safe = StandardChessNotation.SingleLine(value);
            if (safe.Length == 0) safe = "?";
            text.Append('[').Append(name).Append(" \"").Append(safe.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\"]\n");
        }
    }
}
