using System;
using System.Collections.Generic;
using System.Linq;
using ChessButWeird.Online;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Displays the backend's ARAM v2 rules. The local practice runtime has additional
// buffs; it must never adjudicate an online match or roll its random outcomes.
internal sealed class OnlineAramPresenter : IDisposable
{
    private readonly ChessLanController owner;
    private readonly ChessGame game;
    private MatchState state;
    private JToken side;
    private string team, action;
    private readonly List<int> targets = new List<int>();
    private readonly Dictionary<int, string> formation = new Dictionary<int, string>();
    private int? formationPiece;
    private Vector2 scroll;
    private GameObject markers;
    private string markerFingerprint;
    private Camera camera;
    private ChessOrbitCamera orbit;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private bool aiming, savedOrbitEnabled;
    private readonly Dictionary<Renderer, bool> hiddenShooter = new Dictionary<Renderer, bool>();
    private float yaw, pitch;

    private static readonly string[] Buffs = {
        "Commandant Pawn: choose 3 pawns; move two empty squares forward.",
        "Strong Fortress: relaxed castling requirements; destination must be safe.",
        "Freestyle Leap: knights can stop along their L path.",
        "Doppelganger: choose a knight and bishop; toggle movement swap every 5 turns.",
        "Suicide Bomber: your original queen explodes once when captured.",
        "Flying Thunder God: original queen teleports to empty squares, 5 uses; 5-turn cooldown.",
        "Noble Sacrifice: capture an allied pawn to gain Bloodthirsty.",
        "Absolute Sniper: long bishop captures charge Snipe for 5 turns.",
        "Gambling Leads to Misery: pawn captures may grant extra moves with that pawn.",
        "Loot Box: crates at rounds 25/50 grant rook/queen deployments.",
        "Peace T-Shirt: after 15 turns without check, recruit an enemy minor piece/rook/pawn.",
        "Rise of Pawn: pawns retreat; reaching the home rank creates a mine.",
        "Mobile Fortress: load a pawn into a rook; deploy it when the rook dies.",
        "Hiding King: swap king with an allied home-rank piece; 10-turn cooldown.",
        "Gacha Banner: use combat tickets to roll pieces, at most twice per turn.",
        "Substitute Ninjutsu: first check teleports king to safety and leaves a decoy.",
        "High-Tech Era: original rooks capture across exactly one blocker.",
        "Queen's Betrayal: extra queen; 10% chance to switch sides each owner turn.",
        "Definition of ARAM: outer file pairs collapse at rounds 10/15.",
        "RNG Fiesta: randomizes starting pieces except king/queen.",
        "I-Frame Roll: Escape a checkmate within the king's 5x5 region, at most 3 uses.",
        "Ghost Army: move through allies, but cannot use phasing to check the enemy king.",
        "Plague Town: capturers become infected and die after 4 owner turns.",
        "Customize Army: arrange every piece in your half within 120 seconds.",
        "Pawns' Revolution: fill your half with pawns, keeping the king.",
        "One-Man Army: king only; two-square straight moves and a rifle." };

    public OnlineAramPresenter(ChessLanController owner, ChessGame game)
    { this.owner = owner; this.game = game; }

    public void Apply(MatchState next)
    {
        if (state != null && (state.matchId != next.matchId || (string)state.aram?["phase"] != (string)next.aram?["phase"])) CancelTargeting();
        int? previousBuff = (int?)side?["buffId"];
        state = next;
        team = next.players.First(p => p.userId == PlayerAuthService.UserId).color;
        side = next.aram?["sides"]?.FirstOrDefault(s => (string)s["team"] == team);
        if (previousBuff != (int?)side?["buffId"]) CancelTargeting();
        RefreshMarkers();
        UpdateRifle();
    }
    private Rect Panel => new Rect(20, 252, 420, Mathf.Max(150, Screen.height / ChessLanController.OnlineGuiScale - 272));
    public bool ContainsPointer(Vector2 point) => Panel.Contains(point);
    public void Draw()
    {
        if (state?.aram == null || state.status == "Finished" || state.status == "Cancelled") return;
        GUILayout.BeginArea(Panel, GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("ARAM - " + (string)state.aram["phase"]);
        if (state.aram["forcedPawn"]?.Type == JTokenType.Integer) GUILayout.Label("Extra move: pawn #" + (int)state.aram["forcedPawn"]);
        foreach (var s in state.aram["sides"] ?? new JArray())
        {
            int? id = (int?)s["buffId"];
            GUILayout.Label((string)s["team"] + ": " + (id.HasValue ? BuffText(id.Value) : "Choosing a buff..."));
            foreach (var effect in s["effects"] ?? new JArray())
            {
                var active = ((JObject)effect).Properties().Where(p => p.Name != "pieceId" && p.Value.Type != JTokenType.Null &&
                    (p.Value.Type != JTokenType.Boolean || (bool)p.Value) && (p.Value.Type != JTokenType.Integer || (int)p.Value != 0));
                string description = string.Join(", ", active.Select(p => p.Name + ": " + p.Value));
                if (description.Length > 0) GUILayout.Label("#" + (int)effect["pieceId"] + " " + description);
            }
        }
        GUI.enabled = owner.CanSendOnlineCommand;
        if (side != null)
        {
            int? buff = (int?)side["buffId"];
            if (!buff.HasValue)
            {
                foreach (var option in side["draftOptions"] ?? new JArray())
                { int id = (int)option; if (GUILayout.Button(BuffText(id))) Send(new AbilityCommand { kind = "SelectBuff", buffId = id }); }
            }
            else
            {
                GUILayout.Label($"Owner turns: {(int?)side["completedTurns"] ?? 0} | Combat tickets: {(int?)side["tickets"] ?? 0}");
                if ((bool?)side["setupComplete"] != true)
                {
                    if (buff == 0) Button("Choose 3 commandant pawns", "SelectTargets");
                    if (buff == 3) Button("Choose a knight and bishop", "SelectTargets");
                    if (buff == 23)
                    {
                        if (GUILayout.Button("Arrange your army"))
                        {
                            CancelTargeting(); action = "ConfirmFormation";
                            foreach (var p in state.board.Where(p => p.team == team)) formation[p.id] = p.square;
                        }
                        if (side["formationDeadline"]?.Type == JTokenType.Date)
                            GUILayout.Label("Deadline (UTC): " + ((DateTime)side["formationDeadline"]).ToString("HH:mm:ss"));
                    }
                }
                if (state.status == "InProgress")
                {
                    var deployment = state.aram["deployments"]?.FirstOrDefault();
                    if (deployment != null) { if ((string)deployment["team"] == team) Button("Deploy " + (string)deployment["kind"], "Deploy"); }
                    else if ((string)state.aram["escapeTeam"] == team) Button("Escape - choose a safe square", "Escape");
                    else if (state.turn == team && state.aram["rifle"]?.Type != JTokenType.Object)
                    {
                        if (buff == 3 && GUILayout.Button("Toggle knight/bishop swap")) Send(new AbilityCommand { kind = "ToggleSwap" });
                        if (buff == 7) Button("Snipe: bishop, then enemy piece", "Snipe");
                        if (buff == 10) Button("Recruit: enemy, then destination", "Recruit");
                        if (buff == 12) Button("Load pawn: rook, then allied pawn", "LoadPawn");
                        if (buff == 13) Button("Hide king: allied home-rank piece", "HideKing");
                        if (buff == 14 && GUILayout.Button("Battle gacha (combat tickets)")) Send(new AbilityCommand { kind = "BattleGacha" });
                        if (buff == 25 && GUILayout.Button("Aim rifle")) Send(new AbilityCommand { kind = "AimRifle" });
                    }
                }
            }
        }
        if (aiming)
        {
            GUILayout.Label("Hold right mouse to aim. Left mouse fires. Server resolves the shot.");
            GUILayout.Label($"Yaw {yaw:F1} / Pitch {pitch:F1}");
            if (GUILayout.Button("Fire")) Fire();
            if (GUILayout.Button("Exit rifle (keep charge)")) Send(new AbilityCommand { kind = "ExitRifle" });
        }
        if (!string.IsNullOrEmpty(action))
        {
            GUILayout.Label("Click pieces/squares on the board: " + action);
            GUILayout.Label("Selected IDs: " + string.Join(", ", targets));
            if (action == "ConfirmFormation")
            {
                GUILayout.Label("Select a piece, then its destination. An occupied destination swaps both planned positions.");
                foreach (var p in formation) GUILayout.Label($"#{p.Key}: {p.Value}" + (formationPiece == p.Key ? " (selected)" : ""));
                if (GUILayout.Button("Confirm entire formation")) Send(new AbilityCommand { kind = action,
                    formation = formation.Select(p => new FormationPlacement { pieceId = p.Key, square = p.Value }).ToList() });
            }
            if (GUILayout.Button("Cancel targeting")) CancelTargeting();
        }
        GUI.enabled = true;
        GUILayout.EndScrollView(); GUILayout.EndArea();
        if (aiming) GUI.Label(new Rect(Screen.width / ChessLanController.OnlineGuiScale / 2 - 8, Screen.height / ChessLanController.OnlineGuiScale / 2 - 12, 24, 24), "+");
    }
    internal static string BuffText(int id) => id >= 0 && id < Buffs.Length ? Buffs[id] : "Unknown buff " + id;
    private void Button(string label, string kind)
    { if (GUILayout.Button(label)) { CancelTargeting(); action = kind; } }
    private void CancelTargeting() { action = null; targets.Clear(); formation.Clear(); formationPiece = null; }
    private void Send(AbilityCommand command)
    { if (!owner.CanSendOnlineCommand) return; CancelTargeting(); owner.SendAbility(command); }
    public bool HandleClick(Vector2Int square, int? pieceId)
    {
        if (aiming) return true;
        if (string.IsNullOrEmpty(action)) return false;
        if (!owner.CanSendOnlineCommand) return true;
        string targetSquare = ((char)('a' + square.x)).ToString() + (square.y + 1);
        if (action == "ConfirmFormation")
        {
            if (formationPiece.HasValue)
            {
                string previous = formation[formationPiece.Value];
                var displaced = formation.FirstOrDefault(p => p.Value == targetSquare && p.Key != formationPiece.Value);
                if (displaced.Value != null) formation[displaced.Key] = previous;
                formation[formationPiece.Value] = targetSquare; formationPiece = null;
            }
            else if (pieceId.HasValue && formation.ContainsKey(pieceId.Value)) formationPiece = pieceId;
            return true;
        }
        if (action == "Deploy" || action == "Escape") Send(new AbilityCommand { kind = action, target = targetSquare });
        else if (action == "Recruit" && targets.Count == 1)
            Send(new AbilityCommand { kind = action, targetPieceId = targets[0], target = targetSquare });
        else if (pieceId.HasValue)
        {
            if (action == "HideKing") Send(new AbilityCommand { kind = action, targetPieceId = pieceId });
            else
            {
                if (targets.Contains(pieceId.Value)) targets.Remove(pieceId.Value); else targets.Add(pieceId.Value);
                int count = action == "SelectTargets" ? ((int?)side["buffId"] == 0 ? 3 : 2) : 2;
                if (targets.Count == count && action != "Recruit")
                    Send(new AbilityCommand { kind = action, pieceIds = new List<int>(targets),
                        pieceId = action == "SelectTargets" ? null : (int?)targets[0],
                        targetPieceId = action == "SelectTargets" ? null : (int?)targets[1] });
            }
        }
        return true;
    }
    public void Update()
    {
        if (!aiming || !camera || Mouse.current == null || !owner.CanSendOnlineCommand || game.PauseLocked) return;
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw = Mathf.Repeat(yaw + delta.x * .15f, 360);
            pitch = Mathf.Clamp(pitch - delta.y * .15f, -80, 80);
        }
        PlaceRifleCamera();
        var mouse = Mouse.current.position.ReadValue();
        if (Mouse.current.leftButton.wasPressedThisFrame && !owner.BlocksOnlineBoardPointer(mouse) &&
            (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())) Fire();
    }
    private void Fire() => Send(new AbilityCommand { kind = "FireRifle", yaw = yaw, pitch = pitch });
    private void UpdateRifle()
    {
        bool shouldAim = state.IsActive && state.aram?["rifle"] is JObject rifle && (string)rifle["team"] == team;
        if (shouldAim && !aiming)
        {
            camera = Camera.main; if (!camera) camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (!camera) return;
            orbit = camera.GetComponent<ChessOrbitCamera>();
            savedPosition = camera.transform.position; savedRotation = camera.transform.rotation;
            savedOrbitEnabled = orbit && orbit.enabled; if (orbit) orbit.enabled = false;
            var shooter = state.board.FirstOrDefault(p => p.team == team && p.kind == "King");
            var view = shooter == null ? null : game.OnlinePieceView(shooter.id);
            if (view)
                foreach (var renderer in view.GetComponentsInChildren<Renderer>())
                { hiddenShooter[renderer] = renderer.enabled; renderer.enabled = false; }
            yaw = team == "White" ? 0 : 180; pitch = 0; aiming = true;
        }
        else if (!shouldAim && aiming) RestoreCamera();
        if (aiming) PlaceRifleCamera();
    }
    private void PlaceRifleCamera()
    {
        var rifle = state.aram?["rifle"]; if (rifle == null || !game.Board || !camera) return;
        Vector3 origin = game.Board.GetTileCenterWorld(Vector2Int.zero);
        Vector3 x = game.Board.GetTileCenterWorld(Vector2Int.right) - origin;
        Vector3 z = game.Board.GetTileCenterWorld(Vector2Int.up) - origin;
        Vector3 up = Vector3.Cross(z, x).normalized;
        camera.transform.position = origin + x * (float)rifle["originX"] + up * x.magnitude * (float)rifle["originY"] + z * (float)rifle["originZ"];
        float y = yaw * Mathf.Deg2Rad, p = pitch * Mathf.Deg2Rad;
        Vector3 direction = x.normalized * (Mathf.Sin(y) * Mathf.Cos(p)) + z.normalized * (Mathf.Cos(y) * Mathf.Cos(p)) - up * Mathf.Sin(p);
        camera.transform.rotation = Quaternion.LookRotation(direction, up);
    }
    private void RestoreCamera()
    {
        foreach (var renderer in hiddenShooter) if (renderer.Key) renderer.Key.enabled = renderer.Value;
        hiddenShooter.Clear();
        if (camera) { camera.transform.SetPositionAndRotation(savedPosition, savedRotation); }
        if (orbit) orbit.enabled = savedOrbitEnabled;
        aiming = false;
    }
    private void RefreshMarkers()
    {
        string key = string.Join("|", new[] { state.aram?["mines"]?.ToString(), state.aram?["crates"]?.ToString(), state.aram?["collapsedFiles"]?.ToString() });
        if (key == markerFingerprint) return;
        markerFingerprint = key; if (markers) { markers.SetActive(false); UnityEngine.Object.Destroy(markers); }
        if (!game.Board) return;
        markers = new GameObject("Online ARAM markers");
        foreach (var mine in state.aram?["mines"] ?? new JArray()) CreateMarker((string)mine["square"], Color.red, .4f, "Mine");
        foreach (var crate in state.aram?["crates"] ?? new JArray()) CreateMarker((string)crate["square"], Color.yellow, .5f, "Crate " + (string)crate["kind"]);
        int collapsed = (int?)state.aram?["collapsedFiles"] ?? 0;
        for (int file = 0; file < 8; file++)
            if (file < collapsed || file >= 8 - collapsed)
                for (int rank = 1; rank <= 8; rank++) CreateMarker(((char)('a' + file)).ToString() + rank, new Color(.15f, .03f, .03f), .95f, "Collapsed tile");
    }
    private void CreateMarker(string square, Color color, float width, string label)
    {
        if (string.IsNullOrEmpty(square)) return;
        var tile = ChessGame.ParseOnlineSquare(square);
        float size = Vector3.Distance(game.Board.GetTileCenterWorld(Vector2Int.zero), game.Board.GetTileCenterWorld(Vector2Int.right));
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cube); marker.name = label + " " + square;
        marker.transform.SetParent(markers.transform, false);
        marker.transform.position = game.Board.GetTileCenterWorld(tile) + Vector3.up * size * .03f;
        marker.transform.localScale = new Vector3(size * width, size * .04f, size * width);
        marker.GetComponent<Collider>().enabled = false;
        var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", color); block.SetColor("_Color", color);
        marker.GetComponent<Renderer>().SetPropertyBlock(block);
    }
    public void Dispose()
    { RestoreCamera(); if (markers) { markers.SetActive(false); UnityEngine.Object.Destroy(markers); } }
}
