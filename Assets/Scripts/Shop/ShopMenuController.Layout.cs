using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UI = SketchbookUI;

public sealed partial class ShopMenuController
{
    [SerializeField] private ShopPresentationCatalog presentation;
    [SerializeField] private RectTransform design, cardsRoot, previewRoot, confirmation;
    [SerializeField] private CanvasGroup controls;
    [SerializeField] private Button backButton, refreshButton, previousButton, nextButton, goldButton, diamondButton, equipButton;
    [SerializeField] private Button cancelButton, confirmButton, collectionFilter, equippedFilter;
    [SerializeField] private Button[] tabs;
    [SerializeField] private TextMeshProUGUI goldBalance, diamondBalance, status, count, pageLabel, header;
    [SerializeField] private TextMeshProUGUI detailTitle, detailType, detailDescription, detailContents, detailState, equipLabel;
    [SerializeField] private TextMeshProUGUI goldPrice, diamondPrice, confirmationTitle, confirmationText, confirmLabel, heroTitle, heroCopy;
    [SerializeField] private Image detailArt, heroArt;
    [SerializeField] private SketchbookDoodle detailEffect;
    [SerializeField] private TMP_InputField search;
    [SerializeField] private Button heroButton;
    [SerializeField] private Button recoveryButton;
    [SerializeField] private ShopProductCard[] cards;

    // Upgrade existing serialized screens without requiring an Editor rebuild.
    // The asset builder also calls this so newly generated prefabs contain the same controls.
    private void EnsureAdditionalControls()
    {
        if (tabs.Length == 7)
        {
            System.Array.Resize(ref tabs, 8);
            tabs[7] = UI.Button(controls.transform, "Shop Tab TopUp", "TOP UP", new Rect(48 + 7 * 198, 169, 190, 53), UI.Lavender, null, 23);
        }
        if (tabs.Length == 8)
        {
            System.Array.Resize(ref tabs, 9);
            tabs[8] = UI.Button(controls.transform, "Shop Tab Backgrounds", "BACKGROUNDS", new Rect(1448, 169, 176, 53), UI.Blue, null, 20);
        }
        for (int i = 0; i < tabs.Length; i++)
        {
            var rect = (RectTransform)tabs[i].transform;
            rect.sizeDelta = new Vector2(168, 53);
            int column = System.Array.IndexOf(new[] { 0, 1, 2, 8, 3, 4, 5, 6, 7 }, i);
            rect.anchoredPosition = new Vector2(48 + column * 175 + 84, -195.5f);
            var shadow = rect.parent.Find(tabs[i].name + " Pencil Shadow") as RectTransform;
            if (shadow) { shadow.sizeDelta = rect.sizeDelta; shadow.anchoredPosition = rect.anchoredPosition + new Vector2(5, -7); }
            var label = tabs[i].GetComponentInChildren<TextMeshProUGUI>();
            label.rectTransform.sizeDelta = new Vector2(152, 43);
            label.rectTransform.anchoredPosition = new Vector2(84, -26.5f);
            label.fontSizeMax = 21;
        }
        if (!recoveryButton) recoveryButton = UI.Button(controls.transform, "Review Pending Purchase", "REVIEW PENDING PURCHASE",
            new Rect(48, 133, 400, 32), UI.Yellow, null, 18);
        var recoveryLabel = recoveryButton.GetComponentInChildren<TextMeshProUGUI>().rectTransform;
        recoveryLabel.sizeDelta = new Vector2(372, 32);
        recoveryLabel.anchoredPosition = new Vector2(200, -16);
        SetButtonVisible(recoveryButton, false);
    }

    // The editor builder serializes this complete layout into the shipped screen prefab.
    public void BuildLayout(ShopPresentationCatalog assets)
    {
        presentation = assets;
        var screen = (RectTransform)transform;
        UI.Background(screen);
        design = UI.Node(screen, "Shop Design", new Rect(0, 0, 1672, 941));
        design.gameObject.AddComponent<MenuDesignFrame>().Configure(new Vector2(1672, 941));
        UI.Background(design);
        var content = UI.Node(design, "Shop Controls", new Rect(0, 0, 1672, 941));
        controls = content.gameObject.AddComponent<CanvasGroup>();
        UI.Text(content, "Shop Eyebrow", "CHESS BUT WEIRD  /  MAKE IT YOURS", new Rect(48, 26, 800, 28), 20, UI.Muted);
        UI.Card(content, "Title Highlighter", new Rect(49, 118, 504, 13), UI.Yellow, -1, false, 3);
        UI.Text(content, "Shop Title", assets.title, new Rect(48, 60, 850, 69), 55);
        goldBalance = UI.Wallet(content, "Gold", new Rect(918, 52, 239, 88), UI.Yellow, SketchbookDoodle.Shape.Coin, true);
        diamondBalance = UI.Wallet(content, "Diamonds", new Rect(1177, 52, 239, 88), UI.Blue, SketchbookDoodle.Shape.Diamond, true);
        backButton = UI.Button(content, "Back From Shop", "<  BACK", new Rect(1444, 69, 180, 61), UI.Pink, null, 25);
        string[] labels = { "FEATURED", "PIECE SKINS", "BOARDS", "EFFECTS", "BUNDLES", "COLLECTION", "SUMMON  >" };
        tabs = new Button[labels.Length];
        for (int i = 0; i < labels.Length; i++)
            tabs[i] = UI.Button(content, "Shop Tab " + ((ShopCategory)i), labels[i], new Rect(48 + i * 226, 169, 208, 53),
                i == 0 ? UI.Yellow : i == 6 ? UI.Lavender : UI.White, null, 24);

        heroButton = UI.Button(content, "Featured Offer", "", new Rect(48, 245, 994, 137), UI.Blue, null);
        UI.Tape(heroButton.transform, 46, -12, 112, -3);
        UI.Text(heroButton.transform, "Feature Note", "FROM THE SKETCHBOOK", new Rect(26, 16, 650, 25), 18, UI.Muted);
        heroTitle = UI.Text(heroButton.transform, "Feature Title", "Your next favourite look", new Rect(26, 43, 680, 48), 36);
        heroCopy = UI.Text(heroButton.transform, "Feature Copy", assets.subtitle, new Rect(26, 99, 680, 26), 21);
        heroArt = UI.Image(heroButton.transform, "Feature Art", null, new Rect(735, 8, 235, 121));
        header = UI.Text(content, "Category Heading", "Featured finds", new Rect(48, 405, 412, 38), 29);
        search = BuildSearch(content, new Rect(510, 401, 388, 44));
        refreshButton = UI.Button(content, "Refresh Shop", "REFRESH", new Rect(916, 401, 126, 44), UI.White, null, 20);
        cardsRoot = UI.Node(content, "Shop Product Grid", new Rect(48, 468, 994, 420));
        cards = new ShopProductCard[6];
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i] = Instantiate(assets.productCard, cardsRoot);
            cards[i].name = "Product Card " + (i + 1);
            var rect = (RectTransform)cards[i].transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(i % 3 * 336, i / 3 * -210);
            rect.localScale = Vector3.one;
        }
        previousButton = UI.Button(content, "Previous Shop Page", "<", new Rect(48, 897, 55, 36), UI.White, null, 23);
        nextButton = UI.Button(content, "Next Shop Page", ">", new Rect(987, 897, 55, 36), UI.White, null, 23);
        pageLabel = UI.Text(content, "Shop Page", "", new Rect(116, 897, 114, 36), 18, UI.Muted);
        count = UI.Text(content, "Shop Item Count", "", new Rect(250, 897, 714, 36), 19, UI.Muted, TextAlignmentOptions.Right);
        collectionFilter = UI.Button(content, "Collection Kind Filter", "ALL OWNED", new Rect(48, 405, 216, 38), UI.Green, null, 20);
        equippedFilter = UI.Button(content, "Collection Equipped Filter", "EQUIPPED", new Rect(284, 405, 206, 38), UI.White, null, 20);

        var detail = UI.Card(content, "Product Detail Paper", new Rect(1080, 245, 544, 640), UI.White, 0, true, 14).rectTransform;
        UI.Tape(detail, 208, -13, 128, 3);
        detailType = UI.Text(detail, "Detail Type", "", new Rect(26, 24, 492, 27), 20, UI.Muted);
        detailTitle = UI.Text(detail, "Detail Title", "Pick a little something", new Rect(26, 57, 492, 62), 39, null, TextAlignmentOptions.MidlineLeft, true);
        previewRoot = UI.Node(detail, "Product Inspection", new Rect(26, 132, 492, 230));
        UI.Card(previewRoot, "Preview Mat", new Rect(0, 0, 492, 230), UI.Paper, 0, false, 10);
        detailArt = UI.Image(previewRoot, "Large Product Art", null, new Rect(8, 7, 476, 216));
        detailEffect = UI.Doodle(previewRoot, "Effect Inspection", new Rect(179, 49, 134, 134), SketchbookDoodle.Shape.Star, UI.Ink);
        detailDescription = UI.Text(detail, "Product Description", "", new Rect(26, 377, 492, 77), 24, UI.Muted, TextAlignmentOptions.TopLeft, true);
        detailContents = UI.Text(detail, "Bundle Contents", "", new Rect(26, 457, 492, 62), 20, UI.Muted, TextAlignmentOptions.TopLeft, true);
        detailState = UI.Text(detail, "Ownership Detail", "", new Rect(26, 523, 492, 30), 22);
        goldButton = UI.Button(detail, "Buy With Gold", "", new Rect(26, 565, 235, 53), UI.Yellow, null);
        diamondButton = UI.Button(detail, "Buy With Diamonds", "", new Rect(281, 565, 237, 53), UI.Blue, null);
        goldPrice = goldButton.GetComponentInChildren<TextMeshProUGUI>(); goldPrice.fontSizeMax = 25;
        diamondPrice = diamondButton.GetComponentInChildren<TextMeshProUGUI>(); diamondPrice.fontSizeMax = 25;
        equipButton = UI.Button(detail, "Equip Cosmetic", "EQUIP", new Rect(26, 565, 492, 53), UI.Green, null, 26);
        equipLabel = equipButton.GetComponentInChildren<TextMeshProUGUI>();
        status = UI.Text(content, "Shop Status", "", new Rect(1080, 898, 544, 38), 18, UI.Muted, TextAlignmentOptions.MidlineLeft, true);

        confirmation = UI.Node(design, "Purchase Confirmation", new Rect(0, 0, 1672, 941));
        var shade = confirmation.gameObject.AddComponent<Image>(); shade.color = new Color(.12f, .11f, .14f, .48f); shade.raycastTarget = true;
        var dialog = UI.Card(confirmation, "Confirm Purchase Paper", new Rect(450, 242, 772, 457), UI.White, 0, true, 18).rectTransform;
        dialog.GetComponent<HandDrawnRoundedGraphic>().raycastTarget = true;
        UI.Tape(dialog, 317, -14, 140, -3);
        confirmationTitle = UI.Text(dialog, "Confirmation Title", "Make it yours?", new Rect(36, 35, 700, 67), 47);
        confirmationText = UI.Text(dialog, "Confirmation Details", "", new Rect(36, 118, 700, 214), 29, UI.Ink, TextAlignmentOptions.TopLeft, true);
        cancelButton = UI.Button(dialog, "Cancel Purchase", "CANCEL", new Rect(36, 365, 270, 57), UI.Paper, null, 27);
        confirmButton = UI.Button(dialog, "Confirm Purchase", "CONFIRM", new Rect(332, 365, 404, 57), UI.Yellow, null, 27);
        confirmLabel = confirmButton.GetComponentInChildren<TextMeshProUGUI>();
        confirmation.gameObject.SetActive(false);
        EnsureAdditionalControls();
    }
    private static TMP_InputField BuildSearch(Transform parent, Rect area)
    {
        var face = UI.Card(parent, "Search Shop", area, UI.White, 0, false, 8); face.raycastTarget = true;
        var field = face.gameObject.AddComponent<TMP_InputField>();
        var viewport = UI.Node(face.transform, "Search Viewport", new Rect(14, 3, area.width - 28, area.height - 6));
        viewport.gameObject.AddComponent<RectMask2D>();
        field.textViewport = viewport; field.targetGraphic = face;
        field.textComponent = UI.Text(viewport, "Search Text", "", new Rect(0, 0, area.width - 28, area.height - 6), 22);
        field.placeholder = UI.Text(viewport, "Search Hint", "Search your next look...", new Rect(0, 0, area.width - 28, area.height - 6), 22, UI.Muted);
        field.characterLimit = 80; return field;
    }
}
