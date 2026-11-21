using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UI = SketchbookUI;

/// <summary>Shared promotion choice for local, bot and authoritative online games.</summary>
public sealed class PromotionMenuView : MonoBehaviour
{
    private GameObject canvasRoot;
    private TextMeshProUGUI side;
    private readonly Button[] choices = new Button[4];
    private Action<PieceType> selected;
    public bool IsReady => canvasRoot;

    public void Initialize(ChessGame game)
    {
        canvasRoot = new GameObject("Promotion Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(game ? game.transform : transform, false);
        var canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2200;
        ResponsiveUi.ConfigureCanvasScaler(canvasRoot.GetComponent<CanvasScaler>(), new Vector2(1920, 1080));
        var dim = canvasRoot.AddComponent<Image>(); dim.color = new Color(.10f, .09f, .13f, .62f);
        var frame = MenuDesignFrame.Create(canvasRoot.transform, "Pawn Promotion", new Vector2(1920, 1080));
        var sheet = UI.Card(frame, "Promotion Sheet", new Rect(380, 235, 1160, 610), UI.White, 0, true, 18).rectTransform;
        UI.Tape(sheet, 498, -12, 164, -3);
        side = UI.Text(sheet, "Promotion Side", "WHITE / PAWN PROMOTION", new Rect(40, 29, 1080, 34), 23, UI.Muted, TextAlignmentOptions.Center);
        UI.Text(sheet, "Promotion Title", "A pawn, a new possibility", new Rect(40, 79, 1080, 73), 52, UI.Ink, TextAlignmentOptions.Center);
        UI.Text(sheet, "Promotion Hint", "Your pawn reached the final rank. Choose its new piece.", new Rect(40, 164, 1080, 43), 27, UI.Muted, TextAlignmentOptions.Center);
        PieceType[] types = { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };
        SketchbookDoodle.Shape[] icons = { SketchbookDoodle.Shape.Queen, SketchbookDoodle.Shape.Rook, SketchbookDoodle.Shape.Bishop, SketchbookDoodle.Shape.Knight };
        string[] descriptions = { "Lines & diagonals", "Straight lines", "Diagonals", "L-shaped jumps" };
        for (int i = 0; i < types.Length; i++)
        {
            var type = types[i];
            choices[i] = UI.Button(sheet, "Promote to " + type, "", new Rect(40 + i * 275, 235, 255, 285), i == 0 ? UI.Yellow : UI.Paper,
                () => Choose(type));
            UI.Doodle(choices[i].transform, type + " Icon", new Rect(70, 22, 115, 130), icons[i], UI.Ink);
            UI.Text(choices[i].transform, "Piece Name", type.ToString().ToUpperInvariant(), new Rect(16, 175, 223, 43), 32, UI.Ink, TextAlignmentOptions.Center);
            UI.Text(choices[i].transform, "Movement", descriptions[i], new Rect(14, 231, 227, 34), 23, UI.Muted, TextAlignmentOptions.Center);
        }
        for (int i = 0; i < choices.Length; i++)
            choices[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = choices[(i + 3) % 4], selectOnRight = choices[(i + 1) % 4] };
        UI.Text(sheet, "Promotion Footer", "Select a piece to continue the match.", new Rect(40, 548, 1080, 34), 23, UI.Muted, TextAlignmentOptions.Center);
        if (!EventSystem.current) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Hide();
    }

    public void Show(PieceTeam team, Action<PieceType> onSelected)
    {
        selected = onSelected;
        side.text = team.ToString().ToUpperInvariant() + " / PAWN PROMOTION";
        foreach (var choice in choices) choice.interactable = onSelected != null;
        canvasRoot.SetActive(true);
        choices[0].Select();
    }

    private void Choose(PieceType type)
    {
        if (selected == null || !canvasRoot.activeSelf) return;
        var callback = selected;
        Hide();
        callback(type);
    }

    public void Hide()
    {
        selected = null;
        foreach (var choice in choices) if (choice) choice.interactable = false;
        if (canvasRoot) canvasRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (!canvasRoot) return;
        canvasRoot.SetActive(false);
        if (Application.isPlaying) Destroy(canvasRoot); else DestroyImmediate(canvasRoot);
    }
}
