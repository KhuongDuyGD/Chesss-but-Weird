// Copied into an isolated Unity project's Editor directory by verify-match-sharing.ps1.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using ChessButWeird.Application;
using ChessButWeird.Domain;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class MatchSharingUnityAudit
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    private static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, Private).GetValue(obj);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
    private static readonly List<string> lines = new List<string>();
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); lines.Add("PASS " + message); }
    public static async void Run()
    {
        GameObject host = null; ChessFontCatalog catalog = null;
        var clipboard = new MatchClipboardAudit();
        int exitCode = 0;
        try
        {
            Directory.CreateDirectory("captures");
            catalog = ScriptableObject.CreateInstance<ChessFontCatalog>();
            Set(catalog, "tmpFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Schoolbell/Schoolbell TMP.asset"));
            Set(catalog, "resultFallbackFont", AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf"));
            typeof(ChessFontCatalog).GetField("cachedCatalog", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, catalog);
            host = new GameObject("Match Sharing Audit");
            var game = host.AddComponent<ChessGame>();
            var menu = host.AddComponent<ChessTurnSelectionUI>(); Set(game, "turnSelectionUI", menu);
            CheckMenuCanvasOwnership(host, game);
            Set(game, "gameOver", true); Set(game, "gameStarted", false); Set(game, "botMode", true);
            Set(game, "status", ChessGame.ChessGameStatus.Win); Set(game, "winningTeam", PieceTeam.White);
            Set(game, "frozenMatchDurationSeconds", 125f);
            var coordinator = ClassicMatchCoordinator.CreateClassic(Team.White, new BoardOrientation(Team.White));
            foreach (string uci in new[] { "e2e4", "e7e5", "g1f3", "b8c6" })
            {
                StockfishMoveAdapter.TryParseUci(uci, out var move); coordinator.Submit(move, out _);
                game.Statistics.Confirm(coordinator.AcceptedMoves.Count, (Team)(1 - (int)coordinator.Turn), uci, null, true);
            }
            Set(game, "localClassicCoordinator", coordinator);
            CheckBotIdentity(game, menu);
            Set(game, "botMode", false); // Arbitrary-name visual cases are local games.
            var pieceRoot = new GameObject("Recorded board"); pieceRoot.transform.SetParent(host.transform);
            Set(game, "runtimePiecesRoot", pieceRoot.transform);
            var pieces = Get<ChessPiece[,]>(game, "pieces");
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            {
                var p = coordinator.Snapshot.Board.GetPiece(new Square(x, y)); if (p.IsEmpty) continue;
                var node = new GameObject(p.Kind.ToString()); node.transform.SetParent(pieceRoot.transform);
                var view = (ChessPiece)node.AddComponent(typeof(ChessPiece).Assembly.GetType(p.Kind + "Piece"));
                view.Initialize((PieceTeam)p.Team, new Vector2Int(x, y)); pieces[x, y] = view;
            }
            foreach (bool chaos in new[] { false, true })
            {
                Set(game, "aramMode", chaos);
                foreach (var outcome in new[] { "win", "loss", "draw" })
                {
                    Set(game, "status", outcome == "draw" ? ChessGame.ChessGameStatus.Draw : ChessGame.ChessGameStatus.Win);
                    Set(game, "winningTeam", outcome == "win" ? PieceTeam.White : PieceTeam.Black);
                    Set(game, "drawReason", outcome == "draw" ? "Draw by agreement" : "");
                    menu.SetMatchPlayers(outcome == "loss" ? chaos ? new string('W', 90) : "A very long player name with letters all the way across the entire width of this match result card" : "Alice",
                        outcome == "draw" ? new string('B', 90) : "Nguyễn • Bob");
                    using (var renderer = new MatchResultCardRenderer())
                    {
                        renderer.Build(game); var camera = Get<Camera>(renderer, "camera");
                        var blackLabel = Get<GameObject>(renderer, "stage").GetComponentsInChildren<TextMeshProUGUI>().First(t => t.name == "Black Player");
                        Check(blackLabel.font.HasCharacters(blackLabel.text, out uint[] missing, true, true), "Player name glyph coverage " + chaos + outcome);
                        camera.Render(); Canvas.ForceUpdateCanvases(); camera.Render();
                        byte[] png = renderer.EncodePng();
                        Check(png.Length > 10000 && png[0] == 137 && png[1] == 80, (chaos ? "Chaos" : "Standard") + " " + outcome + " produces a real PNG");
                        File.WriteAllBytes("captures/" + (chaos ? "chaos-" : "standard-") + outcome + ".png", png);
                        var read = new Texture2D(2, 2); read.LoadImage(png);
                        Check(read.width == 1200 && read.height == 720, "Fixed card resolution " + chaos + outcome);
                        UnityEngine.Object.DestroyImmediate(read);
                        string path = MatchExportFiles.SaveAsync(png, chaos ? "Chaos" : "Classic", ".png").GetAwaiter().GetResult();
                        Check(File.ReadAllBytes(path).Length == png.Length, "PNG persisted without corruption " + chaos + outcome);
                        File.Delete(path);
                    }
                    var modal = host.AddComponent<MatchShareExportView>(); Set(modal, "game", game); Call(modal, "Build");
                    var canvas = Get<GameObject>(modal, "canvasRoot");
                    if (outcome == "win") CaptureModal(canvas, modal, game, chaos);
                    var labels = canvas.GetComponentsInChildren<Button>(true);
                    Check(Array.Exists(labels, b => b.name == "Copy Result") && Array.Exists(labels, b => b.name == "Copy Image") && Array.Exists(labels, b => b.name == "Refresh"), "Result actions available " + chaos + outcome);
                    Check(!Array.Exists(labels, b => b.name == "Share Image" || b.name == "Preview Result"), "Old image share/preview actions removed " + chaos + outcome);
                    Check(Array.Exists(labels, b => b.name == "Export PGN") == !chaos && Array.Exists(labels, b => b.name == "Copy FEN") == !chaos, "Chaos hides notation objects " + chaos + outcome);
                    Call(modal, "CopyResult");
                    Check(GUIUtility.systemCopyBuffer.Contains("Chess but Weird") && GUIUtility.systemCopyBuffer.Contains(chaos ? "CHAOS" : "Local"), "Clipboard confirms real summary " + chaos + outcome);
                    if (outcome == "draw") Check(GUIUtility.systemCopyBuffer.Contains("1/2-1/2"), "Draw score copied " + chaos);
                    foreach (var size in new[] { new Vector2(1280, 720), new Vector2(960, 720), new Vector2(2560, 720), new Vector2(1280, 2276) })
                    {
                        canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                        canvas.GetComponent<CanvasScaler>().enabled = false; ((RectTransform)canvas.transform).sizeDelta = size;
                        Canvas.ForceUpdateCanvases();
                        foreach (var frame in canvas.GetComponentsInChildren<MenuDesignFrame>(true)) Call(frame, "Apply");
                        foreach (var button in labels)
                        {
                            var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                            foreach (var corner in corners)
                                Check(((RectTransform)canvas.transform).rect.Contains(canvas.transform.InverseTransformPoint(corner)), "Action fits " + size + ": " + button.name);
                        }
                    }
                    modal.Close(); UnityEngine.Object.DestroyImmediate(modal);
                }
                if (chaos)
                {
                    Check(!MatchExportService.Export(game.CaptureExportRecord(), ChessExportKind.Pgn).Success && !MatchExportService.Export(game.CaptureExportRecord(), ChessExportKind.Fen).Success, "Chaos direct services reject both formats");
                    bool denied = false; try { game.ExportFen(); } catch (InvalidOperationException) { denied = true; }
                    Check(denied, "Legacy public FEN entry point rejects Chaos");
                }
            }
            Set(game, "aramMode", false); Set(game, "runtimePiecesRoot", null); game.MarkStatisticsTimeUnavailable();
            using (var renderer = new MatchResultCardRenderer())
            {
                renderer.Build(game); Get<Camera>(renderer, "camera").Render();
                var stage = Get<GameObject>(renderer, "stage");
                Check(stage.transform.Find("Share Card Only/Board Frame/Board Unavailable") != null, "Missing board has explicit visual fallback");
                File.WriteAllBytes("captures/missing-metadata.png", renderer.EncodePng());
                Check(!MatchResultSharing.BuildText(game).Contains("02:05"), "Unavailable duration never fabricated");
            }
            Check(MatchImageClipboard.IsSupported, "Windows supports the native image clipboard");
            await CheckAsyncActions(host, game);
            CheckOnlineRecords(game, coordinator);
            var imported = ClassicMatchCoordinator.FromSnapshot(coordinator.Snapshot, new BoardOrientation(Team.White));
            Set(game, "localClassicCoordinator", imported); Set(game, "botMode", false);
            Check(!MatchExportService.Export(game.CaptureExportRecord(), ChessExportKind.Pgn).Success &&
                MatchExportService.Export(game.CaptureExportRecord(), ChessExportKind.Fen).Success, "Middle-game import cannot fabricate a complete match record");
            Set(game, "localClassicCoordinator", coordinator); Set(game, "botMode", true);
            await CheckUrpRendering(game);
            File.WriteAllLines("sharing-audit-result.txt", lines);
            Debug.Log("PASS " + lines.Count + " match sharing checks");
        }
        catch (Exception error) { Debug.LogException(error); File.WriteAllText("sharing-audit-result.txt", "FAIL " + error); exitCode = 1; }
        finally { clipboard.Dispose(); if (host) UnityEngine.Object.DestroyImmediate(host); if (catalog) UnityEngine.Object.DestroyImmediate(catalog); }
        EditorApplication.Exit(exitCode);
    }

    private static void CheckMenuCanvasOwnership(GameObject host, ChessGame game)
    {
        // Production HandDrawnMenuView puts a disabled Canvas on the controller
        // object while a match is running. The result canvas lives on ChessGame.
        var menuRoot = new GameObject("Hidden main menu", typeof(RectTransform), typeof(Canvas));
        menuRoot.transform.SetParent(host.transform, false);
        var mainCanvas = menuRoot.GetComponent<Canvas>(); mainCanvas.enabled = false; mainCanvas.sortingOrder = 40;
        var modal = menuRoot.AddComponent<MatchShareExportView>(); Set(modal, "game", game); Call(modal, "Build");
        try
        {
            var canvas = Get<GameObject>(modal, "canvasRoot").GetComponent<Canvas>();
            Check(canvas.rootCanvas == canvas && canvas.isRootCanvas,
                "Share modal owns its root canvas outside the hidden main-menu canvas");
            Check(canvas.sortingOrder > 2000 && canvas.isActiveAndEnabled,
                "Share modal renders above the result screen");
            Check(Get<TextMeshProUGUI>(modal, "status").canvas.rootCanvas == canvas,
                "Action feedback belongs to the visible sharing canvas");
        }
        finally { modal.Close(); UnityEngine.Object.DestroyImmediate(menuRoot); }
    }

    private static void CheckBotIdentity(ChessGame game, ChessTurnSelectionUI menu)
    {
        Set(game, "botDifficulty", StockfishDifficulty.Beginner);
        menu.SetMatchPlayers("Alice", new string('B', 90));
        foreach (var side in new[] { PieceTeam.White, PieceTeam.Black })
        {
            Set(game, "playerTeam", side);
            var opponent = side == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
            Check(game.StatisticsPlayerName(opponent) == "Pip", "Pip identity ignores stale menu names for " + side);
            Check(game.StatisticsPlayerName(side) == "You", "Anonymous bot player uses current player identity for " + side);
            var record = game.CaptureExportRecord();
            Check((side == PieceTeam.White ? record.Black : record.White) == "Pip",
                "PGN metadata uses the actual Pip opponent for " + side);
            Check(!MatchResultSharing.BuildText(game).Contains("Alice") && !MatchResultSharing.BuildText(game).Contains("BBBB"),
                "Result summary cannot reuse stale fixture participants for " + side);
        }
        Set(game, "playerTeam", PieceTeam.White);
        Set(game, "statisticsBotPlayerName", "Minh Nguyễn");
        Check(game.StatisticsPlayerName(PieceTeam.White) == "Minh Nguyễn" && game.StatisticsPlayerName(PieceTeam.Black) == "Pip",
            "Bot summary retains the current match player name alongside Pip");
        Set(game, "statisticsBotPlayerName", null);
        Set(game, "botDifficulty", StockfishDifficulty.Medium);
    }

    private static async Task CheckUrpRendering(ChessGame game)
    {
        var previousDefault = GraphicsSettings.defaultRenderPipeline;
        var previousQuality = QualitySettings.renderPipeline;
        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        var asset = UniversalRenderPipelineAsset.Create(rendererData);
        try
        {
            GraphicsSettings.defaultRenderPipeline = asset; QualitySettings.renderPipeline = asset;
            EditorApplication.QueuePlayerLoopUpdate(); await Task.Delay(250);
            using (var renderer = new MatchResultCardRenderer())
            {
                renderer.Build(game);
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = renderer.Texture };
                var camera = Get<Camera>(renderer, "camera");
                Check(RenderPipeline.SupportsRenderRequest(camera, request), "URP supports isolated card rendering");
                RenderPipeline.SubmitRenderRequest(camera, request);
                byte[] png = renderer.EncodePng();
                Check(png.Length > 10000, "URP produces a populated PNG");
                File.WriteAllBytes("captures/urp-card.png", png);
                renderer.Freeze(); Check(!camera.enabled, "Prepared preview stops its render camera");
            }
        }
        finally
        {
            GraphicsSettings.defaultRenderPipeline = previousDefault; QualitySettings.renderPipeline = previousQuality;
            UnityEngine.Object.DestroyImmediate(asset); UnityEngine.Object.DestroyImmediate(rendererData);
        }
    }

    private static async Task WaitUntil(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!done()) { if (DateTime.UtcNow > deadline) throw new Exception("Asynchronous UI action timed out."); await Task.Delay(25); }
    }

    private static async Task CheckAsyncActions(GameObject host, ChessGame game)
    {
        var before = new HashSet<string>(Directory.GetFiles(MatchExportFiles.ExportDirectory));
        var ownedFiles = new List<string>();
        var modal = host.AddComponent<MatchShareExportView>(); Set(modal, "game", game); Call(modal, "Build");
        try
        {
            Call(modal, "RefreshNotation");
            await WaitUntil(() => Get<ChessExportResult>(modal, "pgnResult") != null);
            Check(Get<List<Button>>(modal, "notationButtons").All(b => b.interactable), "Valid final record enables all notation actions");
            Set(modal, "png", File.ReadAllBytes("captures/standard-win.png")); Call(modal, "UpdateButtons");
            foreach (var button in Get<List<Button>>(modal, "buttons"))
                Check(button.navigation.mode == Navigation.Mode.Explicit && button.navigation.selectOnDown.transform.IsChildOf(Get<GameObject>(modal, "canvasRoot").transform), "Keyboard navigation stays in the sharing modal: " + button.name);
            Get<TextMeshProUGUI>(modal, "status").text = "Result ready. Copy or save the image.";
            CaptureModal(Get<GameObject>(modal, "canvasRoot"), modal, game, false);
            Call(modal, "ExportNotation", ChessExportKind.Pgn, true);
            Check(Get<bool>(modal, "busy") && Get<List<Button>>(modal, "buttons").All(b => !b.interactable), "Export disables repeated clicks immediately");
            Call(modal, "ExportNotation", ChessExportKind.Fen, false);
            await WaitUntil(() => !Get<bool>(modal, "busy"));
            Check(GUIUtility.systemCopyBuffer.Contains("[Result \"1/2-1/2\"]") && GUIUtility.systemCopyBuffer.Contains("1. e4 e5"), "Copy PGN writes real notation to the clipboard");
            Call(modal, "ExportNotation", ChessExportKind.Fen, true);
            await WaitUntil(() => !Get<bool>(modal, "busy"));
            Check(GUIUtility.systemCopyBuffer == MatchExportService.Export(game.CaptureExportRecord(), ChessExportKind.Fen).Text, "Copy FEN writes the authoritative final position");
            foreach (var kind in new[] { ChessExportKind.Pgn, ChessExportKind.Fen })
            {
                Call(modal, "ExportNotation", kind, false); await WaitUntil(() => !Get<bool>(modal, "busy"));
                var extension = kind == ChessExportKind.Pgn ? ".pgn" : ".fen";
                string path = Get<TextMeshProUGUI>(modal, "status").text.Split('\n').Last();
                Check(!before.Contains(path) && path.EndsWith(extension) && File.ReadAllText(path) == MatchExportService.Export(game.CaptureExportRecord(), kind).Text, "Save " + kind + " writes the verified file once");
                ownedFiles.Add(path);
            }
            Set(modal, "png", File.ReadAllBytes("captures/standard-win.png"));
            var filesBeforeCopy = new HashSet<string>(Directory.GetFiles(MatchExportFiles.ExportDirectory));
            Call(modal, "CopyImage"); await WaitUntil(() => !Get<bool>(modal, "busy"));
            Check(Get<TextMeshProUGUI>(modal, "status").text.Contains("Image copied"), "Copy Image reports a successful image copy");
            MatchClipboardAudit.CheckImage(Get<byte[]>(modal, "png"), Check);
            Check(filesBeforeCopy.SetEquals(Directory.GetFiles(MatchExportFiles.ExportDirectory)), "Copy Image does not save an unwanted export file");
            Set(game, "aramMode", true);
            Call(modal, "ExportNotation", ChessExportKind.Fen, true); await WaitUntil(() => !Get<bool>(modal, "busy"));
            Check(Get<TextMeshProUGUI>(modal, "status").text.Contains("Chaos"), "Direct UI export is revalidated when the source mode changes");
            Set(game, "aramMode", false);
        }
        finally
        {
            modal.Close(); UnityEngine.Object.DestroyImmediate(modal);
            foreach (var path in ownedFiles) File.Delete(path);
        }
    }

    private static void CheckOnlineRecords(ChessGame game, ClassicMatchCoordinator coordinator)
    {
        var state = new ChessButWeird.Online.MatchState {
            matchId = "export-audit", status = "Finished", turn = coordinator.Turn.ToString(), eventSequence = 4,
            fen = FenCodec.Write(coordinator.Snapshot), settings = new ChessButWeird.Online.GameSettings { mode = "Classic" },
            players = new List<ChessButWeird.Online.Player> { new ChessButWeird.Online.Player { userId = "A", color = "White" }, new ChessButWeird.Online.Player { userId = "B", color = "Black" } }
        };
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
        {
            var p = coordinator.Snapshot.Board.GetPiece(new Square(x, y)); if (p.IsEmpty) continue;
            state.board.Add(new ChessButWeird.Online.Piece { id = p.Id, square = new Square(x,y).ToString(), team = p.Team.ToString(), kind = p.Kind.ToString() });
        }
        Set(game, "dotNetState", state);
        var records = new List<ChessButWeird.Online.MoveRecord>(); int i = 0;
        foreach (var move in coordinator.AcceptedMoves)
        {
            records.Add(new ChessButWeird.Online.MoveRecord { id = "move-" + i, matchId = state.matchId, userId = i % 2 == 0 ? "A" : "B", kind = "Move", sequence = ++i,
                payloadJson = "{\"from\":\"" + move.From + "\",\"to\":\"" + move.To + "\"}" });
        }
        game.RestoreDotNetMoves(records, state);
        Check(MatchExportService.Export(game.CaptureExportRecord(), ChessExportKind.Pgn).Success, "Recovered online records reconstruct final PGN");
        game.RestoreDotNetMoves(records.Take(3).ToList(), state);
        Check(!MatchExportService.Export(game.CaptureExportRecord(), ChessExportKind.Pgn).Success && MatchExportService.Export(game.CaptureExportRecord(), ChessExportKind.Fen).Success, "Truncated online history rejects PGN and preserves valid FEN");
        state.board.RemoveAt(0);
        Check(!MatchExportService.Export(game.CaptureExportRecord(), ChessExportKind.Fen).Success, "Conflicting authoritative online board/FEN is rejected");
        state.settings.mode = "Chaos";
        Check(!MatchExportPolicy.ShowNotation(game.CaptureExportRecord()), "Backend Chaos identifier blocks notation even without a chaos payload");
        state.settings = null;
        Check(MatchResultSharing.BuildText(game).Contains("Unknown mode"), "Missing online mode metadata can still share a result");
        Set(game, "dotNetState", null);
    }

    private static void CaptureModal(GameObject root, MatchShareExportView modal, ChessGame game, bool chaos)
    {
        using (var renderer = new MatchResultCardRenderer())
        {
            renderer.Build(game); Get<Camera>(renderer, "camera").Render();
            Get<RawImage>(modal, "preview").texture = renderer.Texture;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            root.GetComponent<CanvasScaler>().enabled = false;
            ((RectTransform)root.transform).sizeDelta = new Vector2(1920, 1080);
            root.transform.position = new Vector3(38000, 38000, 38000); root.transform.localScale = Vector3.one * .01f;
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 30;
            foreach (var frame in root.GetComponentsInChildren<MenuDesignFrame>(true)) Call(frame, "Apply");
            Canvas.ForceUpdateCanvases();
            var cameraHost = new GameObject("Modal Capture Camera"); var camera = cameraHost.AddComponent<Camera>();
            camera.transform.position = root.transform.position - Vector3.forward * 10;
            camera.orthographic = true; camera.orthographicSize = 5.4f; camera.cullingMask = 1 << 30;
            camera.nearClipPlane = .1f; camera.farClipPlane = 20; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
            var rt = new RenderTexture(1920, 1080, 24); rt.Create(); camera.targetTexture = rt;
            camera.Render();
            var old = RenderTexture.active; RenderTexture.active = rt;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
            File.WriteAllBytes("captures/" + (chaos ? "chaos" : "standard") + "-modal.png", image.EncodeToPNG());
            RenderTexture.active = old; camera.targetTexture = null; rt.Release();
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(cameraHost);
        }
    }
}
