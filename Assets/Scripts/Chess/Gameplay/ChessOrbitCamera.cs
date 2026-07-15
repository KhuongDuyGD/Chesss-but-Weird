using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class ChessOrbitCamera : MonoBehaviour
{
    [Header("Orbit")]
    [SerializeField] private float rotationSpeed = 0.22f;
    [SerializeField] private float zoomSpeed = 9f;
    [SerializeField] private float minDistance = 2.35f;
    [SerializeField] private float maxDistance = 28f;
    [SerializeField] private float minPitch = 15f;
    [SerializeField] private float maxPitch = 78f;

    [Header("Smoothing")]
    [SerializeField] private float pivotSmoothTime = 0.12f;
    [SerializeField] private float rotationSmoothTime = 0.08f;
    [SerializeField] private float distanceSmoothTime = 0.10f;
    [SerializeField] private float pivotSafeHeight = 0.9f;
    [SerializeField] private bool allowPieceLock = true;
    [SerializeField] private float gameplayDistance = 6.25f;
    [SerializeField] private float gameplayPitch = 42f;

    private ChessGame chessGame;
    private Chessboard chessboard;
    private Transform lockedTarget;
    private ChessPiece lockedPiece;
    private Vector3 boardCenter;
    private Vector3 currentPivot;
    private Vector3 pivotVelocity;
    private float yaw;
    private float targetYaw;
    private float yawVelocity;
    private float pitch;
    private float targetPitch;
    private float pitchVelocity;
    private float currentDistance;
    private float targetDistance;
    private float distanceVelocity;
    private float lockedHeightOffset;
    private float baseYaw;
    private float basePitch;
    private float baseDistance;
    private Vector3 freePivot;
    private bool hasFreePivot;
    private bool initialized;

    public Vector3 CurrentPivot => currentPivot;
    public Vector3 BoardCenter => boardCenter;
    public Transform LockedTarget => lockedTarget;

    private void Awake()
    {
        CacheReferences();
        InitializeFromCurrentTransform();
    }

    private void OnEnable()
    {
        TrySubscribeToGame();
    }

    private void OnDisable()
    {
        UnsubscribeFromPiece();
        UnsubscribeFromGame();
    }

    private void LateUpdate()
    {
        CacheReferences();
        if (!initialized)
            InitializeFromCurrentTransform();

        // Neu quan dang duoc khoa bi destroy, tra pivot ve tam ban de tranh loi null.
        if (lockedTarget == null && (lockedPiece != null || lockedHeightOffset > 0f))
            ReleaseLock();

        HandleOrbitInput();
        HandleZoomInput();

        boardCenter = ResolveBoardCenter();
        Vector3 targetPivot = ResolveTargetPivot();

        // Smooth pivot giup thao tac chon quan lien tiep nhanh khong lam goc nhin bi giat.
        currentPivot = Vector3.SmoothDamp(currentPivot, targetPivot, ref pivotVelocity, Mathf.Max(0.01f, pivotSmoothTime));
        yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, Mathf.Max(0.01f, rotationSmoothTime));
        pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, Mathf.Max(0.01f, rotationSmoothTime));
        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, Mathf.Max(0.01f, distanceSmoothTime));

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = currentPivot - rotation * Vector3.forward * currentDistance;

        float boardSurfaceY = chessboard ? chessboard.GetBoardSurfaceY() : boardCenter.y;
        float minCameraY = boardSurfaceY + pivotSafeHeight;
        if (desiredPosition.y < minCameraY)
            desiredPosition.y = minCameraY;

        transform.SetPositionAndRotation(desiredPosition, rotation);
    }

    public void LockOnPiece(Transform piece, float heightOffset = 0.9f)
    {
        if (!allowPieceLock || piece == null)
            return;

        if (lockedTarget == piece && Mathf.Approximately(lockedHeightOffset, heightOffset))
            return;

        UnsubscribeFromPiece();
        lockedTarget = piece;
        lockedHeightOffset = heightOffset;
        lockedPiece = piece.GetComponent<ChessPiece>();
        if (lockedPiece != null)
            lockedPiece.Destroyed += HandleLockedPieceDestroyed;
    }

    public void ReleaseLock()
    {
        ReleaseLock(true);
    }

    private void ReleaseLock(bool preserveCurrentPivot)
    {
        UnsubscribeFromPiece();
        lockedTarget = null;
        lockedHeightOffset = 0f;

        if (preserveCurrentPivot && initialized)
        {
            freePivot = currentPivot;
            hasFreePivot = true;
        }
    }

    public void SetAllowPieceLock(bool allowed)
    {
        allowPieceLock = allowed;
        if (!allowPieceLock)
            ReleaseLock(false);
    }

    public void ResetToBoardView(bool immediate = false)
    {
        ReleaseLock(false);
        hasFreePivot = false;
        targetYaw = baseYaw;
        targetPitch = basePitch;
        targetDistance = baseDistance;

        if (immediate)
            SnapRotationAndDistance();
    }

    public void ConfigureForPlayerSide(PieceTeam playerTeam, bool immediate = false)
    {
        ReleaseLock(false);
        hasFreePivot = false;
        targetYaw = playerTeam == PieceTeam.Black ? baseYaw + 180f : baseYaw;
        targetPitch = Mathf.Clamp(gameplayPitch, minPitch, maxPitch);
        targetDistance = Mathf.Clamp(gameplayDistance, minDistance, maxDistance);

        if (immediate)
            SnapRotationAndDistance();
    }

    private void CacheReferences()
    {
        if (!chessGame)
            chessGame = FindAnyObjectByType<ChessGame>();
        if (!chessboard)
            chessboard = FindAnyObjectByType<Chessboard>();
        if (chessGame)
            TrySubscribeToGame();
    }

    private void InitializeFromCurrentTransform()
    {
        boardCenter = ResolveBoardCenter();
        currentPivot = boardCenter;

        Vector3 pivotToCamera = transform.position - currentPivot;
        currentDistance = Mathf.Clamp(pivotToCamera.magnitude, minDistance, maxDistance);
        targetDistance = currentDistance;

        Vector3 planarDirection = Vector3.ProjectOnPlane(-pivotToCamera.normalized, Vector3.up);
        if (planarDirection.sqrMagnitude < 0.0001f)
            planarDirection = Vector3.forward;

        yaw = Mathf.Atan2(planarDirection.x, planarDirection.z) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(NormalizePitch(transform.eulerAngles.x), minPitch, maxPitch);
        targetYaw = yaw;
        targetPitch = pitch;
        baseYaw = yaw;
        basePitch = pitch;
        baseDistance = currentDistance;
        initialized = true;
    }

    private void TrySubscribeToGame()
    {
        if (!chessGame)
            return;

        chessGame.CameraLockRequested -= HandleCameraLockRequested;
        chessGame.CameraLockReleased -= HandleCameraLockReleased;
        chessGame.CameraLockRequested += HandleCameraLockRequested;
        chessGame.CameraLockReleased += HandleCameraLockReleased;
    }

    private void UnsubscribeFromGame()
    {
        if (!chessGame)
            return;

        chessGame.CameraLockRequested -= HandleCameraLockRequested;
        chessGame.CameraLockReleased -= HandleCameraLockReleased;
    }

    private void UnsubscribeFromPiece()
    {
        if (lockedPiece != null)
            lockedPiece.Destroyed -= HandleLockedPieceDestroyed;

        lockedPiece = null;
    }

    private void HandleOrbitInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.isPressed)
            return;

        Vector2 delta = mouse.delta.ReadValue();
        targetYaw += delta.x * rotationSpeed;
        targetPitch = Mathf.Clamp(targetPitch - delta.y * rotationSpeed, minPitch, maxPitch);
    }

    private void HandleZoomInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        float scrollValue = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scrollValue) < 0.001f)
            return;

        targetDistance = Mathf.Clamp(targetDistance - scrollValue * zoomSpeed * 0.01f, minDistance, maxDistance);
    }

    private Vector3 ResolveBoardCenter()
    {
        return chessboard ? chessboard.GetBoardCenterWorld() : Vector3.zero;
    }

    private Vector3 ResolveTargetPivot()
    {
        if (allowPieceLock && lockedTarget != null)
            return lockedTarget.position + Vector3.up * lockedHeightOffset;

        if (hasFreePivot)
            return freePivot;

        return boardCenter;
    }

    private void HandleCameraLockRequested(Transform pieceTransform, float heightOffset)
    {
        LockOnPiece(pieceTransform, heightOffset);
    }

    private void HandleCameraLockReleased()
    {
        ReleaseLock();
    }

    private void HandleLockedPieceDestroyed(ChessPiece _)
    {
        ReleaseLock();
    }

    private void SnapRotationAndDistance()
    {
        yaw = targetYaw;
        pitch = targetPitch;
        currentDistance = targetDistance;
        yawVelocity = 0f;
        pitchVelocity = 0f;
        distanceVelocity = 0f;
    }

    private static float NormalizePitch(float rawPitch)
    {
        return rawPitch > 180f ? rawPitch - 360f : rawPitch;
    }
}
