using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Compact practice buttons sit between the player cards and the journal controls.</summary>
public sealed class BotPracticeView : MonoBehaviour
{
    private ChessGame game;
    private StockfishBotController controller;
    private GameObject canvasObject;
    private RectTransform root, panel;
    private Button hint, undo;
    private TextMeshProUGUI hintLabel;

    public static float ToolbarWidth(ChessGame source)
    {
        if (!source || !source.IsBotPractice) return 0;
        int count = (source.BotOptions.AllowHints ? 1 : 0) + (source.BotOptions.AllowUndo ? 1 : 0);
        return count == 2 ? 236 : count == 1 ? 112 : 0;
    }

    public void Initialize(ChessGame source, StockfishBotController bot)
    {
        game = source; controller = bot;
        canvasObject = new GameObject("Bot Practice Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(game.transform, false);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.GetComponent<Canvas>().sortingOrder = 19;
        ResponsiveUi.ConfigureCanvasScaler(canvasObject.GetComponent<CanvasScaler>(), new Vector2(1280,720));
        root = MatchHudStyle.Rect(canvasObject.transform, "Practice Safe Area", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.gameObject.AddComponent<ResponsiveSafeArea>();
        panel = MatchHudStyle.Rect(root, "Practice tools", Vector2.one, Vector2.one, new Vector2(236,38), new Vector2(-480,-30));
        hint = MatchHudStyle.Button(panel, "Practice hint", "Hint", controller.RequestHint);
        undo = MatchHudStyle.Button(panel, "Practice undo", "Undo", () => game.UndoBotPractice());
        MatchHudStyle.Place((RectTransform)hint.transform, new Vector2(0,.5f), new Vector2(112,38), new Vector2(56,0));
        MatchHudStyle.Place((RectTransform)undo.transform, new Vector2(1,.5f), new Vector2(112,38), new Vector2(-56,0));
        foreach (var button in new[]{hint,undo})
        {
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.rectTransform.sizeDelta = new Vector2(-12,-4);
            label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 20;
        }
        hintLabel = hint.GetComponentInChildren<TextMeshProUGUI>();
        canvasObject.SetActive(false);
    }

    private void Update()
    {
        if (!game || !controller || !canvasObject) return;
        bool show = controller.IsBotGameActive && game.IsBotPractice && game.GameStarted &&
            !game.GameOver && !game.IsMatchEnding && !game.PauseLocked &&
            (game.BotOptions.AllowHints || game.BotOptions.AllowUndo);
        if (!show) { canvasObject.SetActive(false); return; }
        canvasObject.SetActive(true);
        float width = ToolbarWidth(game);
        MatchHudStyle.Place(panel,Vector2.one,new Vector2(width,38),new Vector2(-362-width/2,-30));
        hint.gameObject.SetActive(game.BotOptions.AllowHints);
        undo.gameObject.SetActive(game.BotOptions.AllowUndo);
        hint.interactable = controller.CanRequestHint;
        undo.interactable = game.CanUndoBotPractice;
        hintLabel.text = controller.IsHintThinking ? "Hint..." : "Hint";
    }

    private void OnDestroy() { if (canvasObject) Destroy(canvasObject); }
}
