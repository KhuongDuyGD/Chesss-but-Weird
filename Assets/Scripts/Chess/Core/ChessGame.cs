using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class ChessGame : MonoBehaviour
{
    public enum ChessGameStatus
    {
        NotStarted,
        Playing,
        Win,
        Draw
    }

    private const int FiftyMoveRuleHalfMoveLimit = 100;

    [SerializeField] private Chessboard chessboard;
    [SerializeField] private Transform piecesRoot;
    [SerializeField] private bool spawnDefaultPiecesOnStart = true;
    [SerializeField] private bool hideImportedPieceRenderers = true;
    [SerializeField] private bool clearRuntimePiecesOnStart = true;
    [SerializeField] private float selectedPieceLiftHeight = 0.28f;
    [SerializeField] private float selectAnimationDuration = 0.12f;
    [SerializeField] private float moveAnimationDuration = 0.22f;
    [SerializeField] private float moveArcHeight = 0.2f;
    [SerializeField] private float checkPulseScale = 1.14f;
    [SerializeField] private float checkPulseDuration = 0.42f;
    [Tooltip("Local Chessboard-axis offset applied after resolving a tile center. Use X/Z to nudge all generated pieces onto the drawn squares.")]
    [SerializeField] private Vector3 pieceBoardLocalOffset;
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pickSound;
    [SerializeField] private AudioClip moveSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip errorSound;
    [SerializeField] private AudioClip castleSound;
    [SerializeField] private AudioClip promotionSound;
    [SerializeField] private AudioClip checkSound;
    [SerializeField] private AudioClip winSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private readonly ChessPiece[,] pieces = new ChessPiece[8, 8];
    private readonly Dictionary<ChessPiece, Coroutine> pieceAnimations = new Dictionary<ChessPiece, Coroutine>();
    private readonly Dictionary<string, int> positionHistory = new Dictionary<string, int>();
    private readonly HashSet<ChessPiece> startingPlacementPieces = new HashSet<ChessPiece>();
    private readonly List<string> moveHistory = new List<string>();
    private readonly List<PieceType> whiteCapturedPieces = new List<PieceType>();
    private readonly List<PieceType> blackCapturedPieces = new List<PieceType>();
    private PieceTeam currentTurn = PieceTeam.White;
    private ChessPiece selectedPiece;
    private Transform runtimePiecesRoot;
    private ChessGameStatus status = ChessGameStatus.NotStarted;
    private bool gameStarted;
    private bool gameOver;
    private bool inputLocked;
    private bool pauseLocked;
    private PieceTeam winningTeam;
    private PieceTeam playerTeam;
    private string drawReason;
    private int halfMoveClock;
    private ChessPiece checkedKing;
    private Coroutine checkPulseCoroutine;
    private Vector3 checkedKingOriginalScale;
    private ChessPiece lastMovedPiece;
    private Vector2Int lastMoveTo;
    private bool lastMoveWasPawnDoubleStep;
    private PawnPiece pendingPromotionPawn;
    private PieceTeam pendingPromotionOpponentTeam;
    private ChessTurnSelectionUI turnSelectionUI;
    private bool restrictInputToControlledTeam;
    private PieceTeam localControlledTeam = PieceTeam.White;
    private bool suppressMoveCommittedEvent;
    private bool serverAuthoritativeMode;
    private bool botMode;
    private bool pendingRemotePromotionResolution;
    private PieceType remotePromotionType = PieceType.Queen;
    private Vector2Int pendingCommittedMoveFrom = -Vector2Int.one;
    private Vector2Int pendingCommittedMoveTo = -Vector2Int.one;
    private float matchStartedAt;
    private float frozenMatchDurationSeconds;
    private float matchPausedAt = -1f;
    private float accumulatedPausedSeconds;
    private string lastMoveSummary = "No moves yet.";
    private PieceTeam moveHistoryFirstTurn = PieceTeam.White;
    public PieceTeam CurrentTurn => currentTurn;
    public ChessPiece SelectedPiece => selectedPiece;
    public bool GameStarted => gameStarted;
    public bool GameOver => gameOver;
    public bool InputLocked => inputLocked;
    public bool PauseLocked => pauseLocked;
    public bool IsBotGame => botMode;
    public PieceTeam WinningTeam => winningTeam;
    public PieceTeam PlayerTeam => playerTeam;
    public ChessGameStatus Status => status;
    public string DrawReason => drawReason;
    public int HalfMoveClock => halfMoveClock;
    public float MatchElapsedSeconds => gameStarted
        ? Mathf.Max(0f, Time.unscaledTime - matchStartedAt - accumulatedPausedSeconds - CurrentPauseDuration)
        : frozenMatchDurationSeconds;
    private float CurrentPauseDuration => matchPausedAt >= 0f ? Time.unscaledTime - matchPausedAt : 0f;
    public string LastMoveSummary => string.IsNullOrWhiteSpace(lastMoveSummary) ? "No moves yet." : lastMoveSummary;
    public IReadOnlyList<string> MoveHistory => moveHistory;
    public PieceTeam MoveHistoryFirstTurn => moveHistoryFirstTurn;
    public event Action<ChessLanMove> MoveCommitted;
    public event Action ReturnedToMainMenu;
    public event Action LocalGameRestarted;

    private void Awake()
    {
        if (!chessboard)
            chessboard = FindAnyObjectByType<Chessboard>();

        EnsureAudioSource();
        AutoAssignDefaultAudioClips();
    }

    private void EnsureAudioSource()
    {
        if (!audioSource)
            audioSource = GetComponent<AudioSource>();

        if (!audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void PlaySound(AudioClip clip)
    {
        if (!clip)
            return;

        EnsureAudioSource();
        audioSource.PlayOneShot(clip, soundVolume);
    }

    private void AutoAssignDefaultAudioClips()
    {
        LoadAudioProfile();

#if UNITY_EDITOR
        pickSound = pickSound ? pickSound : LoadEditorAudioClip("Assets/Audio/SFX/pick.mp3", "Assets/Audio/Music/pick.mp3");
        moveSound = moveSound ? moveSound : LoadEditorAudioClip("Assets/Audio/SFX/move.wav", "Assets/Audio/Music/move.wav");
        hitSound = hitSound ? hitSound : LoadEditorAudioClip("Assets/Audio/SFX/hit.mp3", "Assets/Audio/Music/hit.mp3");
        errorSound = errorSound ? errorSound : LoadEditorAudioClip("Assets/Audio/SFX/error.mp3", "Assets/Audio/Music/error.mp3");
        castleSound = castleSound ? castleSound : LoadEditorAudioClip("Assets/Audio/SFX/nhapthanh.mp3", "Assets/Audio/Music/nhapthanh.mp3");
        promotionSound = promotionSound ? promotionSound : LoadEditorAudioClip("Assets/Audio/SFX/phonghau.mp3", "Assets/Audio/Music/phonghau.mp3");
        checkSound = checkSound ? checkSound : LoadEditorAudioClip("Assets/Audio/SFX/chieu.mp3", "Assets/Audio/Music/chieu.mp3");
        winSound = winSound ? winSound : LoadEditorAudioClip("Assets/Audio/SFX/win.mp3", "Assets/Audio/Music/win.mp3");
#endif
    }

    private void LoadAudioProfile()
    {
        ChessGameAudioProfile audioProfile = Resources.Load<ChessGameAudioProfile>("Chess/ChessGameAudioProfile");
        if (!audioProfile)
            return;

        pickSound = pickSound ? pickSound : audioProfile.pickSound;
        moveSound = moveSound ? moveSound : audioProfile.moveSound;
        hitSound = hitSound ? hitSound : audioProfile.hitSound;
        errorSound = errorSound ? errorSound : audioProfile.errorSound;
        castleSound = castleSound ? castleSound : audioProfile.castleSound;
        promotionSound = promotionSound ? promotionSound : audioProfile.promotionSound;
        checkSound = checkSound ? checkSound : audioProfile.checkSound;
        winSound = winSound ? winSound : audioProfile.winSound;
    }

#if UNITY_EDITOR
    private AudioClip LoadEditorAudioClip(params string[] assetPaths)
    {
        for (int i = 0; i < assetPaths.Length; i++)
        {
            AudioClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPaths[i]);
            if (clip)
                return clip;
        }

        return null;
    }
#endif

    private void Start()
    {
        PrepareGame();
        PlayerAuthService.TryRestoreSession();
        if (PlayerAuthService.IsAuthenticated || PlayerAuthService.IsGuestSession)
        {
            BeginAuthenticatedSession();
            return;
        }

        AuthController.Create(this, QueueAuthenticatedSession);
    }

    private void QueueAuthenticatedSession()
    {
        StartCoroutine(BeginAuthenticatedSessionNextFrame());
    }

    private IEnumerator BeginAuthenticatedSessionNextFrame()
    {
        yield return null;
        yield return null;
        BeginAuthenticatedSession();
    }

    private void BeginAuthenticatedSession()
    {
        PrepareGame();
        if (!turnSelectionUI)
            turnSelectionUI = ChessTurnSelectionUI.Create(this);
        else
            turnSelectionUI.ShowMainMenu();
    }

    private void Update()
    {
        if (!gameStarted || inputLocked || pauseLocked || !CanLocalPlayerInteract() || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        HandleBoardClick();
    }

    public void RefreshPieceMap()
    {
        ClearPieceMap();

        Transform searchRoot = piecesRoot ? piecesRoot : runtimePiecesRoot ? runtimePiecesRoot : transform;
        ChessPiece[] scenePieces = searchRoot.GetComponentsInChildren<ChessPiece>(true);
        for (int i = 0; i < scenePieces.Length; i++)
        {
            ChessPiece piece = scenePieces[i];
            if (!chessboard || !chessboard.IsValidTile(piece.BoardPosition))
                continue;

            pieces[piece.BoardPosition.x, piece.BoardPosition.y] = piece;
        }

        selectedPiece = null;
        chessboard?.ClearLegalMoveHighlights();
    }

    public void BeginGame(PieceTeam firstTurn)
    {
        serverAuthoritativeMode = false;
        botMode = false;
        BeginGameInternal(firstTurn, firstTurn, false, firstTurn);
    }

    public void BeginLanGame(PieceTeam firstTurn, PieceTeam localPlayerTeam)
    {
        serverAuthoritativeMode = true;
        botMode = false;
        BeginGameInternal(firstTurn, localPlayerTeam, true, PieceTeam.White);
    }

    public void BeginBotGame(PieceTeam localPlayerTeam)
    {
        serverAuthoritativeMode = false;
        botMode = true;
        BeginGameInternal(PieceTeam.White, localPlayerTeam, true, PieceTeam.White);
    }

    private void BeginGameInternal(PieceTeam firstTurn, PieceTeam localPlayerTeam, bool restrictInput, PieceTeam frontTeam)
    {
        currentTurn = firstTurn;
        moveHistoryFirstTurn = firstTurn;
        playerTeam = localPlayerTeam;
        localControlledTeam = localPlayerTeam;
        restrictInputToControlledTeam = restrictInput;
        selectedPiece = null;
        gameOver = false;
        inputLocked = false;
        status = ChessGameStatus.Playing;
        drawReason = string.Empty;
        matchStartedAt = Time.unscaledTime;
        frozenMatchDurationSeconds = 0f;
        matchPausedAt = -1f;
        accumulatedPausedSeconds = 0f;
        lastMoveSummary = "No moves yet.";
        moveHistory.Clear();
        whiteCapturedPieces.Clear();
        blackCapturedPieces.Clear();
        ResetDrawTracking();
        ArrangePiecesForFirstTurn(frontTeam);
        SetRuntimePiecesVisible(true);
        RefreshPieceMap();
        RecordCurrentPosition();
        gameStarted = true;
        chessboard?.SetPresentationVisible(true);
        chessboard?.ClearLegalMoveHighlights();
        RefreshLocalInteractionState();
        turnSelectionUI?.SetTurn(currentTurn);
        UpdateCheckWarningForCurrentTurn();
    }

    public bool TrySelectPiece(ChessPiece piece)
    {
        if (!gameStarted || inputLocked || pauseLocked || !piece || piece.Team != currentTurn || !CanControlPieceTeam(piece.Team))
            return false;

        if (selectedPiece == piece)
        {
            DeselectCurrentPiece(true);
            return false;
        }

        DeselectCurrentPiece(true);
        selectedPiece = piece;
        chessboard?.SetLegalMoveHighlights(GetSafeLegalMoves(selectedPiece));
        AnimatePieceToTile(selectedPiece, selectedPiece.BoardPosition, selectedPieceLiftHeight, selectAnimationDuration, 0f);
        PlaySound(pickSound);
        return true;
    }

    public bool TryMoveSelectedPiece(Vector2Int destination)
    {
        if (!gameStarted || gameOver || inputLocked || pauseLocked || !selectedPiece || !chessboard || !chessboard.IsValidTile(destination))
        {
            if (selectedPiece)
                PlaySound(errorSound);

            return false;
        }

        Vector2Int from = selectedPiece.BoardPosition;
        pendingCommittedMoveFrom = from;
        pendingCommittedMoveTo = destination;
        if (!IsLegalMoveAfterKingSafety(selectedPiece, from, destination))
        {
            ClearPendingCommittedMove();
            PlaySound(errorSound);
            return false;
        }

        ChessPiece movingPiece = selectedPiece;
        ChessPiece capturedPiece = pieces[destination.x, destination.y];
        PieceTeam opponentTeam = movingPiece.Team == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        bool isEnPassant = TryGetEnPassantCapture(movingPiece, from, destination, out ChessPiece enPassantCapturedPiece, out Vector2Int enPassantCapturePosition);
        bool isCastling = TryGetCastlingRookMove(movingPiece, from, destination, out ChessPiece castlingRook, out Vector2Int castlingRookFrom, out Vector2Int castlingRookTo);
        bool movedPawn = movingPiece.Type == PieceType.Pawn;
        StopCheckWarning();

        if (isEnPassant)
            capturedPiece = enPassantCapturedPiece;

        bool capturedAnyPiece = capturedPiece;

        pieces[from.x, from.y] = null;
        if (isEnPassant)
            pieces[enPassantCapturePosition.x, enPassantCapturePosition.y] = null;

        pieces[destination.x, destination.y] = movingPiece;

        if (isCastling)
        {
            pieces[castlingRookFrom.x, castlingRookFrom.y] = null;
            pieces[castlingRookTo.x, castlingRookTo.y] = castlingRook;
            castlingRook.SetBoardPosition(castlingRookTo);
            castlingRook.MarkMoved();
        }

        movingPiece.SetBoardPosition(destination);
        movingPiece.MarkMoved();
        RecordLastMove(movingPiece, from, destination);
        lastMoveSummary = BuildMoveSummary(movingPiece, from, destination, capturedPiece, isCastling);
        moveHistory.Add(lastMoveSummary);
        if (capturedPiece)
            GetCapturedPieceList(movingPiece.Team).Add(capturedPiece.Type);
        UpdateHalfMoveClock(movedPawn, capturedAnyPiece);
        selectedPiece = null;
        chessboard.ClearLegalMoveHighlights();

        if (isCastling)
            PlaySound(castleSound);
        else if (capturedAnyPiece)
            PlaySound(hitSound);
        else
            PlaySound(moveSound);

        if (capturedPiece)
            Destroy(capturedPiece.gameObject);

        if (isCastling)
            AnimatePieceToTile(castlingRook, castlingRookTo, 0f, moveAnimationDuration, moveArcHeight * 0.5f);

        if (movingPiece is PawnPiece promotedPawn && IsPromotionRank(promotedPawn))
        {
            StartCoroutine(AnimateMoveAndPromptPromotion(promotedPawn, destination, opponentTeam));
            return true;
        }

        currentTurn = opponentTeam;
        NotifyMoveCommitted(new ChessLanMove(from, destination));
        turnSelectionUI?.SetTurn(currentTurn);
        RefreshLocalInteractionState();

        if (serverAuthoritativeMode)
        {
            UpdateCheckWarningForCurrentTurn();
            StartCoroutine(AnimateMoveAndUnlock(movingPiece, destination));
            return true;
        }

        if (TryFinishGameAfterCompletedMove(movingPiece, destination))
            return true;

        UpdateCheckWarningForCurrentTurn();
        StartCoroutine(AnimateMoveAndUnlock(movingPiece, destination));
        return true;
    }

    private bool TryFinishGameAfterCompletedMove(ChessPiece movingPiece, Vector2Int destination)
    {
        RecordCurrentPosition();

        if (IsCheckmate(currentTurn))
        {
            StartCoroutine(AnimateMoveAndFinishGame(movingPiece, destination, movingPiece.Team));
            return true;
        }

        if (TryGetDrawReason(out string reason))
        {
            StartCoroutine(AnimateMoveAndFinishDraw(movingPiece, destination, reason));
            return true;
        }

        return false;
    }

    public void ClearSelection()
    {
        DeselectCurrentPiece(true);
    }

    public void OpenTurnSelection()
    {
        if (gameStarted || gameOver)
            return;

        SetRuntimePiecesVisible(false);
        RefreshLocalInteractionState();
        turnSelectionUI?.ShowTurnSelection();
    }

    public void RestartToMainMenu()
    {
        pauseLocked = false;
        Time.timeScale = 1f;
        PrepareGame();
        turnSelectionUI?.ShowMainMenu();
        ReturnedToMainMenu?.Invoke();
    }

    public bool RestartCurrentLocalGame()
    {
        if (serverAuthoritativeMode)
            return false;

        PieceTeam selectedTeam = playerTeam;
        bool restartBotGame = botMode;
        pauseLocked = false;
        Time.timeScale = 1f;
        PrepareGame();
        if (restartBotGame)
            BeginBotGame(selectedTeam);
        else
            BeginGame(selectedTeam);
        LocalGameRestarted?.Invoke();
        return true;
    }

    public void SetPauseLocked(bool locked)
    {
        if (pauseLocked == locked)
            return;

        if (locked)
        {
            if (gameStarted)
                matchPausedAt = Time.unscaledTime;
        }
        else if (matchPausedAt >= 0f)
        {
            accumulatedPausedSeconds += Time.unscaledTime - matchPausedAt;
            matchPausedAt = -1f;
        }

        pauseLocked = locked;
        if (locked)
            ClearSelection();
        RefreshLocalInteractionState();
    }

    private void PrepareGame()
    {
        StopCheckWarning();
        StopAllCoroutines();
        pieceAnimations.Clear();

        if (piecesRoot)
        {
            runtimePiecesRoot = piecesRoot;

            if (clearRuntimePiecesOnStart)
                ClearRuntimePiecesRoot();
        }
        else if (clearRuntimePiecesOnStart)
        {
            DestroyRuntimePiecesRoots();
            runtimePiecesRoot = CreateRuntimePiecesRoot();
        }
        else
        {
            runtimePiecesRoot = FindRuntimePiecesRoot() ?? CreateRuntimePiecesRoot();
        }

        if (spawnDefaultPiecesOnStart && runtimePiecesRoot.GetComponentsInChildren<ChessPiece>(true).Length == 0)
            CreateRuntimePiecesFromVisualSet();

        if (hideImportedPieceRenderers)
            HideImportedPieceRenderers();

        RefreshPieceMap();
        AlignAllPiecesToBoard();
        SetRuntimePiecesVisible(false);
        chessboard?.SetPresentationVisible(false);
        chessboard?.SetInteractionEnabled(false);

        currentTurn = PieceTeam.White;
        status = ChessGameStatus.NotStarted;
        gameStarted = false;
        gameOver = false;
        inputLocked = false;
        pauseLocked = false;
        playerTeam = PieceTeam.White;
        localControlledTeam = PieceTeam.White;
        restrictInputToControlledTeam = false;
        serverAuthoritativeMode = false;
        botMode = false;
        winningTeam = PieceTeam.White;
        drawReason = string.Empty;
        ResetDrawTracking();
        lastMovedPiece = null;
        lastMoveTo = -Vector2Int.one;
        lastMoveWasPawnDoubleStep = false;
        pendingPromotionPawn = null;
        suppressMoveCommittedEvent = false;
        pendingRemotePromotionResolution = false;
        remotePromotionType = PieceType.Queen;
        matchStartedAt = 0f;
        frozenMatchDurationSeconds = 0f;
        matchPausedAt = -1f;
        accumulatedPausedSeconds = 0f;
        lastMoveSummary = "No moves yet.";
        moveHistory.Clear();
        whiteCapturedPieces.Clear();
        blackCapturedPieces.Clear();
        ClearPendingCommittedMove();
        RefreshLocalInteractionState();
    }

    private Transform CreateRuntimePiecesRoot()
    {
        GameObject root = new GameObject("Runtime Chess Pieces");
        root.transform.SetParent(transform);
        return root.transform;
    }

    private Transform FindRuntimePiecesRoot()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i] != transform && children[i].name == "Runtime Chess Pieces")
                return children[i];

        return null;
    }

    private void DestroyRuntimePiecesRoots()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = children.Length - 1; i >= 0; i--)
        {
            Transform child = children[i];
            if (!child || child == transform || child.name != "Runtime Chess Pieces")
                continue;

            DestroyRuntimeObject(child.gameObject);
        }
    }

    private void ClearRuntimePiecesRoot()
    {
        for (int i = runtimePiecesRoot.childCount - 1; i >= 0; i--)
            DestroyRuntimeObject(runtimePiecesRoot.GetChild(i).gameObject);
    }

    private void DestroyRuntimeObject(GameObject target)
    {
        if (!target)
            return;

        DestroyImmediate(target);
    }

    private void HandleBoardClick()
    {
        Camera currentCamera = Camera.main;
        if (!currentCamera)
            return;

        Ray ray = currentCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hitInfo, 100f))
        {
            ClearSelection();
            return;
        }

        ChessPiece clickedPiece = hitInfo.collider.GetComponentInParent<ChessPiece>();
        if (clickedPiece)
        {
            if (clickedPiece.Team == currentTurn)
                TrySelectPiece(clickedPiece);
            else if (selectedPiece)
                TryMoveSelectedPiece(clickedPiece.BoardPosition);

            return;
        }

        if (chessboard.TryGetTileFromObject(hitInfo.collider.gameObject, out Vector2Int destination))
        {
            ChessPiece pieceOnTile = pieces[destination.x, destination.y];
            if (pieceOnTile && pieceOnTile.Team == currentTurn)
            {
                TrySelectPiece(pieceOnTile);
                return;
            }

            TryMoveSelectedPiece(destination);
        }
    }

    private List<Vector2Int> GetSafeLegalMoves(ChessPiece piece)
    {
        List<Vector2Int> safeMoves = new List<Vector2Int>();
        if (!piece)
            return safeMoves;

        IReadOnlyList<Vector2Int> candidateMoves = GetCandidateMoves(piece);
        for (int i = 0; i < candidateMoves.Count; i++)
        {
            Vector2Int destination = candidateMoves[i];
            if (IsLegalMoveAfterKingSafety(piece, piece.BoardPosition, destination))
                safeMoves.Add(destination);
        }

        return safeMoves;
    }

    private bool IsLegalMoveAfterKingSafety(ChessPiece piece, Vector2Int from, Vector2Int destination)
    {
        if (!IsLegalMoveIgnoringKingSafety(piece, from, destination))
            return false;

        ChessPiece targetPiece = pieces[destination.x, destination.y];
        if (targetPiece && targetPiece.Type == PieceType.King)
            return false;

        return DoesMoveKeepTeamKingSafe(piece, from, destination, piece.Team);
    }

    private IReadOnlyList<Vector2Int> GetCandidateMoves(ChessPiece piece)
    {
        List<Vector2Int> candidateMoves = new List<Vector2Int>(piece.GetLegalMoves(pieces));
        AddSpecialCandidateMoves(piece, candidateMoves);
        return candidateMoves;
    }

    private void AddSpecialCandidateMoves(ChessPiece piece, List<Vector2Int> candidateMoves)
    {
        if (!piece)
            return;

        Vector2Int from = piece.BoardPosition;
        if (piece.Type == PieceType.Pawn)
        {
            Vector2Int leftEnPassant = new Vector2Int(from.x - 1, from.y + piece.ForwardDirection);
            Vector2Int rightEnPassant = new Vector2Int(from.x + 1, from.y + piece.ForwardDirection);
            AddSpecialCandidateIfLegal(piece, from, leftEnPassant, candidateMoves);
            AddSpecialCandidateIfLegal(piece, from, rightEnPassant, candidateMoves);
        }
        else if (piece.Type == PieceType.King)
        {
            AddSpecialCandidateIfLegal(piece, from, new Vector2Int(from.x + 2, from.y), candidateMoves);
            AddSpecialCandidateIfLegal(piece, from, new Vector2Int(from.x - 2, from.y), candidateMoves);
        }
    }

    private void AddSpecialCandidateIfLegal(ChessPiece piece, Vector2Int from, Vector2Int destination, List<Vector2Int> candidateMoves)
    {
        if (!chessboard || !chessboard.IsValidTile(destination) || candidateMoves.Contains(destination))
            return;

        if (IsEnPassantMove(piece, from, destination) || IsCastlingMove(piece, from, destination))
            candidateMoves.Add(destination);
    }

    private bool IsLegalMoveIgnoringKingSafety(ChessPiece piece, Vector2Int from, Vector2Int destination)
    {
        return ChessMoveRules.IsLegalMove(piece, from, destination, pieces) ||
            IsEnPassantMove(piece, from, destination) ||
            IsCastlingMove(piece, from, destination);
    }

    private bool DoesMoveKeepTeamKingSafe(ChessPiece piece, Vector2Int from, Vector2Int destination, PieceTeam team)
    {
        MoveSimulation simulation = ApplyMoveSimulation(piece, from, destination);

        bool kingIsSafe = !IsTeamInCheck(team);

        RestoreMoveSimulation(piece, from, destination, simulation);

        return kingIsSafe;
    }

    private MoveSimulation ApplyMoveSimulation(ChessPiece piece, Vector2Int from, Vector2Int destination)
    {
        MoveSimulation simulation = new MoveSimulation
        {
            destinationPiece = pieces[destination.x, destination.y]
        };

        if (TryGetEnPassantCapture(piece, from, destination, out ChessPiece enPassantCapturedPiece, out Vector2Int enPassantCapturePosition))
        {
            simulation.isEnPassant = true;
            simulation.enPassantCapturedPiece = enPassantCapturedPiece;
            simulation.enPassantCapturePosition = enPassantCapturePosition;
            pieces[enPassantCapturePosition.x, enPassantCapturePosition.y] = null;
        }

        if (TryGetCastlingRookMove(piece, from, destination, out ChessPiece rook, out Vector2Int rookFrom, out Vector2Int rookTo))
        {
            simulation.isCastling = true;
            simulation.castlingRook = rook;
            simulation.castlingRookFrom = rookFrom;
            simulation.castlingRookTo = rookTo;
            pieces[rookFrom.x, rookFrom.y] = null;
            pieces[rookTo.x, rookTo.y] = rook;
            rook.SetBoardPosition(rookTo);
        }

        pieces[from.x, from.y] = null;
        pieces[destination.x, destination.y] = piece;
        piece.SetBoardPosition(destination);
        return simulation;
    }

    private void RestoreMoveSimulation(ChessPiece piece, Vector2Int from, Vector2Int destination, MoveSimulation simulation)
    {
        piece.SetBoardPosition(from);
        pieces[from.x, from.y] = piece;
        pieces[destination.x, destination.y] = simulation.destinationPiece;

        if (simulation.isEnPassant)
            pieces[simulation.enPassantCapturePosition.x, simulation.enPassantCapturePosition.y] = simulation.enPassantCapturedPiece;

        if (simulation.isCastling && simulation.castlingRook)
        {
            simulation.castlingRook.SetBoardPosition(simulation.castlingRookFrom);
            pieces[simulation.castlingRookFrom.x, simulation.castlingRookFrom.y] = simulation.castlingRook;
            pieces[simulation.castlingRookTo.x, simulation.castlingRookTo.y] = null;
        }
    }

    private bool IsCheckmate(PieceTeam team)
    {
        return IsTeamInCheck(team) && !HasAnySafeLegalMove(team);
    }

    private bool HasAnySafeLegalMove(PieceTeam team)
    {
        for (int x = 0; x < pieces.GetLength(0); x++)
            for (int y = 0; y < pieces.GetLength(1); y++)
            {
                ChessPiece piece = pieces[x, y];
                if (!piece || piece.Team != team)
                    continue;

                IReadOnlyList<Vector2Int> candidateMoves = GetCandidateMoves(piece);
                for (int i = 0; i < candidateMoves.Count; i++)
                    if (IsLegalMoveAfterKingSafety(piece, piece.BoardPosition, candidateMoves[i]))
                        return true;
            }

        return false;
    }

    private bool TryGetDrawReason(out string reason)
    {
        if (IsStalemate(currentTurn))
        {
            reason = "Stalemate";
            return true;
        }

        if (halfMoveClock >= FiftyMoveRuleHalfMoveLimit)
        {
            reason = "50-Move Rule";
            return true;
        }

        if (HasThreefoldRepetition())
        {
            reason = "Threefold Repetition";
            return true;
        }

        if (HasInsufficientMaterial())
        {
            reason = "Insufficient Material";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private bool IsStalemate(PieceTeam team)
    {
        return !IsTeamInCheck(team) && !HasAnySafeLegalMove(team);
    }

    private bool HasThreefoldRepetition()
    {
        string positionKey = BuildPositionKey();
        return positionHistory.TryGetValue(positionKey, out int occurrenceCount) && occurrenceCount >= 3;
    }

    private bool HasInsufficientMaterial()
    {
        List<ChessPiece> nonKingPieces = new List<ChessPiece>();
        for (int x = 0; x < pieces.GetLength(0); x++)
            for (int y = 0; y < pieces.GetLength(1); y++)
            {
                ChessPiece piece = pieces[x, y];
                if (piece && piece.Type != PieceType.King)
                    nonKingPieces.Add(piece);
            }

        if (nonKingPieces.Count == 0)
            return true;

        if (nonKingPieces.Count == 1)
        {
            PieceType type = nonKingPieces[0].Type;
            return type == PieceType.Bishop || type == PieceType.Knight;
        }

        if (nonKingPieces.Count != 2)
            return false;

        ChessPiece first = nonKingPieces[0];
        ChessPiece second = nonKingPieces[1];
        return first.Type == PieceType.Bishop &&
            second.Type == PieceType.Bishop &&
            first.Team != second.Team &&
            IsSameColorSquare(first.BoardPosition, second.BoardPosition);
    }

    private bool IsSameColorSquare(Vector2Int first, Vector2Int second)
    {
        return (first.x + first.y) % 2 == (second.x + second.y) % 2;
    }

    private bool IsTeamInCheck(PieceTeam team)
    {
        ChessPiece king = FindKing(team);
        if (!king)
            return true;

        return IsSquareUnderAttack(king.BoardPosition, team == PieceTeam.White ? PieceTeam.Black : PieceTeam.White);
    }

    private bool IsSquareUnderAttack(Vector2Int square, PieceTeam attackerTeam)
    {
        for (int x = 0; x < pieces.GetLength(0); x++)
            for (int y = 0; y < pieces.GetLength(1); y++)
            {
                ChessPiece piece = pieces[x, y];
                if (!piece || piece.Team != attackerTeam)
                    continue;

                if (CanPieceAttackSquare(piece, square))
                    return true;
            }

        return false;
    }

    private bool CanPieceAttackSquare(ChessPiece piece, Vector2Int square)
    {
        if (!piece || piece.BoardPosition == square)
            return false;

        if (piece.Type == PieceType.Pawn)
        {
            Vector2Int delta = square - piece.BoardPosition;
            return Mathf.Abs(delta.x) == 1 && delta.y == piece.ForwardDirection;
        }

        return piece.IsLegalMove(square, pieces);
    }

    private ChessPiece FindKing(PieceTeam team)
    {
        for (int x = 0; x < pieces.GetLength(0); x++)
            for (int y = 0; y < pieces.GetLength(1); y++)
            {
                ChessPiece piece = pieces[x, y];
                if (piece && piece.Team == team && piece.Type == PieceType.King)
                    return piece;
            }

        return null;
    }

    private bool IsEnPassantMove(ChessPiece piece, Vector2Int from, Vector2Int destination)
    {
        return TryGetEnPassantCapture(piece, from, destination, out _, out _);
    }

    private bool TryGetEnPassantCapture(ChessPiece piece, Vector2Int from, Vector2Int destination, out ChessPiece capturedPawn, out Vector2Int capturePosition)
    {
        capturedPawn = null;
        capturePosition = new Vector2Int(destination.x, from.y);

        if (!piece || piece.Type != PieceType.Pawn || !lastMoveWasPawnDoubleStep || !lastMovedPiece)
            return false;

        Vector2Int delta = destination - from;
        if (Mathf.Abs(delta.x) != 1 || delta.y != piece.ForwardDirection)
            return false;

        if (!ChessMoveRules.IsInsideBoard(destination) || pieces[destination.x, destination.y])
            return false;

        if (lastMovedPiece.Type != PieceType.Pawn || lastMovedPiece.Team == piece.Team)
            return false;

        if (lastMoveTo != capturePosition || lastMovedPiece.BoardPosition != capturePosition)
            return false;

        capturedPawn = pieces[capturePosition.x, capturePosition.y];
        return capturedPawn == lastMovedPiece;
    }

    private bool IsCastlingMove(ChessPiece piece, Vector2Int from, Vector2Int destination)
    {
        if (!TryGetCastlingRookMove(piece, from, destination, out _, out Vector2Int rookFrom, out _))
            return false;

        if (IsTeamInCheck(piece.Team))
            return false;

        int direction = destination.x > from.x ? 1 : -1;
        PieceTeam enemyTeam = piece.Team == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        for (int x = from.x + direction; x != destination.x + direction; x += direction)
            if (IsSquareUnderAttack(new Vector2Int(x, from.y), enemyTeam))
                return false;

        return true;
    }

    private bool TryGetCastlingRookMove(
        ChessPiece piece,
        Vector2Int from,
        Vector2Int destination,
        out ChessPiece rook,
        out Vector2Int rookFrom,
        out Vector2Int rookTo)
    {
        rook = null;
        rookFrom = default;
        rookTo = default;

        if (!piece || piece.Type != PieceType.King || piece.HasMoved)
            return false;

        Vector2Int delta = destination - from;
        if (delta.y != 0 || Mathf.Abs(delta.x) != 2)
            return false;

        if (!ChessMoveRules.IsInsideBoard(destination) || pieces[destination.x, destination.y])
            return false;

        int direction = delta.x > 0 ? 1 : -1;
        rookFrom = new Vector2Int(direction > 0 ? 7 : 0, from.y);
        rookTo = new Vector2Int(from.x + direction, from.y);
        if (!ChessMoveRules.IsInsideBoard(rookFrom) || !ChessMoveRules.IsInsideBoard(rookTo))
            return false;

        rook = pieces[rookFrom.x, rookFrom.y];
        if (!rook || rook.Team != piece.Team || rook.Type != PieceType.Rook || rook.HasMoved)
            return false;

        for (int x = from.x + direction; x != rookFrom.x; x += direction)
            if (pieces[x, from.y])
                return false;

        return true;
    }

    private bool IsPromotionRank(PawnPiece pawn)
    {
        if (!pawn)
            return false;

        return pawn.BoardPosition.y == (pawn.ForwardDirection > 0 ? 7 : 0);
    }

    private void RecordLastMove(ChessPiece movingPiece, Vector2Int from, Vector2Int destination)
    {
        lastMovedPiece = movingPiece;
        lastMoveTo = destination;
        lastMoveWasPawnDoubleStep = movingPiece &&
            movingPiece.Type == PieceType.Pawn &&
            Mathf.Abs(destination.y - from.y) == 2;
    }

    private void UpdateHalfMoveClock(bool movedPawn, bool capturedAnyPiece)
    {
        halfMoveClock = movedPawn || capturedAnyPiece ? 0 : halfMoveClock + 1;
    }

    private void ResetDrawTracking()
    {
        halfMoveClock = 0;
        positionHistory.Clear();
    }

    private void RecordCurrentPosition()
    {
        string positionKey = BuildPositionKey();
        if (positionHistory.TryGetValue(positionKey, out int occurrenceCount))
            positionHistory[positionKey] = occurrenceCount + 1;
        else
            positionHistory.Add(positionKey, 1);
    }

    private string BuildPositionKey()
    {
        StringBuilder builder = new StringBuilder(96);

        for (int rank = 7; rank >= 0; rank--)
        {
            int emptyCount = 0;
            for (int file = 0; file < 8; file++)
            {
                ChessPiece piece = pieces[file, rank];
                if (!piece)
                {
                    emptyCount++;
                    continue;
                }

                if (emptyCount > 0)
                {
                    builder.Append(emptyCount);
                    emptyCount = 0;
                }

                builder.Append(GetFenPieceSymbol(piece));
            }

            if (emptyCount > 0)
                builder.Append(emptyCount);

            if (rank > 0)
                builder.Append('/');
        }

        builder.Append(' ');
        builder.Append(currentTurn == PieceTeam.White ? 'w' : 'b');
        builder.Append(' ');
        AppendCastlingRights(builder);
        builder.Append(' ');
        builder.Append(GetEnPassantTargetKey());
        return builder.ToString();
    }

    private char GetFenPieceSymbol(ChessPiece piece)
    {
        char symbol;
        switch (piece.Type)
        {
            case PieceType.King:
                symbol = 'k';
                break;
            case PieceType.Queen:
                symbol = 'q';
                break;
            case PieceType.Rook:
                symbol = 'r';
                break;
            case PieceType.Bishop:
                symbol = 'b';
                break;
            case PieceType.Knight:
                symbol = 'n';
                break;
            case PieceType.Pawn:
            default:
                symbol = 'p';
                break;
        }

        return piece.Team == PieceTeam.White ? char.ToUpperInvariant(symbol) : symbol;
    }

    private void AppendCastlingRights(StringBuilder builder)
    {
        int startLength = builder.Length;
        AppendCastlingRight(builder, PieceTeam.White, 7, 'K');
        AppendCastlingRight(builder, PieceTeam.White, 0, 'Q');
        AppendCastlingRight(builder, PieceTeam.Black, 7, 'k');
        AppendCastlingRight(builder, PieceTeam.Black, 0, 'q');

        if (builder.Length == startLength)
            builder.Append('-');
    }

    private void AppendCastlingRight(StringBuilder builder, PieceTeam team, int rookFile, char symbol)
    {
        ChessPiece king = FindKing(team);
        if (!king || king.BoardPosition.x != 4 || king.HasMoved)
            return;

        int rank = king.BoardPosition.y;
        ChessPiece rook = pieces[rookFile, rank];

        if (rook && rook.Team == team && rook.Type == PieceType.Rook && !rook.HasMoved)
            builder.Append(symbol);
    }

    private string GetEnPassantTargetKey()
    {
        if (!lastMoveWasPawnDoubleStep || !lastMovedPiece || lastMovedPiece.Type != PieceType.Pawn)
            return "-";

        Vector2Int pawnPosition = lastMovedPiece.BoardPosition;
        int targetRank = pawnPosition.y - lastMovedPiece.ForwardDirection;
        if (!ChessMoveRules.IsInsideBoard(new Vector2Int(pawnPosition.x, targetRank)))
            return "-";

        return FormatSquare(new Vector2Int(pawnPosition.x, targetRank));
    }

    private string FormatSquare(Vector2Int square)
    {
        char file = (char)('a' + square.x);
        char rank = (char)('1' + square.y);
        return new string(new[] { file, rank });
    }

    public string ExportFen()
    {
        int fullMoveNumber = Mathf.Max(1, 1 + moveHistory.Count / 2);
        return $"{BuildPositionKey()} {halfMoveClock} {fullMoveNumber}";
    }

    private IEnumerator AnimateMoveAndPromptPromotion(PawnPiece pawn, Vector2Int destination, PieceTeam opponentTeam)
    {
        inputLocked = true;
        RefreshLocalInteractionState();
        AnimatePieceToTile(pawn, destination, 0f, moveAnimationDuration, moveArcHeight);
        yield return new WaitForSeconds(moveAnimationDuration);

        pendingPromotionPawn = pawn;
        pendingPromotionOpponentTeam = opponentTeam;

        if (pendingRemotePromotionResolution)
        {
            CompletePromotion(remotePromotionType);
            yield break;
        }

        turnSelectionUI?.ShowPromotionChoice(pawn.Team, CompletePromotion);
    }

    private void CompletePromotion(PieceType promotionType)
    {
        if (!pendingPromotionPawn)
            return;

        ChessPiece promotedPiece = PromotePawn(pendingPromotionPawn, promotionType);
        PlaySound(promotionSound);
        pendingPromotionPawn = null;
        inputLocked = false;
        lastMoveSummary = AppendPromotionToMoveSummary(lastMoveSummary, promotionType);
        if (moveHistory.Count > 0)
            moveHistory[moveHistory.Count - 1] = lastMoveSummary;

        currentTurn = pendingPromotionOpponentTeam;
        NotifyMoveCommitted(new ChessLanMove(pendingCommittedMoveFrom, pendingCommittedMoveTo, promotionType));
        turnSelectionUI?.SetTurn(currentTurn);
        pendingRemotePromotionResolution = false;
        suppressMoveCommittedEvent = false;
        RefreshLocalInteractionState();

        if (serverAuthoritativeMode)
        {
            UpdateCheckWarningForCurrentTurn();
            return;
        }

        if (TryFinishGameAfterCompletedMove(promotedPiece, promotedPiece.BoardPosition))
            return;

        UpdateCheckWarningForCurrentTurn();
    }

    private ChessPiece PromotePawn(PawnPiece pawn, PieceType promotionType)
    {
        if (promotionType == PieceType.Pawn || promotionType == PieceType.King)
            promotionType = PieceType.Queen;

        PieceTeam team = pawn.Team;
        Vector2Int boardPosition = pawn.BoardPosition;
        int forwardDirection = pawn.ForwardDirection;

        if (pieceAnimations.TryGetValue(pawn, out Coroutine existingAnimation))
        {
            StopCoroutine(existingAnimation);
            pieceAnimations.Remove(pawn);
        }

        GameObject pawnObject = pawn.gameObject;
        ChessPiece promotedPiece = CreatePromotedPieceObject(team, promotionType, boardPosition, forwardDirection);
        if (!promotedPiece)
            promotedPiece = CreateFallbackPromotedPieceObject(pawnObject, team, promotionType, boardPosition, forwardDirection);

        promotedPiece.Initialize(team, boardPosition, forwardDirection);
        promotedPiece.MarkMoved();
        promotedPiece.gameObject.name = $"{team} {promotionType} {boardPosition.x},{boardPosition.y}";
        MovePieceToTile(promotedPiece, boardPosition, 0f);
        pieces[boardPosition.x, boardPosition.y] = promotedPiece;
        lastMovedPiece = promotedPiece;
        lastMoveWasPawnDoubleStep = false;

        DestroyRuntimeObject(pawnObject);
        return promotedPiece;
    }

    private ChessPiece CreatePromotedPieceObject(PieceTeam team, PieceType promotionType, Vector2Int boardPosition, int forwardDirection)
    {
        Transform sourceTransform = FindVisualChild(GetVisualSourceName(team, promotionType));
        if (!sourceTransform)
            return null;

        MeshFilter sourceMeshFilter = sourceTransform.GetComponent<MeshFilter>();
        MeshRenderer sourceRenderer = sourceTransform.GetComponent<MeshRenderer>();
        if (!sourceMeshFilter || !sourceMeshFilter.sharedMesh || !sourceRenderer)
            return null;

        int sourcePieceCount = promotionType == PieceType.Queen ? 1 : 2;
        List<MeshComponentData> components = SplitMeshIntoSpatialGroups(sourceMeshFilter.sharedMesh, sourcePieceCount);
        if (components.Count == 0)
            return null;

        components.Sort((left, right) =>
            sourceTransform.TransformPoint(left.pivot).x.CompareTo(sourceTransform.TransformPoint(right.pivot).x));

        GameObject promotedObject = new GameObject($"{team} {promotionType} {boardPosition.x},{boardPosition.y}");
        promotedObject.transform.SetParent(runtimePiecesRoot);
        promotedObject.transform.position = sourceTransform.TransformPoint(components[0].pivot);
        promotedObject.transform.rotation = sourceTransform.rotation;
        promotedObject.transform.localScale = sourceTransform.lossyScale;

        MeshFilter meshFilter = promotedObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = components[0].mesh;

        MeshRenderer meshRenderer = promotedObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

        ChessPiece promotedPiece = AddPieceComponent(promotedObject, promotionType);
        EnsurePieceCollider(promotedObject);
        return promotedPiece;
    }

    private ChessPiece CreateFallbackPromotedPieceObject(GameObject pawnObject, PieceTeam team, PieceType promotionType, Vector2Int boardPosition, int forwardDirection)
    {
        GameObject promotedObject = new GameObject($"{team} {promotionType} {boardPosition.x},{boardPosition.y}");
        promotedObject.transform.SetParent(runtimePiecesRoot);
        promotedObject.transform.position = pawnObject.transform.position;
        promotedObject.transform.rotation = pawnObject.transform.rotation;
        promotedObject.transform.localScale = pawnObject.transform.localScale;

        MeshFilter pawnMeshFilter = pawnObject.GetComponent<MeshFilter>();
        MeshRenderer pawnRenderer = pawnObject.GetComponent<MeshRenderer>();
        if (pawnMeshFilter && pawnMeshFilter.sharedMesh)
        {
            MeshFilter meshFilter = promotedObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = pawnMeshFilter.sharedMesh;
        }

        if (pawnRenderer)
        {
            MeshRenderer meshRenderer = promotedObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = pawnRenderer.sharedMaterials;
        }

        ChessPiece promotedPiece = AddPieceComponent(promotedObject, promotionType);
        EnsurePieceCollider(promotedObject);
        return promotedPiece;
    }

    private string GetVisualSourceName(PieceTeam team, PieceType pieceType)
    {
        string suffix = team == PieceTeam.White ? "_1" : "_2";
        switch (pieceType)
        {
            case PieceType.King:
                return "King" + suffix;
            case PieceType.Pawn:
                return "Pawn" + suffix;
            case PieceType.Rook:
                return "Rook" + suffix;
            case PieceType.Bishop:
                return "Bishop" + suffix;
            case PieceType.Knight:
                return "Knight" + suffix;
            case PieceType.Queen:
            default:
                return "Queen" + suffix;
        }
    }

    private ChessPiece AddPieceComponent(GameObject targetObject, PieceType pieceType)
    {
        switch (pieceType)
        {
            case PieceType.King:
                return targetObject.AddComponent<KingPiece>();
            case PieceType.Pawn:
                return targetObject.AddComponent<PawnPiece>();
            case PieceType.Rook:
                return targetObject.AddComponent<RookPiece>();
            case PieceType.Bishop:
                return targetObject.AddComponent<BishopPiece>();
            case PieceType.Knight:
                return targetObject.AddComponent<KnightPiece>();
            case PieceType.Queen:
            default:
                return targetObject.AddComponent<QueenPiece>();
        }
    }

    private void UpdateCheckWarningForCurrentTurn()
    {
        if (!gameStarted || gameOver)
        {
            StopCheckWarning();
            return;
        }

        if (!IsTeamInCheck(currentTurn))
        {
            StopCheckWarning();
            return;
        }

        ShowCheckWarning(currentTurn);
    }

    private void ShowCheckWarning(PieceTeam checkedTeam)
    {
        ChessPiece king = FindKing(checkedTeam);
        if (!king)
            return;

        if (checkedKing == king && checkPulseCoroutine != null)
        {
            turnSelectionUI?.ShowCheckWarning(checkedTeam);
            return;
        }

        StopCheckWarning();
        checkedKing = king;
        checkedKingOriginalScale = checkedKing.transform.localScale;
        checkPulseCoroutine = StartCoroutine(AnimateCheckedKing(checkedKing, checkedTeam));
        PlaySound(checkSound);
        turnSelectionUI?.ShowCheckWarning(checkedTeam);
    }

    private void StopCheckWarning()
    {
        if (checkPulseCoroutine != null)
        {
            StopCoroutine(checkPulseCoroutine);
            checkPulseCoroutine = null;
        }

        if (checkedKing)
            checkedKing.transform.localScale = checkedKingOriginalScale;

        checkedKing = null;
        turnSelectionUI?.HideCheckWarning();
    }

    private IEnumerator AnimateCheckedKing(ChessPiece king, PieceTeam checkedTeam)
    {
        float elapsed = 0f;
        while (king && gameStarted && !gameOver && IsTeamInCheck(checkedTeam))
        {
            elapsed += Time.deltaTime;
            float wave = (Mathf.Sin((elapsed / Mathf.Max(0.001f, checkPulseDuration)) * Mathf.PI * 2f) + 1f) * 0.5f;
            float scale = Mathf.Lerp(1f, checkPulseScale, wave);
            king.transform.localScale = checkedKingOriginalScale * scale;
            yield return null;
        }

        if (king)
            king.transform.localScale = checkedKingOriginalScale;

        checkPulseCoroutine = null;
        checkedKing = null;
        turnSelectionUI?.HideCheckWarning();
    }

    private void CreateRuntimePiecesFromVisualSet()
    {
        CreateSplitPieces<RookPiece>("Rook_1", PieceTeam.White, 0, new[] { 0, 7 });
        CreateSplitPieces<KnightPiece>("Knight_1", PieceTeam.White, 0, new[] { 1, 6 });
        CreateSplitPieces<BishopPiece>("Bishop_1", PieceTeam.White, 0, new[] { 2, 5 });
        CreateSplitPieces<QueenPiece>("Queen_1", PieceTeam.White, 0, new[] { 3 });
        CreateSplitPieces<KingPiece>("King_1", PieceTeam.White, 0, new[] { 4 });
        CreateSplitPieces<PawnPiece>("Pawn_1", PieceTeam.White, 1, new[] { 0, 1, 2, 3, 4, 5, 6, 7 });

        CreateSplitPieces<PawnPiece>("Pawn_2", PieceTeam.Black, 6, new[] { 0, 1, 2, 3, 4, 5, 6, 7 });
        CreateSplitPieces<RookPiece>("Rook_2", PieceTeam.Black, 7, new[] { 0, 7 });
        CreateSplitPieces<KnightPiece>("Knight_2", PieceTeam.Black, 7, new[] { 1, 6 });
        CreateSplitPieces<BishopPiece>("Bishop_2", PieceTeam.Black, 7, new[] { 2, 5 });
        CreateSplitPieces<QueenPiece>("Queen_2", PieceTeam.Black, 7, new[] { 3 });
        CreateSplitPieces<KingPiece>("King_2", PieceTeam.Black, 7, new[] { 4 });
    }

    private void CreateSplitPieces<T>(string sourceName, PieceTeam team, int rank, int[] files) where T : ChessPiece
    {
        Transform sourceTransform = FindVisualChild(sourceName);
        if (!sourceTransform)
        {
            Debug.LogWarning($"Missing visual source mesh '{sourceName}'.");
            return;
        }

        MeshFilter sourceMeshFilter = sourceTransform.GetComponent<MeshFilter>();
        MeshRenderer sourceRenderer = sourceTransform.GetComponent<MeshRenderer>();
        if (!sourceMeshFilter || !sourceMeshFilter.sharedMesh || !sourceRenderer)
        {
            Debug.LogWarning($"Visual source '{sourceName}' has no readable mesh renderer.");
            return;
        }

        List<MeshComponentData> components = SplitMeshIntoSpatialGroups(sourceMeshFilter.sharedMesh, files.Length);
        components.Sort((left, right) =>
            sourceTransform.TransformPoint(left.pivot).x.CompareTo(sourceTransform.TransformPoint(right.pivot).x));

        int spawnCount = Mathf.Min(components.Count, files.Length);
        if (components.Count != files.Length)
            Debug.LogWarning($"Visual source '{sourceName}' split into {components.Count} piece(s), expected {files.Length}.");

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2Int boardPosition = new Vector2Int(files[i], rank);
            GameObject pieceObject = new GameObject($"{team} {typeof(T).Name.Replace("Piece", string.Empty)} {boardPosition.x},{boardPosition.y}");
            pieceObject.transform.SetParent(runtimePiecesRoot);
            pieceObject.transform.position = sourceTransform.TransformPoint(components[i].pivot);
            pieceObject.transform.rotation = sourceTransform.rotation;
            pieceObject.transform.localScale = sourceTransform.lossyScale;

            MeshFilter meshFilter = pieceObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = components[i].mesh;

            MeshRenderer meshRenderer = pieceObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

            T piece = pieceObject.AddComponent<T>();
            piece.Initialize(team, boardPosition, team == PieceTeam.White ? 1 : -1);
            EnsurePieceCollider(pieceObject);
            MovePieceToTile(piece, boardPosition, 0f);
        }
    }

    private Transform FindVisualChild(string objectName)
    {
        Transform visualRoot = GetVisualChessSetRoot();
        if (!visualRoot)
            return null;

        Transform[] children = visualRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i].name == objectName)
                return children[i];

        return null;
    }

    private Transform GetVisualChessSetRoot()
    {
        if (!chessboard)
            return null;

        Transform visualRoot = chessboard.transform.Find("VisualChessSet");
        return visualRoot ? visualRoot : chessboard.transform;
    }

    private void EnsurePieceCollider(GameObject pieceObject)
    {
        if (pieceObject.GetComponentInChildren<Collider>())
            return;

        Renderer[] renderers = pieceObject.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        BoxCollider collider = pieceObject.AddComponent<BoxCollider>();
        collider.center = pieceObject.transform.InverseTransformPoint(bounds.center);

        Vector3 localSize = pieceObject.transform.InverseTransformVector(bounds.size);
        collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
    }

    private List<MeshComponentData> SplitMeshIntoSpatialGroups(Mesh sourceMesh, int expectedGroupCount)
    {
        Vector3[] vertices = sourceMesh.vertices;
        Vector3[] normals = sourceMesh.normals;
        Vector2[] uvs = sourceMesh.uv;
        int subMeshCount = Mathf.Max(1, sourceMesh.subMeshCount);
        bool splitAlongZ = sourceMesh.bounds.size.z > sourceMesh.bounds.size.x;
        List<TriangleData> triangles = new List<TriangleData>();

        for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
        {
            int[] subMeshTriangles = sourceMesh.GetTriangles(subMesh);
            for (int i = 0; i < subMeshTriangles.Length; i += 3)
            {
                int a = subMeshTriangles[i];
                int b = subMeshTriangles[i + 1];
                int c = subMeshTriangles[i + 2];
                float centerAxis = splitAlongZ
                    ? (vertices[a].z + vertices[b].z + vertices[c].z) / 3f
                    : (vertices[a].x + vertices[b].x + vertices[c].x) / 3f;
                triangles.Add(new TriangleData(subMesh, a, b, c, centerAxis));
            }
        }

        List<MeshComponentData> components = new List<MeshComponentData>();
        if (triangles.Count == 0)
            return components;

        int groupCount = Mathf.Clamp(expectedGroupCount, 1, triangles.Count);
        List<TriangleData>[] groups = GroupTrianglesByCenterKMeans(triangles, groupCount);
        for (int i = 0; i < groups.Length; i++)
            if (groups[i].Count > 0)
                components.Add(BuildMeshComponent(sourceMesh, vertices, normals, uvs, subMeshCount, groups[i]));

        return components;
    }

    private List<TriangleData>[] GroupTrianglesByCenterKMeans(List<TriangleData> triangles, int groupCount)
    {
        List<TriangleData>[] groups = CreateTriangleGroups(groupCount);
        if (groupCount == 1)
        {
            groups[0].AddRange(triangles);
            return groups;
        }

        float minX = triangles[0].centerAxis;
        float maxX = triangles[0].centerAxis;
        for (int i = 1; i < triangles.Count; i++)
        {
            minX = Mathf.Min(minX, triangles[i].centerAxis);
            maxX = Mathf.Max(maxX, triangles[i].centerAxis);
        }

        float[] centers = new float[groupCount];
        float range = Mathf.Max(0.0001f, maxX - minX);
        for (int i = 0; i < centers.Length; i++)
            centers[i] = minX + range * ((i + 0.5f) / groupCount);

        for (int iteration = 0; iteration < 12; iteration++)
        {
            groups = CreateTriangleGroups(groupCount);
            for (int i = 0; i < triangles.Count; i++)
                groups[FindNearestCenter(centers, triangles[i].centerAxis)].Add(triangles[i]);

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].Count == 0)
                    continue;

                float sum = 0f;
                for (int j = 0; j < groups[i].Count; j++)
                    sum += groups[i][j].centerAxis;

                centers[i] = sum / groups[i].Count;
            }
        }

        return groups;
    }

    private List<TriangleData>[] CreateTriangleGroups(int groupCount)
    {
        List<TriangleData>[] groups = new List<TriangleData>[groupCount];
        for (int i = 0; i < groups.Length; i++)
            groups[i] = new List<TriangleData>();

        return groups;
    }

    private int FindNearestCenter(float[] centers, float value)
    {
        int nearestIndex = 0;
        float nearestDistance = Mathf.Abs(value - centers[0]);
        for (int i = 1; i < centers.Length; i++)
        {
            float distance = Mathf.Abs(value - centers[i]);
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearestIndex = i;
        }

        return nearestIndex;
    }

    private MeshComponentData BuildMeshComponent(
        Mesh sourceMesh,
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] uvs,
        int subMeshCount,
        List<TriangleData> triangles)
    {
        Bounds localBounds = new Bounds(vertices[triangles[0].a], Vector3.zero);
        for (int i = 0; i < triangles.Count; i++)
        {
            localBounds.Encapsulate(vertices[triangles[i].a]);
            localBounds.Encapsulate(vertices[triangles[i].b]);
            localBounds.Encapsulate(vertices[triangles[i].c]);
        }

        Vector3 pivot = new Vector3(localBounds.center.x, localBounds.min.y, localBounds.center.z);
        Dictionary<int, int> oldToNewIndex = new Dictionary<int, int>();
        List<Vector3> newVertices = new List<Vector3>();
        List<Vector3> newNormals = new List<Vector3>();
        List<Vector2> newUvs = new List<Vector2>();
        List<int>[] newTriangles = new List<int>[subMeshCount];
        for (int i = 0; i < newTriangles.Length; i++)
            newTriangles[i] = new List<int>();

        for (int i = 0; i < triangles.Count; i++)
        {
            TriangleData triangle = triangles[i];
            newTriangles[triangle.subMesh].Add(GetOrCreateMeshIndex(triangle.a, vertices, normals, uvs, pivot, oldToNewIndex, newVertices, newNormals, newUvs));
            newTriangles[triangle.subMesh].Add(GetOrCreateMeshIndex(triangle.b, vertices, normals, uvs, pivot, oldToNewIndex, newVertices, newNormals, newUvs));
            newTriangles[triangle.subMesh].Add(GetOrCreateMeshIndex(triangle.c, vertices, normals, uvs, pivot, oldToNewIndex, newVertices, newNormals, newUvs));
        }

        Mesh mesh = new Mesh
        {
            name = $"{sourceMesh.name} Runtime Piece"
        };
        mesh.SetVertices(newVertices);
        mesh.subMeshCount = subMeshCount;
        for (int i = 0; i < newTriangles.Length; i++)
            mesh.SetTriangles(newTriangles[i], i);

        if (newNormals.Count == newVertices.Count)
            mesh.SetNormals(newNormals);
        else
            mesh.RecalculateNormals();

        if (newUvs.Count == newVertices.Count)
            mesh.SetUVs(0, newUvs);

        mesh.RecalculateBounds();
        return new MeshComponentData(mesh, pivot);
    }

    private int GetOrCreateMeshIndex(
        int oldIndex,
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] uvs,
        Vector3 pivot,
        Dictionary<int, int> oldToNewIndex,
        List<Vector3> newVertices,
        List<Vector3> newNormals,
        List<Vector2> newUvs)
    {
        if (oldToNewIndex.TryGetValue(oldIndex, out int newIndex))
            return newIndex;

        newIndex = newVertices.Count;
        oldToNewIndex.Add(oldIndex, newIndex);
        newVertices.Add(vertices[oldIndex] - pivot);

        if (normals != null && normals.Length == vertices.Length)
            newNormals.Add(normals[oldIndex]);

        if (uvs != null && uvs.Length == vertices.Length)
            newUvs.Add(uvs[oldIndex]);

        return newIndex;
    }

    private void AlignAllPiecesToBoard()
    {
        if (!chessboard)
            return;

        ChessPiece[] scenePieces = runtimePiecesRoot.GetComponentsInChildren<ChessPiece>(true);
        for (int i = 0; i < scenePieces.Length; i++)
            MovePieceToTile(scenePieces[i], scenePieces[i].BoardPosition, 0f);
    }

    private void ArrangePiecesForFirstTurn(PieceTeam frontTeam)
    {
        startingPlacementPieces.Clear();
        PositionTeamPieces(frontTeam, 0, 1, 1);
        PositionTeamPieces(frontTeam == PieceTeam.White ? PieceTeam.Black : PieceTeam.White, 7, 6, -1);
        startingPlacementPieces.Clear();
    }

    private void PositionTeamPieces(PieceTeam team, int backRank, int pawnRank, int forwardDirection)
    {
        PositionSinglePiece(team, PieceType.Rook, 0, backRank, forwardDirection);
        PositionSinglePiece(team, PieceType.Rook, 7, backRank, forwardDirection);
        PositionSinglePiece(team, PieceType.Knight, 1, backRank, forwardDirection);
        PositionSinglePiece(team, PieceType.Knight, 6, backRank, forwardDirection);
        PositionSinglePiece(team, PieceType.Bishop, 2, backRank, forwardDirection);
        PositionSinglePiece(team, PieceType.Bishop, 5, backRank, forwardDirection);
        PositionSinglePiece(team, PieceType.Queen, 3, backRank, forwardDirection);
        PositionSinglePiece(team, PieceType.King, 4, backRank, forwardDirection);

        for (int file = 0; file < 8; file++)
            PositionSinglePiece(team, PieceType.Pawn, file, pawnRank, forwardDirection);
    }

    private void PositionSinglePiece(PieceTeam team, PieceType type, int file, int rank, int forwardDirection)
    {
        ChessPiece piece = FindClosestUnassignedPiece(team, type, file);
        if (!piece)
            return;

        piece.SetStartingBoardPosition(new Vector2Int(file, rank), forwardDirection);
        MovePieceToTile(piece, piece.BoardPosition, 0f);
    }

    private ChessPiece FindClosestUnassignedPiece(PieceTeam team, PieceType type, int targetFile)
    {
        ChessPiece closestPiece = null;
        float closestDistance = float.PositiveInfinity;
        ChessPiece[] scenePieces = runtimePiecesRoot.GetComponentsInChildren<ChessPiece>(true);

        for (int i = 0; i < scenePieces.Length; i++)
        {
            ChessPiece piece = scenePieces[i];
            if (piece.Team != team || piece.Type != type || startingPlacementPieces.Contains(piece))
                continue;

            float distance = Mathf.Abs(piece.BoardPosition.x - targetFile);
            if (distance >= closestDistance)
                continue;

            closestPiece = piece;
            closestDistance = distance;
        }

        if (closestPiece)
            startingPlacementPieces.Add(closestPiece);

        return closestPiece;
    }

    private void SetRuntimePiecesVisible(bool visible)
    {
        if (!runtimePiecesRoot)
            return;

        Renderer[] renderers = runtimePiecesRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = visible;

        Collider[] colliders = runtimePiecesRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = visible;
    }

    private void MovePieceToTile(ChessPiece piece, Vector2Int tile, float liftHeight)
    {
        piece.transform.position = GetPieceTilePosition(piece, tile, liftHeight);
    }

    private Vector3 GetPieceTilePosition(ChessPiece piece, Vector2Int tile, float liftHeight)
    {
        Vector3 targetPosition = chessboard.GetTileCenterWorld(tile);
        targetPosition += chessboard.TransformBoardLocalOffset(pieceBoardLocalOffset);
        targetPosition.y += liftHeight + GetPieceBottomOffset(piece);
        return targetPosition;
    }

    private float GetPieceBottomOffset(ChessPiece piece)
    {
        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return 0f;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return piece.transform.position.y - bounds.min.y;
    }

    private void HideImportedPieceRenderers()
    {
        Transform visualRoot = GetVisualChessSetRoot();
        if (!visualRoot)
            return;

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer currentRenderer = renderers[i];
            if (!currentRenderer.gameObject.name.Contains("ChessBoard"))
                currentRenderer.enabled = false;
        }
    }

    private void DeselectCurrentPiece(bool animateToBoard)
    {
        if (!selectedPiece)
            return;

        ChessPiece pieceToDeselect = selectedPiece;
        selectedPiece = null;
        chessboard?.ClearLegalMoveHighlights();

        if (animateToBoard)
            AnimatePieceToTile(pieceToDeselect, pieceToDeselect.BoardPosition, 0f, selectAnimationDuration, 0f);
    }

    private void AnimatePieceToTile(ChessPiece piece, Vector2Int tile, float liftHeight, float duration, float arcHeight)
    {
        if (!piece || !chessboard)
            return;

        Vector3 targetPosition = GetPieceTilePosition(piece, tile, liftHeight);
        StartPieceAnimation(piece, targetPosition, duration, arcHeight);
    }

    private void StartPieceAnimation(ChessPiece piece, Vector3 targetPosition, float duration, float arcHeight)
    {
        if (pieceAnimations.TryGetValue(piece, out Coroutine existingAnimation))
            StopCoroutine(existingAnimation);

        pieceAnimations[piece] = StartCoroutine(AnimatePiece(piece, targetPosition, duration, arcHeight));
    }

    private IEnumerator AnimatePiece(ChessPiece piece, Vector3 targetPosition, float duration, float arcHeight)
    {
        Vector3 startPosition = piece.transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
            float easedT = t * t * (3f - 2f * t);
            Vector3 nextPosition = Vector3.Lerp(startPosition, targetPosition, easedT);
            nextPosition.y += Mathf.Sin(easedT * Mathf.PI) * arcHeight;
            piece.transform.position = nextPosition;
            yield return null;
        }

        piece.transform.position = targetPosition;
        pieceAnimations.Remove(piece);
    }

    private IEnumerator AnimateMoveAndUnlock(ChessPiece movingPiece, Vector2Int destination)
    {
        inputLocked = true;
        RefreshLocalInteractionState();
        AnimatePieceToTile(movingPiece, destination, 0f, moveAnimationDuration, moveArcHeight);
        yield return new WaitForSeconds(moveAnimationDuration);
        inputLocked = false;
        RefreshLocalInteractionState();
    }

    private IEnumerator AnimateMoveAndFinishGame(ChessPiece movingPiece, Vector2Int destination, PieceTeam winner)
    {
        inputLocked = true;
        AnimatePieceToTile(movingPiece, destination, 0f, moveAnimationDuration, moveArcHeight);
        yield return new WaitForSeconds(moveAnimationDuration);
        FinishGame(winner);
    }

    private IEnumerator AnimateMoveAndFinishDraw(ChessPiece movingPiece, Vector2Int destination, string reason)
    {
        inputLocked = true;
        AnimatePieceToTile(movingPiece, destination, 0f, moveAnimationDuration, moveArcHeight);
        yield return new WaitForSeconds(moveAnimationDuration);
        FinishDraw(reason);
    }

    private void FinishGame(PieceTeam winner)
    {
        StopCheckWarning();
        status = ChessGameStatus.Win;
        winningTeam = winner;
        frozenMatchDurationSeconds = MatchElapsedSeconds;
        PlayerAuthService.RecordGameResult(winner == playerTeam, winner != playerTeam, false);
        PlaySound(winSound);
        drawReason = string.Empty;
        selectedPiece = null;
        gameStarted = false;
        gameOver = true;
        inputLocked = false;
        chessboard?.ClearLegalMoveHighlights();
        RefreshLocalInteractionState();
        turnSelectionUI?.ShowGameOver(winningTeam, playerTeam);
    }

    private void FinishDraw(string reason)
    {
        StopCheckWarning();
        status = ChessGameStatus.Draw;
        frozenMatchDurationSeconds = MatchElapsedSeconds;
        PlayerAuthService.RecordGameResult(false, false, true);
        drawReason = reason;
        selectedPiece = null;
        gameStarted = false;
        gameOver = true;
        inputLocked = false;
        chessboard?.ClearLegalMoveHighlights();
        RefreshLocalInteractionState();
        turnSelectionUI?.ShowDraw(playerTeam, drawReason);
    }

    public bool ApplyNetworkMove(ChessLanMove move)
    {
        return ApplyControlledOpponentMove(move);
    }

    public bool ApplyBotMove(ChessLanMove move)
    {
        if (!botMode || serverAuthoritativeMode)
            return false;

        return ApplyControlledOpponentMove(move);
    }

    private bool ApplyControlledOpponentMove(ChessLanMove move)
    {
        if (!gameStarted || gameOver)
            return false;

        if (!restrictInputToControlledTeam)
            return false;

        if (currentTurn == localControlledTeam)
            return false;

        ChessPiece piece = GetPieceAt(move.from);
        if (!piece || piece.Team != currentTurn)
            return false;

        DeselectCurrentPiece(false);
        selectedPiece = piece;
        suppressMoveCommittedEvent = true;
        pendingRemotePromotionResolution = move.hasPromotion;
        remotePromotionType = move.hasPromotion ? move.promotionType : PieceType.Queen;

        bool moveApplied = TryMoveSelectedPiece(move.to);
        if (!moveApplied)
        {
            suppressMoveCommittedEvent = false;
            pendingRemotePromotionResolution = false;
            selectedPiece = null;
            chessboard?.ClearLegalMoveHighlights();
            ClearPendingCommittedMove();
        }
        else if (!move.hasPromotion)
        {
            suppressMoveCommittedEvent = false;
        }

        return moveApplied;
    }

    public bool ApplyFenState(string fen)
    {
        if (string.IsNullOrWhiteSpace(fen) || runtimePiecesRoot == null)
            return false;

        string[] parts = fen.Trim().Split(' ');
        if (parts.Length < 2)
            return false;

        StopCheckWarning();
        StopAllCoroutines();
        pieceAnimations.Clear();
        ClearRuntimePiecesRoot();
        ClearPieceMap();
        selectedPiece = null;
        pendingPromotionPawn = null;
        pendingRemotePromotionResolution = false;
        suppressMoveCommittedEvent = false;
        drawReason = string.Empty;
        lastMovedPiece = null;
        lastMoveTo = -Vector2Int.one;
        lastMoveWasPawnDoubleStep = false;
        halfMoveClock = parts.Length > 4 && int.TryParse(parts[4], out int parsedHalfMoveClock) ? parsedHalfMoveClock : 0;
        positionHistory.Clear();

        string[] ranks = parts[0].Split('/');
        if (ranks.Length != 8)
            return false;

        for (int rank = 7; rank >= 0; rank--)
        {
            int file = 0;
            string rankText = ranks[7 - rank];
            for (int i = 0; i < rankText.Length; i++)
            {
                char symbol = rankText[i];
                if (char.IsDigit(symbol))
                {
                    file += symbol - '0';
                    continue;
                }

                if (file < 0 || file >= 8)
                    return false;

                Vector2Int boardPosition = new Vector2Int(file, rank);
                ChessPiece piece = CreatePieceFromFenSymbol(symbol, boardPosition);
                if (!piece)
                    return false;

                pieces[file, rank] = piece;
                file++;
            }

            if (file != 8)
                return false;
        }

        currentTurn = string.Equals(parts[1], "b", StringComparison.OrdinalIgnoreCase)
            ? PieceTeam.Black
            : PieceTeam.White;

        ApplyFenMovementFlags(parts.Length > 2 ? parts[2] : "-");
        ApplyFenEnPassant(parts.Length > 3 ? parts[3] : "-");
        RebuildCapturedPiecesFromBoard();

        gameStarted = true;
        gameOver = false;
        inputLocked = false;
        status = ChessGameStatus.Playing;
        SetRuntimePiecesVisible(true);
        chessboard?.SetPresentationVisible(true);
        turnSelectionUI?.SetTurn(currentTurn);
        RefreshLocalInteractionState();
        UpdateCheckWarningForCurrentTurn();
        return true;
    }

    public void ApplyServerGameOver(string result, string reason)
    {
        if (string.Equals(result, "DRAW", StringComparison.OrdinalIgnoreCase))
        {
            FinishDraw(string.IsNullOrWhiteSpace(reason) ? "Draw" : reason);
            return;
        }

        PieceTeam winner = string.Equals(result, "BLACK_WON", StringComparison.OrdinalIgnoreCase)
            ? PieceTeam.Black
            : PieceTeam.White;
        FinishGame(winner);
    }

    private bool CanLocalPlayerInteract()
    {
        return !restrictInputToControlledTeam || currentTurn == localControlledTeam;
    }

    private bool CanControlPieceTeam(PieceTeam team)
    {
        return !restrictInputToControlledTeam || (team == localControlledTeam && currentTurn == localControlledTeam);
    }

    public IReadOnlyList<PieceType> GetCapturedPieces(PieceTeam capturingTeam)
    {
        return GetCapturedPieceList(capturingTeam);
    }

    public int GetCapturedPieceCount(PieceTeam capturingTeam)
    {
        return GetCapturedPieceList(capturingTeam).Count;
    }

    public int GetCapturedMaterialScore(PieceTeam capturingTeam)
    {
        int score = 0;
        List<PieceType> capturedPieces = GetCapturedPieceList(capturingTeam);
        for (int i = 0; i < capturedPieces.Count; i++)
            score += GetPieceMaterialValue(capturedPieces[i]);

        return score;
    }

    public void SetExternalLastMoveSummary(string moveSummary, bool replaceLatestHistory = false)
    {
        if (string.IsNullOrWhiteSpace(moveSummary))
            return;

        lastMoveSummary = moveSummary.Trim();
        if (replaceLatestHistory && moveHistory.Count > 0)
            moveHistory[moveHistory.Count - 1] = lastMoveSummary;
        else
            moveHistory.Add(lastMoveSummary);
    }

    private List<PieceType> GetCapturedPieceList(PieceTeam capturingTeam)
    {
        return capturingTeam == PieceTeam.White ? whiteCapturedPieces : blackCapturedPieces;
    }

    private void RebuildCapturedPiecesFromBoard()
    {
        whiteCapturedPieces.Clear();
        blackCapturedPieces.Clear();
        AppendMissingStartingPieces(PieceTeam.Black, whiteCapturedPieces);
        AppendMissingStartingPieces(PieceTeam.White, blackCapturedPieces);
    }

    private void AppendMissingStartingPieces(PieceTeam capturedTeam, List<PieceType> destination)
    {
        AppendMissingPieces(capturedTeam, PieceType.Queen, 1, destination);
        AppendMissingPieces(capturedTeam, PieceType.Rook, 2, destination);
        AppendMissingPieces(capturedTeam, PieceType.Bishop, 2, destination);
        AppendMissingPieces(capturedTeam, PieceType.Knight, 2, destination);
        AppendMissingPieces(capturedTeam, PieceType.Pawn, 8, destination);
    }

    private void AppendMissingPieces(PieceTeam team, PieceType type, int startingCount, List<PieceType> destination)
    {
        int remaining = 0;
        for (int x = 0; x < pieces.GetLength(0); x++)
            for (int y = 0; y < pieces.GetLength(1); y++)
            {
                ChessPiece piece = pieces[x, y];
                if (piece && piece.Team == team && piece.Type == type)
                    remaining++;
            }

        for (int i = remaining; i < startingCount; i++)
            destination.Add(type);
    }

    private ChessPiece GetPieceAt(Vector2Int position)
    {
        if (!ChessMoveRules.IsInsideBoard(position))
            return null;

        return pieces[position.x, position.y];
    }

    private void NotifyMoveCommitted(ChessLanMove move)
    {
        if (!suppressMoveCommittedEvent)
            MoveCommitted?.Invoke(move);

        ClearPendingCommittedMove();
    }

    private void ClearPendingCommittedMove()
    {
        pendingCommittedMoveFrom = -Vector2Int.one;
        pendingCommittedMoveTo = -Vector2Int.one;
    }

    private int CountPiecesRemaining(PieceTeam team)
    {
        int count = 0;
        for (int x = 0; x < pieces.GetLength(0); x++)
            for (int y = 0; y < pieces.GetLength(1); y++)
            {
                ChessPiece piece = pieces[x, y];
                if (piece && piece.Team == team)
                    count++;
            }

        return count;
    }

    private int GetMaterialScore(PieceTeam team)
    {
        int score = 0;
        for (int x = 0; x < pieces.GetLength(0); x++)
            for (int y = 0; y < pieces.GetLength(1); y++)
            {
                ChessPiece piece = pieces[x, y];
                if (!piece || piece.Team != team)
                    continue;

                score += GetPieceMaterialValue(piece.Type);
            }

        return score;
    }

    private int GetPieceMaterialValue(PieceType pieceType)
    {
        switch (pieceType)
        {
            case PieceType.Queen:
                return 9;
            case PieceType.Rook:
                return 5;
            case PieceType.Bishop:
            case PieceType.Knight:
                return 3;
            case PieceType.Pawn:
                return 1;
            default:
                return 0;
        }
    }

    private string BuildMoveSummary(ChessPiece movingPiece, Vector2Int from, Vector2Int destination, ChessPiece capturedPiece, bool isCastling)
    {
        if (isCastling)
            return destination.x > from.x ? "O-O" : "O-O-O";

        PieceType pieceType = movingPiece != null ? movingPiece.Type : PieceType.Pawn;
        string pieceLabel = pieceType == PieceType.Pawn ? string.Empty : GetMovePieceLabel(pieceType);
        string separator = capturedPiece ? "x" : "-";
        return $"{pieceLabel}{FormatSquare(from)}{separator}{FormatSquare(destination)}";
    }

    private string AppendPromotionToMoveSummary(string moveSummary, PieceType promotionType)
    {
        string normalized = string.IsNullOrWhiteSpace(moveSummary) ? "Pawn move" : moveSummary.Trim();
        return $"{normalized}={GetMovePieceLabel(promotionType)}";
    }

    private string GetMovePieceLabel(PieceType pieceType)
    {
        switch (pieceType)
        {
            case PieceType.King:
                return "K";
            case PieceType.Queen:
                return "Q";
            case PieceType.Rook:
                return "R";
            case PieceType.Bishop:
                return "B";
            case PieceType.Knight:
                return "N";
            default:
                return string.Empty;
        }
    }

    private void RefreshLocalInteractionState()
    {
        bool shouldEnableInteraction = gameStarted &&
            !gameOver &&
            !inputLocked &&
            !pauseLocked &&
            pendingPromotionPawn == null &&
            CanLocalPlayerInteract();

        chessboard?.SetInteractionEnabled(shouldEnableInteraction);
    }

    private void ClearPieceMap()
    {
        for (int x = 0; x < pieces.GetLength(0); x++)
            for (int y = 0; y < pieces.GetLength(1); y++)
                pieces[x, y] = null;
    }

    private ChessPiece CreatePieceFromFenSymbol(char symbol, Vector2Int boardPosition)
    {
        PieceTeam team = char.IsUpper(symbol) ? PieceTeam.White : PieceTeam.Black;
        PieceType pieceType = GetPieceTypeFromFenSymbol(symbol);
        int forwardDirection = team == PieceTeam.White ? 1 : -1;
        ChessPiece piece = CreateVisualPieceObject(team, pieceType, boardPosition, forwardDirection);
        if (!piece)
            return null;

        piece.SetStartingBoardPosition(boardPosition, forwardDirection);
        MovePieceToTile(piece, boardPosition, 0f);
        return piece;
    }

    private PieceType GetPieceTypeFromFenSymbol(char symbol)
    {
        switch (char.ToLowerInvariant(symbol))
        {
            case 'k':
                return PieceType.King;
            case 'q':
                return PieceType.Queen;
            case 'r':
                return PieceType.Rook;
            case 'b':
                return PieceType.Bishop;
            case 'n':
                return PieceType.Knight;
            case 'p':
            default:
                return PieceType.Pawn;
        }
    }

    private ChessPiece CreateVisualPieceObject(PieceTeam team, PieceType pieceType, Vector2Int boardPosition, int forwardDirection)
    {
        string sourceName = GetVisualSourceName(team, pieceType);
        Transform sourceTransform = FindVisualChild(sourceName);
        GameObject pieceObject = new GameObject($"{team} {pieceType} {boardPosition.x},{boardPosition.y}");
        pieceObject.transform.SetParent(runtimePiecesRoot);

        if (sourceTransform)
        {
            pieceObject.transform.rotation = sourceTransform.rotation;
            pieceObject.transform.localScale = sourceTransform.lossyScale;

            MeshFilter sourceMeshFilter = sourceTransform.GetComponent<MeshFilter>();
            MeshRenderer sourceRenderer = sourceTransform.GetComponent<MeshRenderer>();
            if (sourceMeshFilter && sourceMeshFilter.sharedMesh)
            {
                List<MeshComponentData> components = SplitMeshIntoSpatialGroups(sourceMeshFilter.sharedMesh, GetExpectedGroupCount(pieceType));
                if (components.Count > 0)
                {
                    components.Sort((left, right) =>
                        sourceTransform.TransformPoint(left.pivot).x.CompareTo(sourceTransform.TransformPoint(right.pivot).x));
                    int componentIndex = GetPreferredVisualIndex(pieceType, boardPosition.x, components.Count);
                    pieceObject.transform.position = sourceTransform.TransformPoint(components[componentIndex].pivot);
                    MeshFilter meshFilter = pieceObject.AddComponent<MeshFilter>();
                    meshFilter.sharedMesh = components[componentIndex].mesh;
                }
                else
                {
                    pieceObject.transform.position = sourceTransform.position;
                    MeshFilter meshFilter = pieceObject.AddComponent<MeshFilter>();
                    meshFilter.sharedMesh = sourceMeshFilter.sharedMesh;
                }
            }

            if (sourceRenderer)
            {
                MeshRenderer meshRenderer = pieceObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            }
        }

        ChessPiece piece = AddPieceComponent(pieceObject, pieceType);
        EnsurePieceCollider(pieceObject);
        piece.Initialize(team, boardPosition, forwardDirection);
        return piece;
    }

    private int GetExpectedGroupCount(PieceType pieceType)
    {
        switch (pieceType)
        {
            case PieceType.Pawn:
                return 8;
            case PieceType.Rook:
            case PieceType.Bishop:
            case PieceType.Knight:
                return 2;
            case PieceType.King:
            case PieceType.Queen:
            default:
                return 1;
        }
    }

    private int GetPreferredVisualIndex(PieceType pieceType, int file, int componentCount)
    {
        if (componentCount <= 1)
            return 0;

        switch (pieceType)
        {
            case PieceType.Pawn:
                return Mathf.Clamp(file, 0, componentCount - 1);
            case PieceType.Rook:
            case PieceType.Bishop:
            case PieceType.Knight:
                return file <= 3 ? 0 : componentCount - 1;
            default:
                return 0;
        }
    }

    private void ApplyFenMovementFlags(string castlingRights)
    {
        ChessPiece[] scenePieces = runtimePiecesRoot.GetComponentsInChildren<ChessPiece>(true);
        for (int i = 0; i < scenePieces.Length; i++)
        {
            ChessPiece piece = scenePieces[i];
            if (ShouldMarkPieceAsMovedFromFen(piece, castlingRights))
                piece.MarkMoved();
        }
    }

    private bool ShouldMarkPieceAsMovedFromFen(ChessPiece piece, string castlingRights)
    {
        if (!piece)
            return false;

        switch (piece.Type)
        {
            case PieceType.Pawn:
                return piece.Team == PieceTeam.White ? piece.BoardPosition.y != 1 : piece.BoardPosition.y != 6;
            case PieceType.King:
                return piece.Team == PieceTeam.White
                    ? !castlingRights.Contains("K") && !castlingRights.Contains("Q")
                    : !castlingRights.Contains("k") && !castlingRights.Contains("q");
            case PieceType.Rook:
                if (piece.Team == PieceTeam.White && piece.BoardPosition == new Vector2Int(0, 0))
                    return !castlingRights.Contains("Q");
                if (piece.Team == PieceTeam.White && piece.BoardPosition == new Vector2Int(7, 0))
                    return !castlingRights.Contains("K");
                if (piece.Team == PieceTeam.Black && piece.BoardPosition == new Vector2Int(0, 7))
                    return !castlingRights.Contains("q");
                if (piece.Team == PieceTeam.Black && piece.BoardPosition == new Vector2Int(7, 7))
                    return !castlingRights.Contains("k");
                return true;
            default:
                return true;
        }
    }

    private void ApplyFenEnPassant(string enPassantSquare)
    {
        if (string.IsNullOrWhiteSpace(enPassantSquare) || enPassantSquare == "-")
            return;

        if (!TryParseSquare(enPassantSquare, out Vector2Int targetSquare))
            return;

        PieceTeam justMovedTeam = currentTurn == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        Vector2Int pawnPosition = justMovedTeam == PieceTeam.White
            ? new Vector2Int(targetSquare.x, targetSquare.y + 1)
            : new Vector2Int(targetSquare.x, targetSquare.y - 1);
        ChessPiece pawn = GetPieceAt(pawnPosition);
        if (!pawn || pawn.Type != PieceType.Pawn || pawn.Team != justMovedTeam)
            return;

        lastMovedPiece = pawn;
        lastMoveTo = pawnPosition;
        lastMoveWasPawnDoubleStep = true;
    }

    private bool TryParseSquare(string square, out Vector2Int boardPosition)
    {
        boardPosition = -Vector2Int.one;
        if (string.IsNullOrWhiteSpace(square) || square.Length != 2)
            return false;

        char file = char.ToLowerInvariant(square[0]);
        char rank = square[1];
        if (file < 'a' || file > 'h' || rank < '1' || rank > '8')
            return false;

        boardPosition = new Vector2Int(file - 'a', rank - '1');
        return true;
    }

    private readonly struct MeshComponentData
    {
        public readonly Mesh mesh;
        public readonly Vector3 pivot;

        public MeshComponentData(Mesh mesh, Vector3 pivot)
        {
            this.mesh = mesh;
            this.pivot = pivot;
        }
    }

    private readonly struct TriangleData
    {
        public readonly int subMesh;
        public readonly int a;
        public readonly int b;
        public readonly int c;
        public readonly float centerAxis;

        public TriangleData(int subMesh, int a, int b, int c, float centerAxis)
        {
            this.subMesh = subMesh;
            this.a = a;
            this.b = b;
            this.c = c;
            this.centerAxis = centerAxis;
        }
    }

    private struct MoveSimulation
    {
        public ChessPiece destinationPiece;
        public bool isEnPassant;
        public ChessPiece enPassantCapturedPiece;
        public Vector2Int enPassantCapturePosition;
        public bool isCastling;
        public ChessPiece castlingRook;
        public Vector2Int castlingRookFrom;
        public Vector2Int castlingRookTo;
    }
}
