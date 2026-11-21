using System;
using System.Collections.Generic;
using System.Linq;
using ChessButWeird.Application;
using ChessButWeird.Domain;
using UnityEngine;

public partial class ChessGame
{
    private sealed class BotFrame
    {
        public StockfishPosition Position;
        public MatchStatistics.Entry[] Entries;
        public int ConfirmedMoveNumber;
        public bool HistoryComplete, CapturesComplete;
    }

    private readonly List<BotFrame> botFrames = new List<BotFrame>();
    private BotGameOptions botOptions = BotGameOptions.Challenge;
    public BotGameOptions BotOptions => botOptions;
    public bool IsBotPractice => botMode && botOptions.IsPractice;
    public StockfishPosition BotEnginePosition => botFrames.Count > 0
        ? botFrames[botFrames.Count - 1].Position : StockfishPosition.FromFen(ExportFen());
    public event Action BotPositionRestoring;
    public event Action BotPositionRestored;

    private void ResetBotHistory()
    {
        botFrames.Clear();
        if (botMode && UsesClassicDomainSession)
            botFrames.Add(CaptureBotFrame(StockfishPosition.FromFen(ExportFen())));
    }

    private BotFrame CaptureBotFrame(StockfishPosition position) => new BotFrame {
        Position = position, Entries = statistics.Entries.ToArray(), ConfirmedMoveNumber = statistics.ConfirmedMoveNumber,
        HistoryComplete = statistics.HistoryComplete, CapturesComplete = statistics.CapturesComplete };

    private void RecordBotMove(ChessMove move)
    {
        if (!botMode || !UsesClassicDomainSession) return;
        if (botFrames.Count == 0) { ResetBotHistory(); return; }
        var command = new Move(new Square(move.from.x, move.from.y), new Square(move.to.x, move.to.y),
            move.hasPromotion ? (PieceKind?)move.promotionType : null);
        StockfishPosition next = botFrames[botFrames.Count - 1].Position.Play(command);
        if (next.Fen != ExportFen())
            throw new InvalidOperationException("Bot move history diverged from the committed board.");
        botFrames.Add(CaptureBotFrame(next));
    }

    private int PracticeUndoTarget()
    {
        // Always return to the start of the player's previous turn. Black's opening bot
        // move is retained, and a pending bot reply removes just the player's last move.
        for (int i = botFrames.Count - 2; i >= 0; i--)
            if ((PieceTeam)FenCodec.Parse(botFrames[i].Position.Fen).Turn == playerTeam) return i;
        return -1;
    }

    public bool CanUndoBotPractice => IsBotPractice && botOptions.AllowUndo && GameStarted &&
        !gameOver && !matchEnding && !PauseLocked && !InputLocked && !HasPendingPromotion &&
        PracticeUndoTarget() >= 0;

    public bool UndoBotPractice()
    {
        if (!CanUndoBotPractice) return false;
        int target = PracticeUndoTarget();
        BotFrame frame = botFrames[target];
        BotPositionRestoring?.Invoke();
        // ApplyFenState rebuilds presentation and the domain owner; keep the accepted
        // branch separately because ordinary FEN imports establish a new history root.
        BotFrame[] branch = botFrames.Take(target + 1).ToArray();
        if (!ApplyFenState(frame.Position.Fen))
        {
            BotPositionRestored?.Invoke();
            return false;
        }
        botFrames.Clear(); botFrames.AddRange(branch);
        statistics.Restore(frame.Entries, frame.ConfirmedMoveNumber, frame.HistoryComplete, frame.CapturesComplete);
        SyncPresentationStatistics();
        moveHistoryFirstTurn = (PieceTeam)FenCodec.Parse(branch[0].Position.Fen).Turn;
        positionHistory.Clear();
        foreach (var entry in branch)
        {
            string key = FenCodec.RepetitionKey(FenCodec.Parse(entry.Position.Fen));
            positionHistory.TryGetValue(key, out int count); positionHistory[key] = count + 1;
        }
        chessboard?.ClearLegalMoveHighlights();
        PlayerAuthService.RecordBotPracticeAssist(botDifficulty, hint: false);
        BotPositionRestored?.Invoke();
        return true;
    }

    public bool ShowBotPracticeHint(Move move)
    {
        if (!IsBotPractice || !botOptions.AllowHints || !GameStarted || gameOver || matchEnding ||
            InputLocked || PauseLocked || currentTurn != playerTeam || HasPendingPromotion) return false;
        if (!ClassicRules.TryApply(FenCodec.Parse(ExportFen()), move, out _)) return false;
        var piece = GetPieceAt(new Vector2Int(move.From.File, move.From.Rank));
        if (!piece) return false;
        if (selectedPiece == piece) DeselectCurrentPiece(true);
        if (!TrySelectPiece(piece)) return false;
        chessboard?.SetLegalMoveHighlights(new List<Vector2Int> { new Vector2Int(move.To.File, move.To.Rank) });
        return true;
    }
}
