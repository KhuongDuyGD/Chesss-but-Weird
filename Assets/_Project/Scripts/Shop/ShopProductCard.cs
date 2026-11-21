using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UI = SketchbookUI;

public sealed class ShopProductCard : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private HandDrawnRoundedGraphic face;
    [SerializeField] private Image thumbnail;
    [SerializeField] private SketchbookDoodle effectIcon;
    [SerializeField] private TextMeshProUGUI title, category, price, state;
    [SerializeField] private HandDrawnRoundedGraphic badge;
    public Button Button => button;
    public static SketchbookDoodle.Shape IconFor(string key) => key == "diamond_pack" ? SketchbookDoodle.Shape.Diamond :
        key == "gold_pack" || key == "mixed_pack" ? SketchbookDoodle.Shape.Coin : SketchbookDoodle.Shape.Star;
    private void Awake() { var motion = GetComponent<MainMenuButtonMotion>(); if (motion) motion.Configure(); }

    public void Build()
    {
        face = UI.Card(transform, "Product Paper", new Rect(0, 0, 316, 198), UI.White, 0, true, 14);
        face.raycastTarget = true;
        button = gameObject.AddComponent<Button>(); button.targetGraphic = face;
        var colors = button.colors; colors.highlightedColor = colors.selectedColor = new Color(1, 1, .94f);
        colors.pressedColor = new Color(.92f, .90f, .86f); colors.fadeDuration = .12f; button.colors = colors;
        gameObject.AddComponent<MainMenuButtonMotion>().Configure();
        var art = UI.Card(transform, "Art Mat", new Rect(12, 10, 292, 91), UI.Paper, 0, false, 9);
        art.Configure(UI.Paper, Color.clear, 9, 0, 0, 1);
        thumbnail = UI.Image(transform, "Product Art", null, new Rect(18, 12, 280, 86));
        effectIcon = UI.Doodle(transform, "Built In Effect", new Rect(126, 25, 64, 64), SketchbookDoodle.Shape.Star, UI.Ink);
        category = UI.Text(transform, "Type And Rarity", "", new Rect(16, 103, 284, 30), 16, UI.Muted);
        title = UI.Text(transform, "Product Name", "", new Rect(16, 132, 284, 28), 24);
        price = UI.Text(transform, "Product Price", "", new Rect(16, 167, 284, 24), 20);
        badge = UI.Card(transform, "Ownership Sticker", new Rect(177, 15, 125, 28), UI.Green, 0, false, 6);
        state = UI.Text(badge.transform, "State", "", new Rect(4, 0, 117, 28), 16, UI.Ink, TextAlignmentOptions.Center);
    }
    public void Bind(string name, string type, string priceText, string stateText, Sprite art, string effect,
        Color tierColor, bool selected, Action select)
    {
        title.text = name; category.text = type; price.text = priceText; state.text = stateText;
        thumbnail.sprite = art; thumbnail.enabled = art;
        effectIcon.gameObject.SetActive(!art); effectIcon.Configure(IconFor(effect), effect == "confetti" ? UI.Pink : UI.Ink);
        badge.gameObject.SetActive(!string.IsNullOrWhiteSpace(stateText)); badge.color = tierColor;
        face.color = selected ? UI.Blue : UI.White;
        button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => select());
    }
}
