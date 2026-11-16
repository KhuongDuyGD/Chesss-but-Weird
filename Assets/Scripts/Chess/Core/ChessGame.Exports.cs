using System;
using System.Collections.Generic;
using System.Linq;
using ChessButWeird.Application;
using ChessButWeird.Domain;
using Newtonsoft.Json.Linq;

public partial class ChessGame
{
    private DateTime? exportStartedUtc;
    private string exportHistoryMatchId;
    private List<Move> exportOnlineMoves;
    private bool exportOnlineHistoryComplete;
    private string exportVerifiedCountFen;

    private void BeginExportRecord()
    {
        exportStartedUtc = DateTime.UtcNow;
        exportHistoryMatchId = null; exportOnlineMoves = null; exportOnlineHistoryComplete = false;
        exportVerifiedCountFen = null;
    }

    public MatchNotationRules ExportRules => aramMode || dotNetState?.chaos != null || MatchExportPolicy.IsChaos(dotNetState?.settings?.mode)
        ? MatchNotationRules.Chaos : dotNetState != null
        ? (string.Equals(dotNetState.settings?.mode, "Classic", StringComparison.OrdinalIgnoreCase) ? MatchNotationRules.Standard : MatchNotationRules.Unsupported)
        : UsesClassicDomainSession ? MatchNotationRules.Standard : MatchNotationRules.Unsupported;

    public int? ResultMoveCount => statistics.HistoryComplete &&
        (dotNetState == null || ExportRules == MatchNotationRules.Standard && exportHistoryMatchId == dotNetState.matchId &&
            exportVerifiedCountFen != null && exportVerifiedCountFen == OnlineExportFen())
        ? (int?)statistics.ConfirmedMoveNumber : null;

    public MatchExportRecord CaptureExportRecord()
    {
        string initial = null, final = null;
        IEnumerable<Move> moves = null;
        bool complete = false;
        if (ExportRules == MatchNotationRules.Standard && !HasPendingPromotion && !onlinePromotionPending)
        {
            if (dotNetState != null)
            {
                // Only the server's six-field state can supply online counters/rights.
                final = OnlineExportFen();
                initial = FenCodec.InitialPosition;
                moves = exportOnlineMoves;
                complete = exportOnlineHistoryComplete && exportHistoryMatchId == dotNetState.matchId && statistics.HistoryComplete;
            }
            else if (UsesClassicDomainSession)
            {
                final = FenCodec.Write(localClassicCoordinator.Snapshot);
                initial = FenCodec.Write(localClassicCoordinator.InitialSnapshot);
                moves = localClassicCoordinator.AcceptedMoves;
                complete = !localClassicCoordinator.HasPendingPromotion && statistics.HistoryComplete;
                int recordedCount = localClassicCoordinator.AcceptedMoves.Count;
                // Practice undo already preserves its accepted branch. Reuse it rather than
                // exporting the temporary FEN root established by the undo operation.
                if (botMode && botFrames.Count > 0)
                {
                    var position = botFrames[botFrames.Count - 1].Position;
                    initial = position.InitialFen;
                    var branch = new List<Move>();
                    foreach (string uci in position.Moves)
                        if (StockfishMoveAdapter.TryParseUci(uci, out Move move)) branch.Add(move); else complete = false;
                    moves = branch; complete &= position.Fen == final;
                    recordedCount = branch.Count;
                }
                // A middle-game import cannot turn missing original moves into a
                // zero-move PGN. A genuine custom start has no earlier moves.
                complete &= recordedCount == statistics.ConfirmedMoveNumber;
            }
        }
        return new MatchExportRecord(ExportRules, dotNetState?.settings?.mode ?? (aramMode ? "Chaos" : "Classic"), gameOver,
            initial, final, moves, complete, StatisticsPlayerName(PieceTeam.White), StatisticsPlayerName(PieceTeam.Black),
            status == ChessGameStatus.Draw ? "1/2-1/2" : winningTeam == PieceTeam.White ? "1-0" : "0-1",
            dotNetState == null ? exportStartedUtc : null);
    }

    private string OnlineExportFen()
    {
        try
        {
            var state = StandardChessNotation.ParsePosition(dotNetState.fen);
            if (dotNetState.turn != state.Turn.ToString() || dotNetState.board == null) return null;
            var squares = new HashSet<Square>();
            foreach (var piece in dotNetState.board)
            {
                if (piece == null) return null;
                var square = FenCodec.ParseSquare(piece.square);
                var expected = state.Board.GetPiece(square);
                if (!squares.Add(square) || expected.IsEmpty || expected.Kind.ToString() != piece.kind || expected.Team.ToString() != piece.team) return null;
            }
            int count = 0;
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
                if (!state.Board.GetPiece(new Square(x, y)).IsEmpty) count++;
            return count == squares.Count ? dotNetState.fen : null;
        }
        catch (Exception error) when (error is FormatException || error is ArgumentException) { return null; }
    }

    private void RestoreExportMoves(List<ChessButWeird.Online.MoveRecord> records, ChessButWeird.Online.MatchState snapshot)
    {
        exportHistoryMatchId = snapshot.matchId; exportOnlineHistoryComplete = false; exportOnlineMoves = null;
        exportVerifiedCountFen = null;
        if (ExportRules != MatchNotationRules.Standard || records == null) return;
        var moves = new List<Move>();
        var ids = new HashSet<string>(); var sequences = new HashSet<long>();
        try
        {
            foreach (var record in records.OrderBy(r => r.sequence))
            {
                if (record.sequence > snapshot.eventSequence) continue;
                if (record.matchId != snapshot.matchId || !sequences.Add(record.sequence) ||
                    (!string.IsNullOrEmpty(record.id) && !ids.Add(record.id))) return;
                if (record.kind == "Ability") return;
                if (record.kind != "Move") continue;
                var body = JObject.Parse(record.payloadJson);
                PieceKind? promotion = null;
                if (body["promotion"] != null && body["promotion"].Type != JTokenType.Null)
                {
                    if (!Enum.TryParse((string)body["promotion"], out PieceKind kind) ||
                        kind == PieceKind.King || kind == PieceKind.Pawn) return;
                    promotion = kind;
                }
                moves.Add(new Move(FenCodec.ParseSquare((string)body["from"]), FenCodec.ParseSquare((string)body["to"]), promotion));
            }
            exportOnlineMoves = moves;
            // Completeness is proven again by reconstructing all six final FEN fields.
            exportOnlineHistoryComplete = true;
            // Optional counts require a complete standard record too. Chaos has
            // already returned above and never enters standard-chess validation.
            var countState = FenCodec.Parse(FenCodec.InitialPosition);
            foreach (var move in moves)
            {
                if (!ClassicRules.TryApply(countState, move, out var applied)) return;
                countState = applied.State;
            }
            if (FenCodec.Write(countState) == snapshot.fen) exportVerifiedCountFen = snapshot.fen;
        }
        catch (Exception error) when (error is FormatException || error is ArgumentException || error is Newtonsoft.Json.JsonException)
        { exportOnlineHistoryComplete = false; }
    }

    public string ResultTerminationReason
    {
        get
        {
            if (dotNetState?.result != null) return PlayModeNames.DisplayText(dotNetState.result.reason);
            if (status == ChessGameStatus.Draw) return PlayModeNames.DisplayText(drawReason);
            if (gameOver && UsesClassicDomainSession)
            {
                var final = localClassicCoordinator.Snapshot;
                if (ClassicRules.IsInCheck(final.Board, final.Turn) && ClassicRules.LegalMoves(final).Count == 0) return "Checkmate";
            }
            return null;
        }
    }

    /// <summary>Public board representation, including Chaos. This is deliberately not FEN.</summary>
    public char[] CaptureResultBoard()
    {
        if (runtimePiecesRoot == null) return null;
        var board = new char[64];
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
        {
            var piece = pieces[x, y];
            if (!piece) continue;
            int rank = localClassicCoordinator != null ? localClassicCoordinator.Orientation.ToCanonical(new Square(x, y)).Rank : y;
            char symbol = "KQRBNP"[(int)piece.Type];
            board[rank * 8 + x] = piece.Team == PieceTeam.White ? symbol : char.ToLowerInvariant(symbol);
        }
        return board;
    }
}
