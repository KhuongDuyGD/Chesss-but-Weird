using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UI = SketchbookUI;

/// <summary>Local UI fixtures only. Does not connect, queue, equip, spend currency or start a match.</summary>
public static class PlayFlowAudit
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly List<string> checks = new List<string>();
    public static void RunMatchDialogs()
    {
        checks.Clear();
        var scene = EditorSceneManager.NewPreviewScene();
        var host = new GameObject("Match dialog fixture", typeof(RectTransform), typeof(Canvas));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, scene);
        try
        {
            var game = host.AddComponent<ChessGame>();
            var owner = host.AddComponent<ChessTurnSelectionUI>();
            Set(owner, "chessGame", game); Set(game, "turnSelectionUI", owner);
            foreach (PieceTeam team in new[] { PieceTeam.White, PieceTeam.Black })
            foreach (PieceType expected in new[] { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight })
            {
                int count = 0; PieceType? actual = null;
                owner.ShowPromotionChoice(team, type => { count++; actual = type; });
                var promotion = Field<PromotionMenuView>(owner, "promotionMenu");
                var canvas = Field<GameObject>(promotion, "canvasRoot");
                Require(canvas.GetComponentsInChildren<Button>().Length == 4 &&
                    canvas.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == team.ToString().ToUpperInvariant() + " / PAWN PROMOTION"),
                    team + " promotion shows four choices and the correct side");
                Button(canvas.transform, "Promote to " + expected).onClick.Invoke();
                foreach (var button in canvas.GetComponentsInChildren<Button>(true)) button.onClick.Invoke();
                Require(actual == expected && count == 1 && !canvas.activeSelf,
                    team + " promotion dispatches " + expected + " exactly once and closes");
                Require(Field<object>(owner, "state").ToString() == "Playing", "Promotion selection returns UI to playing");
            }
            var popup = Field<PromotionMenuView>(owner, "promotionMenu");
            var popupCanvas = Field<GameObject>(popup, "canvasRoot");
            int cancelled = 0;
            owner.ShowPromotionChoice(PieceTeam.White, _ => cancelled++);
            owner.SetTurn(PieceTeam.Black);
            Button(popupCanvas.transform, "Promote to Queen").onClick.Invoke();
            Require(cancelled == 0 && !popupCanvas.activeSelf, "A turn update revokes stale promotion choices");
            owner.ShowPromotionChoice(PieceTeam.White, _ => cancelled++);
            owner.ShowDraw(PieceTeam.White, "Stalemate");
            Button(popupCanvas.transform, "Promote to Queen").onClick.Invoke();
            Require(cancelled == 0 && !popupCanvas.activeSelf, "An ended match revokes a pending promotion");
            int stages = 0;
            owner.ShowPromotionChoice(PieceTeam.White, _ =>
            {
                stages++;
                owner.ShowPromotionChoice(PieceTeam.Black, _ => stages++);
            });
            Button(popupCanvas.transform, "Promote to Queen").onClick.Invoke();
            Require(popupCanvas.activeSelf, "A reentrant game update can open a fresh promotion");
            Button(popupCanvas.transform, "Promote to Knight").onClick.Invoke();
            Require(stages == 2 && !popupCanvas.activeSelf, "A fresh promotion does not retain the previous callback");
            owner.ShowPromotionChoice(PieceTeam.Black, _ => { });
            CheckLayout((RectTransform)popupCanvas.GetComponentInChildren<MenuDesignFrame>(true).transform, new Vector2(1920, 1080));
            popup.Hide();

            var result = host.AddComponent<ResultMenuView>();
            result.Initialize(owner, null, game); Set(owner, "resultMenu", result);
            var resultCanvas = Field<GameObject>(result, "canvasRoot");
            owner.SetMatchPlayers("White player with a long display name", "Black player with a long display name");
            foreach (ResultMenuView.ResultKind kind in Enum.GetValues(typeof(ResultMenuView.ResultKind)))
            {
                Call(result, "Present", kind, null); resultCanvas.SetActive(true);
                string heading = Field<TextMeshProUGUI>(result, "title").text;
                Require(kind == ResultMenuView.ResultKind.Win ? heading == "YOU WIN!" :
                    kind == ResultMenuView.ResultKind.Lose ? heading == "YOU LOSE" :
                    kind == ResultMenuView.ResultKind.Stalemate ? heading == "STALEMATE" : heading == "IT'S A DRAW",
                    kind + " result has its own title");
                Require(resultCanvas.GetComponentsInChildren<Button>().Length == 3 &&
                    Button(resultCanvas.transform, "New Game").GetComponentInChildren<TextMeshProUGUI>().text == "PLAY AGAIN",
                    kind + " result offers local replay, view board and main menu");
                CheckLayout(Field<RectTransform>(result, "contentRoot"), new Vector2(1920, 1080));
            }
            Call(result, "Present", ResultMenuView.ResultKind.Win, "Black wins");
            Require(Field<TextMeshProUGUI>(result, "title").text == "BLACK WINS", "Hotseat results identify the winning side without claiming You Win");
            Set(game, "dotNetState", new ChessButWeird.Online.MatchState());
            Call(result, "Present", ResultMenuView.ResultKind.Win, null);
            Require(Button(resultCanvas.transform, "New Game").GetComponentInChildren<TextMeshProUGUI>().text == "REQUEST REMATCH",
                "Online results offer a server rematch request");
            Set(game, "dotNetState", null);
            Button(resultCanvas.transform, "Spectated Game").onClick.Invoke();
            Button(resultCanvas.transform, "Main Menu").onClick.Invoke();
            Require(!resultCanvas.activeSelf && Field<object>(owner, "state").ToString() == "Spectating",
                "View Board hides the result and prevents a second queued result action");
            Directory.CreateDirectory("Logs/match-dialogs");
            File.WriteAllLines("Logs/match-dialogs/checks.txt", checks);
            Debug.Log("[MatchDialogAudit] PASS " + checks.Count + " local dialog checks. No API calls, screenshots or live matches.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    public static void Run()
    {
        checks.Clear();
        var account = typeof(PlayerAuthService).GetProperty("CurrentApiUser");
        var guest = typeof(PlayerAuthService).GetProperty("IsGuestSession");
        object previousAccount = account.GetValue(null), previousGuest = guest.GetValue(null);
        var scene = EditorSceneManager.NewPreviewScene();
        var host = new GameObject("Play flow fixture", typeof(RectTransform), typeof(Canvas));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, scene);
        ((RectTransform)host.transform).sizeDelta = new Vector2(1920, 1080);
        object lobby = null; ChessLanController network = null; GameObject networkCanvas = null;
        try
        {
            account.SetValue(null, null); guest.SetValue(null, true);
            int hubPlay = 0, inventoryClicks = 0, gachaClicks = 0;
            var hubRoot = UI.Node(host.transform, "Hub", new Rect(0, 0, 1920, 1080));
            var hub = hubRoot.gameObject.AddComponent<ModeSelectionPresentation>();
            hub.Build(MainMenuLogoOriginal.LoadSprite(), new ModeSelectionPresentation.Actions
            {
                Play = () => hubPlay++, Inventory = () => inventoryClicks++, Gacha = () => gachaClicks++
            });
            var hubButtons = hubRoot.GetComponentsInChildren<Button>(true);
            foreach (string name in new[] { "Play", "Gacha", "Inventory" })
            {
                var button = hubButtons.Single(b => b.name == name);
                var rect = (RectTransform)button.transform;
                Require(rect.sizeDelta.x >= 500 && rect.sizeDelta.y >= 450, name + " is a large hub card");
                button.onClick.Invoke();
            }
            Require(hubPlay == 1 && inventoryClicks == 1 && gachaClicks == 1, "Hub cards dispatch exactly one action");
            Require(!hubButtons.Any(b => b.name == "Local" || b.name == "Online" || b.name == "Aram"), "Hub merges the old three mode buttons into Play");

            var modesRoot = UI.Node(host.transform, "Modes", new Rect(0, 0, 1920, 1080));
            var modes = modesRoot.gameObject.AddComponent<PlayModeSelectionView>();
            PlayModeOption? launched = null;
            modes.Build(() => { }, selected => launched = selected);
            var play = Button(modesRoot, "Play Selected Mode");
            Require(!play.interactable && !modes.SelectedMode.HasValue, "Mode selection requires a checked row");
            Button(modesRoot, "PVE Card").onClick.Invoke();
            Require(!play.interactable, "Opening PVE alone does not launch a mode");
            modesRoot.GetComponentsInChildren<Toggle>(true).Single(t => t.name == "PVE Bot Rush").isOn = true;
            play.onClick.Invoke();
            Require(launched == PlayModeOption.PveBotRush, "PVE Bot Rush dispatches through Play");
            Button(modesRoot, "CHAOS Card").onClick.Invoke();
            Require(!modes.SelectedMode.HasValue && !play.interactable, "Changing category clears a stale row selection");
            modesRoot.GetComponentsInChildren<Toggle>(true).Single(t => t.name == "CHAOS Normal").isOn = true;
            play.onClick.Invoke();
            Require(launched == PlayModeOption.ChaosNormal, "Chaos Normal dispatches through Play");
            var ranked = modesRoot.GetComponentsInChildren<Toggle>(true).Where(t => t.name.EndsWith("Ranked")).ToArray();
            Require(ranked.Length == 2 && ranked.All(t => t.interactable && !t.isOn), "Both Ranked choices are available without selecting a row automatically");
            Button(modesRoot, "CLASSIC Card").onClick.Invoke();
            modesRoot.GetComponentsInChildren<Toggle>(true).Single(t => t.name == "CLASSIC Normal").isOn = true;
            play.onClick.Invoke();
            Require(launched == PlayModeOption.ClassicNormal, "Classic Normal dispatches through Play");
            Button(modesRoot, "CUSTOM Card").onClick.Invoke();
            Require(!play.interactable && !modes.SelectedMode.HasValue, "Opening CUSTOM clears the previous category selection");
            var create = modesRoot.GetComponentsInChildren<Toggle>(true).Single(t => t.name == "CUSTOM Create room");
            var browse = modesRoot.GetComponentsInChildren<Toggle>(true).Single(t => t.name == "CUSTOM Browser room");
            create.isOn = true; play.onClick.Invoke();
            Require(launched == PlayModeOption.CustomCreateRoom, "CUSTOM Create room dispatches through Play");
            browse.isOn = true; play.onClick.Invoke();
            Require(launched == PlayModeOption.CustomBrowserRoom && !create.isOn, "Browser room switches selection without keeping Create checked");
            // Check rendered geometry/alpha, not just the toggle's logical state.
            foreach (var selected in modesRoot.GetComponentsInChildren<Toggle>(true).Where(t => t.interactable))
            {
                Button(modesRoot, selected.name.Split(' ')[0] + " Card").onClick.Invoke();
                selected.isOn = true;
                var tick = selected.graphic as SketchbookDoodle;
                using (var mesh = new VertexHelper())
                {
                    typeof(SketchbookDoodle).GetMethod("OnPopulateMesh", Fields, null, new[] { typeof(VertexHelper) }, null).Invoke(tick, new object[] { mesh });
                    Require(mesh.currentVertCount > 0 && tick.canvasRenderer.GetAlpha() > .99f,
                        selected.name + " displays a vector check when selected");
                }
                selected.isOn = false;
                Require(tick.canvasRenderer.GetAlpha() == 0, selected.name + " hides its check when unselected");
            }
            Require(PlayModeNames.Backend(PlayModeOption.ChaosNormal) == "Chaos" && PlayModeNames.DisplayBackend("Aram") == "Chaos", "Chaos uses the canonical server ruleset key and displays legacy history");

            var loading = host.AddComponent<LoadingManager>();
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/Game/Data/CosmeticCatalog.asset");
            typeof(LoadingManager).GetProperty("Catalog").SetValue(loading, catalog);
            var loadoutRoot = UI.Node(host.transform, "Loadout", new Rect(0, 0, 1672, 941));
            var picker = loadoutRoot.gameObject.AddComponent<MatchLoadoutController>();
            picker.Initialize(loadoutRoot, loading);
            int started = 0, cancelled = 0;
            picker.Open(true, PieceTeam.White, selection => started++, () => { });
            picker.ConfigureLobby("PVE / BOT RUSH", "FIND MATCH");
            Require(loadoutRoot.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t => t.text.Contains("PVE / BOT RUSH")), "PVE mode is visible above the loadout");
            var find = Field<Button>(picker, "startButton");
            picker.ConfigureLobby("CHAOS / NORMAL", "FIND MATCH");
            picker.SetOnlineContext(() => false, "Fixture");
            picker.SetMatchmakingState(true, true, "01:23", false, () => cancelled++);
            Require(find.interactable && find.GetComponentInChildren<TextMeshProUGUI>().text == "01:23   X", "Find Match becomes an elapsed-time cancel button");
            find.onClick.Invoke();
            Require(cancelled == 1 && started == 0, "One search-button click cancels without re-submitting equipment");
            Require(Field<RectTransform>(picker, "summaryRoot").GetComponentsInChildren<Button>().All(b => !b.interactable) &&
                !Field<Button>(picker, "effectButton").interactable, "Queueing locks board, chess and effect changes");
            picker.SetMatchmakingState(true, true, "01:24", true, () => cancelled++);
            find.onClick.Invoke();
            Require(cancelled == 1 && !find.interactable, "Pending cancellation blocks duplicate clicks");
            picker.SetOnlineContext(() => true, "");
            picker.SetMatchmakingState(false, false, "", false, () => cancelled++);
            Require(find.interactable && find.GetComponentInChildren<TextMeshProUGUI>().text == "FIND MATCH", "Cancellation returns to Find Match on the same loadout");
            Require(Field<Button>(picker, "effectButton").interactable, "Cancelled search unlocks the loadout");

            var confirmRoot = UI.Node(host.transform, "Confirmation", new Rect(0, 0, 1672, 941));
            var confirmation = confirmRoot.gameObject.AddComponent<MatchFoundConfirmationView>();
            int yes = 0, no = 0;
            confirmation.Build(() => yes++, () => no++);
            confirmation.SetState(true, "Fixture opponent", 19.5f, false, false, true);
            Require(confirmRoot.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == "Are you sure??"), "Found-match modal asks Are you sure??");
            Button(confirmRoot, "Yes").onClick.Invoke();
            Require(yes == 1 && no == 0, "Yes dispatches acceptance once");
            confirmation.SetState(true, "Fixture opponent", 18, true, false, true);
            Button(confirmRoot, "Yes").onClick.Invoke();
            Require(yes == 1 && !Button(confirmRoot, "Yes").interactable, "Accepted state prevents duplicate acceptance");
            Button(confirmRoot, "No").onClick.Invoke();
            Require(no == 1, "No dispatches decline");
            confirmation.SetState(true, "Fixture opponent", 0, false, false, true);
            Require(!Button(confirmRoot, "Yes").interactable && !Button(confirmRoot, "No").interactable, "Expired acceptance disables both actions");
            confirmation.SetState(false, "", 0, false, false, false);
            Require(!confirmRoot.gameObject.activeSelf, "Confirmation hides after leaving acceptance");

            var ownerRoot = new GameObject("PVE owner");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ownerRoot, scene);
            var owner = ownerRoot.AddComponent<ChessTurnSelectionUI>();
            Set(owner, "pendingPveLoadout", new CosmeticSelection { whiteSkinId = "ChessSets/CrystalBlue", boardId = "Boards/Celestial", moveEffectId = "confetti" });
            var black = (CosmeticSelection)Call(owner, "PreparePveLoadout", PieceTeam.Black);
            var white = (CosmeticSelection)Call(owner, "PreparePveLoadout", PieceTeam.White);
            Require(black.blackSkinId == "ChessSets/CrystalBlue" && black.whiteSkinId == CosmeticSelection.LowPolyId, "Choosing Black carries the PVE lobby set to Black and keeps bot pieces default");
            Require(white.whiteSkinId == "ChessSets/CrystalBlue" && white.blackSkinId == CosmeticSelection.LowPolyId &&
                white.boardId == "Boards/Celestial" && black.moveEffectId == "confetti", "Choosing either side preserves the PVE board/effect");

            var game = host.AddComponent<ChessGame>();
            network = host.AddComponent<ChessLanController>(); Set(network, "chessGame", game);
            SetPage(network, "Loadout"); Set(network, "requestedGameMode", "Chaos");
            var type = typeof(ChessLanController).GetNestedType("NetworkLobbyUiController", BindingFlags.NonPublic);
            lobby = Activator.CreateInstance(type, Fields, null, new object[] { network }, null);
            Set(network, "lobbyUi", lobby);
            networkCanvas = Field<GameObject>(lobby, "canvasRoot");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(networkCanvas, scene);
            Call(lobby, "Show", NetworkLobbyUiMode.Multiplayer);
            var retained = Field<MatchLoadoutController>(lobby, "loadoutPicker");
            Require(networkCanvas.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t => t.text.Contains("CHAOS / NORMAL")), "Online lobby displays the chosen Chaos mode");
            SetPage(network, "Searching"); Call(lobby, "Refresh");
            Require(Field<MatchLoadoutController>(lobby, "loadoutPicker") == retained, "Entering search preserves the loadout instance");
            SetPage(network, "Accept"); Call(lobby, "Refresh");
            Require(Field<MatchLoadoutController>(lobby, "loadoutPicker") == retained &&
                networkCanvas.GetComponentInChildren<MatchFoundConfirmationView>(true).gameObject.activeSelf, "Match found overlays the same lobby");
            SetPage(network, "Loadout"); Call(lobby, "Refresh");
            Require(Field<MatchLoadoutController>(lobby, "loadoutPicker") == retained &&
                !networkCanvas.GetComponentInChildren<MatchFoundConfirmationView>(true).gameObject.activeSelf, "Decline/cancel returns to the same lobby without an inventory reload");
            Set(network, "loadoutOpenVersion", Field<int>(network, "loadoutOpenVersion") + 1);
            SetPage(network, "Loadout"); Call(lobby, "Refresh");
            var reopened = Field<MatchLoadoutController>(lobby, "loadoutPicker");
            Require(reopened != retained, "Explicitly reopening a lobby reloads the collection");
            Set(network, "lifecycle", Field<int>(network, "lifecycle") + 1);
            SetPage(network, "Loadout"); Call(lobby, "Refresh");
            Require(Field<MatchLoadoutController>(lobby, "loadoutPicker") != reopened, "Authentication lifecycle changes cannot reuse another session's collection");
            Set(network, "customRoomEntry", (PlayModeOption?)PlayModeOption.CustomCreateRoom);
            Set(network, "loadoutOpenVersion", Field<int>(network, "loadoutOpenVersion") + 1);
            SetPage(network, "Loadout"); Call(lobby, "Refresh");
            Require(networkCanvas.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t => t.text.Contains("CUSTOM / CREATE ROOM")), "Custom creation is identified above the shared loadout");
            Call(network, "CustomRoomDestination");
            Require(networkCanvas.GetComponentsInChildren<Button>(true).Any(b => b.name == "CREATE ROOM") &&
                !networkCanvas.GetComponentsInChildren<Button>(true).Any(b => b.name == "FIND ROOM"), "Custom creation reuses the room configuration and create action");
            Set(network, "customRoomEntry", (PlayModeOption?)PlayModeOption.CustomBrowserRoom);
            SetPage(network, "FindRoom"); Call(lobby, "Refresh");
            Require(networkCanvas.GetComponentInChildren<TMP_InputField>(true) && networkCanvas.GetComponentInChildren<ScrollRect>(true) &&
                networkCanvas.GetComponentsInChildren<Button>(true).Any(b => b.name == "Join"), "Custom browser reuses the room list and room-code join controls");
            CheckLayout(Field<RectTransform>(lobby, "contentRoot"), new Vector2(1672, 941));

            var hud = host.AddComponent<AnalysisBoardView>(); hud.Initialize(game);
            var hudCanvas = Field<GameObject>(hud, "canvasObject");
            var hudRoot = Field<RectTransform>(hud, "root");
            hudRoot.GetComponent<ResponsiveSafeArea>().enabled = false;
            hudRoot.anchorMin = hudRoot.anchorMax = Vector2.zero; hudRoot.sizeDelta = new Vector2(1280, 720);
            foreach (bool bot in new[] { false, true })
            {
                Set(game, "botMode", bot); Call(hud, "Layout");
                var cameraHint = Field<RectTransform>(hud, "cameraHint");
                var lowerLeft = cameraHint.anchoredPosition - Vector2.Scale(cameraHint.sizeDelta, cameraHint.pivot);
                Require(Vector2.Distance(lowerLeft, new Vector2(12, 12)) < .1f,
                    "Camera hint stays 12 units from the safe lower-left corner (bot=" + bot + ")");
            }
            // Avoid deferred destruction in this edit-mode fixture.
            Set(hud, "canvasObject", null); UnityEngine.Object.DestroyImmediate(hudCanvas);
            CheckLayout(hubRoot, new Vector2(1920, 1080));
            CheckLayout(modesRoot, new Vector2(1920, 1080));
            CheckLayout(loadoutRoot, new Vector2(1672, 941));
            confirmation.SetState(true, "Fixture", 30, false, false, true);
            CheckLayout(confirmRoot, new Vector2(1672, 941));
            Directory.CreateDirectory("Logs/play-flow");
            File.WriteAllLines("Logs/play-flow/checks.txt", checks);
            Debug.Log("[PlayFlowAudit] PASS " + checks.Count + " local UI checks. No API calls, screenshots or live matches.");
        }
        finally
        {
            if (network) Set(network, "lobbyUi", null);
            if (networkCanvas) UnityEngine.Object.DestroyImmediate(networkCanvas);
            UnityEngine.Object.DestroyImmediate(host);
            EditorSceneManager.ClosePreviewScene(scene);
            account.SetValue(null, previousAccount); guest.SetValue(null, previousGuest);
        }
    }
    private static Button Button(Transform root, string name) => root.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
    private static void CheckLayout(RectTransform page, Vector2 designSize)
    {
        var parent = page.parent;
        var min = page.anchorMin; var max = page.anchorMax; var pivot = page.pivot;
        var position = page.anchoredPosition; var size = page.sizeDelta; var scale = page.localScale;
        var viewport = UI.Node(parent, "Layout fixture", new Rect(0, 0, 1920, 1080));
        var frame = MenuDesignFrame.Create(viewport, "Layout", designSize);
        page.SetParent(frame, false); page.anchorMin = Vector2.zero; page.anchorMax = Vector2.one;
        page.offsetMin = page.offsetMax = Vector2.zero;
        try
        {
            foreach (Vector2 resolution in new[] { new Vector2(1920, 1080), new Vector2(1280, 720), new Vector2(800, 600), new Vector2(1080, 1920) })
            {
                viewport.sizeDelta = resolution;
                Call(frame.GetComponent<MenuDesignFrame>(), "Apply");
                Canvas.ForceUpdateCanvases();
                foreach (var fitter in page.GetComponentsInChildren<InventoryContentRootFitter>(true)) Call(fitter, "Apply");
                Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4];
                foreach (var control in page.GetComponentsInChildren<Selectable>())
                {
                    ((RectTransform)control.transform).GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = viewport.InverseTransformPoint(corner);
                        var bounds = viewport.rect;
                        if (point.x < bounds.xMin - 1 || point.x > bounds.xMax + 1 || point.y < bounds.yMin - 1 || point.y > bounds.yMax + 1)
                            throw new InvalidOperationException(page.name + " clips " + control.name + " at " + resolution);
                    }
                }
                Require(true, page.name + " controls fit " + resolution);
            }
        }
        finally
        {
            page.SetParent(parent, false); page.anchorMin = min; page.anchorMax = max; page.pivot = pivot;
            page.anchoredPosition = position; page.sizeDelta = size; page.localScale = scale;
            UnityEngine.Object.DestroyImmediate(viewport.gameObject);
        }
    }
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, Fields).GetValue(instance);
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Fields).SetValue(instance, value);
    private static object Call(object instance, string name, params object[] values) => instance.GetType().GetMethod(name, Fields).Invoke(instance, values);
    private static void SetPage(ChessLanController owner, string page)
    {
        var field = typeof(ChessLanController).GetField("menuPage", Fields);
        field.SetValue(owner, Enum.Parse(field.FieldType, page));
        Set(owner, "menuRevision", Field<int>(owner, "menuRevision") + 1);
    }
    private static void Require(bool value, string description)
    {
        if (!value) throw new InvalidOperationException("[PlayFlowAudit] " + description);
        checks.Add("PASS " + description);
    }
}

