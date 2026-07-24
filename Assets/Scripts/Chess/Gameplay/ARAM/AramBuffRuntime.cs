using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AramBuffRuntime : MonoBehaviour
{
    private const int PracticeRollOptionCount = 3;
    private const int CommandantPawnTargetCount = 3;

    private readonly AramTeamState whiteState = new AramTeamState(PieceTeam.White);
    private readonly AramTeamState blackState = new AramTeamState(PieceTeam.Black);
    private readonly List<AramBuffDefinition> draftPool = new List<AramBuffDefinition>();
    private readonly List<Vector2Int> scratchMoves = new List<Vector2Int>(64);
    private readonly List<AramBuffPieceMarker> activeMarkers = new List<AramBuffPieceMarker>();
    private readonly Queue<AramTargetTask> targetTasks = new Queue<AramTargetTask>();
    private readonly List<ChessPiece> pendingTargetPieces = new List<ChessPiece>();
    private ChessGame game;
    private AramBuffDraftView draftView;
    private AramTargetTask currentTargetTask;
    private AramTargetKind currentTargetKind;
    private bool active;
    private bool networkMatch;
    private PieceTeam visibleTeam = PieceTeam.White;
    private BackendAramStatePayload lastNetworkState;

    public bool IsActive => active;
    public bool IsNetworkMatch => active && networkMatch;
    public bool IsSelectingSetupTargets => active && currentTargetKind != AramTargetKind.None;

    public IReadOnlyList<AramBuffDefinition> GetBuffs(PieceTeam team)
    {
        return GetState(team).Buffs;
    }

    public void BeginMatch(ChessGame owner)
    {
        game = owner;
        active = true;
        networkMatch = false;
        lastNetworkState = null;
        whiteState.Reset();
        blackState.Reset();
        draftPool.Clear();
        draftPool.AddRange(AramBuffLibrary.CreateBuiltInDefinitions());

        EnsureDraftView();
        game?.SetAramInputLocked(true);
        RollFairPracticeBuffOptions(out List<AramBuffDefinition> whiteOptions, out List<AramBuffDefinition> blackOptions);
        draftView.ShowPracticeRoll(whiteOptions, blackOptions, HandleDraftCompleted);
    }

    public void BeginNetworkMatch(ChessGame owner, PieceTeam localTeam, string seed, BackendAramStatePayload serverState)
    {
        game = owner;
        active = true;
        networkMatch = true;
        visibleTeam = localTeam;
        lastNetworkState = serverState;
        whiteState.Reset();
        blackState.Reset();
        draftPool.Clear();
        draftPool.AddRange(AramBuffLibrary.CreateBuiltInDefinitions());
        EnsureDraftView();

        if (serverState != null)
            ApplyNetworkState(serverState);
        else
            BuildDeterministicNetworkState(seed);

        game?.SetAramInputLocked(false);
        ShowCurrentHud();
    }

    public void EndMatch()
    {
        active = false;
        networkMatch = false;
        lastNetworkState = null;
        currentTargetKind = AramTargetKind.None;
        targetTasks.Clear();
        pendingTargetPieces.Clear();
        ClearMarkers();
        whiteState.Reset();
        blackState.Reset();
        if (draftView)
            draftView.HideAll();
    }

    public void OnTurnStarted(PieceTeam team)
    {
        if (!active)
            return;

        GetState(team).TickTurnCooldowns();
        ShowCurrentHud();
    }

    public bool HasBuff(PieceTeam team, AramBuffId id)
    {
        return active && GetState(team).HasBuff(id);
    }

    public void AddCandidateMoves(ChessPiece piece, List<Vector2Int> moves, ChessPiece[,] board)
    {
        if (!active || !piece || moves == null || board == null)
            return;

        scratchMoves.Clear();
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                Vector2Int destination = new Vector2Int(x, y);
                if (moves.Contains(destination))
                    continue;
                if (IsLegalAramMove(piece, piece.BoardPosition, destination, board))
                    scratchMoves.Add(destination);
            }

        for (int i = 0; i < scratchMoves.Count; i++)
            moves.Add(scratchMoves[i]);
    }

    public bool IsLegalAramMove(ChessPiece piece, Vector2Int from, Vector2Int destination, ChessPiece[,] board)
    {
        if (!active || !piece || board == null || !ChessMoveRules.IsInsideBoard(from) || !CanUseDestination(piece, destination, board))
            return false;

        AramTeamState state = GetState(piece.Team);
        Vector2Int delta = destination - from;

        if (state.HasBuff(AramBuffId.CommandantPawn) &&
            piece.Type == PieceType.Pawn &&
            state.CommandantPawns.Contains(piece) &&
            delta.x == 0 &&
            delta.y == piece.ForwardDirection * 2)
        {
            int middleY = from.y + piece.ForwardDirection;
            return ChessMoveRules.IsInsideBoard(new Vector2Int(from.x, middleY)) &&
                board[from.x, middleY] == null &&
                board[destination.x, destination.y] == null;
        }

        if (state.HasBuff(AramBuffId.FreestyleLeap) && piece.Type == PieceType.Knight)
        {
            int absoluteX = Mathf.Abs(delta.x);
            int absoluteY = Mathf.Abs(delta.y);
            bool normalKnight = (absoluteX == 1 && absoluteY == 2) || (absoluteX == 2 && absoluteY == 1);
            bool diagonalLeap = absoluteX == 2 && absoluteY == 2;
            if (normalKnight || diagonalLeap)
                return true;
        }

        if (state.HasBuff(AramBuffId.Doppelganger))
        {
            if (piece == state.SwappedKnight)
                return IsBishopPattern(from, destination, board);
            if (piece == state.SwappedBishop)
                return IsKnightPattern(delta);
        }

        if (state.HasBuff(AramBuffId.FlyingThunderGod) &&
            piece == state.OriginalQueen &&
            board[destination.x, destination.y] == null &&
            state.CanUseQueenTeleport(piece))
        {
            return true;
        }

        return false;
    }

    public bool SuppressesStandardMovement(ChessPiece piece)
    {
        if (!active || !piece)
            return false;

        AramTeamState state = GetState(piece.Team);
        return state.HasBuff(AramBuffId.Doppelganger) &&
            (piece == state.SwappedKnight || piece == state.SwappedBishop);
    }

    public bool CanAramPieceAttackSquare(ChessPiece piece, Vector2Int square, ChessPiece[,] board)
    {
        if (!active || !piece || board == null || !ChessMoveRules.IsInsideBoard(square) || piece.BoardPosition == square)
            return false;

        AramTeamState state = GetState(piece.Team);
        Vector2Int delta = square - piece.BoardPosition;
        if (state.HasBuff(AramBuffId.FreestyleLeap) && piece.Type == PieceType.Knight)
        {
            int absoluteX = Mathf.Abs(delta.x);
            int absoluteY = Mathf.Abs(delta.y);
            if ((absoluteX == 1 && absoluteY == 2) || (absoluteX == 2 && absoluteY == 1) || (absoluteX == 2 && absoluteY == 2))
                return true;
        }

        if (!state.HasBuff(AramBuffId.Doppelganger))
            return false;

        if (piece == state.SwappedKnight)
            return IsBishopPattern(piece.BoardPosition, square, board);
        if (piece == state.SwappedBishop)
            return IsKnightPattern(delta);

        return false;
    }

    public bool AllowsStrongFortressCastle(PieceTeam team)
    {
        return HasBuff(team, AramBuffId.StrongFortress);
    }

    public void OnMoveAccepted(ChessPiece piece, Vector2Int from, Vector2Int destination, ChessPiece[,] board)
    {
        if (!active || !piece)
            return;

        AramTeamState state = GetState(piece.Team);
        if (!state.HasBuff(AramBuffId.FlyingThunderGod) || piece != state.OriginalQueen)
            return;

        Vector2Int delta = destination - from;
        bool queenPattern = (delta.x == 0 || delta.y == 0 || Mathf.Abs(delta.x) == Mathf.Abs(delta.y)) &&
            IsPathClear(from, destination, board);
        if (!queenPattern)
            state.MarkQueenTeleportUsed(piece, 5);
    }

    public bool TryGetQueenExplosion(ChessPiece capturedPiece, Vector2Int center, ChessPiece movingPiece, ChessPiece[,] board, List<ChessPiece> victims)
    {
        if (!active || !capturedPiece || capturedPiece.Type != PieceType.Queen || board == null || victims == null)
            return false;

        AramTeamState state = GetState(capturedPiece.Team);
        if (!state.HasBuff(AramBuffId.SuicideBomber) || capturedPiece != state.OriginalQueen || state.SuicideBomberUsed)
            return false;

        state.SuicideBomberUsed = true;
        victims.Clear();
        for (int x = center.x - 1; x <= center.x + 1; x++)
            for (int y = center.y - 1; y <= center.y + 1; y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                if (!ChessMoveRules.IsInsideBoard(position) || position == center)
                    continue;

                ChessPiece victim = board[x, y];
                if (!victim || victim == movingPiece || victim.Type == PieceType.King || victims.Contains(victim))
                    continue;

                victims.Add(victim);
            }

        if (!networkMatch || capturedPiece.Team == visibleTeam)
            draftView?.ShowBuffToast(capturedPiece.Team, "Suicide Bomber detonated", state.GetBuff(AramBuffId.SuicideBomber));
        return true;
    }

    public bool TryHandleSetupPieceClick(ChessPiece piece)
    {
        if (!IsSelectingSetupTargets)
            return false;

        if (!piece)
        {
            ShowTargetWarning("Select one of the highlighted team pieces.");
            return true;
        }

        if (piece.Team != currentTargetTask.Team)
        {
            ShowTargetWarning($"{currentTargetTask.Team} setup: choose a {currentTargetTask.Team} piece.");
            return true;
        }

        switch (currentTargetKind)
        {
            case AramTargetKind.CommandantPawn:
                return TrySelectCommandantPawn(piece);
            case AramTargetKind.DoppelgangerKnight:
                return TrySelectDoppelgangerKnight(piece);
            case AramTargetKind.DoppelgangerBishop:
                return TrySelectDoppelgangerBishop(piece);
            default:
                return true;
        }
    }

    public void ApplyNetworkState(BackendAramStatePayload serverState)
    {
        if (!active || !networkMatch || serverState == null)
            return;

        lastNetworkState = serverState;
        ApplyNetworkTeamState(whiteState, serverState.white);
        ApplyNetworkTeamState(blackState, serverState.black);
        ApplyAllMarkers();
        ShowCurrentHud();
    }

    public BackendAramStatePayload CaptureNetworkState()
    {
        if (!active || !networkMatch)
            return null;

        return new BackendAramStatePayload
        {
            version = lastNetworkState != null ? lastNetworkState.version : 1,
            seed = lastNetworkState != null ? lastNetworkState.seed : string.Empty,
            white = CaptureTeamState(whiteState),
            black = CaptureTeamState(blackState)
        };
    }

    private static BackendAramTeamStatePayload CaptureTeamState(AramTeamState state)
    {
        BackendAramTeamStatePayload payload = new BackendAramTeamStatePayload
        {
            team = state.Team.ToString().ToUpperInvariant(),
            buff = state.Buffs.Count > 0 && state.Buffs[0] ? state.Buffs[0].Id.ToString() : string.Empty,
            commandantPawns = new List<string>(),
            swappedKnight = ToSquare(state.SwappedKnight),
            swappedBishop = ToSquare(state.SwappedBishop),
            originalQueen = ToSquare(state.OriginalQueen),
            suicideBomberUsed = state.SuicideBomberUsed,
            queenTeleportUses = state.GetQueenTeleportUsesForSync(),
            queenTeleportCooldown = state.GetQueenTeleportCooldownForSync()
        };
        foreach (ChessPiece pawn in state.CommandantPawns)
            if (pawn)
                payload.commandantPawns.Add(ToSquare(pawn));
        return payload;
    }

    private static string ToSquare(ChessPiece piece)
    {
        if (!piece || !ChessMoveRules.IsInsideBoard(piece.BoardPosition))
            return string.Empty;
        return $"{(char)('a' + piece.BoardPosition.x)}{(char)('1' + piece.BoardPosition.y)}";
    }

    private void ApplyNetworkTeamState(AramTeamState state, BackendAramTeamStatePayload payload)
    {
        state.Reset();
        if (payload == null)
            return;

        List<AramBuffDefinition> buffs = new List<AramBuffDefinition>();
        if (AramBuffLibrary.TryParseId(payload.buff, out AramBuffId buffId))
        {
            AramBuffDefinition definition = AramBuffLibrary.FindById(draftPool, buffId);
            if (definition)
                buffs.Add(definition);
        }
        state.SetBuffs(buffs);

        if (payload.commandantPawns != null)
            for (int i = 0; i < payload.commandantPawns.Count; i++)
            {
                ChessPiece pawn = FindPiece(state.Team, PieceType.Pawn, payload.commandantPawns[i]);
                if (pawn)
                    state.CommandantPawns.Add(pawn);
            }

        state.SwappedKnight = FindPiece(state.Team, PieceType.Knight, payload.swappedKnight);
        state.SwappedBishop = FindPiece(state.Team, PieceType.Bishop, payload.swappedBishop);
        state.OriginalQueen = string.IsNullOrWhiteSpace(payload.originalQueen)
            ? GetFirstPiece(game != null ? game.GetActivePiecesForAram(state.Team) : null, PieceType.Queen)
            : FindPiece(state.Team, PieceType.Queen, payload.originalQueen);
        state.SuicideBomberUsed = payload.suicideBomberUsed;
        state.SetQueenTeleportState(state.OriginalQueen, payload.queenTeleportUses, payload.queenTeleportCooldown);
    }

    private void BuildDeterministicNetworkState(string seed)
    {
        ConfigureDeterministicTeam(whiteState, seed, "WHITE");
        ConfigureDeterministicTeam(blackState, seed, "BLACK");
        ApplyAllMarkers();
    }

    private void ConfigureDeterministicTeam(AramTeamState state, string seed, string teamSalt)
    {
        state.Reset();
        if (draftPool.Count == 0)
            return;

        uint hash = StableHash($"{seed ?? string.Empty}|{teamSalt}|ARAM-V1");
        AramBuffDefinition selected = draftPool[(int)(hash % (uint)draftPool.Count)];
        state.SetBuffs(new List<AramBuffDefinition> { selected });
        CaptureInitialTeamState(state);

        List<ChessPiece> teamPieces = game != null ? game.GetActivePiecesForAram(state.Team) : null;
        if (teamPieces == null)
            return;

        if (selected.Id == AramBuffId.CommandantPawn)
        {
            ChessPiece[] pawns = GetSortedPieces(teamPieces, PieceType.Pawn);
            for (int i = 0; i < Mathf.Min(CommandantPawnTargetCount, pawns.Length); i++)
                state.CommandantPawns.Add(pawns[i]);
        }
        else if (selected.Id == AramBuffId.Doppelganger)
        {
            ChessPiece[] knights = GetSortedPieces(teamPieces, PieceType.Knight);
            ChessPiece[] bishops = GetSortedPieces(teamPieces, PieceType.Bishop);
            state.SwappedKnight = knights.Length > 0 ? knights[(int)(hash % (uint)knights.Length)] : null;
            state.SwappedBishop = bishops.Length > 0 ? bishops[(int)((hash >> 8) % (uint)bishops.Length)] : null;
        }
    }

    private ChessPiece FindPiece(PieceTeam team, PieceType type, string square)
    {
        if (!TryParseSquare(square, out Vector2Int position) || game == null)
            return null;

        List<ChessPiece> pieces = game.GetActivePiecesForAram(team);
        for (int i = 0; i < pieces.Count; i++)
        {
            ChessPiece piece = pieces[i];
            if (piece && piece.Type == type && piece.BoardPosition == position)
                return piece;
        }
        return null;
    }

    private void ShowCurrentHud()
    {
        if (!draftView)
            return;

        if (networkMatch)
            draftView.ShowPrivateHud(visibleTeam, GetState(visibleTeam).Buffs);
        else
            draftView.RefreshHud(whiteState.Buffs, blackState.Buffs);
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }
            return hash;
        }
    }

    private static bool TryParseSquare(string square, out Vector2Int position)
    {
        position = -Vector2Int.one;
        if (string.IsNullOrWhiteSpace(square) || square.Length != 2)
            return false;
        char file = char.ToLowerInvariant(square[0]);
        char rank = square[1];
        if (file < 'a' || file > 'h' || rank < '1' || rank > '8')
            return false;
        position = new Vector2Int(file - 'a', rank - '1');
        return true;
    }

    private void HandleDraftCompleted(List<AramBuffDefinition> whiteBuffs, List<AramBuffDefinition> blackBuffs)
    {
        whiteState.SetBuffs(whiteBuffs);
        blackState.SetBuffs(blackBuffs);
        CaptureInitialTeamState(whiteState);
        CaptureInitialTeamState(blackState);
        ClearMarkers();
        BuildTargetTaskQueue();
        if (!TryStartNextTargetTask())
            FinishSetup();
    }

    private bool TrySelectCommandantPawn(ChessPiece piece)
    {
        if (!piece || piece.Type != PieceType.Pawn)
        {
            ShowTargetWarning($"{currentTargetTask.Team} Commandant: choose a Pawn.");
            return true;
        }

        if (pendingTargetPieces.Contains(piece))
        {
            ShowTargetWarning("That Pawn is already selected.");
            return true;
        }

        pendingTargetPieces.Add(piece);
        AddMarker(piece, currentTargetTask.Buff, $"CMD {pendingTargetPieces.Count}");
        int requiredCount = Mathf.Min(CommandantPawnTargetCount, CountPieces(currentTargetTask.Team, PieceType.Pawn));
        if (pendingTargetPieces.Count < requiredCount)
        {
            ShowCurrentTargetPrompt();
            return true;
        }

        AramTeamState state = GetState(currentTargetTask.Team);
        state.CommandantPawns.Clear();
        for (int i = 0; i < pendingTargetPieces.Count; i++)
            state.CommandantPawns.Add(pendingTargetPieces[i]);

        TryStartNextTargetTask();
        return true;
    }

    private bool TrySelectDoppelgangerKnight(ChessPiece piece)
    {
        if (!piece || piece.Type != PieceType.Knight)
        {
            ShowTargetWarning($"{currentTargetTask.Team} Doppelganger: choose a Knight first.");
            return true;
        }

        GetState(currentTargetTask.Team).SwappedKnight = piece;
        AddMarker(piece, currentTargetTask.Buff, "BISH");
        TryStartNextTargetTask();
        return true;
    }

    private bool TrySelectDoppelgangerBishop(ChessPiece piece)
    {
        if (!piece || piece.Type != PieceType.Bishop)
        {
            ShowTargetWarning($"{currentTargetTask.Team} Doppelganger: choose a Bishop.");
            return true;
        }

        GetState(currentTargetTask.Team).SwappedBishop = piece;
        AddMarker(piece, currentTargetTask.Buff, "KNIGHT");
        TryStartNextTargetTask();
        return true;
    }

    private void BuildTargetTaskQueue()
    {
        targetTasks.Clear();
        EnqueueTargetTasks(whiteState);
        EnqueueTargetTasks(blackState);
    }

    private void EnqueueTargetTasks(AramTeamState state)
    {
        AramBuffDefinition commandant = state.GetBuff(AramBuffId.CommandantPawn);
        if (commandant)
            targetTasks.Enqueue(new AramTargetTask(state.Team, commandant, AramTargetKind.CommandantPawn));

        AramBuffDefinition doppelganger = state.GetBuff(AramBuffId.Doppelganger);
        if (doppelganger)
        {
            targetTasks.Enqueue(new AramTargetTask(state.Team, doppelganger, AramTargetKind.DoppelgangerKnight));
            targetTasks.Enqueue(new AramTargetTask(state.Team, doppelganger, AramTargetKind.DoppelgangerBishop));
        }
    }

    private bool TryStartNextTargetTask()
    {
        pendingTargetPieces.Clear();
        while (targetTasks.Count > 0)
        {
            currentTargetTask = targetTasks.Dequeue();
            currentTargetKind = currentTargetTask.Kind;
            if (!HasValidTargets(currentTargetTask))
                continue;

            ShowCurrentTargetPrompt();
            return true;
        }

        currentTargetKind = AramTargetKind.None;
        FinishSetup();
        return false;
    }

    private void FinishSetup()
    {
        currentTargetKind = AramTargetKind.None;
        pendingTargetPieces.Clear();
        ApplyAllMarkers();
        if (networkMatch)
            draftView.ShowPrivateHud(visibleTeam, GetState(visibleTeam).Buffs);
        else
            draftView.ShowHud(whiteState.Buffs, blackState.Buffs);
        game?.SetAramInputLocked(false);
    }

    private bool HasValidTargets(AramTargetTask task)
    {
        switch (task.Kind)
        {
            case AramTargetKind.CommandantPawn:
                return CountPieces(task.Team, PieceType.Pawn) > 0;
            case AramTargetKind.DoppelgangerKnight:
                return CountPieces(task.Team, PieceType.Knight) > 0;
            case AramTargetKind.DoppelgangerBishop:
                return CountPieces(task.Team, PieceType.Bishop) > 0;
            default:
                return false;
        }
    }

    private void ShowCurrentTargetPrompt()
    {
        RefreshCurrentTargetMarkers();

        string message;
        switch (currentTargetKind)
        {
            case AramTargetKind.CommandantPawn:
                int requiredCount = Mathf.Min(CommandantPawnTargetCount, CountPieces(currentTargetTask.Team, PieceType.Pawn));
                message = $"{currentTargetTask.Team} Commandant: select Pawns {pendingTargetPieces.Count}/{requiredCount}.";
                break;
            case AramTargetKind.DoppelgangerKnight:
                message = $"{currentTargetTask.Team} Doppelganger: select the Knight that will move like a Bishop.";
                break;
            case AramTargetKind.DoppelgangerBishop:
                message = $"{currentTargetTask.Team} Doppelganger: select the Bishop that will move like a Knight.";
                break;
            default:
                message = string.Empty;
                break;
        }

        draftView.ShowTargetPrompt(currentTargetTask.Team, message, currentTargetTask.Buff);
    }

    private void ShowTargetWarning(string message)
    {
        draftView.ShowTargetPrompt(currentTargetTask.Team, message, currentTargetTask.Buff);
    }

    private void RefreshCurrentTargetMarkers()
    {
        ClearMarkers();
        ApplyCommittedSetupTargetMarkers(whiteState);
        ApplyCommittedSetupTargetMarkers(blackState);

        switch (currentTargetKind)
        {
            case AramTargetKind.CommandantPawn:
                for (int i = 0; i < pendingTargetPieces.Count; i++)
                    AddMarker(pendingTargetPieces[i], currentTargetTask.Buff, $"CMD {i + 1}");
                AddCandidateMarkers(currentTargetTask.Team, PieceType.Pawn, currentTargetTask.Buff, "PICK", pendingTargetPieces);
                break;
            case AramTargetKind.DoppelgangerKnight:
                AddCandidateMarkers(currentTargetTask.Team, PieceType.Knight, currentTargetTask.Buff, "PICK", null);
                break;
            case AramTargetKind.DoppelgangerBishop:
                AddCandidateMarkers(currentTargetTask.Team, PieceType.Bishop, currentTargetTask.Buff, "PICK", null);
                break;
        }
    }

    private void ApplyCommittedSetupTargetMarkers(AramTeamState state)
    {
        AramBuffDefinition commandant = state.GetBuff(AramBuffId.CommandantPawn);
        if (commandant)
            foreach (ChessPiece pawn in state.CommandantPawns)
                AddMarker(pawn, commandant, "CMD");

        AramBuffDefinition doppelganger = state.GetBuff(AramBuffId.Doppelganger);
        if (doppelganger)
        {
            AddMarker(state.SwappedKnight, doppelganger, "BISH");
            AddMarker(state.SwappedBishop, doppelganger, "KNIGHT");
        }
    }

    private void AddCandidateMarkers(PieceTeam team, PieceType type, AramBuffDefinition buff, string label, List<ChessPiece> excludedPieces)
    {
        List<ChessPiece> pieces = game != null ? game.GetActivePiecesForAram(team) : null;
        if (pieces == null)
            return;

        for (int i = 0; i < pieces.Count; i++)
        {
            ChessPiece piece = pieces[i];
            if (!piece || piece.Type != type || (excludedPieces != null && excludedPieces.Contains(piece)))
                continue;

            AddMarker(piece, buff, label);
        }
    }

    private void RollFairPracticeBuffOptions(out List<AramBuffDefinition> whiteOptions, out List<AramBuffDefinition> blackOptions)
    {
        whiteOptions = new List<AramBuffDefinition>();
        blackOptions = new List<AramBuffDefinition>();

        List<AramBuffTier> tiers = GetAvailableTiers(draftPool);
        if (tiers.Count == 0)
            return;

        AramBuffTier selectedTier = tiers[UnityEngine.Random.Range(0, tiers.Count)];
        List<AramBuffDefinition> tierPool = GetBuffsByTier(draftPool, selectedTier);
        if (tierPool.Count == 0)
            return;

        int optionCount = Mathf.Min(PracticeRollOptionCount, tierPool.Count);
        whiteOptions.AddRange(PickRandomOptions(tierPool, optionCount));
        blackOptions.AddRange(PickRandomOptions(tierPool, optionCount));
    }

    private static List<AramBuffDefinition> PickRandomOptions(List<AramBuffDefinition> pool, int count)
    {
        List<AramBuffDefinition> options = new List<AramBuffDefinition>();
        if (pool == null || count <= 0)
            return options;

        options.AddRange(pool);
        Shuffle(options);
        if (options.Count > count)
            options.RemoveRange(count, options.Count - count);
        return options;
    }

    private static List<AramBuffTier> GetAvailableTiers(List<AramBuffDefinition> pool)
    {
        List<AramBuffTier> tiers = new List<AramBuffTier>();
        if (pool == null)
            return tiers;

        for (int i = 0; i < pool.Count; i++)
        {
            AramBuffDefinition buff = pool[i];
            if (buff && !tiers.Contains(buff.Tier))
                tiers.Add(buff.Tier);
        }

        return tiers;
    }

    private static List<AramBuffDefinition> GetBuffsByTier(List<AramBuffDefinition> pool, AramBuffTier tier)
    {
        List<AramBuffDefinition> result = new List<AramBuffDefinition>();
        if (pool == null)
            return result;

        for (int i = 0; i < pool.Count; i++)
            if (pool[i] && pool[i].Tier == tier)
                result.Add(pool[i]);
        return result;
    }

    private static void Shuffle<T>(List<T> values)
    {
        if (values == null)
            return;

        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T temp = values[i];
            values[i] = values[j];
            values[j] = temp;
        }
    }

    private void CaptureInitialTeamState(AramTeamState state)
    {
        state.CommandantPawns.Clear();
        state.SwappedKnight = null;
        state.SwappedBishop = null;
        state.OriginalQueen = null;

        List<ChessPiece> pieces = game != null ? game.GetActivePiecesForAram(state.Team) : null;
        if (pieces == null)
            return;

        state.OriginalQueen = GetFirstPiece(pieces, PieceType.Queen);
    }

    private void ApplyAllMarkers()
    {
        ClearMarkers();
        if (networkMatch)
            ApplyPieceMarkers(GetState(visibleTeam));
        else
        {
            ApplyPieceMarkers(whiteState);
            ApplyPieceMarkers(blackState);
        }
    }

    private void ApplyPieceMarkers(AramTeamState state)
    {
        List<ChessPiece> pieces = game != null ? game.GetActivePiecesForAram(state.Team) : null;
        if (pieces == null)
            return;

        if (state.HasBuff(AramBuffId.CommandantPawn))
        {
            AramBuffDefinition buff = state.GetBuff(AramBuffId.CommandantPawn);
            foreach (ChessPiece pawn in state.CommandantPawns)
                AddMarker(pawn, buff, "CMD");
        }

        if (state.HasBuff(AramBuffId.StrongFortress))
            AddMarker(GetFirstPiece(pieces, PieceType.King), state.GetBuff(AramBuffId.StrongFortress), "FORT");

        if (state.HasBuff(AramBuffId.FreestyleLeap))
        {
            AramBuffDefinition buff = state.GetBuff(AramBuffId.FreestyleLeap);
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i] && pieces[i].Type == PieceType.Knight)
                    AddMarker(pieces[i], buff, "LEAP");
        }

        if (state.HasBuff(AramBuffId.Doppelganger))
        {
            AramBuffDefinition buff = state.GetBuff(AramBuffId.Doppelganger);
            AddMarker(state.SwappedKnight, buff, "BISH");
            AddMarker(state.SwappedBishop, buff, "KNIGHT");
        }

        if (state.HasBuff(AramBuffId.SuicideBomber))
            AddMarker(state.OriginalQueen, state.GetBuff(AramBuffId.SuicideBomber), "BOMB");

        if (state.HasBuff(AramBuffId.FlyingThunderGod))
            AddMarker(state.OriginalQueen, state.GetBuff(AramBuffId.FlyingThunderGod), "TP");
    }

    private void AddMarker(ChessPiece piece, AramBuffDefinition buff, string label)
    {
        if (!piece || !buff)
            return;

        AramBuffPieceMarker marker = piece.GetComponent<AramBuffPieceMarker>();
        if (!marker)
            marker = piece.gameObject.AddComponent<AramBuffPieceMarker>();
        marker.Configure(buff.AccentColor, label);
        if (!activeMarkers.Contains(marker))
            activeMarkers.Add(marker);
    }

    private void ClearMarkers()
    {
        for (int i = activeMarkers.Count - 1; i >= 0; i--)
            if (activeMarkers[i])
                Destroy(activeMarkers[i]);
        activeMarkers.Clear();
    }

    private static ChessPiece[] GetSortedPieces(List<ChessPiece> pieces, PieceType type)
    {
        List<ChessPiece> matches = new List<ChessPiece>();
        if (pieces == null)
            return matches.ToArray();
        for (int i = 0; i < pieces.Count; i++)
            if (pieces[i] && pieces[i].Type == type)
                matches.Add(pieces[i]);

        matches.Sort((left, right) =>
        {
            float leftCenter = Mathf.Abs(left.BoardPosition.x - 3.5f);
            float rightCenter = Mathf.Abs(right.BoardPosition.x - 3.5f);
            int centerCompare = leftCenter.CompareTo(rightCenter);
            return centerCompare != 0 ? centerCompare : left.BoardPosition.x.CompareTo(right.BoardPosition.x);
        });
        return matches.ToArray();
    }

    private static ChessPiece GetFirstPiece(List<ChessPiece> pieces, PieceType type)
    {
        if (pieces == null)
            return null;
        for (int i = 0; i < pieces.Count; i++)
            if (pieces[i] && pieces[i].Type == type)
                return pieces[i];
        return null;
    }

    private int CountPieces(PieceTeam team, PieceType type)
    {
        List<ChessPiece> pieces = game != null ? game.GetActivePiecesForAram(team) : null;
        if (pieces == null)
            return 0;

        int count = 0;
        for (int i = 0; i < pieces.Count; i++)
            if (pieces[i] && pieces[i].Type == type)
                count++;
        return count;
    }

    private void EnsureDraftView()
    {
        if (draftView)
            return;

        draftView = gameObject.GetComponent<AramBuffDraftView>();
        if (!draftView)
            draftView = gameObject.AddComponent<AramBuffDraftView>();
    }

    private AramTeamState GetState(PieceTeam team)
    {
        return team == PieceTeam.White ? whiteState : blackState;
    }

    private enum AramTargetKind
    {
        None,
        CommandantPawn,
        DoppelgangerKnight,
        DoppelgangerBishop
    }

    private readonly struct AramTargetTask
    {
        public AramTargetTask(PieceTeam team, AramBuffDefinition buff, AramTargetKind kind)
        {
            Team = team;
            Buff = buff;
            Kind = kind;
        }

        public PieceTeam Team { get; }
        public AramBuffDefinition Buff { get; }
        public AramTargetKind Kind { get; }
    }

    private static bool CanUseDestination(ChessPiece piece, Vector2Int destination, ChessPiece[,] board)
    {
        if (!ChessMoveRules.IsInsideBoard(destination) || piece.BoardPosition == destination)
            return false;

        ChessPiece targetPiece = board[destination.x, destination.y];
        return !targetPiece || targetPiece.Team != piece.Team;
    }

    private static bool IsKnightPattern(Vector2Int delta)
    {
        int absoluteX = Mathf.Abs(delta.x);
        int absoluteY = Mathf.Abs(delta.y);
        return (absoluteX == 1 && absoluteY == 2) || (absoluteX == 2 && absoluteY == 1);
    }

    private static bool IsBishopPattern(Vector2Int from, Vector2Int destination, ChessPiece[,] board)
    {
        Vector2Int delta = destination - from;
        return Mathf.Abs(delta.x) == Mathf.Abs(delta.y) && IsPathClear(from, destination, board);
    }

    private static bool IsPathClear(Vector2Int from, Vector2Int destination, ChessPiece[,] board)
    {
        Vector2Int step = new Vector2Int(
            Mathf.Clamp(destination.x - from.x, -1, 1),
            Mathf.Clamp(destination.y - from.y, -1, 1));

        Vector2Int current = from + step;
        while (current != destination)
        {
            if (board[current.x, current.y])
                return false;
            current += step;
        }

        return true;
    }

    private sealed class AramTeamState
    {
        private readonly List<AramBuffDefinition> buffs = new List<AramBuffDefinition>();
        private readonly Dictionary<ChessPiece, int> queenTeleportCooldowns = new Dictionary<ChessPiece, int>();
        private readonly Dictionary<ChessPiece, int> queenTeleportUses = new Dictionary<ChessPiece, int>();

        public AramTeamState(PieceTeam team)
        {
            Team = team;
        }

        public PieceTeam Team { get; }
        public IReadOnlyList<AramBuffDefinition> Buffs => buffs;
        public HashSet<ChessPiece> CommandantPawns { get; } = new HashSet<ChessPiece>();
        public ChessPiece SwappedKnight { get; set; }
        public ChessPiece SwappedBishop { get; set; }
        public ChessPiece OriginalQueen { get; set; }
        public bool SuicideBomberUsed { get; set; }

        public void Reset()
        {
            buffs.Clear();
            CommandantPawns.Clear();
            queenTeleportCooldowns.Clear();
            queenTeleportUses.Clear();
            SwappedKnight = null;
            SwappedBishop = null;
            OriginalQueen = null;
            SuicideBomberUsed = false;
        }

        public void SetBuffs(List<AramBuffDefinition> newBuffs)
        {
            buffs.Clear();
            if (newBuffs == null)
                return;

            for (int i = 0; i < newBuffs.Count; i++)
                if (newBuffs[i] && !HasBuff(newBuffs[i].Id))
                    buffs.Add(newBuffs[i]);
        }

        public bool HasBuff(AramBuffId id)
        {
            for (int i = 0; i < buffs.Count; i++)
                if (buffs[i] && buffs[i].Id == id)
                    return true;
            return false;
        }

        public AramBuffDefinition GetBuff(AramBuffId id)
        {
            for (int i = 0; i < buffs.Count; i++)
                if (buffs[i] && buffs[i].Id == id)
                    return buffs[i];
            return null;
        }

        public bool CanUseQueenTeleport(ChessPiece queen)
        {
            if (!queen || GetQueenTeleportUses(queen) >= 5)
                return false;
            return GetQueenTeleportCooldown(queen) <= 0;
        }

        public void MarkQueenTeleportUsed(ChessPiece queen, int cooldown)
        {
            if (!queen)
                return;

            queenTeleportUses[queen] = GetQueenTeleportUses(queen) + 1;
            queenTeleportCooldowns[queen] = Mathf.Max(0, cooldown);
        }

        public void TickTurnCooldowns()
        {
            List<ChessPiece> keys = new List<ChessPiece>(queenTeleportCooldowns.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                ChessPiece key = keys[i];
                if (!key)
                {
                    queenTeleportCooldowns.Remove(key);
                    continue;
                }

                queenTeleportCooldowns[key] = Mathf.Max(0, queenTeleportCooldowns[key] - 1);
            }
        }

        public void SetQueenTeleportState(ChessPiece queen, int uses, int cooldown)
        {
            queenTeleportUses.Clear();
            queenTeleportCooldowns.Clear();
            if (!queen)
                return;
            queenTeleportUses[queen] = Mathf.Clamp(uses, 0, 5);
            queenTeleportCooldowns[queen] = Mathf.Max(0, cooldown);
        }

        private int GetQueenTeleportCooldown(ChessPiece queen)
        {
            return queen && queenTeleportCooldowns.TryGetValue(queen, out int cooldown) ? cooldown : 0;
        }

        private int GetQueenTeleportUses(ChessPiece queen)
        {
            return queen && queenTeleportUses.TryGetValue(queen, out int uses) ? uses : 0;
        }

        public int GetQueenTeleportUsesForSync()
        {
            return GetQueenTeleportUses(OriginalQueen);
        }

        public int GetQueenTeleportCooldownForSync()
        {
            return GetQueenTeleportCooldown(OriginalQueen);
        }
    }
}
