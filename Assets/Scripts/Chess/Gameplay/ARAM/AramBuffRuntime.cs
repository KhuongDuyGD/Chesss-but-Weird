using ChessButWeird.Domain;
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed partial class AramBuffRuntime : MonoBehaviour
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
    private readonly AramBuffDefinition[] disguisedBuff = new AramBuffDefinition[2];
    private readonly List<AramBuffDefinition>[] displayBuffs = { new List<AramBuffDefinition>(), new List<AramBuffDefinition>() };
    private readonly System.Random presentationRandom = new System.Random();
    // Keep one registry for movement, attack queries, and hypothetical king-safety moves.
    // Local games use the document rules. Online games use server snapshots.
    private readonly AramRules domainRules = AramRules.BuiltIn;
    private ChessGame game;
    private AramBuffDraftView draftView;
    private AramTargetTask currentTargetTask;
    private AramTargetKind currentTargetKind;
    private bool active;

    public bool IsActive => active;
    public bool IsSelectingSetupTargets => active && currentTargetKind != AramTargetKind.None;
    internal AramRules DomainRules => domainRules;

    public IReadOnlyList<AramBuffDefinition> GetBuffs(PieceTeam team)
    {
        return GetState(team).Buffs;
    }

    public bool DisguisesBuff(PieceTeam team, PieceTeam viewer) =>
        active && team != viewer && HasBuff(team, AramBuffId.Doppelganger);

    public IReadOnlyList<AramBuffDefinition> GetDisplayBuffs(PieceTeam team, PieceTeam viewer)
    {
        var actual = GetBuffs(team);
        if (!DisguisesBuff(team, viewer)) return actual;
        int side = Side(team);
        if (!disguisedBuff[side])
        {
            var candidates = new List<AramBuffDefinition>();
            foreach (var buff in draftPool)
                if (buff && buff.Tier == AramBuffTier.Gold && buff.Id != AramBuffId.Doppelganger) candidates.Add(buff);
            if (candidates.Count == 0) return Array.Empty<AramBuffDefinition>();
            disguisedBuff[side] = candidates[presentationRandom.Next(candidates.Count)];
        }
        var display = displayBuffs[side]; display.Clear();
        foreach (var buff in actual) display.Add(buff && buff.Id == AramBuffId.Doppelganger ? disguisedBuff[side] : buff);
        return display;
    }

    public void BeginMatch(ChessGame owner)
    {
        game = owner;
        active = true;
        ResetExtendedState();
        whiteState.Reset();
        blackState.Reset();
        draftPool.Clear();
        draftPool.AddRange(AramBuffLibrary.CreateBuiltInDefinitions());

        EnsureDraftView();
        game?.SetAramInputLocked(true);
        RollFairPracticeBuffOptions(out List<AramBuffDefinition> whiteOptions, out List<AramBuffDefinition> blackOptions);
        draftView.ShowPracticeRoll(whiteOptions, blackOptions, HandleDraftCompleted);
    }

    public void EndMatch()
    {
        ResetExtendedState();
        active = false;
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
        BeginExtendedTurn(team);
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
                if (IsLegalAramMove(piece, piece.BoardPosition, destination, board) || IsSniperAttack(piece,destination))
                    scratchMoves.Add(destination);
            }

        for (int i = 0; i < scratchMoves.Count; i++)
            moves.Add(scratchMoves[i]);
    }

    internal AramPieceContext CreateDomainContext(ChessPiece piece)
    {
        AramTeamState state = GetState(piece.Team);
        AramBuffs flags = AramBuffs.None;
        for (int i = 0; i < state.Buffs.Count; i++)
        {
            AramBuffDefinition definition = state.Buffs[i];
            if (definition) flags |= (AramBuffs)(1UL << (int)definition.Id);
        }
        return new AramPieceContext(flags, state.CommandantPawns.Contains(piece),
            piece == state.SwappedKnight && IsSwapActive(piece.Team), piece == state.SwappedBishop && IsSwapActive(piece.Team),
            piece == state.OriginalQueen, state.CanUseQueenTeleport(piece), bloodthirsty.Contains(piece), cannonTurns.ContainsKey(piece), decoys.Contains(piece),
            piece==state.FreestyleKnight,queenMastery.Contains(piece),trapPawns.Contains(piece),
            piece==state.OriginalQueen&&state.HasBuff(AramBuffId.SuicideBomber)&&!state.SuicideBomberUsed);
    }

    public bool IsLegalAramMove(ChessPiece piece, Vector2Int from, Vector2Int destination, ChessPiece[,] board)
    {
        if (!active || !piece || board == null) return false;
        return DomainRules.Allows(new UnityBoardAdapter(board), UnityBoardAdapter.ToState(piece),
            UnityBoardAdapter.ToSquare(from), UnityBoardAdapter.ToSquare(destination), CreateDomainContext(piece));
    }

    public bool SuppressesStandardMovement(ChessPiece piece)
    {
        return active && piece && AramRules.SuppressesStandardMovement(CreateDomainContext(piece));
    }

    public bool CanAramPieceAttackSquare(ChessPiece piece, Vector2Int square, ChessPiece[,] board)
    {
        if (!active || !piece || board == null) return false;
        return DomainRules.Allows(new UnityBoardAdapter(board), UnityBoardAdapter.ToState(piece),
            UnityBoardAdapter.ToSquare(piece.BoardPosition), UnityBoardAdapter.ToSquare(square),
            CreateDomainContext(piece), attack: true);
    }

    public bool AllowsStrongFortressCastle(PieceTeam team)
    {
        return StrongFortressBuff.WaivesCastleAttackChecks(
            HasBuff(team, AramBuffId.StrongFortress) ? AramBuffs.StrongFortress : AramBuffs.None);
    }

    public void OnMoveAccepted(ChessPiece piece, Vector2Int from, Vector2Int destination, ChessPiece[,] board)
    {
        if (!active || !piece)
            return;

        AramTeamState state = GetState(piece.Team);
        if(piece.Type==PieceType.King&&from.y==destination.y&&Mathf.Abs(destination.x-from.x)>=2&&
            (destination.x==2||destination.x==6)&&HasBuff(piece.Team,AramBuffId.StrongFortress))castledWings[Side(piece.Team)]|=destination.x==6?1:2;
        if (!state.HasBuff(AramBuffId.FlyingThunderGod) || piece != state.OriginalQueen)
            return;

        Vector2Int delta = destination - from;
        bool queenPattern = (delta.x == 0 || delta.y == 0 || Mathf.Abs(delta.x) == Mathf.Abs(delta.y)) &&
            MovementRules.IsPathClear(new UnityBoardAdapter(board), UnityBoardAdapter.ToSquare(from), UnityBoardAdapter.ToSquare(destination));
        if (!queenPattern)
            state.MarkQueenTeleportUsed(piece, FlyingThunderGodBuff.CooldownTurns);
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
                if (!ChessMoveRules.IsInsideBoard(position))
                    continue;

                ChessPiece victim = board[x, y];
                if (!SuicideBomberBuff.IsVictim(UnityBoardAdapter.ToState(victim),
                    UnityBoardAdapter.ToState(movingPiece).Id, UnityBoardAdapter.ToSquare(center),
                    UnityBoardAdapter.ToSquare(position)) || victims.Contains(victim))
                    continue;

                victims.Add(victim);
            }

        draftView?.ShowBuffToast(capturedPiece.Team, "Suicide Bomber detonated", state.GetBuff(AramBuffId.SuicideBomber));
        return true;
    }

    internal bool WouldQueenExplode(ChessPiece piece)
    {
        if (!active || !piece || piece.Type != PieceType.Queen) return false;
        AramTeamState state = GetState(piece.Team);
        return state.HasBuff(AramBuffId.SuicideBomber) && piece == state.OriginalQueen && !state.SuicideBomberUsed;
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
            case AramTargetKind.FreestyleKnight:
                if(piece.Type!=PieceType.Knight){ShowTargetWarning("Choose a Knight for Freestyle Leap.");return true;}
                GetState(piece.Team).FreestyleKnight=piece;TryStartNextTargetTask();return true;
            default:
                return true;
        }
    }

    private void HandleDraftCompleted(List<AramBuffDefinition> whiteBuffs, List<AramBuffDefinition> blackBuffs)
    {
        whiteState.SetBuffs(whiteBuffs);
        blackState.SetBuffs(blackBuffs);
        CaptureInitialTeamState(whiteState);
        CaptureInitialTeamState(blackState);
        ApplyInitialEffects();
        ClearMarkers();
        BuildTargetTaskQueue();
        TryStartNextTargetTask();
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
        var freestyle=state.GetBuff(AramBuffId.FreestyleLeap);
        if(freestyle)targetTasks.Enqueue(new AramTargetTask(state.Team,freestyle,AramTargetKind.FreestyleKnight));
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
        draftView.ShowHud(whiteState.Buffs, blackState.Buffs);
        game?.SetAramInputLocked(false);
        game?.AramInitializeDrawTracking();
        EnsureActionView();
    }

    private bool HasValidTargets(AramTargetTask task)
    {
        switch (task.Kind)
        {
            case AramTargetKind.CommandantPawn:
                return CountPieces(task.Team, PieceType.Pawn) > 0;
            case AramTargetKind.DoppelgangerKnight:
            case AramTargetKind.FreestyleKnight:
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
            case AramTargetKind.FreestyleKnight:
                message=$"{currentTargetTask.Team} Freestyle Leap: select one Knight.";
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
            case AramTargetKind.FreestyleKnight:
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
        state.FreestyleKnight = null;
        state.OriginalQueen = null;

        List<ChessPiece> pieces = game != null ? game.GetActivePiecesForAram(state.Team) : null;
        if (pieces == null)
            return;

        state.OriginalQueen = GetFirstPiece(pieces, PieceType.Queen);
    }

    private void ApplyAllMarkers()
    {
        ClearMarkers();
        ApplyPieceMarkers(whiteState);
        ApplyPieceMarkers(blackState);
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
            AddMarker(state.FreestyleKnight,buff,"");
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
        marker.Configure(buff.AccentColor, label=="PICK"?label:string.Empty,.35f,.55f);
        if (!activeMarkers.Contains(marker))
            activeMarkers.Add(marker);
    }

    private void ClearMarkers()
    {
        for (int i = activeMarkers.Count - 1; i >= 0; i--)
            if (activeMarkers[i])
                activeMarkers[i].enabled=false;
        activeMarkers.Clear();
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
        DoppelgangerBishop,
        FreestyleKnight
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

    private sealed class AramTeamState
    {
        private readonly List<AramBuffDefinition> buffs = new List<AramBuffDefinition>();
        private ChessPiece trackedQueen;
        private LimitedUseState queenTeleport = new LimitedUseState(FlyingThunderGodBuff.MaximumUses);

        public AramTeamState(PieceTeam team)
        {
            Team = team;
        }

        public PieceTeam Team { get; }
        public IReadOnlyList<AramBuffDefinition> Buffs => buffs;
        public HashSet<ChessPiece> CommandantPawns { get; } = new HashSet<ChessPiece>();
        public ChessPiece SwappedKnight { get; set; }
        public ChessPiece SwappedBishop { get; set; }
        public ChessPiece FreestyleKnight { get; set; }
        public ChessPiece OriginalQueen { get; set; }
        public bool SuicideBomberUsed { get; set; }

        public void Reset()
        {
            buffs.Clear();
            CommandantPawns.Clear();
            trackedQueen = null;
            queenTeleport = new LimitedUseState(FlyingThunderGodBuff.MaximumUses);
            SwappedKnight = null;
            SwappedBishop = null;
            FreestyleKnight = null;
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
            return queen && (queen != trackedQueen || queenTeleport.CanUse);
        }

        public void MarkQueenTeleportUsed(ChessPiece queen, int cooldown)
        {
            if (!queen) return;
            if (queen != trackedQueen)
            {
                trackedQueen = queen;
                queenTeleport = new LimitedUseState(FlyingThunderGodBuff.MaximumUses);
            }
            if (queenTeleport.TryConsume(cooldown, out LimitedUseState next)) queenTeleport = next;
        }

        public void TickTurnCooldowns()
        {
            if (trackedQueen) queenTeleport = queenTeleport.Tick();
        }

        private int GetQueenTeleportCooldown(ChessPiece queen)
        {
            return queen && queen == trackedQueen ? queenTeleport.Cooldown : 0;
        }

        private int GetQueenTeleportUses(ChessPiece queen)
        {
            return queen && queen == trackedQueen ? queenTeleport.Uses : 0;
        }

        public int GetQueenTeleportUses()
        {
            return GetQueenTeleportUses(OriginalQueen);
        }

        public int GetQueenTeleportCooldown()
        {
            return GetQueenTeleportCooldown(OriginalQueen);
        }
    }
}
