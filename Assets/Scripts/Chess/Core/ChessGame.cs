using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class ChessGame : MonoBehaviour
{
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

    private readonly ChessPiece[,] pieces = new ChessPiece[8, 8];
    private readonly Dictionary<ChessPiece, Coroutine> pieceAnimations = new Dictionary<ChessPiece, Coroutine>();
    private readonly HashSet<ChessPiece> startingPlacementPieces = new HashSet<ChessPiece>();
    private PieceTeam currentTurn = PieceTeam.White;
    private ChessPiece selectedPiece;
    private Transform runtimePiecesRoot;
    private bool gameStarted;
    private bool gameOver;
    private bool inputLocked;
    private PieceTeam winningTeam;
    private PieceTeam playerTeam;
    private ChessPiece checkedKing;
    private Coroutine checkPulseCoroutine;
    private Vector3 checkedKingOriginalScale;
    private ChessPiece lastMovedPiece;
    private Vector2Int lastMoveTo;
    private bool lastMoveWasPawnDoubleStep;
    private PawnPiece pendingPromotionPawn;
    private PieceTeam pendingPromotionOpponentTeam;
    private ChessTurnSelectionUI turnSelectionUI;

    public PieceTeam CurrentTurn => currentTurn;
    public ChessPiece SelectedPiece => selectedPiece;
    public bool GameStarted => gameStarted;
    public bool GameOver => gameOver;
    public PieceTeam WinningTeam => winningTeam;

    private void Awake()
    {
        if (!chessboard)
            chessboard = FindAnyObjectByType<Chessboard>();
    }

    private void Start()
    {
        PrepareGame();
        turnSelectionUI = ChessTurnSelectionUI.Create(this);
    }

    private void Update()
    {
        if (!gameStarted || inputLocked || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
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
        currentTurn = firstTurn;
        playerTeam = firstTurn;
        selectedPiece = null;
        gameOver = false;
        ArrangePiecesForFirstTurn(firstTurn);
        SetRuntimePiecesVisible(true);
        RefreshPieceMap();
        gameStarted = true;
        chessboard?.SetInteractionEnabled(true);
        chessboard?.ClearLegalMoveHighlights();
        turnSelectionUI?.SetTurn(currentTurn);
        UpdateCheckWarningForCurrentTurn();
    }

    public bool TrySelectPiece(ChessPiece piece)
    {
        if (!gameStarted || inputLocked || !piece || piece.Team != currentTurn)
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
        return true;
    }

    public bool TryMoveSelectedPiece(Vector2Int destination)
    {
        if (!gameStarted || gameOver || inputLocked || !selectedPiece || !chessboard || !chessboard.IsValidTile(destination))
            return false;

        Vector2Int from = selectedPiece.BoardPosition;
        if (!IsLegalMoveAfterKingSafety(selectedPiece, from, destination))
            return false;

        ChessPiece movingPiece = selectedPiece;
        ChessPiece capturedPiece = pieces[destination.x, destination.y];
        PieceTeam opponentTeam = movingPiece.Team == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        bool isEnPassant = TryGetEnPassantCapture(movingPiece, from, destination, out ChessPiece enPassantCapturedPiece, out Vector2Int enPassantCapturePosition);
        bool isCastling = TryGetCastlingRookMove(movingPiece, from, destination, out ChessPiece castlingRook, out Vector2Int castlingRookFrom, out Vector2Int castlingRookTo);
        StopCheckWarning();

        if (isEnPassant)
            capturedPiece = enPassantCapturedPiece;

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
        selectedPiece = null;
        chessboard.ClearLegalMoveHighlights();

        if (capturedPiece)
            Destroy(capturedPiece.gameObject);

        if (isCastling)
            AnimatePieceToTile(castlingRook, castlingRookTo, 0f, moveAnimationDuration, moveArcHeight * 0.5f);

        if (movingPiece is PawnPiece promotedPawn && IsPromotionRank(promotedPawn))
        {
            StartCoroutine(AnimateMoveAndPromptPromotion(promotedPawn, destination, opponentTeam));
            return true;
        }

        if (IsCheckmate(opponentTeam))
        {
            StartCoroutine(AnimateMoveAndFinishGame(movingPiece, destination, movingPiece.Team));
            return true;
        }

        currentTurn = currentTurn == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        turnSelectionUI?.SetTurn(currentTurn);
        UpdateCheckWarningForCurrentTurn();
        StartCoroutine(AnimateMoveAndUnlock(movingPiece, destination));
        return true;
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
        chessboard?.SetInteractionEnabled(false);
        turnSelectionUI?.ShowTurnSelection();
    }

    public void RestartToMainMenu()
    {
        PrepareGame();
        turnSelectionUI?.ShowMainMenu();
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
        chessboard?.SetInteractionEnabled(false);

        currentTurn = PieceTeam.White;
        gameStarted = false;
        gameOver = false;
        inputLocked = false;
        playerTeam = PieceTeam.White;
        lastMovedPiece = null;
        lastMoveTo = -Vector2Int.one;
        lastMoveWasPawnDoubleStep = false;
        pendingPromotionPawn = null;
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

    private IEnumerator AnimateMoveAndPromptPromotion(PawnPiece pawn, Vector2Int destination, PieceTeam opponentTeam)
    {
        inputLocked = true;
        chessboard?.SetInteractionEnabled(false);
        AnimatePieceToTile(pawn, destination, 0f, moveAnimationDuration, moveArcHeight);
        yield return new WaitForSeconds(moveAnimationDuration);

        pendingPromotionPawn = pawn;
        pendingPromotionOpponentTeam = opponentTeam;
        turnSelectionUI?.ShowPromotionChoice(pawn.Team, CompletePromotion);
    }

    private void CompletePromotion(PieceType promotionType)
    {
        if (!pendingPromotionPawn)
            return;

        ChessPiece promotedPiece = PromotePawn(pendingPromotionPawn, promotionType);
        pendingPromotionPawn = null;
        inputLocked = false;
        chessboard?.SetInteractionEnabled(true);

        if (IsCheckmate(pendingPromotionOpponentTeam))
        {
            FinishGame(promotedPiece.Team);
            return;
        }

        currentTurn = pendingPromotionOpponentTeam;
        turnSelectionUI?.SetTurn(currentTurn);
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
        AnimatePieceToTile(movingPiece, destination, 0f, moveAnimationDuration, moveArcHeight);
        yield return new WaitForSeconds(moveAnimationDuration);
        inputLocked = false;
    }

    private IEnumerator AnimateMoveAndFinishGame(ChessPiece movingPiece, Vector2Int destination, PieceTeam winner)
    {
        inputLocked = true;
        AnimatePieceToTile(movingPiece, destination, 0f, moveAnimationDuration, moveArcHeight);
        yield return new WaitForSeconds(moveAnimationDuration);
        FinishGame(winner);
    }

    private void FinishGame(PieceTeam winner)
    {
        StopCheckWarning();
        winningTeam = winner;
        selectedPiece = null;
        gameStarted = false;
        gameOver = true;
        inputLocked = false;
        chessboard?.ClearLegalMoveHighlights();
        chessboard?.SetInteractionEnabled(false);
        turnSelectionUI?.ShowGameOver(winningTeam, playerTeam);
    }

    private void ClearPieceMap()
    {
        for (int x = 0; x < pieces.GetLength(0); x++)
            for (int y = 0; y < pieces.GetLength(1); y++)
                pieces[x, y] = null;
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
