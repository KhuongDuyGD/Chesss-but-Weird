using System;
using System.Collections.Generic;
using System.Linq;
using ChessButWeird.Online;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Presents server-authoritative Chaos through the same cards and controls as practice.
// The local runtime must never adjudicate an online match or roll its outcomes.
internal sealed partial class OnlineAramPresenter : IDisposable
{
    private readonly ChessLanController owner;
    private readonly ChessGame game;
    private MatchState state;
    private JToken side;
    private string team, action;
    private readonly List<int> targets = new List<int>();
    private readonly Dictionary<int, string> formation = new Dictionary<int, string>();
    private int? formationPiece;
    private GameObject uiHost;
    private AramBuffDraftView draftView;
    private AramAbilityView abilityView;
    private readonly List<AramBuffDefinition> definitions = AramBuffLibrary.CreateBuiltInDefinitions();
    private string draftFingerprint;
    private bool disposed;

    private bool Visible => !disposed && state?.chaos != null && state.IsActive && !game.PauseLocked;
    private bool CanInteract => Visible && owner.CanSendOnlineCommand;
    private GameObject markers;
    private string markerFingerprint;
    private Camera camera;
    private ChessOrbitCamera orbit;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private bool aiming, savedOrbitEnabled, savedOrthographic;
    private float savedOrthographicSize, savedFieldOfView, savedNearClipPlane;
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
        "Definition of CHAOS: outer file pairs collapse at rounds 10/15.",
        "RNG Fiesta: randomizes starting pieces except king/queen.",
        "I-Frame Roll: Escape a checkmate within the king's 5x5 region, at most 3 uses.",
        "Ghost Army: move through allies, but cannot use phasing to check the enemy king.",
        "Plague Town: capturers become infected and die after 4 owner turns.",
        "Customize Army: arrange every piece in your half within 120 seconds.",
        "Pawns' Revolution: fill your half with pawns, keeping the king.",
        "One-Man Army: king only; two-square straight moves and a rifle." };

    public OnlineAramPresenter(ChessLanController owner, ChessGame game)
    {
        this.owner = owner; this.game = game;
        uiHost = new GameObject("Online CHAOS UI");
        uiHost.transform.SetParent(game.transform, false);
        draftView = uiHost.AddComponent<AramBuffDraftView>();
        abilityView = uiHost.AddComponent<AramAbilityView>();
        abilityView.InitializeOnline(BuildPresentation, () => Visible && !draftView.IsDraftVisible);
        // Older servers have no metadata. Their v2 descriptions still differ
        // from practice, so never fall back to the current local rule text.
        ResetOnlineDefinitions();
    }

    private void ResetOnlineDefinitions()
    {
        foreach (var definition in definitions.Where(d => (int)d.Id < Buffs.Length))
        {
            string copy = BuffText((int)definition.Id);
            definition.Configure(definition.Id, definition.Tier, copy.Split(':')[0], definition.ShortName,
                copy, definition.AccentColor, AramBuffDefinition.FirstSentence(copy));
        }
    }

    public void Apply(MatchState next)
    {
        if (state != null && (state.matchId != next.matchId || (string)state.chaos?["phase"] != (string)next.chaos?["phase"])) CancelTargeting();
        int? previousBuff = (int?)side?["buffId"];
        if (state == null || state.matchId != next.matchId)
        {
            if ((int?)next.chaos?["rulesVersion"] == 3) ResetPracticeDefinitions();
            else ResetOnlineDefinitions();
        }
        string previousDecision = state?.chaos?["deployments"]?.FirstOrDefault()?["id"]?.ToString();
        string nextDecision = next.chaos?["deployments"]?.FirstOrDefault()?["id"]?.ToString();
        if (previousDecision != nextDecision || state != null && state.turn != next.turn) CancelTargeting();
        state = next;
        team = next.players.First(p => p.userId == PlayerAuthService.UserId).color;
        side = next.chaos?["sides"]?.FirstOrDefault(s => (string)s["team"] == team);
        if (previousBuff != (int?)side?["buffId"]) CancelTargeting();
        foreach (var metadata in next.chaos?["buffDefinitions"] ?? new JArray())
        {
            var definition = Definition((int)metadata["id"]);
            if (definition)
            {
                var tier = Enum.TryParse((string)metadata["tier"], out AramBuffTier parsedTier) && Enum.IsDefined(typeof(AramBuffTier), parsedTier)
                    ? parsedTier : definition.Tier;
                definition.Configure(definition.Id, tier,
                    (string)metadata["displayName"] ?? definition.DisplayName, definition.ShortName,
                    (string)metadata["description"] ?? definition.Description, definition.AccentColor,
                    (string)metadata["summary"] ?? AramBuffDefinition.FirstSentence((string)metadata["description"] ?? definition.Description));
            }
        }
        RefreshDraft();
        RefreshMarkers();
        PresentVisuals();
        UpdateRifle();
    }
    private AramBuffDefinition Definition(int id) => AramBuffLibrary.FindById(definitions, (AramBuffId)id);
    public bool ContainsPointer(Vector2 screen) => draftView && draftView.ContainsPointer(screen) ||
        abilityView && abilityView.ContainsPointer(screen);

    private void RefreshDraft()
    {
        bool choosing = state.IsActive && (string)state.chaos?["phase"] == "Draft" && side?["buffId"]?.Type != JTokenType.Integer;
        if (!choosing)
        {
            draftFingerprint = null;
            draftView.HideAll();
            return;
        }
        string key = state.matchId + "|" + team + "|" + side?["draftOptions"];
        if (key == draftFingerprint) return;
        draftFingerprint = key;
        var options = new List<AramBuffDefinition>();
        foreach (var option in side?["draftOptions"] ?? new JArray())
        {
            var definition = Definition((int)option);
            if (definition) options.Add(definition);
        }
        draftView.ShowOnlineDraft(options, (team == "Black" ? PieceTeam.Black : PieceTeam.White), definition =>
        {
            // Revalidate the latest snapshot, not a captured draft or an enemy option.
            if (!CanInteract || (string)state.chaos?["phase"] != "Draft" || side?["buffId"]?.Type == JTokenType.Integer ||
                side?["draftOptions"]?.Any(o => (int)o == (int)definition.Id) != true) return;
            Send(new AbilityCommand { kind = "SelectBuff", buffId = (int)definition.Id });
        });
    }

    private AramAbilityView.Presentation BuildPresentation()
    {
        if ((int?)state?.chaos?["rulesVersion"] == 3) return BuildChaos35Presentation();
        var view = new AramAbilityView.Presentation { Visible = Visible && !draftView.IsDraftVisible,
            Context = state?.matchId + "|" + state?.stateVersion + "|" + action,
            PresentationContext = state?.matchId + "|" + side?["buffId"] + "|" + state?.chaos?["phase"] + "|" + action,
            InstructionRequired = state?.status == "AwaitingReady" || !string.IsNullOrEmpty(action),
            RifleActive = aiming, RifleHint = "Hold right mouse to aim. Left mouse fires." };
        if (!view.Visible) return view;
        var summary = new List<string>();
        var details = new List<string>();
        foreach (var s in state.chaos["sides"] ?? new JArray())
        {
            int? id = (int?)s["buffId"];
            var definition = id.HasValue ? Definition(id.Value) : null;
            summary.Add((string)s["team"] + ": " + (definition ? definition.DisplayName : "Choosing a buff..."));
            if (definition) details.Add((string)s["team"] + ": " + definition.Description);
            foreach (var effect in s["effects"] ?? new JArray())
            {
                var active = ((JObject)effect).Properties().Where(p => p.Name != "pieceId" && p.Value.Type != JTokenType.Null &&
                    (p.Value.Type != JTokenType.Boolean || (bool)p.Value) && (p.Value.Type != JTokenType.Integer || (int)p.Value != 0));
                string text = string.Join(", ", active.Select(p => p.Name + ": " + p.Value));
                if (text.Length > 0) details.Add("#" + (int)effect["pieceId"] + " " + text);
            }
        }
        view.Summary = string.Join("\n", summary);
        view.Details = string.Join("\n\n", details);
        view.Status = "CHAOS / " + (string)state.chaos["phase"];
        if (!owner.CanSendOnlineCommand) view.Status += " / Waiting for server...";
        if (state.chaos["forcedPawn"]?.Type == JTokenType.Integer) view.Status += "\nExtra move: pawn #" + (int)state.chaos["forcedPawn"];
        void Add(string label, Action invoke, string id) => view.Actions.Add(new AramAbilityView.AbilityAction(label, invoke, CanInteract, id: id));
        void Target(string label, string kind) => Add(label, () => { CancelTargeting(); action = kind; }, kind);
        int? buff = (int?)side?["buffId"];
        if (buff.HasValue)
        {
            view.Status += $"\nOwner turns: {(int?)side["completedTurns"] ?? 0} / Tickets: {(int?)side["tickets"] ?? 0}";
            if ((string)state.chaos["phase"] == "Draft") view.Status += "\nWaiting for your opponent to choose...";
            if ((string)state.chaos["phase"] == "Setup" && (bool?)side["setupComplete"] != true)
            {
                if (buff == 0) Target("Choose 3 commandant pawns", "SelectTargets");
                if (buff == 3) Target("Choose a knight and bishop", "SelectTargets");
            }
            if ((string)state.chaos["phase"] == "Setup" && buff == 23 && (bool?)side["formationComplete"] != true)
            {
                if (side["formationDeadline"]?.Type == JTokenType.Date)
                    view.Status += "\nFormation ends (UTC): " + ((DateTime)side["formationDeadline"]).ToString("HH:mm:ss");
                Add("Arrange your army", () => {
                    CancelTargeting(); action = "ConfirmFormation";
                    foreach (var p in state.board.Where(p => p.team == team)) formation[p.id] = p.square;
                }, "Arrange");
            }
            if (state.status == "InProgress")
            {
                var deployment = state.chaos["deployments"]?.FirstOrDefault();
                if (deployment != null) { if ((string)deployment["team"] == team) Target("Deploy " + (string)deployment["kind"], "Deploy"); }
                else if ((string)state.chaos["escapeTeam"] == team) Target("Escape: choose a safe square", "Escape");
                else if (state.turn == team && state.chaos["rifle"]?.Type != JTokenType.Object)
                {
                    if (buff == 3) Add("Toggle knight/bishop swap", () => Send(new AbilityCommand { kind = "ToggleSwap" }), "ToggleSwap");
                    if (buff == 7) Target("Snipe: bishop, then enemy", "Snipe");
                    if (buff == 10) Target("Recruit: enemy, then destination", "Recruit");
                    if (buff == 12) Target("Load pawn: rook, then pawn", "LoadPawn");
                    if (buff == 13) Target("Hide king: home-rank ally", "HideKing");
                    if (buff == 14) Add("Battle gacha", () => Send(new AbilityCommand { kind = "BattleGacha" }), "BattleGacha");
                    if (buff == 25) Add("Aim rifle", () => Send(new AbilityCommand { kind = "AimRifle" }), "AimRifle");
                }
            }
        }
        if (aiming)
        {
            view.Status += $"\nYaw {yaw:F1} / Pitch {pitch:F1}";
            Add("Fire", Fire, "FireRifle");
            Add("Exit rifle", () => Send(new AbilityCommand { kind = "ExitRifle" }), "ExitRifle");
        }
        if (!string.IsNullOrEmpty(action))
        {
            view.InstructionRequired = true;
            view.Status += "\nClick the board: " + action + "\nSelected: " + string.Join(", ", targets);
            if (action == "ConfirmFormation")
            {
                view.Status += "\nSelect a piece, then its destination. Occupied squares swap.";
                view.Details += "\n\n" + string.Join("\n", formation.Select(p => $"#{p.Key}: {p.Value}" + (formationPiece == p.Key ? " (selected)" : "")));
                Add("Confirm entire formation", () => Send(new AbilityCommand { kind = "ConfirmFormation",
                    formation = formation.Select(p => new FormationPlacement { pieceId = p.Key, square = p.Value }).ToList() }), "ConfirmFormation");
            }
            Add("Cancel targeting", CancelTargeting, "CancelTargeting");
        }
        return view;
    }
    internal static string BuffText(int id) => id >= 0 && id < Buffs.Length ? Buffs[id] : "Unknown buff " + id;
    private void CancelTargeting() { action = null; actionPieceKind = null; targets.Clear(); formation.Clear(); formationPiece = null;buffVfx?.ClearPreview(); }
    private void Send(AbilityCommand command)
    { if (!CanInteract) return; CancelTargeting(); owner.SendAbility(command); }
    public bool HandleClick(Vector2Int square, int? pieceId)
    {
        if (!Visible || draftView.IsDraftVisible || aiming) return true;
        if (string.IsNullOrEmpty(action)) return false;
        if (!owner.CanSendOnlineCommand) return true;
        if ((int?)state?.chaos?["rulesVersion"] == 3 && action != "ConfirmFormation") return HandleChaos35Click(square, pieceId);
        string targetSquare = ((char)('a' + square.x)).ToString() + (square.y + 1);
        if (action == "ConfirmFormation")
        {
            if(!FormationCell(square))return true;
            if (formationPiece.HasValue)
            {
                string previous = formation[formationPiece.Value];
                var displaced = formation.FirstOrDefault(p => p.Value == targetSquare && p.Key != formationPiece.Value);
                if (displaced.Value != null) formation[displaced.Key] = previous;
                formation[formationPiece.Value] = targetSquare; formationPiece = null;
            }
            else {var virtualPiece=formation.FirstOrDefault(p=>p.Value==targetSquare);if(virtualPiece.Value!=null)formationPiece=virtualPiece.Key;}
            nextPreview=0;RefreshInteractionVisuals();
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
        if(buffVfx)buffVfx.Paused=game.PauseLocked;
        RefreshInteractionVisuals();
        draftView.SetOverlayVisible(Visible);
        draftView.SetOnlineAvailability(CanInteract);
        abilityView.SetVisible(Visible);
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
        bool shouldAim = state.IsActive && state.chaos?["rifle"] is JObject rifle && (string)rifle["team"] == team;
        if (shouldAim && !aiming)
        {
            camera = Camera.main; if (!camera) camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (!camera) return;
            orbit = camera.GetComponent<ChessOrbitCamera>();
            savedPosition = camera.transform.position; savedRotation = camera.transform.rotation;
            savedOrthographic = camera.orthographic; savedOrthographicSize = camera.orthographicSize;
            savedFieldOfView = camera.fieldOfView; savedNearClipPlane = camera.nearClipPlane;
            savedOrbitEnabled = orbit && orbit.enabled; if (orbit) orbit.enabled = false;
            camera.orthographic = false; camera.fieldOfView = 60f; camera.nearClipPlane = .03f;
            camera.ResetProjectionMatrix();
            camera.nonJitteredProjectionMatrix = camera.projectionMatrix;
            var shooter = state.board.FirstOrDefault(p => p.team == team && p.kind == "King");
            var view = shooter == null ? null : game.OnlinePieceView(shooter.id);
            if (view)
                foreach (var renderer in view.GetComponentsInChildren<Renderer>())
                { hiddenShooter[renderer] = renderer.enabled; renderer.enabled = false; }
            yaw = team == "White" ? 0 : 180; pitch = 0; aiming = true;
            if(buffVfx&&buffVfx.Ready)rifleViewModel=buffVfx.CreateRifleView(camera.transform);
        }
        else if (!shouldAim && aiming) RestoreCamera();
        if (aiming) PlaceRifleCamera();
    }
    private void PlaceRifleCamera()
    {
        var rifle = state.chaos?["rifle"]; if (rifle == null || !game.Board || !camera) return;
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
        if(rifleViewModel){UnityEngine.Object.Destroy(rifleViewModel);rifleViewModel=null;}
        foreach (var renderer in hiddenShooter) if (renderer.Key) renderer.Key.enabled = renderer.Value;
        hiddenShooter.Clear();
        if (camera)
        {
            camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
            camera.orthographic = savedOrthographic; camera.orthographicSize = savedOrthographicSize;
            camera.fieldOfView = savedFieldOfView; camera.nearClipPlane = savedNearClipPlane;
        }
        if (orbit) orbit.enabled = savedOrbitEnabled;
        aiming = false;
    }
    private void RefreshMarkers()
    {
        if(!visualUnavailable&&(int?)state.chaos?["rulesVersion"]==3&&state.chaos?["visualBoard"] is JArray)
        {if(markers){markers.SetActive(false);UnityEngine.Object.Destroy(markers);markers=null;}markerFingerprint=null;return;}
        string key = string.Join("|", new[] { state.chaos?["mines"]?.ToString(), state.chaos?["crates"]?.ToString(), state.chaos?["collapsedFiles"]?.ToString() });
        if (key == markerFingerprint) return;
        markerFingerprint = key; if (markers) { markers.SetActive(false); UnityEngine.Object.Destroy(markers); }
        if (!game.Board) return;
        markers = new GameObject("Online CHAOS markers");
        foreach (var mine in state.chaos?["mines"] ?? new JArray()) CreateMarker((string)mine["square"], Color.red, .4f, "Mine");
        foreach (var crate in state.chaos?["crates"] ?? new JArray()) CreateMarker((string)crate["square"], Color.yellow, .5f, "Crate " + (string)crate["kind"]);
        int collapsed = (int?)state.chaos?["collapsedFiles"] ?? 0;
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
    {
        disposed = true;
        RestoreCamera();
        if (uiHost) { uiHost.SetActive(false); UnityEngine.Object.Destroy(uiHost); }
        foreach (var definition in definitions) if (definition) UnityEngine.Object.Destroy(definition);
        definitions.Clear();
        if (markers) { markers.SetActive(false); UnityEngine.Object.Destroy(markers); }
    }
}
