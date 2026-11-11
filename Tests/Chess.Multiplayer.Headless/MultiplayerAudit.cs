using System;
using System.Collections;
using System.IO;
using System.Reflection;
using ChessButWeird.Online;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class MultiplayerAudit
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static Piece P(int id, string kind, string team, string square) => new Piece { id = id, kind = kind, team = team, square = square };

    public static void Run()
    {
        var lines = new System.Collections.Generic.List<string>();
        GameObject host = null, kingHost = null;
        AudioClip move = null, capture = null, castle = null;
        try
        {
            // Edit mode only: no Start/auth, network, bootstrap scene or account data.
            host = new GameObject("Multiplayer regression fixture");
            var game = host.AddComponent<ChessGame>();
            var controller = host.AddComponent<ChessLanController>();
            controller.Initialize(game, null);
            foreach (string outcome in new[] { "WhiteWin", "BlackWin", "Draw" })
            {
                var session = (OnlineSession)Call(controller, "EnsureOnline");
                typeof(OnlineSession).GetProperty("State").SetValue(session, new MatchState { matchId = outcome, status = "Finished" });
                Set(game, "dotNetState", new MatchState { matchId = outcome, status = "Finished" });
                foreach (string flag in new[] { "gameOver", "matchEnding", "serverAuthoritativeMode", "pauseLocked" }) Set(game, flag, true);
                Time.timeScale = 0;
                game.RestartToMainMenu();
                Check(!game.GameStarted && !Get<bool>(game, "gameOver") && !Get<bool>(game, "matchEnding"), outcome + " retained terminal flags");
                Check(!game.UsesDotNetOnline && !Get<bool>(game, "serverAuthoritativeMode") && Time.timeScale == 1, outcome + " retained online/pause state");
                Check(Get<OnlineSession>(controller, "online") == null && !Get<bool>(controller, "requestInFlight"), outcome + " retained online controller session");
            }
            lines.Add("PASS win/loss/draw -> menu with unloaded board clears match flags and disposes online session");

            var menu = host.AddComponent<ChessTurnSelectionUI>();
            // Edit mode has no persistent-object lifecycle. Supply the real
            // manager instance so menu routing can run without DontDestroyOnLoad.
            var music = host.AddComponent<GameMusicManager>();
            typeof(GameMusicManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, music);
            Set(menu, "chessGame", game);
            menu.ReturnToMainMenuFromResult();
            Check(Get<object>(menu, "state").ToString() == "TransitionToTurnSelection", "Result exit returned to the primary title menu instead of the play hub");
            lines.Add("PASS result menu exit returns to the play hub (main menu 2)");

            move = AudioClip.Create("move", 32, 1, 8000, false);
            capture = AudioClip.Create("capture", 32, 1, 8000, false);
            castle = AudioClip.Create("castle", 32, 1, 8000, false);
            Set(game, "moveSound", move); Set(game, "hitSound", capture); Set(game, "castleSound", castle);
            var before = new MatchState { turn = "White", board = new System.Collections.Generic.List<Piece> { P(1, "Pawn", "White", "e2") } };
            var after = new MatchState { turn = "Black", board = new System.Collections.Generic.List<Piece> { P(1, "Pawn", "White", "e4") } };
            Check(Call(game, "OnlineMoveClip", before, after) == move, "Online move SFX missing");
            Check(Call(game, "OnlineMoveClip", after, after) == null, "Duplicate snapshot replayed move SFX");
            before.board.Add(P(2, "Pawn", "Black", "e4"));
            Check(Call(game, "OnlineMoveClip", before, after) == capture, "Online capture SFX missing");
            before.board[1].square = "d4";
            Check(Call(game, "OnlineMoveClip", before, after) == capture, "En passant removal SFX missing");
            before.board.Clear(); after.board.Clear();
            before.board.Add(P(1, "King", "White", "e1")); before.board.Add(P(2, "Rook", "White", "h1"));
            after.board.Add(P(1, "King", "White", "g1")); after.board.Add(P(2, "Rook", "White", "f1"));
            Check(Call(game, "OnlineMoveClip", before, after) == castle, "Online castling SFX missing");
            lines.Add("PASS move/capture/en-passant/castling SFX selection and unchanged-snapshot suppression");

            kingHost = new GameObject("Checked king");
            var king = kingHost.AddComponent<KingPiece>(); king.Initialize(PieceTeam.Black, new Vector2Int(4, 7));
            var check = new MatchState { status = "InProgress", turn = "Black", checkedTeam = "Black", chaos = new JObject() };
            Set(game, "dotNetState", check); Set(game, "gameStarted", true); Set(game, "gameOver", false);
            Set(game, "checkedKing", king); Set(game, "checkedKingOriginalScale", Vector3.one);
            var pulse = (IEnumerator)Call(game, "AnimateCheckedKing", king, PieceTeam.Black);
            Check(pulse.MoveNext(), "Online check coroutine stopped immediately");
            Check(king.transform.localScale != Vector3.one, "Online king did not pulse");
            check.checkedTeam = null;
            Check(!pulse.MoveNext() && king.transform.localScale == Vector3.one, "Check pulse did not reset when check cleared");
            lines.Add("PASS authoritative ARAM check drives king pulse and restores scale when check clears");

            Set(controller, "directMatchmaking", true);
            foreach (var mode in new[] { PlayModeOption.ClassicNormal, PlayModeOption.ChaosNormal, PlayModeOption.ClassicRanked, PlayModeOption.ChaosRanked })
            {
                Set(controller, "selectedOnlineMode", mode);
                Check(((LobbyRequest)Call(controller, "NewRequest")).rated == PlayModeNames.IsRanked(mode), "Wrong rated request for " + mode);
            }
            Set(controller, "directMatchmaking", false);
            Check(!((LobbyRequest)Call(controller, "NewRequest")).rated, "Custom room requested ranked play");
            lines.Add("PASS normal/ranked/custom request classification");
            File.WriteAllLines("audit-result.txt", lines);
            foreach (string line in lines) Debug.Log(line);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error); File.WriteAllText("audit-result.txt", "FAIL " + error); EditorApplication.Exit(1);
        }
        finally
        {
            if (host) UnityEngine.Object.DestroyImmediate(host);
            if (kingHost) UnityEngine.Object.DestroyImmediate(kingHost);
            foreach (var clip in new[] { move, capture, castle }) if (clip) UnityEngine.Object.DestroyImmediate(clip);
        }
    }
}
