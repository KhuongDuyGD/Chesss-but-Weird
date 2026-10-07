using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Rendering;

// Retain the component name and script GUID used by existing scenes.
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class ChessOrbitCamera : MonoBehaviour
{
    [Header("Board Orbit")]
    [SerializeField, Range(45f, 85f)] private float boardViewPitch = 50f;
    [SerializeField, Min(1f)] private float framePadding = 1.12f;
    [SerializeField, Min(0f)] private float pieceHeadroomInTiles = 1.5f;
    [SerializeField, Min(.01f)] private float orbitSensitivity = .14f;
    private float orbitYaw, orbitPitch;
    private bool orbitDragging, orbitUnlocked;

    [Header("Mouse Wheel Zoom")]
    [SerializeField, Range(.25f, 1f)] private float minimumZoom = .55f;
    [SerializeField, Min(1f)] private float maximumZoom = 1.65f;
    [SerializeField, Min(.01f)] private float zoomStep = .14f;
    [SerializeField, Min(.01f)] private float zoomSmoothTime = .08f;

    private Camera viewCamera;
    private Chessboard chessboard;
    private ChessGame chessGame;
    private Vector3 boardCenter;
    private Vector3 currentPivot;
    private PieceTeam boardViewTeam = PieceTeam.White;
    private float currentZoom = 1f, targetZoom = 1f, zoomVelocity;

    public Vector3 CurrentPivot => currentPivot;
    public Vector3 BoardCenter => boardCenter;
    public bool IsOrbitUnlocked => orbitUnlocked;
    public bool IsBoardViewActive => isActiveAndEnabled && viewCamera && viewCamera.enabled;

    private void OnEnable()
    {
        viewCamera = GetComponent<Camera>();
        RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
        ApplyParallelProjection();
        FrameBoard();
    }
    private void OnDisable()
    {
        orbitDragging = false;
        RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
        // Release our explicit matrix so the separate rifle view can use perspective.
        if (viewCamera) viewCamera.ResetProjectionMatrix();
    }
    private void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == viewCamera && isActiveAndEnabled) ApplyParallelProjection();
    }
    private void OnPreCull()
    {
        if (GraphicsSettings.currentRenderPipeline == null) ApplyParallelProjection();
    }
    private void ApplyParallelProjection()
    {
        if (!viewCamera) return;
        viewCamera.orthographic = true;
        viewCamera.usePhysicalProperties = false;
        viewCamera.lensShift = Vector2.zero;
        float halfHeight = Mathf.Max(.01f, viewCamera.orthographicSize);
        float halfWidth = halfHeight * Mathf.Max(.1f, viewCamera.aspect);
        // The homogeneous W stays 1: depth cannot shrink distant rows or converge parallels.
        var projection = Matrix4x4.Ortho(-halfWidth, halfWidth, -halfHeight, halfHeight,
            viewCamera.nearClipPlane, viewCamera.farClipPlane);
        viewCamera.projectionMatrix = projection;
        viewCamera.nonJitteredProjectionMatrix = projection;
    }
    private void LateUpdate()
    {
        HandleZoomInput();
        currentZoom = Mathf.SmoothDamp(currentZoom, targetZoom, ref zoomVelocity,
            Mathf.Max(.01f, zoomSmoothTime), Mathf.Infinity, Time.unscaledDeltaTime);
        FrameBoard();
    }

    public void ResetToBoardView(bool immediate = false)
    {
        boardViewTeam = PieceTeam.White;
        ResetZoom(immediate);
    }

    private void ResetZoom(bool immediate)
    {
        orbitYaw = orbitPitch = 0f;
        orbitDragging = false;
        orbitUnlocked = false;
        targetZoom = 1f;
        if (immediate) { currentZoom = 1f; zoomVelocity = 0f; }
        FrameBoard();
    }

    // Start from the assigned player's side. Turns never reset the user's orbit.
    public void ConfigureForPlayerSide(PieceTeam playerTeam, bool immediate = false)
    {
        boardViewTeam = playerTeam;
        ResetZoom(immediate);
    }

    private void HandleZoomInput()
    {
        if (!viewCamera || !viewCamera.enabled || !chessGame || !chessGame.GameStarted ||
            chessGame.PauseLocked || !Application.isFocused)
        { orbitDragging = false; return; }
        // Y toggles orbit; locking returns to the original assigned-side view.
        var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        bool typing = selected &&
            ((selected.GetComponentInParent<TMP_InputField>() is TMP_InputField tmp && tmp.isFocused) ||
             (selected.GetComponentInParent<InputField>() is InputField input && input.isFocused));
        if (typing) { orbitDragging = false; return; }
        if (UserSettings.KeyPressed("key_orbit"))
        {
            orbitUnlocked = !orbitUnlocked;
            orbitDragging = false;
            if (!orbitUnlocked) orbitYaw = orbitPitch = 0f;
        }
        if (Mouse.current == null) return;
        // Let scrollable menus and the match journal handle their own wheel input.
        var mouse = Mouse.current;
        bool overUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
        overUi |= chessGame.OnlinePointerBlocked?.Invoke(mouse.position.ReadValue()) == true;
        bool right = UserSettings.Enabled("right_orbit"), middle = UserSettings.Enabled("middle_orbit");
        if ((right && mouse.rightButton.wasPressedThisFrame) || (middle && mouse.middleButton.wasPressedThisFrame))
            orbitDragging = orbitUnlocked && !overUi;
        if (!(right && mouse.rightButton.isPressed) && !(middle && mouse.middleButton.isPressed)) orbitDragging = false;
        if (orbitUnlocked && orbitDragging && !overUi)
        {
            Vector2 delta = mouse.delta.ReadValue() * (UserSettings.Get("orbit_sensitivity") / 100f);
            orbitYaw = Mathf.Repeat(orbitYaw + delta.x * orbitSensitivity, 360f);
            orbitPitch = Mathf.Clamp(orbitPitch - delta.y * orbitSensitivity, 25f - boardViewPitch, 85f - boardViewPitch);
        }
        if (overUi || !UserSettings.Enabled("wheel_zoom")) return;
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < .001f) return;
        // Windows reports 120 units per wheel notch; normalized devices report 1.
        float steps = Mathf.Abs(scroll) <= 1f ? scroll : scroll / 120f;
        targetZoom = Mathf.Clamp(targetZoom * Mathf.Exp(-steps * Mathf.Max(.01f, zoomStep)),
            Mathf.Clamp(minimumZoom, .25f, 1f), Mathf.Max(1f, maximumZoom));
    }

    private void FrameBoard()
    {
        if (!viewCamera) viewCamera = GetComponent<Camera>();
        if (!chessboard) chessboard = FindAnyObjectByType<Chessboard>();
        if (!chessGame) chessGame = FindAnyObjectByType<ChessGame>();
        if (!viewCamera || !viewCamera.enabled || !chessboard) return;

        boardCenter = chessboard.GetBoardCenterWorld();
        Vector3 origin = chessboard.GetTileCenterWorld(Vector2Int.zero);
        Vector3 tileRight = chessboard.GetTileCenterWorld(Vector2Int.right) - origin;
        Vector3 tileForward = chessboard.GetTileCenterWorld(Vector2Int.up) - origin;
        Vector3 boardUp = Vector3.Cross(tileForward, tileRight).normalized;
        if (boardUp.sqrMagnitude < .001f) return;

        float tileScale = Mathf.Max(tileRight.magnitude, tileForward.magnitude);
        // Include both prisoner rails in the default board framing.
        Vector3 halfRight = tileRight * 5.6f;
        Vector3 halfForward = tileForward * 4f;
        Vector3 halfHeight = boardUp * tileScale * Mathf.Max(0f, pieceHeadroomInTiles) * .5f;
        currentPivot = boardCenter + halfHeight;
        Vector3 homeForward = boardViewTeam == PieceTeam.Black ? -tileForward.normalized : tileForward.normalized;
        Vector3 viewForward = Quaternion.AngleAxis(orbitYaw, boardUp) * homeForward;
        Quaternion rotation = Quaternion.LookRotation(viewForward, boardUp) *
            Quaternion.Euler(Mathf.Clamp(boardViewPitch + orbitPitch, 25f, 85f), 0f, 0f);
        Quaternion inverseRotation = Quaternion.Inverse(rotation);
        // Fit from the locked view, so orbit does not automatically zoom in/out.
        Quaternion framingRotation = Quaternion.LookRotation(homeForward, boardUp) *
            Quaternion.Euler(Mathf.Clamp(boardViewPitch, 45f, 85f), 0f, 0f);
        Quaternion inverseFraming = Quaternion.Inverse(framingRotation);
        float halfWidth = 0f, halfViewHeight = 0f, halfDepth = 0f;
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                for (int y = -1; y <= 1; y += 2)
                {
                    Vector3 offset = halfRight * x + halfForward * z + halfHeight * y;
                    Vector3 projected = inverseFraming * offset;
                    halfWidth = Mathf.Max(halfWidth, Mathf.Abs(projected.x));
                    halfViewHeight = Mathf.Max(halfViewHeight, Mathf.Abs(projected.y));
                    halfDepth = Mathf.Max(halfDepth, Mathf.Abs((inverseRotation * offset).z));
                }

        // Leave space for the existing match HUD without letting its FOV changes
        // alter this orthographic view. Narrow screens fit the full board horizontally.
        float hudFraction = chessGame && chessGame.GameStarted ? (chessGame.IsAramGame || chessGame.UsesDotNetOnline ? .76f : .88f) : .94f;
        float safeWidth = 1f, safeHeight = 1f;
        if (Screen.width > 0 && Screen.height > 0 && Screen.safeArea.width > 0 && Screen.safeArea.height > 0)
        {
            safeWidth = Mathf.Clamp(Screen.safeArea.width / Screen.width, .1f, 1f);
            safeHeight = Mathf.Clamp(Screen.safeArea.height / Screen.height, .1f, 1f);
        }
        float aspect = Mathf.Max(.1f, viewCamera.aspect);
        viewCamera.orthographic = true;
        viewCamera.orthographicSize = Mathf.Max(.01f,
            Mathf.Max(halfViewHeight / safeHeight, halfWidth / (aspect * safeWidth)) *
            Mathf.Max(1f, framePadding) * currentZoom / hudFraction);
        float distance = Mathf.Max(tileScale * 10f, halfDepth + viewCamera.nearClipPlane + tileScale * 2f);
        viewCamera.farClipPlane = Mathf.Max(viewCamera.farClipPlane, distance + halfDepth + tileScale * 2f);
        transform.SetPositionAndRotation(currentPivot - rotation * Vector3.forward * distance, rotation);
        ApplyParallelProjection();
    }
}
