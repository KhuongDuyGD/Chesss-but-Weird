#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChessButWeird.Online;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class Chaos35OnlineAudit
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks, ticks;
    private static readonly List<string> results = new List<string>();
    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Private).GetValue(value);
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static AramAbilityView.Presentation Read(OnlineAramPresenter p) => (AramAbilityView.Presentation)p.GetType().GetMethod("BuildPresentation", Private).Invoke(p, null);
    private static void Tick(OnlineAramPresenter p)
    {
        p.Update(); var view = Field<AramAbilityView>(p, "abilityView");
        view.GetType().GetField("nextRefresh", Private).SetValue(view, 0f);
        view.GetType().GetMethod("Update", Private).Invoke(view, null); Canvas.ForceUpdateCanvases();
    }
    public static void Run()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\','/').Contains("/Logs/hud-headless/")) throw new Exception("Use the isolated HUD tool.");
        EditorApplication.update += Start;
    }
    private static void Start()
    {
        if (++ticks < 8) return; EditorApplication.update -= Start;
        try
        {
            Audit(); results.Add("PASS " + checks + " Chaos 35 client checks using real backend snapshots; transport and authentication are inert.");
            File.WriteAllLines("audit-result.txt", results); EditorApplication.Exit(0);
        }
        catch (Exception ex) { File.WriteAllText("audit-result.txt", "FAIL " + ex); Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    private static void Audit()
    {
        var fixtures = JArray.Parse(File.ReadAllText("fixtures.json"));
        foreach (string color in new[] { "White", "Black" })
        {
            PlayerAuthService.UserId = color.ToLowerInvariant();
            var host = new GameObject("Chaos 35 " + color); var game = host.AddComponent<ChessGame>();
            var owner = new ChessLanController(); var p = new OnlineAramPresenter(owner, game);
            foreach (var fixture in fixtures)
            {
                var state = fixture[color.ToLowerInvariant()].ToObject<MatchState>();
                owner.CanSendOnlineCommand = true; p.Apply(state); Tick(p);
                var view = Read(p); var own = state.chaos["sides"].Single(s => (string)s["team"] == color);
                int? buff = (int?)own["buffId"];
                Check(view.Visible, color + " missing abilities " + fixture["name"]);
                Check(state.chaos["buffDefinitions"].Count() == 35, "Metadata lost on client decode");
                string serverName = (string)state.chaos["buffDefinitions"].First(d => (int)d["id"] == buff)["displayName"];
                string expectedName = SettingsLocalization.Text("buff." + (AramBuffId)buff.Value + ".name", serverName);
                Check(view.Summary.Contains(expectedName), "Client ignored localized buff name");
                if ((string)state.chaos["phase"] == "Setup" && (bool?)own["setupComplete"] != true)
                {
                    string expected = buff == 27 ? "ReplaceQueen-Rook" : buff == 13 ? "HideKing" : "SelectTargets";
                    Check(view.Actions.Any(a => a.Id == expected), "Missing mandatory setup for " + buff + " " + color);
                }
                if (state.status == "InProgress" && state.turn == color && buff == 31)
                {
                    Check(view.Actions.Count(a => a.Id.StartsWith("Buy-")) == 5, "Credit Card is missing purchase types");
                    view.Actions.Single(a => a.Id == "Buy-Queen").Invoke();
                    p.HandleClick(new Vector2Int(0,color == "White" ? 2 : 5), null);
                    var sent = owner.Sent.Last(); Check(sent.kind == "BuyPiece" && sent.pieceKind == "Queen", "Buy command lost piece kind");
                    Check(sent.target == (color == "White" ? "a3" : "a6"), "Buy command used wrong board orientation");
                    owner.CanSendOnlineCommand = false; int count = owner.Sent.Count;
                    p.HandleClick(new Vector2Int(1,2), null); Check(owner.Sent.Count == count, "Pending action sent duplicate command");
                }
                if ((string)state.chaos["phase"] == "Setup" && buff == 23 && (bool?)own["formationComplete"] != true)
                {
                    view.Actions.Single(a => a.Id == "Arrange").Invoke();
                    var pawn = state.board.Single(piece => piece.team == color && piece.square == (color == "White" ? "a2" : "a7"));
                    p.HandleClick(ChessGame.ParseOnlineSquare(pawn.square), pawn.id);
                    string destination = color == "White" ? "a3" : "a6";
                    p.HandleClick(ChessGame.ParseOnlineSquare(destination), null);
                    var planned = Read(p);
                    Check(planned.Details.Contains("Pawn #" + pawn.id + ": " + pawn.square + " -> " + destination), "Formation plan lost its visible destination");
                    planned.Actions.Single(a => a.Id == "ConfirmFormation").Invoke();
                    var sent = owner.Sent.Last();
                    Check(sent.formation.Count == state.board.Count(piece => piece.team == color), "Formation omitted owned pieces");
                    Check(sent.formation.Single(piece => piece.pieceId == pawn.id).square == destination, "Formation command ignored the plan");
                }
                game.PauseLocked = true; Tick(p); Check(!Read(p).Visible, "Pause kept online controls active"); game.PauseLocked = false;
            }
            // All nine new stable IDs must use the shared draft and select only the server offer.
            foreach (int id in Enumerable.Range(26,9))
            {
                var state = fixtures[0][color.ToLowerInvariant()].ToObject<MatchState>(); state.matchId = "draft-" + color + id;
                state.status = "AwaitingReady"; state.chaos["phase"] = "Draft";
                var own = (JObject)state.chaos["sides"].Single(s => (string)s["team"] == color);
                own["buffId"] = null; own["draftOptions"] = new JArray(id,(id+1)%35,(id+2)%35);
                owner.CanSendOnlineCommand = true; p.Apply(state); Tick(p);
                var draft = Field<AramBuffDraftView>(p,"draftView"); Check(draft.IsDraftVisible,"New buff draft was hidden");
                var cards = Field<RectTransform>(draft,"cardRoot").Cast<Transform>().Where(t => t.gameObject.activeSelf).ToList();
                Check(cards.Count == 3 && cards.All(c => c.GetComponentInChildren<AramBuffCardVisual>()), "Shared 35-buff artwork missing");
                var choose = cards[0].Find("Choose buff").GetComponent<Button>(); choose.onClick.Invoke();
                Check(owner.Sent.Last().kind == "SelectBuff" && owner.Sent.Last().buffId == id, "New buff sent an incorrect ID");
                Check(draft.IsDraftVisible, "Client selected before server confirmation");
            }
            if(color=="White")AuditStrategyInteraction(p,owner,game);
            p.Dispose(); UnityEngine.Object.DestroyImmediate(host); results.Add("PASS " + color + ": 35 buff snapshots, setups, draft IDs, purchases, recovery/pause and command guards");
        }
    }
    private static void AuditStrategyInteraction(OnlineAramPresenter presenter,ChessLanController owner,ChessGame game)
    {
        var fixtures=JArray.Parse(File.ReadAllText("strategy-fixtures.json"));game.Board=game.gameObject.AddComponent<FixtureOnlineBoard>();
        void Apply(JToken data,string match)
        {
            game.TestSelected=null;foreach(var view in game.TestViews.Values)UnityEngine.Object.DestroyImmediate(view.gameObject);game.TestViews.Clear();
            var state=data.ToObject<MatchState>();state.matchId=match;
            foreach(var piece in state.board){var go=new GameObject("Actual test view "+piece.id);go.transform.SetParent(game.transform);go.transform.position=game.Board.GetTileCenterWorld(ChessGame.ParseOnlineSquare(piece.square));var view=go.AddComponent<ChessPiece>();view.Team=piece.team=="White"?PieceTeam.White:PieceTeam.Black;view.Type=(PieceType)Enum.Parse(typeof(PieceType),piece.kind);game.TestViews[piece.id]=view;}
            owner.CanSendOnlineCommand=true;presenter.Apply(state);Tick(presenter);
        }
        var formationCase=fixtures.Single(f=>(string)f["name"]=="formation-applied")["white"]["before"];Apply(formationCase,"formation-ghost-QA");
        var current=Field<MatchState>(presenter,"state");string original=JsonConvert.SerializeObject(current.board);int sent=owner.Sent.Count;
        Read(presenter).Actions.Single(a=>a.Id=="Arrange").Invoke();var pawn=current.board.Single(b=>b.square=="a2");
        presenter.HandleClick(new Vector2Int(0,1),pawn.id);presenter.HandleClick(new Vector2Int(0,2),null);
        var fx=Field<ChessButWeird.BuffVfx.BuffVfxOrchestrator>(presenter,"buffVfx");Check(fx.PreviewGhosts==16,"Online real presenter displays sixteen draft-only ghosts");
        Check(original==JsonConvert.SerializeObject(current.board)&&owner.Sent.Count==sent,"No actual snapshot/transport mutation before confirm");
        Check(game.TestViews[pawn.id].transform.position==new Vector3(0,0,1),"Actual visual remains at authoritative source during preview");
        presenter.HandleClick(new Vector2Int(0,2),null);presenter.HandleClick(new Vector2Int(1,2),null);
        var draft=Field<Dictionary<int,string>>(presenter,"formation");Check(draft[pawn.id]=="b3","Virtual destination can be selected again without a real piece collider");
        presenter.HandleClick(new Vector2Int(1,2),null);presenter.HandleClick(new Vector2Int(1,6),null);Check(draft[pawn.id]=="b3","Enemy half cannot enter online draft");
        Read(presenter).Actions.Single(a=>a.Id=="CancelTargeting").Invoke();Check(fx.PreviewGhosts==0&&fx.PreviewTargets==0&&original==JsonConvert.SerializeObject(current.board),"Cancel clears all online ghosts without board mutation");
        foreach(string scenario in new[]{"selected-0","freestyle-leap","fortress-castle"})
        {
            var data=fixtures.Single(f=>(string)f["name"]==scenario)["white"][scenario=="selected-0"?"after":"before"];Apply(data,"legal-preview-"+scenario);current=Field<MatchState>(presenter,"state");
            var side=current.chaos["sides"].Single(s=>(string)s["team"]=="White");int id=scenario=="selected-0"?(int)side["commandantPawns"][0]:scenario=="freestyle-leap"?(int)side["freestyleKnight"]:current.board.Single(b=>b.team=="White"&&b.kind=="King").id;
            game.TestSelected=game.TestViews[id];presenter.GetType().GetField("nextPreview",Private).SetValue(presenter,0f);Tick(presenter);fx=Field<ChessButWeird.BuffVfx.BuffVfxOrchestrator>(presenter,"buffVfx");
            string square=current.board.Single(b=>b.id==id).square;var from=ChessGame.ParseOnlineSquare(square);int expected=current.chaos["legalMoves"].Count(m=>(string)m["from"]==square&&(scenario!="fortress-castle"||Mathf.Abs(ChessGame.ParseOnlineSquare((string)m["to"]).x-from.x)>=2));
            Check(expected>0&&fx.PreviewTargets==expected,"Production online preview uses exact sanitized legal service: "+scenario);
            game.PauseLocked=true;Tick(presenter);Check(fx.PreviewTargets==0,"Pause removes online legal preview");game.PauseLocked=false;
        }
    }
}
#endif
