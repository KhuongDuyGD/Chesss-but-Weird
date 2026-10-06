using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class StockfishCandidate
{
    public string Move { get; }
    public int ScoreCentipawns { get; }
    public int MultiPv { get; }
    public int Depth { get; }

    public StockfishCandidate(string move, int scoreCentipawns, int multiPv, int depth)
    {
        Move = move;
        ScoreCentipawns = scoreCentipawns;
        MultiPv = multiPv;
        Depth = depth;
    }
}

public sealed class StockfishUciClient : IDisposable
{
    private readonly int commandTimeoutMs;
    private readonly int stopGraceMs;
    private readonly string executablePath;
    private readonly object stateLock = new object();
    private readonly SemaphoreSlim commandGate = new SemaphoreSlim(1, 1);
    private Process process;
    private TaskCompletionSource<bool> uciReadySignal;
    private TaskCompletionSource<bool> engineReadySignal;
    private SearchSession activeSearch;
    private bool disposed;
    private bool initialized;

    public StockfishUciClient(string executablePath = null, int commandTimeoutMs = 10000, int stopGraceMs = 1500)
    {
        this.executablePath = executablePath ?? Path.Combine(Application.streamingAssetsPath, "Stockfish", "Windows", "stockfish.exe");
        this.commandTimeoutMs = Math.Max(1, commandTimeoutMs);
        this.stopGraceMs = Math.Max(1, stopGraceMs);
    }

    public bool IsRunning { get { lock (stateLock) return process != null && !process.HasExited; } }

    public async Task<IReadOnlyList<StockfishCandidate>> FindCandidatesAsync(
        string fen,
        StockfishDifficultyProfile profile,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fen))
            throw new ArgumentException("A FEN position is required.", nameof(fen));
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        await commandGate.WaitAsync(cancellationToken);
        SearchSession search = null;
        try
        {
            await EnsureStartedAsync(cancellationToken);
            SendCommand($"setoption name MultiPV value {profile.MultiPv}");
            await WaitUntilReadyAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            search = new SearchSession();
            lock (stateLock)
                activeSearch = search;

            SendCommand($"position fen {fen}");
            SendCommand($"go depth {profile.SearchDepth}");
            var result = await AwaitSignalAsync(search.Completion.Task, cancellationToken, commandTimeoutMs);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException)
        {
            // Retain ownership until the old bestmove is consumed. isready does NOT stop a search.
            if (search != null)
            {
                TrySendCommand("stop");
                try { await AwaitSignalAsync(search.Completion.Task, CancellationToken.None, stopGraceMs); }
                catch { ResetProcess(); }
            }
            else ResetProcess(); // A partially initialized process must never be reused.
            throw;
        }
        catch
        {
            // A timeout or broken pipe leaves the protocol ambiguous; a fresh process is the boundary.
            ResetProcess();
            throw;
        }
        finally
        {
            lock (stateLock)
                activeSearch = null;
            commandGate.Release();
        }
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (IsRunning && initialized)
            return;
        ResetProcess();

        if (!File.Exists(executablePath))
            throw new FileNotFoundException("Stockfish executable was not found.", executablePath);

        var startedProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        lock (stateLock)
        {
            ThrowIfDisposed();
            process = startedProcess;
            process.OutputDataReceived += HandleOutputLine;
            process.Exited += HandleProcessExited;
            uciReadySignal = NewSignal<bool>();
            if (!process.Start()) throw new InvalidOperationException("Unable to start Stockfish.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        SendCommand("uci");
        await AwaitSignalAsync(uciReadySignal.Task, cancellationToken, commandTimeoutMs);

        SendCommand("setoption name Threads value 1");
        SendCommand("setoption name Hash value 32");
        SendCommand("setoption name Ponder value false");
        SendCommand("ucinewgame");
        await WaitUntilReadyAsync(cancellationToken);
        initialized = true;
    }

    private async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        lock (stateLock) engineReadySignal = NewSignal<bool>();
        SendCommand("isready");
        await AwaitSignalAsync(engineReadySignal.Task, cancellationToken, commandTimeoutMs);
    }

    private void HandleOutputLine(object sender, DataReceivedEventArgs args)
    {
        string line = args.Data;
        if (string.IsNullOrWhiteSpace(line))
            return;
        lock (stateLock)
        {
            if (!ReferenceEquals(sender, process)) return;
            if (line == "uciok")
            {
                uciReadySignal?.TrySetResult(true);
                return;
            }

            if (line == "readyok")
            {
                engineReadySignal?.TrySetResult(true);
                return;
            }

            SearchSession search;
            search = activeSearch;

            if (search == null)
                return;

            if (line.StartsWith("info ", StringComparison.Ordinal))
                search.AcceptInfo(line);
            else if (line.StartsWith("bestmove ", StringComparison.Ordinal))
                search.Complete(line);
        }
    }

    private void HandleProcessExited(object sender, EventArgs args)
    {
        lock (stateLock)
        {
            if (!ReferenceEquals(sender, process)) return;
            initialized = false;
            FaultSignals(new InvalidOperationException("Stockfish stopped unexpectedly."));
        }
    }

    private void SendCommand(string command)
    {
        lock (stateLock)
        {
            ThrowIfDisposed();
            if (!IsRunning) throw new InvalidOperationException("Stockfish is not running.");
            process.StandardInput.WriteLine(command);
            process.StandardInput.Flush();
        }
    }

    private void TrySendCommand(string command)
    {
        try
        {
            if (IsRunning)
                SendCommand(command);
        }
        catch
        {
            // Cancellation and shutdown are best-effort.
        }
    }

    private static async Task<T> AwaitSignalAsync<T>(Task<T> signal, CancellationToken cancellationToken, int timeoutMs)
    {
        using (var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            Task completed = await Task.WhenAny(signal, Task.Delay(timeoutMs, delayCancellation.Token));
            delayCancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            if (completed == signal) return await signal;
            throw new TimeoutException("Stockfish did not respond in time.");
        }
    }

    private static TaskCompletionSource<T> NewSignal<T>()
    {
        return new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(StockfishUciClient));
    }

    public void Dispose()
    {
        lock (stateLock)
        {
            if (disposed) return;
            disposed = true;
        }
        ResetProcess();
    }

    private void FaultSignals(Exception error)
    {
        uciReadySignal?.TrySetException(error);
        engineReadySignal?.TrySetException(error);
        activeSearch?.Completion.TrySetException(error);
    }

    private void ResetProcess()
    {
        Process previous;
        lock (stateLock)
        {
            previous = process; process = null; initialized = false;
            FaultSignals(new InvalidOperationException("Stockfish session was reset."));
            activeSearch = null;
        }
        if (previous == null) return;
        previous.OutputDataReceived -= HandleOutputLine;
        previous.Exited -= HandleProcessExited;
        try { if (!previous.HasExited) previous.Kill(); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        finally { previous.Dispose(); }
    }

    private sealed class SearchSession
    {
        private readonly object candidateLock = new object();
        private readonly Dictionary<int, StockfishCandidate> candidates = new Dictionary<int, StockfishCandidate>();

        public TaskCompletionSource<IReadOnlyList<StockfishCandidate>> Completion { get; } =
            NewSignal<IReadOnlyList<StockfishCandidate>>();

        public void AcceptInfo(string line)
        {
            string[] tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int depth = ReadIntegerAfter(tokens, "depth", 0);
            int multiPv = ReadIntegerAfter(tokens, "multipv", 1);
            int scoreIndex = Array.IndexOf(tokens, "score");
            int pvIndex = Array.IndexOf(tokens, "pv");
            if (Array.IndexOf(tokens, "lowerbound") >= 0 || Array.IndexOf(tokens, "upperbound") >= 0) return;
            if (scoreIndex < 0 || scoreIndex + 2 >= tokens.Length || pvIndex < 0 || pvIndex + 1 >= tokens.Length)
                return;

            int score;
            if (tokens[scoreIndex + 1] == "mate")
            {
                if (!int.TryParse(tokens[scoreIndex + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mateIn))
                    return;
                score = mateIn > 0 ? 100000 - Math.Abs(mateIn) * 100 : -100000 + Math.Abs(mateIn) * 100;
            }
            else if (tokens[scoreIndex + 1] == "cp")
            {
                if (!int.TryParse(tokens[scoreIndex + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out score))
                    return;
            }
            else
            {
                return;
            }

            StockfishCandidate candidate = new StockfishCandidate(tokens[pvIndex + 1], score, multiPv, depth);
            lock (candidateLock)
            {
                if (!candidates.TryGetValue(multiPv, out StockfishCandidate previous) || depth >= previous.Depth)
                    candidates[multiPv] = candidate;
            }
        }

        public void Complete(string bestMoveLine)
        {
            string[] bestMoveTokens = bestMoveLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string bestMove = bestMoveTokens.Length > 1 ? bestMoveTokens[1] : string.Empty;
            List<StockfishCandidate> result;

            lock (candidateLock)
                result = candidates.Values.OrderBy(candidate => candidate.MultiPv).ToList();

            if (result.Count == 0 && !string.IsNullOrWhiteSpace(bestMove) && bestMove != "(none)")
                result.Add(new StockfishCandidate(bestMove, 0, 1, 0));
            else if (result.Count > 0 && !string.IsNullOrWhiteSpace(bestMove) && result.All(candidate => candidate.Move != bestMove))
                result.Insert(0, new StockfishCandidate(bestMove, result[0].ScoreCentipawns, 1, result[0].Depth));

            Completion.TrySetResult(result);
        }

        private static int ReadIntegerAfter(string[] tokens, string key, int fallback)
        {
            int index = Array.IndexOf(tokens, key);
            if (index < 0 || index + 1 >= tokens.Length)
                return fallback;

            return int.TryParse(tokens[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
        }
    }
}
