using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One visual per captured kind; it never participates in the playable board.</summary>
[DisallowMultipleComponent]
public sealed class CapturedPieceDisplay : MonoBehaviour
{
    private static readonly PieceType[] Kinds = { PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen, PieceType.King };
    private ChessGame game;
    private Chessboard board;
    private Transform root;
    private readonly ChessPiece[,] models = new ChessPiece[2, 6];
    private GameObject countCanvas;
    private RectTransform countRoot;
    private readonly TextMeshProUGUI[,] labels = new TextMeshProUGUI[2, 6];
    private readonly int[,] counts = new int[2, 6];
    public void Initialize(ChessGame source, Chessboard chessboard)
    {
        ResetDisplay(); game = source; board = chessboard;
    }
    internal bool TryPickForHover(Ray ray, float maxDistance, out ChessPiece result)
    {
        result = null;
        if (!root || !root.gameObject.activeInHierarchy) return false;
        // Prisoners keep their colliders disabled; renderer bounds provide hover only.
        foreach (var model in models)
        {
            if (!model || !model.gameObject.activeInHierarchy) continue;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.bounds.IntersectRay(ray, out float distance) || distance >= maxDistance) continue;
                maxDistance = distance; result = model;
            }
        }
        return result != null;
    }
    public void ResetDisplay()
    {
        if (root) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
        root = null;
        if (countCanvas) { countCanvas.SetActive(false); Destroy(countCanvas); }
        countCanvas = null; countRoot = null;
        System.Array.Clear(models, 0, models.Length);
        System.Array.Clear(labels, 0, labels.Length);
        System.Array.Clear(counts, 0, counts.Length);
    }
    private void LateUpdate()
    {
        if (!game || !board) return;
        bool visible = game.GameStarted || game.GameOver;
        if (root) root.gameObject.SetActive(visible);
        if (countCanvas) countCanvas.SetActive(visible);
        if (!visible) return;
        for (int side = 0; side < 2; side++) for (int slot = 0; slot < Kinds.Length; slot++)
        {
            var capturer = side == 0 ? PieceTeam.White : PieceTeam.Black;
            int count = game.PrisonerCount(capturer, Kinds[slot]);
            if (count > 0 && !models[side, slot]) Create(side, slot, capturer);
            if (!models[side, slot]) continue;
            models[side, slot].gameObject.SetActive(count > 0);
            labels[side, slot].transform.parent.gameObject.SetActive(count > 0);
            if (counts[side, slot] != count)
            {
                var text = labels[side, slot];
                text.text = "x" + count; counts[side, slot] = count;
                ((RectTransform)text.transform.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                    Mathf.Max(46, text.GetPreferredValues(text.text).x + 12));
            }
        }
    }
    private void Create(int side, int slot, PieceTeam capturer)
    {
        if (!root)
        {
            root = new GameObject("Captured prisoners").transform;
            root.SetParent(game.transform, false);
        }
        Vector3 pad = board.GetPrisonSlotWorld(capturer, slot);
        var victim = capturer == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
        var model = game.CreatePrisonerPiece(Kinds[slot], victim, root, pad);
        models[side, slot] = model;
        EnsureCountCanvas();
        var badge = MatchHudStyle.Rect(countRoot, capturer + " " + Kinds[slot] + " count", Vector2.one * .5f, Vector2.one * .5f, new Vector2(46, 30), Vector2.zero);
        MatchHudStyle.Surface(badge, false);
        var text = MatchHudStyle.Text(badge, "Quantity", "", 22);
        text.rectTransform.sizeDelta = new Vector2(-8, -2);
        text.alignment = TextAlignmentOptions.Center;
        labels[side, slot] = text;
    }
    private void EnsureCountCanvas()
    {
        if (countCanvas) return;
        countCanvas = new GameObject("Prisoner quantity canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        countCanvas.transform.SetParent(game.transform, false);
        var canvas = countCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 17; // Below match panels and menus; quantity badges never block input.
        var scaler = countCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
        countRoot = countCanvas.GetComponent<RectTransform>();
    }
    private void PositionCounts()
    {
        if (!game || !board || !countCanvas || !countCanvas.activeInHierarchy) return;
        var camera = game.PieceHoverCamera;
        if (!camera || !camera.enabled) { countCanvas.SetActive(false); return; }
        float tile = Vector3.Distance(board.GetTileCenterWorld(Vector2Int.zero), board.GetTileCenterWorld(Vector2Int.right));
        var safe = Screen.safeArea;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(countRoot, safe.min, null, out var min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(countRoot, safe.max, null, out var max);
        for (int side = 0; side < 2; side++) for (int slot = 0; slot < Kinds.Length; slot++)
        {
            var text = labels[side, slot];
            if (!text) continue;
            var badge = (RectTransform)text.transform.parent;
            Vector3 screen = camera.WorldToScreenPoint(board.GetPrisonSlotWorld(side == 0 ? PieceTeam.White : PieceTeam.Black, slot) + board.transform.up * tile * .78f);
            bool visible = counts[side, slot] > 0 && models[side, slot] && models[side, slot].gameObject.activeInHierarchy &&
                screen.z > camera.nearClipPlane && camera.pixelRect.Contains(new Vector2(screen.x, screen.y));
            badge.gameObject.SetActive(visible);
            if (!visible) continue;
            // Canvas rendering follows all LateUpdates, including camera orbit/zoom.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(countRoot, screen, null, out var point);
            point += new Vector2(20, 12);
            Vector2 half = badge.sizeDelta * .5f;
            point.x = Mathf.Clamp(point.x, min.x + half.x + 4f, max.x - half.x - 4f);
            point.y = Mathf.Clamp(point.y, min.y + half.y + 4f, max.y - half.y - 4f);
            badge.anchoredPosition = point;
        }
    }
    private void OnEnable() { Canvas.willRenderCanvases += PositionCounts; }
    private void OnDisable() { Canvas.willRenderCanvases -= PositionCounts; if (countCanvas) countCanvas.SetActive(false); }
    private void OnDestroy() { Canvas.willRenderCanvases -= PositionCounts; ResetDisplay(); }
}
