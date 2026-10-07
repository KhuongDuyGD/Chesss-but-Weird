using MatchReward = MatchRewardPolicy.MatchReward;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class ChessGame : MonoBehaviour
{
    public enum ChessGameStatus
    {
        NotStarted,
        Playing,
        Win,
        Draw
    }

    private const int BoardSize = 8;
    private const int FiftyMoveRuleHalfMoveLimit = 100;
    private const float BoardClickRaycastDistance = 100f;

    [SerializeField] private Chessboard chessboard;
    [SerializeField] private Transform piecesRoot;
    [SerializeField] private bool spawnDefaultPiecesOnStart = true;
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
    private PieceAnimator pieceAnimator;
    private PieceAnimator Animator => pieceAnimator ?? (pieceAnimator = new PieceAnimator(this));
    private readonly Dictionary<string, int> positionHistory = new Dictionary<string, int>();
    private readonly HashSet<ChessPiece> startingPlacementPieces = new HashSet<ChessPiece>();
    private readonly List<string> moveHistory = new List<string>();
    private readonly List<PieceType> whiteCapturedPieces = new List<PieceType>();
    private readonly List<PieceType> blackCapturedPieces = new List<PieceType>();
    private readonly List<Vector2Int> candidateMoveBuffer = new List<Vector2Int>(32);
    private readonly List<Vector2Int> safeMoveBuffer = new List<Vector2Int>(32);
    private readonly List<ChessPiece> nonKingPieceBuffer = new List<ChessPiece>(30);
    private readonly Dictionary<int, ChessPiece> localClassicPiecesById = new Dictionary<int, ChessPiece>();
    private PieceTeam currentTurn = PieceTeam.White;
    private ChessPiece selectedPiece;
    private Transform runtimePiecesRoot;
    private ChessGameStatus status = ChessGameStatus.NotStarted;
    private bool gameStarted;
    private bool contentLoading;
    private bool gameOver;
    private bool matchEnding;
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
    private bool aramMode;
    private StockfishDifficulty botDifficulty = StockfishDifficulty.Medium;
    private bool pendingRemotePromotionResolution;
    private PieceType remotePromotionType = PieceType.Queen;
    private ChessButWeird.Application.ClassicMatchCoordinator localClassicCoordinator;
    private Vector2Int pendingCommittedMoveFrom = -Vector2Int.one;
    private Vector2Int pendingCommittedMoveTo = -Vector2Int.one;
    private float matchStartedAt;
    private float frozenMatchDurationSeconds;
    private float matchPausedAt = -1f;
    private float accumulatedPausedSeconds;
    private string lastMoveSummary = "No moves yet.";
    private PieceTeam moveHistoryFirstTurn = PieceTeam.White;
    private ChessOrbitCamera orbitCamera;
    private Camera gameplayCamera;
    private int boardClickRaycastMask;
    private ChessPiece whiteKing;
    private ChessPiece blackKing;
    private string whitePieceSkinId = PieceSkinCatalog.DefaultSkinId;
    private string blackPieceSkinId = PieceSkinCatalog.DefaultSkinId;
    private ChessButWeird.Application.AramMatchCoordinator aramCoordinator;
    private readonly ChessButWeird.Application.MatchResultRecorder matchResultRecorder =
        new ChessButWeird.Application.MatchResultRecorder();
    public PieceTeam CurrentTurn => currentTurn;
    public ChessPiece SelectedPiece => selectedPiece;
    public bool GameStarted => gameStarted && !contentLoading;
    public Chessboard Board => chessboard;
    public bool GameOver => gameOver;
    public bool IsMatchEnding => matchEnding;
    public bool InputLocked => inputLocked || contentLoading;
    public bool PauseLocked => pauseLocked;
    public bool IsBotGame => botMode;
    public bool IsHotseatGame => !botMode && !serverAuthoritativeMode && !restrictInputToControlledTeam;
    public bool IsAramGame => aramMode;
    public bool UsesLocalClassicSession => localClassicCoordinator != null && !aramMode && !serverAuthoritativeMode && !botMode;
    public bool UsesClassicDomainSession => localClassicCoordinator != null && !aramMode && !serverAuthoritativeMode;
    public bool HasPendingPromotion => pendingPromotionPawn != null;
    public PieceTeam WinningTeam => winningTeam;
    public PieceTeam PlayerTeam => playerTeam;
    public ChessGameStatus Status => status;
    public bool HasSelectedPiece => selectedPiece != null;
    public string DrawReason => drawReason;
    public int HalfMoveClock => halfMoveClock;
    public float MatchElapsedSeconds => gameStarted
        ? Mathf.Max(0f, Time.unscaledTime - matchStartedAt - accumulatedPausedSeconds - CurrentPauseDuration)
        : frozenMatchDurationSeconds;
    private float CurrentPauseDuration => matchPausedAt >= 0f ? Time.unscaledTime - matchPausedAt : 0f;
    public string LastMoveSummary => string.IsNullOrWhiteSpace(lastMoveSummary) ? "No moves yet." : lastMoveSummary;
    public IReadOnlyList<string> MoveHistory => moveHistory;
    public PieceTeam MoveHistoryFirstTurn => moveHistoryFirstTurn;
    public event Action<ChessMove> MoveCommitted;
    public event Action ReturnedToMainMenu;
    public event Action ContentReady;
    public event Action LocalGameRestarted;
    public event Action MatchEnding;

    private void Awake()
    {
        GameRuntimeSettings.ApplySaved();

        if (!chessboard)
            chessboard = FindAnyObjectByType<Chessboard>();

        boardClickRaycastMask = CreateLayerMaskOrDefault("ChessPiece", "Tile", "Hover");
        EnsureOrbitCamera();

        EnsureAudioSource();
        AutoAssignDefaultAudioClips();
    }

    private void EnsureAudioSource()
    {
        audioSource = MatchAudioPresenter.EnsureSource(gameObject, audioSource);
    }

    private Camera GetGameplayCamera()
    {
        if (gameplayCamera)
            return gameplayCamera;

        gameplayCamera = Camera.main;
        if (!gameplayCamera)
            gameplayCamera = FindAnyObjectByType<Camera>();

        return gameplayCamera;
    }

    private static int CreateLayerMaskOrDefault(params string[] layerNames)
    {
        int mask = LayerMask.GetMask(layerNames);
        return mask == 0 ? Physics.DefaultRaycastLayers : mask;
    }

    private void PlaySound(AudioClip clip)
    {
        if ((clip == moveSound || clip == castleSound) && !UserSettings.Enabled("move_sounds")) return;
        if (clip == hitSound && !UserSettings.Enabled("capture_sounds")) return;
        audioSource = MatchAudioPresenter.Play(gameObject, audioSource, clip,
            soundVolume);
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

    private async void Start()
    {
        // Restore the new API session by fetching current data from the server.
        if (AuthStorage.HasSession())
        {
            try
            {
                UserMeResponse user = await new UserService().GetMeAsync();
                if (this == null) return;
                PlayerAuthService.ApplyApiUser(user);
                QueueAuthenticatedSession();
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ChessGame] Session restore failed: " + exception.Message);
                if (this == null) return;
                if (exception is ApiException apiError && (apiError.StatusCode == 401 || apiError.StatusCode == 403))
                    AuthStorage.Clear();
            }
        }

        if (PlayerAuthService.IsGuestSession)
            QueueAuthenticatedSession();
        else
            AuthController.Create(this, QueueAuthenticatedSession);
    }

    public void QueueAuthenticatedSession()
    {
        SessionLoadingController.For(this).Begin();
    }

    internal void PrepareAuthenticatedBoard()
    {
        if (chessboard) PrepareGame();
    }

    public void AttachBoard(Chessboard board)
    {
        chessboard = board;
        gameplayCamera = null;
        if (board) EnsureOrbitCamera();
    }

    public void SetContentLoading(bool loading)
    {
        bool wasLoading = contentLoading;
        contentLoading = loading;
        RefreshLocalInteractionState();
        if (wasLoading && !loading) ContentReady?.Invoke();
    }

    internal void ClearCosmeticPieces()
    {
        GetComponent<CapturedPieceDisplay>()?.ResetDisplay();
        ResetDotNetPresentation();
        ClearLocalClassicSession();
        aramCoordinator?.EndMatch();
        pieceAnimator?.CancelAll();
        StopCheckWarning();
        StopAllCoroutines();
        if (runtimePiecesRoot)
        {
            runtimePiecesRoot.gameObject.SetActive(false);
            Destroy(runtimePiecesRoot.gameObject);
            runtimePiecesRoot = null;
        }
        ClearPieceMap();
        gameStarted = false;
        status = ChessGameStatus.NotStarted;
    }

    internal void CompleteAuthenticatedSession()
    {
        if (!turnSelectionUI)
            turnSelectionUI = ChessTurnSelectionUI.Create(this);
        else
            turnSelectionUI.RestoreAuthenticatedMenu();
    }

    private void Update()
    {
        if (contentLoading) return;
        if (dotNetState != null) { HandleDotNetInput(); return; }
        if (aramMode && aramCoordinator != null && aramCoordinator.Runtime.HandleAbilityInput()) return;
        if (gameStarted && aramMode && aramCoordinator != null && aramCoordinator.IsSelectingSetupTargets)
        {
            if (pauseLocked || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            aramCoordinator.TryHandleSetupPieceClick(GetPieceUnderPointerForAramSetup());
            return;
        }

        if (!gameStarted || inputLocked || pauseLocked || !CanLocalPlayerInteract() || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        HandleBoardClick();
    }

    private ChessPiece GetPieceUnderPointerForAramSetup()
    {
        Camera currentCamera = GetGameplayCamera();
        if (!currentCamera)
            return null;

        Ray ray = currentCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hitInfo, BoardClickRaycastDistance, boardClickRaycastMask))
            return null;

        ChessPiece clickedPiece = hitInfo.collider.GetComponentInParent<ChessPiece>();
        if (clickedPiece)
            return clickedPiece;

        if (chessboard.TryGetTileFromObject(hitInfo.collider.gameObject, out Vector2Int tile))
            return pieces[tile.x, tile.y];

        return null;
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
            CacheKing(piece);
        }

        selectedPiece = null;
        chessboard?.ClearLegalMoveHighlights();
    }

    public void BeginGame(PieceTeam firstTurn)
    {
        ResetDotNetPresentation();
        serverAuthoritativeMode = false;
        botMode = false;
        aramMode = false;
        botDifficulty = StockfishDifficulty.Medium;
        GameMusicManager.PlayInGameMusic(false, StockfishDifficulty.Medium);
        BeginGameInternal(firstTurn, firstTurn, false, firstTurn);
    }

    public void BeginAramGame()
    {
        ResetDotNetPresentation();
        serverAuthoritativeMode = false;
        botMode = false;
        aramMode = true;
        botDifficulty = StockfishDifficulty.Medium;
        EnsureAramRuntime();
        GameMusicManager.PlayInGameMusic(false, StockfishDifficulty.Medium);
        BeginGameInternal(PieceTeam.White, PieceTeam.White, false, PieceTeam.White);
        aramCoordinator.BeginMatch(this);
    }

    public void BeginBotGame(PieceTeam localPlayerTeam)
    {
        BeginBotGame(localPlayerTeam, StockfishDifficulty.Medium);
    }

    public void BeginBotGame(PieceTeam localPlayerTeam, StockfishDifficulty selectedDifficulty)
        => BeginBotGame(localPlayerTeam, selectedDifficulty, BotGameOptions.Challenge);

    public void BeginBotGame(PieceTeam localPlayerTeam, StockfishDifficulty selectedDifficulty, BotGameOptions options)
    {
        ResetDotNetPresentation();
        serverAuthoritativeMode = false;
        botMode = true;
        aramMode = false;
        botDifficulty = selectedDifficulty;
        botOptions = options ?? BotGameOptions.Challenge;
        BeginGameInternal(PieceTeam.White, localPlayerTeam, true, PieceTeam.White);
    }

    public void ConfigurePieceSkinsForBot(PieceTeam playerSide, string playerSkinId)
    {
        whitePieceSkinId = PieceSkinCatalog.DefaultSkinId;
        blackPieceSkinId = PieceSkinCatalog.DefaultSkinId;
        SetPieceSkinId(playerSide, playerSkinId);
        ApplyPieceSkinsToRuntimePieces();
    }

    public void ConfigurePieceSkinsForLocalPlayers(string whiteSkinId, string blackSkinId)
    {
        whitePieceSkinId = PieceSkinCatalog.NormalizeId(whiteSkinId);
        blackPieceSkinId = PieceSkinCatalog.NormalizeId(blackSkinId);

        ApplyPieceSkinsToRuntimePieces();
    }

    public string GetPieceSkinId(PieceTeam team)
    {
        return team == PieceTeam.White ? whitePieceSkinId : blackPieceSkinId;
    }

    private void SetPieceSkinId(PieceTeam team, string skinId)
    {
        if (team == PieceTeam.White)
            whitePieceSkinId = PieceSkinCatalog.NormalizeId(skinId);
        else
            blackPieceSkinId = PieceSkinCatalog.NormalizeId(skinId);
    }

    private void BeginGameInternal(PieceTeam firstTurn, PieceTeam localPlayerTeam, bool restrictInput, PieceTeam frontTeam)
    {
        // Direct scene play can enter here before the normal content loader prepares pieces.
        if (!runtimePiecesRoot)
        {
            if (!chessboard) throw new InvalidOperationException("Cannot start a match without a Chessboard.");
            PrepareGame();
        }
        ClearLocalClassicSession();
        var prisoners = GetComponent<CapturedPieceDisplay>();
        if (!prisoners) prisoners = gameObject.AddComponent<CapturedPieceDisplay>();
        prisoners.Initialize(this, chessboard);
        var pieceNames = GetComponent<PieceHoverTooltip>();
        if (!pieceNames) pieceNames = gameObject.AddComponent<PieceHoverTooltip>();
        pieceNames.Initialize(this);
        matchResultRecorder.Begin(Guid.NewGuid().ToString("N"));
        currentTurn = firstTurn;
        moveHistoryFirstTurn = firstTurn;
        playerTeam = localPlayerTeam;
        localControlledTeam = localPlayerTeam;
        restrictInputToControlledTeam = restrictInput;
        selectedPiece = null;
        gameOver = false;
        matchEnding = false;
        inputLocked = false;
        status = ChessGameStatus.Playing;
        drawReason = string.Empty;
        matchStartedAt = Time.unscaledTime;
        frozenMatchDurationSeconds = 0f;
        matchPausedAt = -1f;
        accumulatedPausedSeconds = 0f;
        lastMoveSummary = "No moves yet.";
        moveHistory.Clear();
        ResetStatistics();
        whiteCapturedPieces.Clear();
        blackCapturedPieces.Clear();
        ResetDrawTracking();
        ArrangePiecesForFirstTurn(frontTeam);
        ApplyPieceSkinsToRuntimePieces();
        SetRuntimePiecesVisible(true);
        RefreshPieceMap();
        if (!aramMode && !serverAuthoritativeMode)
            InitializeClassicSession(firstTurn, frontTeam);
        RecordCurrentPosition();
        gameStarted = true;
        ResetBotHistory();
        ApplyGameplayCameraState(localPlayerTeam, true);
        chessboard?.SetPresentationVisible(true);
        chessboard?.ClearLegalMoveHighlights();
        RefreshLocalInteractionState();
        turnSelectionUI?.SetTurn(currentTurn);
        UpdateCheckWarningForCurrentTurn();
    }

    private void InitializeClassicSession(PieceTeam firstTurn, PieceTeam frontTeam)
    {
        var orientation = new ChessButWeird.Domain.BoardOrientation((ChessButWeird.Domain.Team)frontTeam);
        localClassicCoordinator = ChessButWeird.Application.ClassicMatchCoordinator.CreateClassic(
            (ChessButWeird.Domain.Team)firstTurn, orientation);
        RebindLocalClassicPieces();
    }

    private void RebindLocalClassicPieces()
    {
        localClassicPiecesById.Clear();
        if (localClassicCoordinator == null)
            return;

        for (int x = 0; x < BoardSize; x++)
            for (int y = 0; y < BoardSize; y++)
            {
                ChessPiece piece = pieces[x, y];
                if (!piece)
                    continue;

                ChessButWeird.Domain.PieceState state = localClassicCoordinator.GetPiece(
                    new ChessButWeird.Domain.Square(x, y));
                if (!state.IsEmpty && (PieceTeam)state.Team == piece.Team &&
                    (PieceType)state.Kind == piece.Type)
                    localClassicPiecesById[state.Id] = piece;
            }
    }

    private void ClearLocalClassicSession()
    {
        if (localClassicCoordinator != null)
            localClassicCoordinator.ClearPendingCommand();
        localClassicCoordinator = null;
        localClassicPiecesById.Clear();
    }

    private void RemoveLocalClassicPiece(ChessPiece piece)
    {
        if (!piece)
            return;

        int idToRemove = 0;
        foreach (KeyValuePair<int, ChessPiece> entry in localClassicPiecesById)
            if (entry.Value == piece)
            {
                idToRemove = entry.Key;
                break;
            }

        if (idToRemove != 0)
            localClassicPiecesById.Remove(idToRemove);
    }

    private ChessPiece GetLocalClassicPieceById(int id)
    {
        return id > 0 && localClassicPiecesById.TryGetValue(id, out ChessPiece piece) ? piece : null;
    }

    private void ReplaceLocalClassicPiece(ChessPiece oldPiece, ChessPiece newPiece)
    {
        if (!oldPiece || !newPiece || localClassicCoordinator == null)
            return;

        int idToReplace = 0;
        foreach (KeyValuePair<int, ChessPiece> entry in localClassicPiecesById)
            if (entry.Value == oldPiece)
            {
                idToReplace = entry.Key;
                break;
            }

        if (idToReplace != 0)
            localClassicPiecesById[idToReplace] = newPiece;
    }

    public bool TrySelectPiece(ChessPiece piece)
    {
        if (!gameStarted || inputLocked || pauseLocked || !piece || piece.Team != currentTurn || !CanControlPieceTeam(piece.Team))
            return false;

        if (aramMode && !serverAuthoritativeMode && selectedPiece && selectedPiece != piece && selectedPiece.Type == PieceType.Pawn &&
            piece.Type == PieceType.Pawn && aramCoordinator.Runtime.HasBuff(currentTurn, AramBuffId.NobleSacrifice) &&
            TryMoveSelectedPiece(piece.BoardPosition)) return true;

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
        if (dotNetState != null) return RequestDotNetMove(selectedPiece, destination);
        if(aramMode&&!serverAuthoritativeMode&&aramCoordinator.Runtime.IsSniperAttack(selectedPiece,destination))
        {
            var bishop=selectedPiece;
            if(!aramCoordinator.Runtime.TrySniperAttack(bishop,destination)){PlaySound(errorSound);return false;}
            // A ranged capture spends this move while the Bishop stays on its square.
            lastMoveSummary=$"{FormatSquare(from)}x{FormatSquare(destination)} (snipe)";
            moveHistory.Add(lastMoveSummary);
            PlaySound(hitSound);
            if(AramIsAlive(bishop))AnimatePieceToTile(bishop,from,0f,selectAnimationDuration,0f);
            return true;
        }
        pendingCommittedMoveFrom = from;
        pendingCommittedMoveTo = destination;
        bool localSessionPromotionPending = false;
        ChessButWeird.Domain.MatchSessionMoveResult localSessionResult = null;
        bool moveAccepted;
        if (UsesClassicDomainSession)
        {
            ChessButWeird.Application.ClassicMoveStatus submission = localClassicCoordinator.Submit(
                new ChessButWeird.Domain.Move(UnityBoardAdapter.ToSquare(from),
                    UnityBoardAdapter.ToSquare(destination)), out localSessionResult);
            localSessionPromotionPending = submission == ChessButWeird.Application.ClassicMoveStatus.PromotionRequired;
            moveAccepted = submission == ChessButWeird.Application.ClassicMoveStatus.Applied ||
                localSessionPromotionPending;
        }
        else
        {
            moveAccepted = IsLegalMoveAfterKingSafety(selectedPiece, from, destination);
        }

        if (!moveAccepted)
        {
            ClearPendingCommittedMove();
            PlaySound(errorSound);
            return false;
        }

        ChessPiece movingPiece = selectedPiece;
        ChessPiece capturedPiece = pieces[destination.x, destination.y];
        PieceTeam opponentTeam = movingPiece.Team == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        bool isEnPassant;
        ChessPiece enPassantCapturedPiece;
        Vector2Int enPassantCapturePosition;
        bool isCastling;
        ChessPiece castlingRook;
        Vector2Int castlingRookFrom;
        Vector2Int castlingRookTo;
        if (UsesClassicDomainSession && localSessionResult != null)
        {
            // The accepted domain result is the single rule decision for local
            // classic. Resolve its stable IDs into the existing visual objects
            // instead of asking the legacy helpers to decide capture/castle again.
            isEnPassant = false;
            enPassantCapturedPiece = null;
            enPassantCapturePosition = default;
            isCastling = localSessionResult.IsCastle;
            castlingRook = null;
            castlingRookFrom = default;
            castlingRookTo = default;
            for (int changeIndex = 0; changeIndex < localSessionResult.Changes.Count; changeIndex++)
            {
                ChessButWeird.Domain.BoardChange change = localSessionResult.Changes[changeIndex];
                if (change.Kind == ChessButWeird.Domain.BoardChangeKind.Capture)
                {
                    enPassantCapturedPiece = GetLocalClassicPieceById(change.PieceId);
                    if (enPassantCapturedPiece)
                    {
                        capturedPiece = enPassantCapturedPiece;
                        enPassantCapturePosition = new Vector2Int(change.From.File, change.From.Rank);
                        isEnPassant = capturedPiece != pieces[destination.x, destination.y];
                    }
                }
                else if (change.Kind == ChessButWeird.Domain.BoardChangeKind.Move && change.From != UnityBoardAdapter.ToSquare(from))
                {
                    castlingRook = GetLocalClassicPieceById(change.PieceId);
                    if (castlingRook)
                    {
                        castlingRookFrom = new Vector2Int(change.From.File, change.From.Rank);
                        castlingRookTo = new Vector2Int(change.To.File, change.To.Rank);
                    }
                }
            }
            if (isEnPassant)
                enPassantCapturedPiece = capturedPiece;
        }
        else if (UsesClassicDomainSession)
        {
            // A promotion is validated before the user's choice and committed
            // in CompletePromotion. Its capture is still the destination piece;
            // en-passant and castling cannot occur on a promotion rank.
            isEnPassant = false;
            enPassantCapturedPiece = null;
            enPassantCapturePosition = default;
            isCastling = false;
            castlingRook = null;
            castlingRookFrom = default;
            castlingRookTo = default;
        }
        else
        {
            isEnPassant = TryGetEnPassantCapture(movingPiece, from, destination, out enPassantCapturedPiece, out enPassantCapturePosition);
            isCastling = TryGetCastlingRookMove(movingPiece, from, destination, out castlingRook, out castlingRookFrom, out castlingRookTo);
        }
        bool movedPawn = movingPiece.Type == PieceType.Pawn;
        StopCheckWarning();

        if (isEnPassant)
            capturedPiece = enPassantCapturedPiece;

        bool capturedAnyPiece = capturedPiece;
        if (aramMode) aramCoordinator.Runtime.BeforeNormalMove(movingPiece, capturedPiece, from, destination);

        if (UsesClassicDomainSession && capturedPiece)
            RemoveLocalClassicPiece(capturedPiece);

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
        aramCoordinator?.OnMoveAccepted(movingPiece, from, destination, pieces);
        lastMoveSummary = BuildMoveSummary(movingPiece, from, destination, capturedPiece, isCastling);
        moveHistory.Add(lastMoveSummary);
        RecordStatisticsMove(movingPiece.Team, lastMoveSummary, capturedPiece);
        if (capturedPiece && capturedPiece.Team != movingPiece.Team)
            GetCapturedPieceList(movingPiece.Team).Add(capturedPiece.Type);
        List<ChessPiece> aramExplosionVictims = null;
        if (capturedPiece && aramCoordinator != null)
        {
            aramExplosionVictims = new List<ChessPiece>();
            if (!aramCoordinator.TryGetQueenExplosion(capturedPiece, destination, movingPiece, pieces, aramExplosionVictims))
                aramExplosionVictims = null;
        }
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
        if (aramExplosionVictims != null)
            DestroyAramExplosionVictims(aramExplosionVictims);
        PieceTeam nextAramTurn = opponentTeam;
        if (aramMode && !serverAuthoritativeMode)
        {
            nextAramTurn = aramCoordinator.Runtime.AfterNormalMove(movingPiece, capturedPiece, from, destination);
            if (pieces[destination.x, destination.y] != movingPiece) movingPiece = null;
            if (AramFinishIfKingMissing()) return true;
        }

        if (isCastling)
            AnimatePieceToTile(castlingRook, castlingRookTo, 0f, moveAnimationDuration, moveArcHeight * 0.5f);

        if (movingPiece is PawnPiece promotedPawn && IsPromotionRank(promotedPawn) &&
            !(aramMode&&!serverAuthoritativeMode&&aramCoordinator.Runtime.BlocksPromotion(promotedPawn)))
        {
            StartCoroutine(AnimateMoveAndPromptPromotion(promotedPawn, destination, nextAramTurn));
            return true;
        }

        currentTurn = UsesClassicDomainSession
            ? (PieceTeam)localClassicCoordinator.Turn
            : nextAramTurn;
        aramCoordinator?.OnTurnStarted(currentTurn);
        NotifyMoveCommitted(new ChessMove(from, destination));
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
        if (aramMode && aramCoordinator.Runtime.HasPendingDecision) return false;
        RecordCurrentPosition();

        if (IsCheckmate(currentTurn))
        {
            if (aramMode && aramCoordinator.Runtime.TryOfferEscape(currentTurn)) return true;
            BeginMatchEnding();
            StartCoroutine(AnimateMoveAndFinishGame(movingPiece, destination, currentTurn == PieceTeam.White ? PieceTeam.Black : PieceTeam.White));
            return true;
        }

        if(aramMode&&!serverAuthoritativeMode&&aramCoordinator.Runtime.TryFinishTurnDeadline())return true;

        if (TryGetDrawReason(out string reason))
        {
            BeginMatchEnding();
            StartCoroutine(AnimateMoveAndFinishDraw(movingPiece, destination, reason));
            return true;
        }

        return false;
    }

    private void DestroyAramExplosionVictims(List<ChessPiece> victims)
    {
        if (victims == null)
            return;

        for (int i = 0; i < victims.Count; i++)
        {
            ChessPiece victim = victims[i];
            if (!victim || (victim.Type == PieceType.King && !victim.GetComponent<AramDecoyTag>()))
                continue;
            if(aramMode&&!serverAuthoritativeMode){AramRemove(victim);continue;}

            Vector2Int position = victim.BoardPosition;
            RecordStatisticsEvent(victim.Team, victim.Type + " destroyed at " + FormatSquare(position));
            if (ChessMoveRules.IsInsideBoard(position) && pieces[position.x, position.y] == victim)
                pieces[position.x, position.y] = null;

            pieceAnimator?.Cancel(victim);

            if (aramMode && !serverAuthoritativeMode) aramCoordinator.Runtime.OnPieceRemoved(victim);

            Destroy(victim.gameObject);
        }
    }

    public void ClearSelection()
    {
        DeselectCurrentPiece(true);
    }

    public bool TryCancelSelectionFromEscape()
    {
        if (!HasSelectedPiece)
            return false;

        ClearSelection();
        return true;
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
        var loading = GetComponent<LoadingManager>();
        if (loading && loading.HasMatchContent) { loading.ReturnToMenu(); return; }
        FinishReturnToMainMenu();
    }

    internal void FinishReturnToMainMenu()
    {
        pauseLocked = false;
        Time.timeScale = 1f;
        if (chessboard) PrepareGame();
        turnSelectionUI?.ShowMainMenu();
        ReturnedToMainMenu?.Invoke();
    }

    public bool RestartCurrentLocalGame()
    {
        if (serverAuthoritativeMode)
            return false;

        PieceTeam selectedTeam = playerTeam;
        bool restartBotGame = botMode;
        bool restartAramGame = aramMode;
        StockfishDifficulty selectedBotDifficulty = botDifficulty;
        BotGameOptions selectedBotOptions = botOptions;
        pauseLocked = false;
        Time.timeScale = 1f;
        PrepareGame();
        if (restartAramGame)
            BeginAramGame();
        else if (restartBotGame)
            BeginBotGame(selectedTeam, selectedBotDifficulty, selectedBotOptions);
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

    private void OnDestroy()
    {
        pieceAnimator?.CancelAll();
        aramCoordinator?.Dispose();
    }

    private void PrepareGame()
    {
        ResetDotNetPresentation();
        StopCheckWarning();
        StopAllCoroutines();
        pieceAnimator?.CancelAll();
        ClearLocalClassicSession();
        aramCoordinator?.EndMatch();
        botFrames.Clear();

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
            CreateDefaultPieces();

        RefreshPieceMap();
        AlignAllPiecesToBoard();
        ApplyPieceSkinsToRuntimePieces();
        SetRuntimePiecesVisible(false);
        chessboard?.SetPresentationVisible(false);
        chessboard?.SetInteractionEnabled(false);

        currentTurn = PieceTeam.White;
        status = ChessGameStatus.NotStarted;
        gameStarted = false;
        gameOver = false;
        matchEnding = false;
        inputLocked = false;
        pauseLocked = false;
        playerTeam = PieceTeam.White;
        localControlledTeam = PieceTeam.White;
        restrictInputToControlledTeam = false;
        serverAuthoritativeMode = false;
        botMode = false;
        aramMode = false;
        botDifficulty = StockfishDifficulty.Medium;
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
        ResetStatistics();
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

    private void EnsureOrbitCamera()
    {
        Camera gameplayCamera = GetGameplayCamera();

        if (!gameplayCamera)
            return;

        orbitCamera = gameplayCamera.GetComponent<ChessOrbitCamera>();
        if (orbitCamera != null)
            return;

        orbitCamera = gameplayCamera.gameObject.AddComponent<ChessOrbitCamera>();
    }

    private void EnsureAramRuntime()
    {
        if (aramCoordinator != null)
            return;

        aramCoordinator = new ChessButWeird.Application.AramMatchCoordinator(gameObject);
    }

    private void ApplyGameplayCameraState(PieceTeam playerSide, bool immediate)
    {
        if (orbitCamera == null)
            EnsureOrbitCamera();

        if (orbitCamera == null)
            return;

        orbitCamera.ConfigureForPlayerSide(serverAuthoritativeMode || botMode ? playerSide : PieceTeam.White, immediate);
    }

    private void HandleBoardClick()
    {
        Camera currentCamera = GetGameplayCamera();
        if (!currentCamera)
            return;

        Ray ray = currentCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hitInfo, BoardClickRaycastDistance, boardClickRaycastMask))
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
            return;
        }

        ClearSelection();
    }

    private List<Vector2Int> GetSafeLegalMoves(ChessPiece piece)
    {
        safeMoveBuffer.Clear();
        if (!piece)
            return safeMoveBuffer;

        if (dotNetState != null) return GetDotNetMoveHints(piece);

        if (UsesClassicDomainSession)
        {
            List<ChessButWeird.Domain.Move> sessionMoves = localClassicCoordinator.LegalMovesFrom(
                UnityBoardAdapter.ToSquare(piece.BoardPosition));
            for (int i = 0; i < sessionMoves.Count; i++)
            {
                Vector2Int destination = new Vector2Int(sessionMoves[i].To.File, sessionMoves[i].To.Rank);
                if (!safeMoveBuffer.Contains(destination))
                    safeMoveBuffer.Add(destination);
            }
            return safeMoveBuffer;
        }

        List<Vector2Int> candidateMoves = GetCandidateMoves(piece);
        for (int i = 0; i < candidateMoves.Count; i++)
        {
            Vector2Int destination = candidateMoves[i];
            if (IsLegalMoveAfterKingSafety(piece, piece.BoardPosition, destination))
                safeMoveBuffer.Add(destination);
        }

        return safeMoveBuffer;
    }

    private bool IsLegalMoveAfterKingSafety(ChessPiece piece, Vector2Int from, Vector2Int destination)
    {
        if (aramMode && !serverAuthoritativeMode && !aramCoordinator.Runtime.CanMove(piece, destination)) return false;
        if (UsesClassicDomainSession)
            return localClassicCoordinator.CanApply(UnityBoardAdapter.ToSquare(from), UnityBoardAdapter.ToSquare(destination));

        if (!IsLegalMoveIgnoringKingSafety(piece, from, destination) &&
            !(aramMode&&!serverAuthoritativeMode&&aramCoordinator.Runtime.IsSniperAttack(piece,destination)))
            return false;

        ChessPiece targetPiece = pieces[destination.x, destination.y];
        if (targetPiece && targetPiece.Type == PieceType.King && !targetPiece.GetComponent<AramDecoyTag>())
            return false;

        return DoesMoveKeepTeamKingSafe(piece, from, destination, piece.Team);
    }

    private List<Vector2Int> GetCandidateMoves(ChessPiece piece)
    {
        candidateMoveBuffer.Clear();
        if (!piece)
            return candidateMoveBuffer;

        if (UsesClassicDomainSession)
        {
            List<ChessButWeird.Domain.Move> sessionMoves = localClassicCoordinator.LegalMovesFrom(
                UnityBoardAdapter.ToSquare(piece.BoardPosition));
            for (int i = 0; i < sessionMoves.Count; i++)
            {
                Vector2Int destination = new Vector2Int(sessionMoves[i].To.File, sessionMoves[i].To.Rank);
                if (!candidateMoveBuffer.Contains(destination))
                    candidateMoveBuffer.Add(destination);
            }
            return candidateMoveBuffer;
        }

        if (aramCoordinator == null || !aramCoordinator.SuppressesStandardMovement(piece))
            piece.CollectLegalMoves(pieces, candidateMoveBuffer);
        AddSpecialCandidateMoves(piece, candidateMoveBuffer);
        aramCoordinator?.AddCandidateMoves(piece, candidateMoveBuffer, pieces);
        return candidateMoveBuffer;
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
            if(aramMode&&!serverAuthoritativeMode&&aramCoordinator.Runtime.HasBuff(piece.Team,AramBuffId.StrongFortress))
            {
                AddSpecialCandidateIfLegal(piece,from,new Vector2Int(2,from.y),candidateMoves);
                AddSpecialCandidateIfLegal(piece,from,new Vector2Int(6,from.y),candidateMoves);
            }
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
        bool suppressStandardMovement = aramCoordinator != null && aramCoordinator.SuppressesStandardMovement(piece);
        return (!suppressStandardMovement && ChessMoveRules.IsLegalMove(piece, from, destination, pieces)) ||
            IsEnPassantMove(piece, from, destination) ||
            IsCastlingMove(piece, from, destination) ||
            (aramCoordinator != null && aramCoordinator.IsLegalAramMove(piece, from, destination, pieces));
    }

    private bool DoesMoveKeepTeamKingSafe(ChessPiece piece, Vector2Int from, Vector2Int destination, PieceTeam team)
    {
        if(aramMode&&!serverAuthoritativeMode&&aramCoordinator.Runtime.IsSniperAttack(piece,destination))
            return AramSafeSnipe(pieces[destination.x,destination.y],team);
        if(aramMode&&!serverAuthoritativeMode&&aramCoordinator.Runtime.IsKingless(team))return true;
        var none = new ChessButWeird.Domain.Square(-1, -1);
        var capture = UnityBoardAdapter.ToSquare(destination);
        if (TryGetEnPassantCapture(piece, from, destination, out _, out Vector2Int enPassantPosition))
            capture = UnityBoardAdapter.ToSquare(enPassantPosition);
        var rookFrom = none;
        var rookTo = none;
        if (TryGetCastlingRookMove(piece, from, destination, out _, out Vector2Int source, out Vector2Int target))
        {
            rookFrom = UnityBoardAdapter.ToSquare(source);
            rookTo = UnityBoardAdapter.ToSquare(target);
        }
        bool explosion = aramCoordinator != null && aramCoordinator.WouldQueenExplode(pieces[destination.x, destination.y]);
        if(explosion&&!serverAuthoritativeMode)
        {
            var projection=new ChessButWeird.Domain.MoveBoardView<UnityBoardAdapter>(new UnityBoardAdapter(pieces),
                UnityBoardAdapter.ToSquare(from),UnityBoardAdapter.ToSquare(destination),capture,rookFrom,rookTo,false,
                aramCoordinator.Runtime.MovingPieceWillDie(piece,from,destination));
            var contexts=new UnityBuffContextAdapter(pieces,aramCoordinator.Runtime);
            var chained=new ChessButWeird.Domain.ExplosionChainBoardView<ChessButWeird.Domain.MoveBoardView<UnityBoardAdapter>>(projection,UnityBoardAdapter.ToSquare(destination),contexts);
            return !ChessButWeird.Domain.KingSafetyRules.IsInCheck(chained,(ChessButWeird.Domain.Team)team,contexts,aramCoordinator.DomainRules);
        }
        var simulation = new ChessButWeird.Domain.MoveBoardView<UnityBoardAdapter>(new UnityBoardAdapter(pieces),
            UnityBoardAdapter.ToSquare(from), UnityBoardAdapter.ToSquare(destination), capture, rookFrom, rookTo, explosion,
            aramMode && !serverAuthoritativeMode && aramCoordinator.Runtime.MovingPieceWillDie(piece,from,destination));
        return !ChessButWeird.Domain.KingSafetyRules.IsInCheck(simulation, (ChessButWeird.Domain.Team)team,
            new UnityBuffContextAdapter(pieces, aramCoordinator != null ? aramCoordinator.Runtime : null),
            aramCoordinator != null ? aramCoordinator.DomainRules : ChessButWeird.Domain.AramRules.BuiltIn);
    }

    private bool IsCheckmate(PieceTeam team)
    {
        return IsTeamInCheck(team) && !HasAnySafeLegalMove(team);
    }

    private bool HasAnySafeLegalMove(PieceTeam team)
    {
        if (UsesClassicDomainSession)
            return localClassicCoordinator.LegalMoves().Count > 0;

        for (int x = 0; x < BoardSize; x++)
            for (int y = 0; y < BoardSize; y++)
            {
                ChessPiece piece = pieces[x, y];
                if (!piece || piece.Team != team)
                    continue;

                List<Vector2Int> candidateMoves = GetCandidateMoves(piece);
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

        if (!aramMode && HasInsufficientMaterial())
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
        string positionKey = BuildDrawPositionKey();
        return positionHistory.TryGetValue(positionKey, out int occurrenceCount) && occurrenceCount >= 3;
    }

    private bool HasInsufficientMaterial()
    {
        nonKingPieceBuffer.Clear();
        for (int x = 0; x < BoardSize; x++)
            for (int y = 0; y < BoardSize; y++)
            {
                ChessPiece piece = pieces[x, y];
                if (piece && piece.Type != PieceType.King)
                    nonKingPieceBuffer.Add(piece);
            }

        if (nonKingPieceBuffer.Count == 0)
            return true;

        if (nonKingPieceBuffer.Count == 1)
        {
            PieceType type = nonKingPieceBuffer[0].Type;
            return type == PieceType.Bishop || type == PieceType.Knight;
        }

        if (nonKingPieceBuffer.Count != 2)
            return false;

        ChessPiece first = nonKingPieceBuffer[0];
        ChessPiece second = nonKingPieceBuffer[1];
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
        if(aramMode&&!serverAuthoritativeMode&&aramCoordinator.Runtime.IsKingless(team))return false;
        ChessPiece king = FindKing(team);
        if (!king)
            return true;

        return IsSquareUnderAttack(king.BoardPosition, team == PieceTeam.White ? PieceTeam.Black : PieceTeam.White);
    }

    private bool IsSquareUnderAttack(Vector2Int square, PieceTeam attackerTeam)
    {
        ChessButWeird.Domain.AramRules rules = aramCoordinator != null
            ? aramCoordinator.DomainRules
            : ChessButWeird.Domain.AramRules.BuiltIn;
        return ChessButWeird.Domain.KingSafetyRules.IsAttacked(
            new UnityBoardAdapter(pieces),
            UnityBoardAdapter.ToSquare(square),
            (ChessButWeird.Domain.Team)attackerTeam,
            new UnityBuffContextAdapter(pieces, aramCoordinator != null ? aramCoordinator.Runtime : null),
            rules);
    }

    private ChessPiece FindKing(PieceTeam team)
    {
        ChessPiece cachedKing = team == PieceTeam.White ? whiteKing : blackKing;
        if (IsCachedKingValid(cachedKing, team))
            return cachedKing;

        for (int x = 0; x < BoardSize; x++)
            for (int y = 0; y < BoardSize; y++)
            {
                ChessPiece piece = pieces[x, y];
                if (piece && piece.Team == team && piece.Type == PieceType.King && !piece.GetComponent<AramDecoyTag>())
                {
                    CacheKing(piece);
                    return piece;
                }
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

        bool strongFortress = aramCoordinator != null && aramCoordinator.AllowsStrongFortressCastle(piece.Team);
        if (!strongFortress && IsTeamInCheck(piece.Team))
            return false;

        int direction = destination.x > from.x ? 1 : -1;
        PieceTeam enemyTeam = piece.Team == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        if (!strongFortress)
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

        bool extendedFortress = aramMode && !serverAuthoritativeMode && aramCoordinator.Runtime.HasBuff(piece ? piece.Team : currentTurn, AramBuffId.StrongFortress);
        if (!piece || piece.Type != PieceType.King || piece.GetComponent<AramDecoyTag>() || (piece.HasMoved && !extendedFortress))
            return false;
        if(extendedFortress&&(from.y!=(piece.ForwardDirection>0?0:7)||
            (destination.x!=2&&destination.x!=6)||!aramCoordinator.Runtime.FortressWingAvailable(piece.Team,destination.x)))return false;

        Vector2Int delta = destination - from;
        if (delta.y != 0 || (extendedFortress ? (delta.x==0 || (destination.x!=2&&destination.x!=6)) : Mathf.Abs(delta.x)!=2))
            return false;

        if (!ChessMoveRules.IsInsideBoard(destination) || pieces[destination.x, destination.y])
            return false;

        int direction = delta.x > 0 ? 1 : -1;
        rookFrom = new Vector2Int(direction > 0 ? 7 : 0, from.y);
        rookTo = new Vector2Int(extendedFortress?(destination.x==6?5:3):from.x+direction, from.y);
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
        lastAramRepetitionMove=-1;
        positionHistory.Clear();
    }

    private void RecordCurrentPosition()
    {
        if(aramMode&&!serverAuthoritativeMode)
        {
            if(lastAramRepetitionMove==statistics.ConfirmedMoveNumber)return;
            lastAramRepetitionMove=statistics.ConfirmedMoveNumber;
        }
        string positionKey = BuildDrawPositionKey();
        if (positionHistory.TryGetValue(positionKey, out int occurrenceCount))
            positionHistory[positionKey] = occurrenceCount + 1;
        else
            positionHistory.Add(positionKey, 1);
    }

    private string BuildDrawPositionKey() => aramMode&&!serverAuthoritativeMode&&aramCoordinator!=null
        ? BuildPositionKey()+" | "+aramCoordinator.Runtime.RepetitionState : BuildPositionKey();

    private string BuildPositionKey()
    {
        if (UsesClassicDomainSession && pendingPromotionPawn == null)
        {
            return ChessButWeird.Domain.FenCodec.RepetitionKey(localClassicCoordinator.Snapshot);
        }

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
        if (UsesClassicDomainSession && pendingPromotionPawn == null)
            return ChessButWeird.Domain.FenCodec.Write(localClassicCoordinator.Snapshot);

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

        if (UsesClassicDomainSession)
        {
            PieceType normalizedPromotion = promotionType == PieceType.King || promotionType == PieceType.Pawn
                ? PieceType.Queen
                : promotionType;
            ChessButWeird.Application.ClassicMoveStatus submission = localClassicCoordinator.Submit(
                new ChessButWeird.Domain.Move(
                    UnityBoardAdapter.ToSquare(pendingCommittedMoveFrom),
                    UnityBoardAdapter.ToSquare(pendingCommittedMoveTo),
                    (ChessButWeird.Domain.PieceKind)normalizedPromotion), out _);
            if (submission != ChessButWeird.Application.ClassicMoveStatus.Applied)
            {
                Debug.LogError("Local classic promotion could not be committed by MatchSession.");
                return;
            }
        }

        ChessPiece originalPawn=pendingPromotionPawn;
        ChessPiece promotedPiece = PromotePawn(pendingPromotionPawn, promotionType);
        if(aramMode&&!serverAuthoritativeMode)aramCoordinator.Runtime.PawnPromoted(originalPawn,promotedPiece);
        PlaySound(promotionSound);
        pendingPromotionPawn = null;
        inputLocked = false;
        lastMoveSummary = AppendPromotionToMoveSummary(lastMoveSummary, promotionType);
        statistics.ReviseLastMove(lastMoveSummary);
        if (moveHistory.Count > 0)
            moveHistory[moveHistory.Count - 1] = lastMoveSummary;

        currentTurn = UsesClassicDomainSession
            ? (PieceTeam)localClassicCoordinator.Turn
            : pendingPromotionOpponentTeam;
        aramCoordinator?.OnTurnStarted(currentTurn);
        NotifyMoveCommitted(new ChessMove(pendingCommittedMoveFrom, pendingCommittedMoveTo, promotionType));
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

        pieceAnimator?.Cancel(pawn);

        GameObject pawnObject = pawn.gameObject;
        ChessPiece promotedPiece = CreatePromotedPieceObject(team, promotionType, boardPosition, forwardDirection);
        if (!promotedPiece)
            promotedPiece = CreateFallbackPromotedPieceObject(pawnObject, team, promotionType, boardPosition, forwardDirection);

        promotedPiece.Initialize(team, boardPosition, forwardDirection);
        promotedPiece.MarkMoved();
        promotedPiece.gameObject.name = $"{team} {promotionType} {boardPosition.x},{boardPosition.y}";
        ApplySkinToPiece(promotedPiece);
        PieceViewGeometry.EnsurePieceCollider(promotedPiece.gameObject);
        MovePieceToTile(promotedPiece, boardPosition, 0f);
        pieces[boardPosition.x, boardPosition.y] = promotedPiece;
        if (UsesClassicDomainSession)
            ReplaceLocalClassicPiece(pawn, promotedPiece);
        lastMovedPiece = promotedPiece;
        lastMoveWasPawnDoubleStep = false;

        DestroyRuntimeObject(pawnObject);
        return promotedPiece;
    }

    private ChessPiece CreatePromotedPieceObject(PieceTeam team, PieceType promotionType, Vector2Int boardPosition, int forwardDirection)
    {
        var piece = CreateCosmeticPiece(promotionType, team, boardPosition);
        piece.Initialize(team, boardPosition, forwardDirection);
        return piece;
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
        return promotedPiece;
    }

    private void ApplySkinToPiece(ChessPiece piece)
    {
        if (piece) PieceSkinPresenter.Apply(piece, GetPieceSkinId(piece.Team));
    }

    private void ApplyPieceSkinsToRuntimePieces()
    {
        if (!runtimePiecesRoot)
            return;

        ChessPiece[] scenePieces = runtimePiecesRoot.GetComponentsInChildren<ChessPiece>(true);
        for (int i = 0; i < scenePieces.Length; i++)
            ApplySkinToPiece(scenePieces[i]);
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

    private void CreateDefaultPieces()
    {
        // LoadingManager prepares the selected set (Tazji by default) before gameplay.
        PieceType[] backRank = { PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen, PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook };
        foreach (PieceTeam team in new[] { PieceTeam.White, PieceTeam.Black })
            for (int file = 0; file < 8; file++)
            {
                CreateCosmeticPiece(backRank[file], team, new Vector2Int(file, team == PieceTeam.White ? 0 : 7));
                CreateCosmeticPiece(PieceType.Pawn, team, new Vector2Int(file, team == PieceTeam.White ? 1 : 6));
            }
    }

    private ChessPiece CreateCosmeticPiece(PieceType type, PieceTeam team, Vector2Int position)
    {
        var root = new GameObject($"{team} {type} {position.x},{position.y}");
        root.transform.SetParent(runtimePiecesRoot, false);
        // A shared primitive provides stable fitting/collider bounds and offline fallback.
        float tile = Vector3.Distance(chessboard.GetTileCenterWorld(Vector2Int.zero), chessboard.GetTileCenterWorld(Vector2Int.right));
        var fallback = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fallback.name = "Default fitting visual";
        fallback.transform.SetParent(root.transform, false);
        float height = tile * (type == PieceType.Pawn ? .65f : .95f);
        fallback.transform.localScale = new Vector3(tile * .55f, height / 2, tile * .55f);
        fallback.transform.localPosition = Vector3.up * height / 2;
        Destroy(fallback.GetComponent<Collider>());
        var block = new MaterialPropertyBlock();
        Color color = team == PieceTeam.White ? new Color(.94f, .9f, .8f) : new Color(.17f, .19f, .24f);
        block.SetColor("_BaseColor", color); block.SetColor("_Color", color);
        fallback.GetComponent<Renderer>().SetPropertyBlock(block);
        ChessPiece piece = AddPieceComponent(root, type);
        piece.Initialize(team, position, team == PieceTeam.White ? 1 : -1);
        ApplySkinToPiece(piece);
        PieceViewGeometry.EnsurePieceCollider(root);
        MovePieceToTile(piece, position, 0);
        return piece;
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

        ChessPiece[] scenePieces = runtimePiecesRoot.GetComponentsInChildren<ChessPiece>(true);
        for (int i = 0; i < scenePieces.Length; i++)
        {
            ChessPiece piece = scenePieces[i];
            if (!piece)
                continue;

            PieceSkinVisualState skinState = piece.GetComponent<PieceSkinVisualState>();
            if (skinState)
            {
                skinState.SetRuntimeVisible(visible);
                continue;
            }

            Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                renderers[rendererIndex].enabled = visible;
        }

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
        targetPosition.y += (UserSettings.Enabled("selected_piece") ? liftHeight : 0f) + GetPieceBottomOffset(piece);
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
        Animator.Play(piece, targetPosition, duration, arcHeight);
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

    private void BeginMatchEnding()
    {
        if (matchEnding) return;
        matchEnding = true;
        MatchEnding?.Invoke();
    }

    private void FinishGame(PieceTeam winner)
    {
        if (gameOver)
            return;

        BeginMatchEnding();
        StopCheckWarning();
        status = ChessGameStatus.Win;
        winningTeam = winner;
        frozenMatchDurationSeconds = MatchElapsedSeconds;
        bool won = winner == playerTeam;
        bool lost = winner != playerTeam;
        MatchReward reward = CalculateMatchReward(won, lost, false);
        if (IsHotseatGame) matchResultRecorder.TryRecord(() => PlayerAuthService.RecordHotseatResult(
            GetProfileMatchMode(), winner + " wins", winner + " won", matchResultRecorder.MatchId));
        else if (botMode) matchResultRecorder.TryRecord(() => PlayerAuthService.RecordBotResult(
            botDifficulty, botOptions.Mode, won ? "Win" : "Lose", winner + " won", matchResultRecorder.MatchId));
        else if (dotNetState == null) matchResultRecorder.TryRecord(() => PlayerAuthService.RecordGameResult(
                won,
                lost,
                false,
                GetProfileMatchMode(),
                GetProfileOpponentName(),
                MatchRewardPolicy.AppendRewardDetail($"{winner} won", reward),
                reward.gold,
                reward.diamonds,
                reward.tickets,
                matchResultRecorder.MatchId));
        PlayResultSoundIfDefaultPack(IsHotseatGame || won ? ResultMenuView.ResultKind.Win : ResultMenuView.ResultKind.Lose);
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
        if (gameOver)
            return;

        if(aramMode&&!serverAuthoritativeMode&&aramCoordinator!=null)
        {
            bool whiteLoses=aramCoordinator.Runtime.DrawIsLoss(PieceTeam.White),blackLoses=aramCoordinator.Runtime.DrawIsLoss(PieceTeam.Black);
            if(whiteLoses!=blackLoses&&!AramLostRoyalCondition(whiteLoses?PieceTeam.Black:PieceTeam.White))
            {FinishGame(whiteLoses?PieceTeam.Black:PieceTeam.White);return;}
        }
        BeginMatchEnding();
        StopCheckWarning();
        status = ChessGameStatus.Draw;
        frozenMatchDurationSeconds = MatchElapsedSeconds;
        MatchReward reward = CalculateMatchReward(false, false, true);
        if (IsHotseatGame) matchResultRecorder.TryRecord(() => PlayerAuthService.RecordHotseatResult(
            GetProfileMatchMode(), "Draw", reason, matchResultRecorder.MatchId));
        else if (botMode) matchResultRecorder.TryRecord(() => PlayerAuthService.RecordBotResult(
            botDifficulty, botOptions.Mode, "Draw", reason, matchResultRecorder.MatchId));
        else if (dotNetState == null) matchResultRecorder.TryRecord(() => PlayerAuthService.RecordGameResult(
                false,
                false,
                true,
                GetProfileMatchMode(),
                GetProfileOpponentName(),
                MatchRewardPolicy.AppendRewardDetail(reason, reward),
                reward.gold,
                reward.diamonds,
                reward.tickets,
                matchResultRecorder.MatchId));
        PlayResultSoundIfDefaultPack(ResultMenuView.ResultKind.Draw);
        drawReason = reason;
        selectedPiece = null;
        gameStarted = false;
        gameOver = true;
        inputLocked = false;
        chessboard?.ClearLegalMoveHighlights();
        RefreshLocalInteractionState();
        turnSelectionUI?.ShowDraw(playerTeam, drawReason);
    }

    private string GetProfileMatchMode()
    {
        if (aramMode)
            return "ARAM";
        if (botMode)
            return IsBotPractice ? "Bot Practice" : "Bot Challenge";
        if (serverAuthoritativeMode)
            return "Online";
        if (restrictInputToControlledTeam)
            return "Multiplayer";
        return "Local";
    }

    private string GetProfileOpponentName()
    {
        if (botMode)
            return StockfishDifficultyProfiles.Get(botDifficulty).BotName + " / " + StockfishDifficultyProfiles.Get(botDifficulty).DisplayName;
        return "Player";
    }

    private void PlayResultSoundIfDefaultPack(ResultMenuView.ResultKind resultKind)
    {
        if (GameMusicManager.ActivePack != GameMusicPack.Default)
            return;

        switch (resultKind)
        {
            case ResultMenuView.ResultKind.Win:
            case ResultMenuView.ResultKind.Lose:
                PlaySound(winSound);
                break;
        }
    }

    private MatchReward CalculateMatchReward(bool won, bool lost, bool draw)
    {
        // Bot rewards are calculated with persisted first-win and daily quota data when recording the result.
        if (botMode) return MatchReward.None;

        if (serverAuthoritativeMode || restrictInputToControlledTeam)
            return MatchRewardPolicy.CalculateNetworkReward(won, lost, draw);

        return MatchRewardPolicy.CalculateLocalReward(won, lost, draw);
    }

    public bool ApplyBotMove(ChessMove move)
    {
        if (!botMode || serverAuthoritativeMode)
            return false;

        return ApplyControlledOpponentMove(move);
    }

    /// <summary>
    /// Binds a server match identity to the current game lifecycle so repeated
    /// terminal messages cannot create a second profile/history write.
    /// </summary>
    public void SetActiveMatchIdentity(string matchId)
    {
        if (!string.IsNullOrWhiteSpace(matchId))
            matchResultRecorder.Begin(matchId);
    }

    public bool ApplyBotMove(ChessButWeird.Domain.Move move)
    {
        if (!botMode || serverAuthoritativeMode || !move.From.IsValid || !move.To.IsValid)
            return false;

        Vector2Int from = new Vector2Int(move.From.File, move.From.Rank);
        Vector2Int to = new Vector2Int(move.To.File, move.To.Rank);
        if (!move.Promotion.HasValue)
            return ApplyControlledOpponentMove(new ChessMove(from, to));

        PieceType promotion = (PieceType)move.Promotion.Value;
        if (promotion == PieceType.King || promotion == PieceType.Pawn ||
            !Enum.IsDefined(typeof(PieceType), promotion))
            return false;

        return ApplyControlledOpponentMove(new ChessMove(from, to, promotion));
    }

    private bool ApplyControlledOpponentMove(ChessMove move)
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

        bool restoreClassicDomainSession = UsesClassicDomainSession;
        ChessButWeird.Domain.BoardOrientation localClassicOrientation = default;
        ChessButWeird.Domain.MatchState importedLocalClassicState = null;
        if (restoreClassicDomainSession)
        {
            localClassicOrientation = localClassicCoordinator.Orientation;
            if (localClassicOrientation.IsRankFlipped)
            {
                Debug.LogWarning("Local classic FEN import currently requires the canonical-facing board.");
                return false;
            }

            try
            {
                importedLocalClassicState = ChessButWeird.Domain.FenCodec.Parse(fen);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // Imported snapshots intentionally start a fresh ownership boundary. The
        // network/ARAM callers rebuild their adapter state immediately afterwards.
        ClearLocalClassicSession();
        StopCheckWarning();
        StopAllCoroutines();
        pieceAnimator?.CancelAll();
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
                CacheKing(piece);
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
        // FEN describes a position, not capture history (promotion and ARAM can change material).

        gameStarted = true;
        gameOver = false;
        matchEnding = false;
        inputLocked = false;
        status = ChessGameStatus.Playing;
        SetRuntimePiecesVisible(true);
        chessboard?.SetPresentationVisible(true);
        if (restoreClassicDomainSession)
        {
            localClassicCoordinator = ChessButWeird.Application.ClassicMatchCoordinator.FromSnapshot(
                importedLocalClassicState, localClassicOrientation);
            RebindLocalClassicPieces();
        }
        ResetBotHistory();
        turnSelectionUI?.SetTurn(currentTurn);
        RefreshLocalInteractionState();
        UpdateCheckWarningForCurrentTurn();
        return true;
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

    public List<ChessPiece> GetActivePiecesForAram(PieceTeam team)
    {
        List<ChessPiece> result = new List<ChessPiece>();
        for (int x = 0; x < BoardSize; x++)
            for (int y = 0; y < BoardSize; y++)
            {
                ChessPiece piece = pieces[x, y];
                if (piece && piece.Team == team)
                    result.Add(piece);
            }

        return result;
    }

    public void SetAramInputLocked(bool locked)
    {
        if (!aramMode)
            return;

        inputLocked = locked;
        RefreshLocalInteractionState();
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
        if (replaceLatestHistory) statistics.ReviseLastMove(lastMoveSummary);
        else statistics.AddLocal((ChessButWeird.Domain.Team)(currentTurn == PieceTeam.White ? PieceTeam.Black : PieceTeam.White), lastMoveSummary, false, null);
        if (replaceLatestHistory && moveHistory.Count > 0)
            moveHistory[moveHistory.Count - 1] = lastMoveSummary;
        else
            moveHistory.Add(lastMoveSummary);
    }

    private List<PieceType> GetCapturedPieceList(PieceTeam capturingTeam)
    {
        return capturingTeam == PieceTeam.White ? whiteCapturedPieces : blackCapturedPieces;
    }

    private ChessPiece GetPieceAt(Vector2Int position)
    {
        if (!ChessMoveRules.IsInsideBoard(position))
            return null;

        return pieces[position.x, position.y];
    }

    private void NotifyMoveCommitted(ChessMove move)
    {
        RecordBotMove(move);
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
        bool shouldEnableInteraction = gameStarted && !contentLoading &&
            !gameOver &&
            !inputLocked &&
            !pauseLocked &&
            pendingPromotionPawn == null &&
            CanLocalPlayerInteract();

        chessboard?.SetInteractionEnabled(shouldEnableInteraction);
    }

    private void ClearPieceMap()
    {
        whiteKing = null;
        blackKing = null;

        for (int x = 0; x < BoardSize; x++)
            for (int y = 0; y < BoardSize; y++)
                pieces[x, y] = null;
    }

    private void CacheKing(ChessPiece piece)
    {
        if (!piece || piece.Type != PieceType.King)
            return;

        if (piece.Team == PieceTeam.White)
            whiteKing = piece;
        else
            blackKing = piece;
    }

    private bool IsCachedKingValid(ChessPiece king, PieceTeam team)
    {
        return king &&
            king.Team == team &&
            king.Type == PieceType.King &&
            ChessMoveRules.IsInsideBoard(king.BoardPosition) &&
            pieces[king.BoardPosition.x, king.BoardPosition.y] == king;
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
        var piece = CreateCosmeticPiece(pieceType, team, boardPosition);
        piece.Initialize(team, boardPosition, forwardDirection);
        return piece;
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


}
