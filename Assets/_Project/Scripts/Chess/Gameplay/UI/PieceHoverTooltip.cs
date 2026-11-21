using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Read-only piece names; works on either player's turn and never intercepts clicks.</summary>
[DisallowMultipleComponent]
public sealed class PieceHoverTooltip : MonoBehaviour
{
    private ChessGame game;
    private GameObject canvasObject;
    private RectTransform root, panel;
    private TextMeshProUGUI label;
    private ChessPiece hovered;
    private PieceType shownKind;
    private PieceTeam shownTeam;
    private float hoverSince;
    private float appliedTextScale;

    public void Initialize(ChessGame source)
    {
        game = source;
        if (!canvasObject)
        {
            canvasObject = new GameObject("Piece names canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(game.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            canvasObject.AddComponent<SettingsUiScale>();
            root = canvasObject.GetComponent<RectTransform>();
            panel = MatchHudStyle.Rect(root, "Piece name", Vector2.one * .5f, Vector2.one * .5f, new Vector2(180, 44), Vector2.zero);
            MatchHudStyle.Surface(panel, false);
            label = MatchHudStyle.Text(panel, "Name", "", 22);
            label.alignment = TextAlignmentOptions.Center;
            // No GraphicRaycaster: hovering the name cannot block selection or movement.
        }
        Hide();
    }

    private void LateUpdate()
    {
        var mouse = Mouse.current;
        if (!game || !game.GameStarted || game.PauseLocked || mouse == null ||
            mouse.rightButton.isPressed || mouse.middleButton.isPressed ||
            (EventSystem.current && EventSystem.current.IsPointerOverGameObject()))
        { Hide(); return; }
        Vector2 pointer = mouse.position.ReadValue();
        var camera = game.PieceHoverCamera;
        if (!camera || !camera.enabled || !camera.pixelRect.Contains(pointer) ||
            game.OnlinePointerBlocked?.Invoke(pointer) == true)
        { Hide(); return; }
        var piece = game.PickPieceForHover(camera.ScreenPointToRay(pointer), Mathf.Max(100f, camera.farClipPlane));
        if (!piece) { Hide(); return; }
        bool changed = hovered != piece || shownKind != piece.Type || shownTeam != piece.Team;
        if(changed)hoverSince=Time.unscaledTime;
        if (changed || !Mathf.Approximately(appliedTextScale,TooltipPreferences.TextScale))
        {
            hovered = piece; shownKind = piece.Type; shownTeam = piece.Team;
            appliedTextScale=TooltipPreferences.TextScale;label.fontSize=22*appliedTextScale;
            bool prisoner = !game.Board.IsValidTile(piece.BoardPosition);
            label.text = piece.Team + " " + piece.Type + (prisoner ? " · Prisoner" : "");
            float width = Mathf.Clamp(label.GetPreferredValues(label.text).x + 32f, 140f, Mathf.Max(140f, root.rect.width - 24f));
            panel.sizeDelta = new Vector2(width, 44*appliedTextScale);
        }
        if(Time.unscaledTime-hoverSince<UserSettings.Get("tooltip_delay")){panel.gameObject.SetActive(false);return;}
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointer, null, out var point);
        var safe = Screen.safeArea;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, safe.min, null, out var min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, safe.max, null, out var max);
        Vector2 half = panel.sizeDelta * .5f;
        point += half + Vector2.one * 14f;
        point.x = Mathf.Clamp(point.x, min.x + half.x + 8f, max.x - half.x - 8f);
        point.y = Mathf.Clamp(point.y, min.y + half.y + 8f, max.y - half.y - 8f);
        panel.anchoredPosition = point;
        panel.gameObject.SetActive(true);
    }
    private void Hide() { hovered = null; if (panel) panel.gameObject.SetActive(false); }
    private void OnDisable() => Hide();
    private void OnDestroy() { if (canvasObject) Destroy(canvasObject); }
}
