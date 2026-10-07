using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChessButWeird.Application;
using ChessButWeird.Domain;
using UnityEngine;

public sealed class StockfishBotController : MonoBehaviour
{
    private readonly System.Random random = new System.Random();
    private ChessGame chessGame;
    private StockfishUciClient client;
    private StockfishUciClient reviewClient;
    private CancellationTokenSource reviewCancellation;
    private StockfishPosition lastPosition;
    private StockfishUciClient hintClient;
    private CancellationTokenSource hintCancellation;
    private int hintSequence;
    private static readonly StockfishDifficultyProfile HintProfile = new StockfishDifficultyProfile(
        StockfishDifficulty.Expert, "Practice hint", string.Empty, 10, 1, 0, 1, 0, 0);
    private int reviewSequence;
    private CancellationTokenSource turnCancellation;
    private bool botGameActive;
    private bool thinking;
    private int gameGeneration;
    private int turnSequence;
    private PieceTeam botTeam;
    private StockfishDifficulty difficulty = StockfishDifficulty.Medium;

    public bool IsBotGameActive => botGameActive;
    public bool IsThinking => thinking;
    public StockfishDifficulty Difficulty => difficulty;
    public string ErrorMessage { get; private set; }
    public string Activity { get; private set; } = string.Empty;
    public string HintText { get; private set; } = string.Empty;
    public bool IsHintThinking { get; private set; }
    public bool CanRequestHint => botGameActive && chessGame && chessGame.IsBotPractice && chessGame.BotOptions.AllowHints &&
        chessGame.GameStarted && !chessGame.GameOver && !chessGame.IsMatchEnding && !chessGame.InputLocked &&
        !chessGame.PauseLocked && !chessGame.HasPendingPromotion && chessGame.CurrentTurn == chessGame.PlayerTeam && !IsHintThinking;
    public event Action<string> ActivityChanged;
    public event Action<BotMoveQuality> PlayerMoveAssessed;
    public event Action<string> HintReady;

    private void SetActivity(string value) { Activity = value; ActivityChanged?.Invoke(value); }

    public void RetryBotTurn()
    {
        if (thinking || !botGameActive || chessGame.GameOver || chessGame.IsMatchEnding || chessGame.CurrentTurn != botTeam) return;
        ErrorMessage = null;
        ResetReview();
        QueueBotTurnIfNeeded();
    }

    public void Initialize(ChessGame newChessGame)
    {
        chessGame = newChessGame;
        chessGame.MoveCommitted += HandleMoveCommitted;
        chessGame.ReturnedToMainMenu += HandleReturnedToMainMenu;
        chessGame.LocalGameRestarted += HandleLocalGameRestarted;
        chessGame.ContentReady += HandleContentReady;
        chessGame.MatchEnding += HandleMatchEnding;
        chessGame.BotPositionRestoring += HandleBotPositionRestoring;
        chessGame.BotPositionRestored += HandleBotPositionRestored;
        var companion = gameObject.AddComponent<BotCompanionView>();
        companion.Initialize(chessGame, this);
        gameObject.AddComponent<BotPracticeView>().Initialize(chessGame, this);
    }

    public void StartBotGame(PieceTeam playerTeam, StockfishDifficulty selectedDifficulty)
        => StartBotGame(playerTeam, selectedDifficulty, BotGameOptions.Challenge);

    public void StartBotGame(PieceTeam playerTeam, StockfishDifficulty selectedDifficulty, BotGameOptions options)
    {
        CancelPendingTurn();
        gameGeneration++;
        botGameActive = true;
        difficulty = selectedDifficulty;
        ErrorMessage = null;
        ResetReview();
        client?.Dispose(); client = null;
        botTeam = playerTeam == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        GameMusicManager.PlayInGameMusic(true, difficulty);
        CancelHint();
        chessGame.BeginBotGame(playerTeam, difficulty, options);
        lastPosition=chessGame.BotEnginePosition;
        QueueBotTurnIfNeeded();
    }

    private void HandleMoveCommitted(ChessMove move)
    {
        CancelHint();
        StockfishPosition before=lastPosition;
        lastPosition=chessGame.BotEnginePosition;
        if(botGameActive&&!chessGame.GameOver&&!chessGame.IsMatchEnding&&before!=null&&FenCodec.Parse(before.Fen).Turn==(Team)chessGame.PlayerTeam)
        {
            reviewCancellation?.Cancel();reviewCancellation?.Dispose();
            reviewCancellation=new CancellationTokenSource(3500);
            int sequence=++reviewSequence;
            var played=new Move(new Square(move.from.x,move.from.y),new Square(move.to.x,move.to.y),
                move.hasPromotion?(PieceKind?)move.promotionType:null);
            _=ReviewPlayerMoveAsync(before,lastPosition,played,gameGeneration,sequence,reviewCancellation.Token);
        }
        QueueBotTurnIfNeeded();
    }
    private async Task ReviewPlayerMoveAsync(StockfishPosition before,StockfishPosition after,Move played,int generation,int sequence,CancellationToken token)
    {
        try
        {
            if(reviewClient==null)reviewClient=new StockfishUciClient();
            var quality=await BotMoveAssessment.AssessAsync(reviewClient,before,after,played,token);
            token.ThrowIfCancellationRequested();
            if(botGameActive&&generation==gameGeneration&&sequence==reviewSequence&&!chessGame.GameOver&&!chessGame.IsMatchEnding&&quality!=BotMoveQuality.Ordinary)
                PlayerMoveAssessed?.Invoke(quality);
        }
        catch(OperationCanceledException) { }
        catch(Exception) { /* Optional review never interrupts play or reports an uncertain grade. */ }
    }
    private void ResetReview()
    {
        reviewSequence++;reviewCancellation?.Cancel();reviewCancellation?.Dispose();reviewCancellation=null;
        reviewClient?.Dispose();reviewClient=null;lastPosition=null;
    }
    private void HandleContentReady()
    {
        if (!thinking && ErrorMessage == null) QueueBotTurnIfNeeded();
    }

    private void HandleLocalGameRestarted()
    {
        if (!botGameActive)
            return;

        CancelPendingTurn();
        gameGeneration++;
        ErrorMessage = null;
        CancelHint();
        ResetReview();lastPosition=chessGame.BotEnginePosition;
        client?.Dispose(); client = null;
        GameMusicManager.PlayInGameMusic(true, difficulty);
        QueueBotTurnIfNeeded();
    }

    private void HandleReturnedToMainMenu()
    {
        botGameActive = false;
        gameGeneration++;
        CancelPendingTurn();
        ErrorMessage = null;
        ResetReview();
        CancelHint();
        client?.Dispose(); client = null;
    }

    private void HandleMatchEnding()
    {
        if (!botGameActive) return;
        gameGeneration++;
        CancelPendingTurn();
        ErrorMessage = null;
        ResetReview();
        CancelHint();
        client?.Dispose(); client = null;
    }

    private void HandleBotPositionRestoring()
    {
        gameGeneration++;
        CancelPendingTurn(); CancelHint(); ResetReview();
        ErrorMessage = null;
        client?.Dispose(); client = null;
    }

    private void HandleBotPositionRestored()
    {
        lastPosition = chessGame.BotEnginePosition;
        QueueBotTurnIfNeeded();
    }

    public void RequestHint()
    {
        if (!CanRequestHint) return;
        CancelHint();
        hintCancellation = new CancellationTokenSource();
        IsHintThinking = true; HintText = "Finding a hint...";
        _ = FindHintAsync(chessGame.BotEnginePosition, gameGeneration, hintSequence, hintCancellation.Token);
    }

    private async Task FindHintAsync(StockfishPosition requested, int generation, int sequence, CancellationToken token)
    {
        try
        {
            hintClient = new StockfishUciClient();
            var choices = await hintClient.FindCandidatesAsync(requested, HintProfile, token);
            token.ThrowIfCancellationRequested();
            if (generation != gameGeneration || sequence != hintSequence) return;
            if (chessGame.ExportFen() != requested.Fen || chessGame.GameOver || chessGame.IsMatchEnding ||
                chessGame.InputLocked || chessGame.HasPendingPromotion || chessGame.CurrentTurn != chessGame.PlayerTeam)
            { HintText = string.Empty; return; }
            foreach (var choice in choices.OrderByDescending(c => c.ScoreCentipawns))
            {
                if (!StockfishMoveAdapter.TryParseUci(choice.Move, out var move) ||
                    !ClassicRules.TryApply(FenCodec.Parse(requested.Fen), move, out _)) continue;
                var piece = FenCodec.Parse(requested.Fen).Board.GetPiece(move.From);
                HintText = "Try moving your " + piece.Kind.ToString().ToLowerInvariant() + " from " + move.From + " to " + move.To +
                    (move.Promotion.HasValue ? " and promoting it to a " + move.Promotion.Value.ToString().ToLowerInvariant() : string.Empty) + ".";
                if (!chessGame.PauseLocked && !chessGame.InputLocked) chessGame.ShowBotPracticeHint(move);
                PlayerAuthService.RecordBotPracticeAssist(Difficulty, hint: true);
                HintReady?.Invoke(HintText);
                return;
            }
            HintText = "No hint is available for this position.";
            HintReady?.Invoke(HintText);
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (!token.IsCancellationRequested && generation == gameGeneration && sequence == hintSequence)
            {
                HintText = "Hint unavailable. Try again.";
                HintReady?.Invoke(HintText);
            }
        }
        finally
        {
            if (generation == gameGeneration && sequence == hintSequence)
            {
                IsHintThinking = false;
                hintClient?.Dispose(); hintClient = null;
            }
        }
    }

    private void CancelHint()
    {
        hintSequence++;
        hintCancellation?.Cancel(); hintCancellation?.Dispose(); hintCancellation = null;
        hintClient?.Dispose(); hintClient = null;
        IsHintThinking = false; HintText = string.Empty;
    }

    private void QueueBotTurnIfNeeded()
    {
        if (!botGameActive || chessGame == null || !chessGame.GameStarted || chessGame.GameOver || chessGame.IsMatchEnding || chessGame.CurrentTurn != botTeam)
            return;

        CancelPendingTurn();
        turnCancellation = new CancellationTokenSource();
        _ = PlayBotTurnAsync(gameGeneration, turnSequence, turnCancellation.Token);
    }

    private async Task PlayBotTurnAsync(int generation, int sequence, CancellationToken cancellationToken)
    {
        thinking = true;
        StockfishDifficultyProfile profile = StockfishDifficultyProfiles.Get(difficulty);

        try
        {
            StockfishPosition requestedPosition = chessGame.BotEnginePosition;
            string requestedFen = requestedPosition.Fen;
            ErrorMessage = null;
            // MoveCommitted precedes result adjudication and its final animation.
            // Checkmate/stalemate has no next bot turn, rather than a failed engine search.
            var position = FenCodec.Parse(requestedFen);
            if (ClassicRules.LegalMoves(position).Count == 0) return;
            SetActivity("Thinking...");
            var minimumThinkTime = Task.Delay(profile.MinimumThinkTimeMs, cancellationToken);
            IReadOnlyList<StockfishCandidate> candidates = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (client == null) client = new StockfishUciClient();
                    candidates = await client.FindCandidatesAsync(requestedPosition, profile, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    candidates = candidates.Where(candidate => candidate != null &&
                        StockfishMoveAdapter.TryParseUci(candidate.Move, out Move legalMove) &&
                        ClassicRules.TryApply(position, legalMove, out _)).ToArray();
                    if (candidates.Count == 0) throw new InvalidOperationException("Stockfish returned no legal move.");
                    SetActivity("Thinking...");
                    break;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception) when (attempt == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    client?.Dispose(); client = null;
                    SetActivity("Reconnecting...");
                    await Task.Delay(350, cancellationToken);
                }
            }
            await minimumThinkTime;

            while (chessGame.InputLocked || chessGame.PauseLocked)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            if (!botGameActive || generation != gameGeneration || chessGame.GameOver || chessGame.IsMatchEnding ||
                chessGame.CurrentTurn != botTeam || chessGame.ExportFen() != requestedFen)
                return;

            StockfishCandidate selected = SelectCandidate(candidates, profile);
            if (selected == null || !StockfishMoveAdapter.TryParseUci(selected.Move, out Move move))
                throw new InvalidOperationException("Stockfish did not return a usable move.");

            if (!ClassicRules.TryApply(FenCodec.Parse(requestedFen),move,out var committed)||!chessGame.ApplyBotMove(move))
                throw new InvalidOperationException($"The game rejected Stockfish move {selected.Move}.");
            // Controlled moves suppress MoveCommitted; promotion commits after animation, so use its complete domain state.
            lastPosition=requestedPosition.Play(move);
        }
        catch (OperationCanceledException)
        {
            // A restart, main-menu transition, or newer turn superseded this search.
        }
        catch (Exception exception)
        {
            if (!cancellationToken.IsCancellationRequested && generation == gameGeneration && sequence == turnSequence &&
                !chessGame.GameOver && !chessGame.IsMatchEnding)
            {
                ErrorMessage = "I lost my train of thought. Try reconnecting me.";
                SetActivity("Connection paused");
                Debug.LogError($"[StockfishBot] {exception.Message}");
            }
        }
        finally
        {
            if (generation == gameGeneration && sequence == turnSequence)
            {
                thinking = false;
                if (ErrorMessage == null) SetActivity(string.Empty);
            }
        }
    }

    private StockfishCandidate SelectCandidate(
        IReadOnlyList<StockfishCandidate> candidates,
        StockfishDifficultyProfile profile)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        List<StockfishCandidate> ordered = candidates
            .Where(candidate => candidate != null && !string.IsNullOrWhiteSpace(candidate.Move))
            .OrderByDescending(candidate => candidate.ScoreCentipawns)
            .ToList();
        if (ordered.Count <= 1 || random.NextDouble() < profile.BestMoveChance)
            return ordered.FirstOrDefault();

        int bestScore = ordered[0].ScoreCentipawns;
        List<StockfishCandidate> alternatives = ordered
            .Skip(1)
            .Where(candidate => bestScore - candidate.ScoreCentipawns <= profile.MaximumCentipawnLoss)
            .ToList();
        if (alternatives.Count == 0)
            return ordered[0];

        double targetLoss = profile.TargetCentipawnLoss * (0.65d + random.NextDouble() * 0.7d);
        return alternatives
            .OrderBy(candidate => Math.Abs((bestScore - candidate.ScoreCentipawns) - targetLoss) + random.NextDouble() * 45d)
            .First();
    }

    private void CancelPendingTurn()
    {
        turnSequence++;
        turnCancellation?.Cancel();
        turnCancellation?.Dispose();
        turnCancellation = null;
        thinking = false;
        SetActivity(string.Empty);
    }

    private void OnDestroy()
    {
        if (chessGame != null)
        {
            chessGame.MoveCommitted -= HandleMoveCommitted;
            chessGame.ReturnedToMainMenu -= HandleReturnedToMainMenu;
            chessGame.LocalGameRestarted -= HandleLocalGameRestarted;
            chessGame.ContentReady -= HandleContentReady;
            chessGame.MatchEnding -= HandleMatchEnding;
            chessGame.BotPositionRestoring -= HandleBotPositionRestoring;
            chessGame.BotPositionRestored -= HandleBotPositionRestored;
        }

        CancelPendingTurn();
        ResetReview();
        CancelHint();
        client?.Dispose();
        client = null;
    }
}
