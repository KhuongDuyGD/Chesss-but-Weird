using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class StockfishBotController : MonoBehaviour
{
    private readonly System.Random random = new System.Random();
    private ChessGame chessGame;
    private StockfishUciClient client;
    private CancellationTokenSource turnCancellation;
    private bool botGameActive;
    private bool thinking;
    private int gameGeneration;
    private PieceTeam botTeam;
    private StockfishDifficulty difficulty = StockfishDifficulty.Medium;

    public bool IsBotGameActive => botGameActive;
    public bool IsThinking => thinking;
    public StockfishDifficulty Difficulty => difficulty;

    public void Initialize(ChessGame newChessGame)
    {
        chessGame = newChessGame;
        chessGame.MoveCommitted += HandleMoveCommitted;
        chessGame.ReturnedToMainMenu += HandleReturnedToMainMenu;
        chessGame.LocalGameRestarted += HandleLocalGameRestarted;
    }

    public void StartBotGame(PieceTeam playerTeam, StockfishDifficulty selectedDifficulty)
    {
        CancelPendingTurn();
        gameGeneration++;
        botGameActive = true;
        difficulty = selectedDifficulty;
        botTeam = playerTeam == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        GameMusicManager.PlayInGameMusic(true, difficulty);
        chessGame.BeginBotGame(playerTeam);
        QueueBotTurnIfNeeded();
    }

    private void HandleMoveCommitted(ChessLanMove move)
    {
        QueueBotTurnIfNeeded();
    }

    private void HandleLocalGameRestarted()
    {
        if (!botGameActive)
            return;

        CancelPendingTurn();
        gameGeneration++;
        GameMusicManager.PlayInGameMusic(true, difficulty);
        QueueBotTurnIfNeeded();
    }

    private void HandleReturnedToMainMenu()
    {
        botGameActive = false;
        gameGeneration++;
        CancelPendingTurn();
    }

    private void QueueBotTurnIfNeeded()
    {
        if (!botGameActive || chessGame == null || !chessGame.GameStarted || chessGame.GameOver || chessGame.CurrentTurn != botTeam)
            return;

        CancelPendingTurn();
        turnCancellation = new CancellationTokenSource();
        _ = PlayBotTurnAsync(gameGeneration, turnCancellation.Token);
    }

    private async Task PlayBotTurnAsync(int generation, CancellationToken cancellationToken)
    {
        thinking = true;
        string requestedFen = chessGame.ExportFen();
        StockfishDifficultyProfile profile = StockfishDifficultyProfiles.Get(difficulty);

        try
        {
            if (client == null)
                client = new StockfishUciClient();

            Task<IReadOnlyList<StockfishCandidate>> search =
                client.FindCandidatesAsync(requestedFen, profile, cancellationToken);
            Task minimumThinkTime = Task.Delay(profile.MinimumThinkTimeMs, cancellationToken);
            await Task.WhenAll(search, minimumThinkTime);

            while (chessGame.InputLocked || chessGame.PauseLocked)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            if (!botGameActive || generation != gameGeneration || chessGame.GameOver ||
                chessGame.CurrentTurn != botTeam || chessGame.ExportFen() != requestedFen)
                return;

            StockfishCandidate selected = SelectCandidate(search.Result, profile);
            if (selected == null || !TryParseUciMove(selected.Move, out ChessLanMove move))
                throw new InvalidOperationException("Stockfish did not return a usable move.");

            if (!chessGame.ApplyBotMove(move))
                throw new InvalidOperationException($"The game rejected Stockfish move {selected.Move}.");
        }
        catch (OperationCanceledException)
        {
            // A restart, main-menu transition, or newer turn superseded this search.
        }
        catch (Exception exception)
        {
            Debug.LogError($"[StockfishBot] {exception.Message}");
        }
        finally
        {
            thinking = false;
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

    private static bool TryParseUciMove(string uciMove, out ChessLanMove move)
    {
        move = default;
        if (string.IsNullOrWhiteSpace(uciMove) || (uciMove.Length != 4 && uciMove.Length != 5))
            return false;

        if (!TryParseSquare(uciMove.Substring(0, 2), out Vector2Int from) ||
            !TryParseSquare(uciMove.Substring(2, 2), out Vector2Int to))
            return false;

        if (uciMove.Length == 4)
        {
            move = new ChessLanMove(from, to);
            return true;
        }

        PieceType promotion;
        switch (char.ToLowerInvariant(uciMove[4]))
        {
            case 'q': promotion = PieceType.Queen; break;
            case 'r': promotion = PieceType.Rook; break;
            case 'b': promotion = PieceType.Bishop; break;
            case 'n': promotion = PieceType.Knight; break;
            default: return false;
        }

        move = new ChessLanMove(from, to, promotion);
        return true;
    }

    private static bool TryParseSquare(string square, out Vector2Int position)
    {
        position = -Vector2Int.one;
        if (square == null || square.Length != 2)
            return false;

        char file = char.ToLowerInvariant(square[0]);
        char rank = square[1];
        if (file < 'a' || file > 'h' || rank < '1' || rank > '8')
            return false;

        position = new Vector2Int(file - 'a', rank - '1');
        return true;
    }

    private void CancelPendingTurn()
    {
        if (turnCancellation == null)
            return;

        turnCancellation.Cancel();
        turnCancellation.Dispose();
        turnCancellation = null;
        thinking = false;
    }

    private void OnDestroy()
    {
        if (chessGame != null)
        {
            chessGame.MoveCommitted -= HandleMoveCommitted;
            chessGame.ReturnedToMainMenu -= HandleReturnedToMainMenu;
            chessGame.LocalGameRestarted -= HandleLocalGameRestarted;
        }

        CancelPendingTurn();
        client?.Dispose();
        client = null;
    }
}
