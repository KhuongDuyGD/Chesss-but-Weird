using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UI = SketchbookUI;

// Presentation is kept apart from the service calls in GachaMenuController.cs.
public sealed partial class GachaMenuController
{
    private void BuildContentRoot()
    {
        var background = root.gameObject.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
        background.color = UI.Paper;
        contentRoot = CreateChild(root, "Gacha Content Root", Vector2.zero, new Vector2(DesignWidth, DesignHeight));
        contentRoot.gameObject.AddComponent<GachaContentRootFitter>().Configure(DesignWidth, DesignHeight);
    }

    private void BuildMainScreen()
    {
        mainScreen = CreateLayer("Gacha Main");
        UI.Background(mainScreen);
        UI.Text(mainScreen, "Eyebrow", "CHESS BUT WEIRD  /  A LITTLE LUCK, A LOT OF WEIRD", new Rect(94, 38, 800, 32), 21, UI.Muted);
        UI.Doodle(mainScreen, "Title Highlight", new Rect(90, 131, 442, 25), SketchbookDoodle.Shape.Scribble, UI.Pink, -1f);
        UI.Text(mainScreen, "Title", "Chaos Gacha", new Rect(90, 76, 700, 89), 68);
        UI.Doodle(mainScreen, "Title Star", new Rect(522, 90, 51, 51), SketchbookDoodle.Shape.Star, UI.Yellow, 14);
        AddResourceBars(mainScreen);

        var picker = UI.Card(mainScreen, "Banner Picker", new Rect(92, 214, 326, 181), UI.White).rectTransform;
        UI.Text(picker, "Picker Caption", "PICK YOUR LUCK", new Rect(24, 17, 277, 28), 22, UI.Muted);
        selectorLabel = UI.Text(picker, "Selected Banner", "Loading banners...", new Rect(24, 57, 277, 59), 30, UI.Ink, TextAlignmentOptions.Left, true);
        previousBannerButton = UI.Button(picker, "Previous Banner", "<", new Rect(23, 121, 63, 45), UI.Paper, () => ChangeBanner(-1), 24);
        bannerPageLabel = UI.Text(picker, "Banner Page", "-- / --", new Rect(97, 126, 132, 36), 22, UI.Muted, TextAlignmentOptions.Center);
        nextBannerButton = UI.Button(picker, "Next Banner", ">", new Rect(240, 121, 63, 45), UI.Yellow, NextBanner, 24);
        AddPityPanel(mainScreen);

        heroCard = UI.Card(mainScreen, "Banner Poster", new Rect(458, 214, 1120, 393), UI.White);
        var hero = heroCard.rectTransform;
        UI.Tape(hero, 773, -10, 165, 4);
        bannerTag = UI.Text(hero, "Banner Tag", "THE NEXT FIND IS A MYSTERY", new Rect(35, 28, 650, 33), 22, UI.Muted);
        bannerLabel = UI.Text(hero, "Banner Name", "A new surprise awaits", new Rect(35, 80, 707, 112), 52, UI.Ink, TextAlignmentOptions.MidlineLeft, true);
        bannerDescription = UI.Text(hero, "Banner Description", "", new Rect(35, 211, 672, 77), 27, UI.Muted, TextAlignmentOptions.TopLeft, true);
        UI.Image(hero, "Mystery Card", GetSprite("Skin5StarPR.png"), new Rect(798, 59, 272, 278)).rectTransform.localRotation = Quaternion.Euler(0, 0, -7);
        UI.Doodle(hero, "Poster Star One", new Rect(735, 48, 60, 60), SketchbookDoodle.Shape.Star, UI.Yellow, 12);
        UI.Doodle(hero, "Poster Star Two", new Rect(1040, 315, 39, 39), SketchbookDoodle.Shape.Star, UI.Pink, -9);
        string[] rarities = { "COMMON", "RARE", "EPIC", "LEGENDARY" };
        Color[] colors = { UI.Paper, UI.Blue, UI.Lavender, UI.Yellow };
        for (int i = 0; i < 4; i++)
        {
            var pill = UI.Card(hero, rarities[i] + " Rate", new Rect(30 + i * 177, 315, 165, 57), colors[i], 0, false, 12).rectTransform;
            rateLabels[i] = UI.Text(pill, "Rate", rarities[i] + " --", new Rect(10, 7, 145, 42), 20, UI.Ink, TextAlignmentOptions.Center);
        }
        UI.Text(mainScreen, "Summon Caption", "A new piece of your collection starts here.", new Rect(474, 628, 1090, 39), 26, UI.Muted);
        BuildSummonButton(mainScreen, "Summon x1", 1, new Rect(458, 686, 530, 116), UI.White);
        BuildSummonButton(mainScreen, "Summon x10", 10, new Rect(1018, 686, 560, 116), UI.Yellow);
        UI.Button(mainScreen, "History", "HISTORY", new Rect(92, 739, 154, 63), UI.White, ShowHistory, 23);
        UI.Button(mainScreen, "Banner Details", "DETAILS", new Rect(264, 739, 154, 63), UI.White, ShowBannerDetails, 23);
        UI.Button(mainScreen, "Back Gacha", "<  BACK TO MENU", new Rect(92, 850, 326, 59), UI.Paper, BackToModeSelection, 24);
        statusLabel = UI.Text(mainScreen, "Gacha Status", "", new Rect(478, 850, 1080, 60), 24, UI.Muted, TextAlignmentOptions.Center, true);
        BuildHistory(mainScreen);
        BuildBannerDetails(mainScreen);
    }

    private void BuildSummonButton(RectTransform parent, string name, int count, Rect area, Color color)
    {
        var button = UI.Button(parent, name, "", area, color, () => StartSummon(count));
        summonButtons.Add(button);
        UI.Text(button.transform, "Summon Label", "SUMMON x" + count, new Rect(25, 12, area.width - 50, 49), 37, UI.Ink, TextAlignmentOptions.Center);
        summonCosts.Add(UI.Text(button.transform, count == 1 ? "Cost x1" : "Cost x10", "--",
            new Rect(25, 63, area.width - 50, 33), 24, UI.Muted, TextAlignmentOptions.Center));
    }

    private void BuildResultScreen()
    {
        resultScreen = CreateLayer("Gacha Result");
        UI.Background(resultScreen);
        UI.Text(resultScreen, "Result Eyebrow", "FRESH FROM THE SKETCHBOOK", new Rect(94, 39, 750, 33), 22, UI.Muted);
        UI.Doodle(resultScreen, "Results Highlight", new Rect(90, 126, 473, 25), SketchbookDoodle.Shape.Scribble, UI.Yellow);
        UI.Text(resultScreen, "Results Heading", "Look what you found!", new Rect(90, 73, 760, 85), 57);
        AddResourceBars(resultScreen);
        resultSummaryLabel = UI.Text(resultScreen, "Result Summary", "", new Rect(94, 173, 1475, 39), 27, UI.Muted);
        resultGrid = UI.Node(resultScreen, "Result Grid", new Rect(92, 238, 1486, 468));
        UI.Button(resultScreen, "Result Back", "<  BACK TO BANNER", new Rect(92, 760, 404, 111), UI.White, ShowMainScreen, 27);
        BuildSummonButton(resultScreen, "Result Summon x1", 1, new Rect(526, 760, 506, 111), UI.White);
        BuildSummonButton(resultScreen, "Result Summon x10", 10, new Rect(1062, 760, 516, 111), UI.Yellow);
        resultStatusLabel = UI.Text(resultScreen, "Result Status", "", new Rect(110, 887, 1450, 36), 22, UI.Muted, TextAlignmentOptions.Center);
    }

    private void AddResourceBars(RectTransform parent)
    {
        goldLabels.Add(UI.Wallet(parent, "Gold", new Rect(899, 69, 211, 90), UI.Yellow, SketchbookDoodle.Shape.Coin, true));
        diamondLabels.Add(UI.Wallet(parent, "Diamonds", new Rect(1133, 69, 211, 90), UI.Blue, SketchbookDoodle.Shape.Diamond, true));
        ticketLabels.Add(UI.Wallet(parent, "Tickets", new Rect(1367, 69, 211, 90), UI.Pink, SketchbookDoodle.Shape.Ticket, true));
    }

    private void AddPityPanel(RectTransform parent)
    {
        var panel = UI.Card(parent, "Pity Notebook", new Rect(92, 426, 326, 281), UI.White).rectTransform;
        UI.Text(panel, "Pity Heading", "Luck is adding up...", new Rect(24, 17, 279, 40), 28);
        UI.Text(panel, "Legendary Caption", "LEGENDARY", new Rect(24, 76, 278, 26), 20, UI.Muted);
        pityLabel = UI.Text(panel, "Pity Text", "Pity -- / --", new Rect(24, 106, 278, 36), 29);
        UI.Card(panel, "Legendary Track", new Rect(24, 148, 278, 20), UI.Paper, 0, false, 5)
            .Configure(UI.Paper, UI.Ink, 5, 1.6f, .4f, 12);
        pityFill = UI.Card(panel, "Legendary Fill", new Rect(28, 152, 270, 12), UI.Yellow, 0, false, 2).rectTransform;
        UI.Text(panel, "Epic Caption", "EPIC", new Rect(24, 186, 110, 25), 20, UI.Muted);
        epicPityLabel = UI.Text(panel, "Epic Pity Text", "-- / --", new Rect(139, 181, 162, 34), 25, UI.Ink, TextAlignmentOptions.Right);
        UI.Card(panel, "Epic Track", new Rect(24, 225, 278, 20), UI.Paper, 0, false, 5)
            .Configure(UI.Paper, UI.Ink, 5, 1.6f, .4f, 12);
        epicPityFill = UI.Card(panel, "Epic Fill", new Rect(28, 229, 270, 12), UI.Lavender, 0, false, 2).rectTransform;
    }

    private RectTransform CreateOverlay(RectTransform parent, string name, string title, UnityAction close, out RectTransform sheet)
    {
        var overlay = UI.Node(parent, name, new Rect(0, 0, DesignWidth, DesignHeight));
        var dim = overlay.gameObject.AddComponent<Image>();
        dim.color = new Color(.16f, .13f, .20f, .48f);
        sheet = UI.Card(overlay, name + " Paper", new Rect(236, 75, 1200, 792), UI.White).rectTransform;
        // The paper catches clicks on blank space, keeping the menu behind it inert.
        sheet.GetComponent<HandDrawnRoundedGraphic>().raycastTarget = true;
        UI.Tape(sheet, 518, -12, 170, -3);
        UI.Text(sheet, "Heading", title, new Rect(38, 25, 900, 60), 43);
        UI.Button(sheet, "Close " + name, "X", new Rect(1091, 24, 68, 59), UI.Pink, close, 28);
        return overlay;
    }

    private void BuildHistory(RectTransform parent)
    {
        historyPanel = CreateOverlay(parent, "Summon History", "Your lucky trail", HideHistory, out var sheet);
        UI.Text(sheet, "History Hint", "Every summon, saved in your notebook. Scroll to see more.", new Rect(39, 91, 1020, 37), 24, UI.Muted);
        historyContent = UI.ScrollArea(sheet, "History Scroll", new Rect(35, 148, 1130, 520), out historyScroll);
        previousHistoryButton = UI.Button(sheet, "Previous Page", "<  PREVIOUS", new Rect(37, 698, 233, 61), UI.Paper, PreviousHistoryPage, 24);
        nextHistoryButton = UI.Button(sheet, "Next Page", "NEXT  >", new Rect(930, 698, 233, 61), UI.Yellow, NextHistoryPage, 24);
        historyPageLabel = UI.Text(sheet, "History Page", "", new Rect(295, 710, 610, 34), 25, UI.Muted, TextAlignmentOptions.Center);
        historyPanel.gameObject.SetActive(false);
    }

    private void BuildBannerDetails(RectTransform parent)
    {
        bannerDetailPanel = CreateOverlay(parent, "Banner Details", "The fine scribbles", HideBannerDetails, out var sheet);
        UI.Text(sheet, "Details Hint", "What's inside, what it costs, and how luck works.", new Rect(40, 91, 1080, 39), 24, UI.Muted);
        detailsContent = UI.ScrollArea(sheet, "Details Scroll", new Rect(38, 151, 1123, 571), out detailsScroll);
        bannerDetailText = UI.Text(detailsContent, "Banner Details Body", "", new Rect(10, 0, 1083, 571), 26, UI.Ink, TextAlignmentOptions.TopLeft, true);
        bannerDetailText.enableAutoSizing = false;
        bannerDetailText.overflowMode = TextOverflowModes.Overflow;
        bannerDetailText.lineSpacing = 8;
        UI.Text(sheet, "Scroll Hint", "SCROLL FOR THE FULL REWARD LIST", new Rect(41, 741, 1115, 28), 18, UI.Muted, TextAlignmentOptions.Center);
        bannerDetailPanel.gameObject.SetActive(false);
    }
}
