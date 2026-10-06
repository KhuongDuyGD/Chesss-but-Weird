using System;
using System.Collections.Generic;
using System.Linq;
using ChessButWeird.Online;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public partial class ChessGame
{
    private MatchState dotNetState;
    private readonly Dictionary<int, ChessPiece> dotNetPieces = new Dictionary<int, ChessPiece>();
    public event Action<ChessMove> OnlineMoveRequested;
    public event Func<Vector2Int, int?, bool> OnlineBoardClicked;
    public Func<Vector2, bool> OnlinePointerBlocked;
    public bool UsesDotNetOnline => dotNetState != null;
    private bool onlinePromotionPending;

    private void ResetDotNetPresentation()
    { dotNetState = null; dotNetPieces.Clear(); onlinePromotionPending = false; }

    public void BeginDotNetMatch(MatchState state)
    {
        ResetDotNetPresentation();
        aramCoordinator?.EndMatch();
        var local = state.players.Single(p => p.userId == PlayerAuthService.UserId);
        serverAuthoritativeMode = true; botMode = false; aramMode = state.aram != null;
        GameMusicManager.PlayInGameMusic(false, StockfishDifficulty.Medium);
        BeginGameInternal(PieceTeam.White, ParseOnlineTeam(local.color), true, PieceTeam.White);
        SetActiveMatchIdentity(state.matchId);
        // The old ARAM runtime is not started: online mechanics come from the
        // backend's v2 snapshot, independently of newer local practice rules.
        ClearRuntimePiecesRoot(); ClearPieceMap();
        ApplyDotNetState(state);
    }
    public void ApplyDotNetState(MatchState state)
    {
        if (state == null || runtimePiecesRoot == null) return;
        bool changed = dotNetState == null || !SameBoard(dotNetState, state);
        bool turnChanged = dotNetState == null || dotNetState.turn != state.turn || dotNetState.status != state.status;
        dotNetState = state;
        if (changed)
        {
            StopCheckWarning(); ClearSelection(); pieceAnimator?.CancelAll(); ClearPieceMap();
            var live = new HashSet<int>(state.board.Select(p => p.id));
            foreach (int id in dotNetPieces.Keys.Where(id => !live.Contains(id)).ToList())
            {
                if (dotNetPieces[id]) { dotNetPieces[id].gameObject.SetActive(false); Destroy(dotNetPieces[id].gameObject); }
                dotNetPieces.Remove(id);
            }
            foreach (var p in state.board)
            {
                var square = ParseOnlineSquare(p.square);
                var team = ParseOnlineTeam(p.team);
                if (!Enum.TryParse(p.kind, out PieceType kind)) throw new InvalidOperationException("Unsupported server piece kind.");
                dotNetPieces.TryGetValue(p.id, out var view);
                if (view && (view.Type != kind || view.Team != team))
                { view.gameObject.SetActive(false); Destroy(view.gameObject); view = null; }
                if (!view) view = CreateCosmeticPiece(kind, team, square);
                view.Initialize(team, square, p.forward); if (p.hasMoved) view.MarkMoved();
                dotNetPieces[p.id] = view; pieces[square.x, square.y] = view;
                view.gameObject.name = $"Online {p.id} {team} {kind}";
                bool decoy = state.aram?["sides"]?.SelectMany(s => s["effects"] ?? new Newtonsoft.Json.Linq.JArray())
                    .Any(e => (int?)e["pieceId"] == p.id && (bool?)e["decoy"] == true) == true;
                var tag = view.GetComponent<AramDecoyTag>();
                if (decoy && !tag) view.gameObject.AddComponent<AramDecoyTag>();
                else if (!decoy && tag) Destroy(tag);
                if (!decoy) CacheKing(view);
                MovePieceToTile(view, square, 0);
                if (state.aram != null) ConfigureOnlineHitbox(view, p.kind, state);
            }
        }
        currentTurn = ParseOnlineTeam(state.turn);
        halfMoveClock = state.halfMoveClock;
        gameStarted = !gameOver;
        inputLocked = state.status != "InProgress";
        if (!gameOver) status = ChessGameStatus.Playing;
        SetRuntimePiecesVisible(true); chessboard?.SetPresentationVisible(true);
        if (!gameOver && turnChanged && !onlinePromotionPending) turnSelectionUI?.SetTurn(currentTurn);
        RefreshLocalInteractionState();
        if (!gameOver && state.aram == null && !string.IsNullOrEmpty(state.fen))
        {
            if (ChessButWeird.Domain.ClassicRules.IsInCheck(ChessButWeird.Domain.FenCodec.Parse(state.fen).Board, (ChessButWeird.Domain.Team)currentTurn)) ShowCheckWarning(currentTurn);
            else StopCheckWarning();
        }
        if (state.status == "Finished" && state.result != null && !gameOver)
        {
            if (state.result.outcome == "Draw") FinishDraw(state.result.reason);
            else FinishGame(state.result.outcome == "BlackWin" ? PieceTeam.Black : PieceTeam.White);
        }
    }
    private static bool SameBoard(MatchState a, MatchState b) => a.turn == b.turn && a.board.Count == b.board.Count &&
        a.board.Zip(b.board, (x, y) => x.id == y.id && x.square == y.square && x.kind == y.kind && x.team == y.team &&
            x.hasMoved == y.hasMoved && x.forward == y.forward).All(equal => equal);
    public void SetOnlineInputAvailable(bool available)
    { if (dotNetState != null) { inputLocked = !available || onlinePromotionPending || dotNetState.status != "InProgress"; RefreshLocalInteractionState(); } }
    public int? OnlinePieceId(ChessPiece piece) => dotNetPieces.Where(p => p.Value == piece).Select(p => (int?)p.Key).FirstOrDefault();
    public ChessPiece OnlinePieceView(int id) => dotNetPieces.TryGetValue(id, out var piece) ? piece : null;
    public bool TryGetOnlineBuffSummary(PieceTeam team, out string name, out string details)
    {
        name = details = null;
        if (dotNetState?.aram == null) return false;
        var side = dotNetState.aram["sides"]?.FirstOrDefault(s => (string)s["team"] == team.ToString());
        int? id = (int?)side?["buffId"];
        details = id.HasValue ? OnlineAramPresenter.BuffText(id.Value) : "The player is choosing a buff.";
        name = id.HasValue ? details.Split(':')[0] : "Choosing...";
        if (id.HasValue) details += $"\nOwner turns: {(int?)side["completedTurns"] ?? 0}; combat tickets: {(int?)side["tickets"] ?? 0}.";
        return true;
    }
    public static Vector2Int ParseOnlineSquare(string square)
    {
        var parsed = ChessButWeird.Domain.FenCodec.ParseSquare(square);
        return new Vector2Int(parsed.File, parsed.Rank);
    }
    private static PieceTeam ParseOnlineTeam(string team) => team == "Black" ? PieceTeam.Black : PieceTeam.White;

    private void HandleDotNetInput()
    {
        if (!gameStarted || gameOver || pauseLocked || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (OnlinePointerBlocked?.Invoke(Mouse.current.position.ReadValue()) == true) return;
        var camera = GetGameplayCamera(); if (!camera) return;
        var ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out var hit, BoardClickRaycastDistance, boardClickRaycastMask)) { ClearSelection(); return; }
        var clicked = hit.collider.GetComponentInParent<ChessPiece>();
        Vector2Int square;
        if (clicked) square = clicked.BoardPosition;
        else if (!chessboard.TryGetTileFromObject(hit.collider.gameObject, out square)) return;
        if (OnlineBoardClicked?.Invoke(square, clicked ? OnlinePieceId(clicked) : null) == true) return;
        if (inputLocked || currentTurn != localControlledTeam) return;
        if (selectedPiece && clicked != selectedPiece)
        {
            bool sacrifice = dotNetState.aram?["sides"]?.Any(s => (string)s["team"] == localControlledTeam.ToString() && (int?)s["buffId"] == 6) == true;
            if (!clicked || clicked.Team != currentTurn || sacrifice && clicked.Type == PieceType.Pawn && selectedPiece.Type == PieceType.Pawn)
            { TryMoveSelectedPiece(square); return; }
        }
        if (clicked && clicked.Team == currentTurn) TrySelectPiece(clicked);
        else if (selectedPiece) TryMoveSelectedPiece(square);
    }
    private bool RequestDotNetMove(ChessPiece piece, Vector2Int destination)
    {
        var from = piece.BoardPosition;
        if (from == destination || currentTurn != localControlledTeam || dotNetState.status != "InProgress") return false;
        ClearSelection(); inputLocked = true; RefreshLocalInteractionState();
        if (piece.Type == PieceType.Pawn && destination.y == (piece.ForwardDirection > 0 ? 7 : 0))
        {
            onlinePromotionPending = true;
            turnSelectionUI?.ShowPromotionChoice(piece.Team, promotion =>
            { onlinePromotionPending = false; OnlineMoveRequested?.Invoke(new ChessMove(from, destination, promotion)); });
        }
        else OnlineMoveRequested?.Invoke(new ChessMove(from, destination));
        return true;
    }
    private List<Vector2Int> GetDotNetMoveHints(ChessPiece piece)
    {
        // Hints never adjudicate a server command. Classic hints use the shared
        // domain engine; ARAM highlights geometric candidates from v2 effects.
        safeMoveBuffer.Clear();
        if (dotNetState.aram == null && !string.IsNullOrEmpty(dotNetState.fen))
        {
            var state = ChessButWeird.Domain.FenCodec.Parse(dotNetState.fen);
            foreach (var move in ChessButWeird.Domain.ClassicRules.LegalMovesFrom(state,
                new ChessButWeird.Domain.Square(piece.BoardPosition.x, piece.BoardPosition.y)))
            { var to = new Vector2Int(move.To.File, move.To.Rank); if (!safeMoveBuffer.Contains(to)) safeMoveBuffer.Add(to); }
        }
        else
        {
            var board = new UnityBoardAdapter(pieces);
            var side = dotNetState.aram?["sides"]?.FirstOrDefault(s => (string)s["team"] == piece.Team.ToString());
            var effect = side?["effects"]?.FirstOrDefault(e => (int?)e["pieceId"] == OnlinePieceId(piece));
            int? buff = (int?)side?["buffId"];
            var id = OnlinePieceId(piece);
            var context = new ChessButWeird.Domain.AramPieceContext(buff.HasValue ? (ChessButWeird.Domain.AramBuffs)(1 << buff.Value) : 0,
                side?["commandantPawns"]?.Any(p => (int)p == id) == true,
                (int?)side?["swappedKnight"] == id && (bool?)side?["swapActive"] == true,
                (int?)side?["swappedBishop"] == id && (bool?)side?["swapActive"] == true,
                (int?)side?["originalQueen"] == id,
                (int?)side?["teleportUses"] < 5 && (int?)side?["completedTurns"] >= (int?)side?["teleportReady"],
                (bool?)effect?["bloodthirsty"] == true, (int?)effect?["cannonUntil"] > (int?)side?["completedTurns"], (bool?)effect?["decoy"] == true);
            var from = new ChessButWeird.Domain.Square(piece.BoardPosition.x, piece.BoardPosition.y);
            var p = board.GetPiece(from);
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            {
                var to = new ChessButWeird.Domain.Square(x, y);
                if (ChessButWeird.Domain.MovementRules.IsLegalPattern(board, p, from, to) ||
                    ChessButWeird.Domain.AramRules.BuiltIn.Allows(board, p, from, to, context)) safeMoveBuffer.Add(new Vector2Int(x, y));
            }
        }
        return safeMoveBuffer;
    }
    private void ConfigureOnlineHitbox(ChessPiece view, string kind, MatchState state)
    {
        var spec = state.aram?["hitboxes"]?.FirstOrDefault(b => (string)b["kind"] == kind);
        if (spec == null) return;
        foreach (var collider in view.GetComponentsInChildren<Collider>()) collider.enabled = false;
        var child = view.transform.Find("Online Canonical Hitbox");
        if (!child) { child = new GameObject("Online Canonical Hitbox", typeof(BoxCollider)).transform; }
        var x = chessboard.GetTileCenterWorld(Vector2Int.right) - chessboard.GetTileCenterWorld(Vector2Int.zero);
        var z = chessboard.GetTileCenterWorld(Vector2Int.up) - chessboard.GetTileCenterWorld(Vector2Int.zero);
        var up = Vector3.Cross(z, x).normalized;
        child.SetParent(null);
        child.position = chessboard.GetTileCenterWorld(view.BoardPosition) + up * ((float)spec["height"] * x.magnitude * .5f);
        child.rotation = Quaternion.LookRotation(z.normalized, up);
        child.localScale = new Vector3((float)spec["halfWidth"] * 2 * x.magnitude, (float)spec["height"] * x.magnitude, (float)spec["halfDepth"] * 2 * z.magnitude);
        child.SetParent(view.transform, true);
        child.GetComponent<BoxCollider>().enabled = true;
    }

    public void RestoreDotNetMoves(List<MoveRecord> records, MatchState snapshot)
    {
        if (snapshot == null || snapshot.matchId != dotNetState?.matchId) return;
        var entries = new List<ChessButWeird.Application.MatchStatistics.Entry>();
        int number = 0; bool capturesKnown = snapshot.aram == null;
        var classic = ChessButWeird.Domain.FenCodec.Parse(ChessButWeird.Domain.FenCodec.InitialPosition);
        foreach (var record in records.OrderBy(r => r.sequence))
        {
            if (record.sequence > snapshot.eventSequence) continue;
            var actor = snapshot.players.First(p => p.userId == record.userId).color == "Black" ? ChessButWeird.Domain.Team.Black : ChessButWeird.Domain.Team.White;
            var body = Newtonsoft.Json.Linq.JObject.Parse(record.payloadJson);
            bool move = record.kind == "Move";
            string text = move ? (string)body["from"] + "-" + (string)body["to"] + (body["promotion"]?.Type == Newtonsoft.Json.Linq.JTokenType.String ? "=" + (string)body["promotion"] : "") : (string)body["kind"];
            ChessButWeird.Domain.PieceKind? captured = null;
            if (move)
            {
                number++;
                if (capturesKnown)
                {
                    string from = (string)body["from"], to = (string)body["to"];
                    capturesKnown &= ChessButWeird.Application.MatchStatistics.TryClassicCapture(ChessButWeird.Domain.FenCodec.Write(classic), from, to, out _, out captured);
                    ChessButWeird.Domain.PieceKind? promotion = Enum.TryParse((string)body["promotion"], out ChessButWeird.Domain.PieceKind kind) ? (ChessButWeird.Domain.PieceKind?)kind : null;
                    if (ChessButWeird.Domain.ClassicRules.TryApply(classic, new ChessButWeird.Domain.Move(ChessButWeird.Domain.FenCodec.ParseSquare(from), ChessButWeird.Domain.FenCodec.ParseSquare(to), promotion), out var applied)) classic = applied.State;
                    else capturesKnown = false;
                }
            }
            entries.Add(new ChessButWeird.Application.MatchStatistics.Entry(record.id, number, actor, text, move, false, captured));
        }
        statistics.Restore(entries, number, true, capturesKnown);
        // The contract does not expose startedAt in state. Do not invent a
        // recovered duration or infer ARAM captures from missing pieces.
        statisticsTimeKnown = false; SyncPresentationStatistics();
    }
}
