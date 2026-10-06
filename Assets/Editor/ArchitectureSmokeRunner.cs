using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch play-mode verification without changing scenes or writing match rewards.</summary>
[InitializeOnLoad]
public static class ArchitectureSmokeRunner
{
    private const string Key = "Chess.ArchitectureSmoke.Active";
    private static int step;
    private static double nextAt;
    private static double startedAt;
    private static readonly List<string> checks = new List<string>();
    private static ChessGame game;
    private static string unexpectedRuntimeError;
    private static bool ignoredSearchStartupError;

    static ArchitectureSmokeRunner()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLogMessage;
    }

    public static void Run()
    {
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/ChessClassic.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            step = 0; checks.Clear();
            unexpectedRuntimeError = null;
            ignoredSearchStartupError = false;
            startedAt = EditorApplication.timeSinceStartup;
            nextAt = startedAt + 2;
        }
    }

    private static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (!SessionState.GetBool(Key, false))
            return;

        if (stackTrace != null && stackTrace.Contains("UnityEditor.Search.SearchDatabase"))
        {
            ignoredSearchStartupError = true;
            return;
        }

        bool projectRuntimeError = type == LogType.Exception || type == LogType.Assert ||
            (type == LogType.Error && ((stackTrace != null && stackTrace.Contains("Assets/")) ||
                (condition != null && (condition.Contains("MissingReferenceException") ||
                    condition.Contains("NullReferenceException")))));
        if (projectRuntimeError && string.IsNullOrEmpty(unexpectedRuntimeError))
            unexpectedRuntimeError = $"{type}: {condition}\n{stackTrace}";
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks.Add(message);
        Debug.Log("[ArchitectureSmoke] PASS " + message);
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (EditorApplication.timeSinceStartup < nextAt) return;
        try
        {
            if (!string.IsNullOrEmpty(unexpectedRuntimeError))
                throw new InvalidOperationException("Unexpected gameplay log during smoke test: " + unexpectedRuntimeError);
            if (EditorApplication.timeSinceStartup - startedAt > 120) throw new TimeoutException("Smoke test timed out.");
            switch (step++)
            {
                case 0:
                    game = UnityEngine.Object.FindAnyObjectByType<ChessGame>();
                    Check(game, "Scene creates ChessGame");
                    game.BeginGame(PieceTeam.White);
                    Check(game.UsesLocalClassicSession, "Local classic uses MatchSession authority");
                    Check(game.GetActivePiecesForAram(PieceTeam.White).Count == 16 &&
                        game.GetActivePiecesForAram(PieceTeam.Black).Count == 16, "Initial 32 pieces");
                    Check(game.ExportFen() == ChessButWeird.Domain.FenCodec.InitialPosition, "Unity initial FEN matches Domain");
                    break;
                case 1:
                    var pawn = FindPiece(PieceTeam.White, new Vector2Int(4, 1));
                    Check(game.TrySelectPiece(pawn), "Select white pawn");
                    Check(game.TryMoveSelectedPiece(new Vector2Int(4, 3)), "Commit e2-e4");
                    break;
                case 2:
                    Check(!game.InputLocked && game.CurrentTurn == PieceTeam.Black, "Animation unlocks next turn");
                    Check(game.ExportFen().Contains("4P3"), "Pawn view and board moved");
                    game.SetPauseLocked(true);
                    Check(!game.TrySelectPiece(FindPiece(PieceTeam.Black, new Vector2Int(4, 6))), "Pause blocks input");
                    game.SetPauseLocked(false);
                    Check(game.TrySelectPiece(FindPiece(PieceTeam.Black, new Vector2Int(4, 6))), "Resume allows input");
                    Check(game.TryMoveSelectedPiece(new Vector2Int(4, 4)), "Commit e7-e5");
                    break;
                case 3:
                    Check(!game.InputLocked && game.CurrentTurn == PieceTeam.White, "Second animation unlocks");
                    Check(game.RestartCurrentLocalGame(), "Restart local match");
                    break;
                case 4:
                    Check(game.ExportFen() == ChessButWeird.Domain.FenCodec.InitialPosition, "Restart restores board");
                    game.BeginGame(PieceTeam.Black);
                    Check(game.UsesLocalClassicSession, "Reversed local classic uses MatchSession authority");
                    Check(FindPiece(PieceTeam.Black, new Vector2Int(4, 1)), "Black-first orientation preserved");
                    Check(game.TrySelectPiece(FindPiece(PieceTeam.Black, new Vector2Int(4, 1))) &&
                        game.TryMoveSelectedPiece(new Vector2Int(4, 3)), "Black-first pawn moves forward");
                    break;
                case 5:
                    game.RestartToMainMenu();
                    game.BeginGame(PieceTeam.White);
                    Check(game.ApplyFenState("k7/8/8/8/8/8/8/4K2R w K - 0 1"), "Load local castling fixture");
                    Check(game.UsesLocalClassicSession, "Local castling fixture keeps MatchSession authority");
                    Check(Play("e1", "g1") && FindPiece(PieceTeam.White, new Vector2Int(6, 0))?.Type == PieceType.King &&
                        FindPiece(PieceTeam.White, new Vector2Int(5, 0))?.Type == PieceType.Rook,
                        "Local MatchSession castling projects king and rook");
                    break;
                case 6:
                    game.RestartToMainMenu();
                    game.BeginGame(PieceTeam.White);
                    Check(game.ApplyFenState("k7/8/8/3pP3/8/8/8/4K3 w - d6 0 1"), "Load local en-passant fixture");
                    Check(game.UsesLocalClassicSession, "Local en-passant fixture keeps MatchSession authority");
                    Check(Play("e5", "d6") && FindPiece(PieceTeam.White, new Vector2Int(3, 5)) &&
                        !FindPiece(PieceTeam.Black, new Vector2Int(3, 4)),
                        "Local MatchSession en-passant removes the captured pawn");
                    break;
                case 7:
                    game.RestartToMainMenu();
                    game.BeginGame(PieceTeam.White);
                    Check(game.ApplyFenState("7k/P7/8/8/8/8/7p/K7 w - - 0 1"), "Load local promotion fixture");
                    Check(game.UsesLocalClassicSession, "Local promotion fixture keeps MatchSession authority");
                    Check(Play("a7", "a8"), "Start local MatchSession promotion");
                    break;
                case 8:
                    Check(game.HasPendingPromotion, "Local MatchSession promotion waits for choice");
                    Check(CompletePromotion(PieceType.Knight), "Commit local MatchSession promotion");
                    Check(FindPiece(PieceTeam.White, new Vector2Int(0, 7))?.Type == PieceType.Knight &&
                        game.UsesLocalClassicSession && !game.InputLocked,
                        "Local promotion replaces visual and unlocks input");
                    break;
                case 9:
                    game.RestartToMainMenu();
                    game.BeginAramGame();
                    Check(game.IsAramGame && game.InputLocked && !game.UsesLocalClassicSession, "ARAM draft keeps adapter path");
                    break;
                case 10:
                    game.RestartToMainMenu();
                    Check(!game.GameStarted, "Return to menu clears match");
                    Finish(null);
                    break;
            }
            nextAt = EditorApplication.timeSinceStartup + 0.7;
        }
        catch (Exception error) { Finish(error); }
    }

    private static ChessPiece FindPiece(PieceTeam team, Vector2Int square)
    {
        foreach (ChessPiece piece in game.GetActivePiecesForAram(team))
            if (piece.BoardPosition == square) return piece;
        return null;
    }

    private static bool Play(string from, string to)
    {
        var a = ChessButWeird.Domain.FenCodec.ParseSquare(from);
        var b = ChessButWeird.Domain.FenCodec.ParseSquare(to);
        ChessPiece piece = FindPiece(PieceTeam.White, new Vector2Int(a.File, a.Rank));
        return piece && game.TrySelectPiece(piece) &&
            game.TryMoveSelectedPiece(new Vector2Int(b.File, b.Rank));
    }

    private static bool CompletePromotion(PieceType promotionType)
    {
        MethodInfo method = typeof(ChessGame).GetMethod("CompletePromotion",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            return false;

        method.Invoke(game, new object[] { promotionType });
        return !game.InputLocked;
    }

    private static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/architecture-smoke-results.txt", checks);
        if (ignoredSearchStartupError)
            Debug.LogWarning("[ArchitectureSmoke] Ignored known UnityEditor.Search.SearchDatabase startup exception.");
        if (error != null)
        {
            File.AppendAllText("Logs/architecture-smoke-results.txt", "\nFAIL " + error);
            Debug.LogException(error);
        }
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
