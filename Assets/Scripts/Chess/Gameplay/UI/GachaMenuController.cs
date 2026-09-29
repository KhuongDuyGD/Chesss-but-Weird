using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UI = SketchbookUI;

public sealed partial class GachaMenuController : MonoBehaviour
{
    private const float DesignWidth = 1672f;
    private const float DesignHeight = 941f;
    private const string AssetFolder = "Assets/Materials/Gacha_menu";

    private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private readonly List<Sprite> runtimeSprites = new List<Sprite>();
    private readonly List<TextMeshProUGUI> goldLabels = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> diamondLabels = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> ticketLabels = new List<TextMeshProUGUI>();

    private RectTransform root;
    private RectTransform contentRoot;
    private RectTransform mainScreen;
    private RectTransform resultScreen;
    private RectTransform resultGrid;
    private RectTransform historyPanel;
    private RectTransform bannerDetailPanel;
    private RectTransform pityFill;
    private TextMeshProUGUI pityLabel;
    private TextMeshProUGUI epicPityLabel;
    private TextMeshProUGUI statusLabel;
    private TextMeshProUGUI resultSummaryLabel;
    private TextMeshProUGUI bannerLabel;
    private TextMeshProUGUI selectorLabel, bannerDescription, bannerTag, bannerPageLabel, resultStatusLabel;
    private readonly TextMeshProUGUI[] rateLabels = new TextMeshProUGUI[4];
    private readonly List<Button> summonButtons = new List<Button>();
    private readonly List<TextMeshProUGUI> summonCosts = new List<TextMeshProUGUI>();
    private HandDrawnRoundedGraphic heroCard;
    private RectTransform epicPityFill, historyContent, detailsContent;
    private ScrollRect historyScroll, detailsScroll;
    private Button previousBannerButton, previousHistoryButton, nextHistoryButton;
    private bool historyLoading;
    private TextMeshProUGUI historyPageLabel;
    private TextMeshProUGUI bannerDetailText;
    private Button nextBannerButton;
    private GachaSummonRevealController summonReveal;
    private UnityAction backToModeSelection;
    private readonly GachaService gacha = new GachaService();
    private List<GachaBannerSummaryResponse> banners = new List<GachaBannerSummaryResponse>();
    private GachaBannerDetailResponse activeBanner;
    private GachaPityResponse pity;
    private GachaWalletResponse wallet;
    private GachaHistoryResponse history;
    private int bannerIndex;
    private int historyPage = 1;
    private bool loading;
    private List<GachaReward> pendingResults = new List<GachaReward>();
    private bool summonInProgress;
    private Guid pendingRequestId;
    private string pendingBannerCode;
    private int pendingCount;

    public void Initialize(RectTransform newRoot, UnityAction newBackToModeSelection)
    {
        root = newRoot;
        backToModeSelection = newBackToModeSelection;
        LoadSprites();
        BuildContentRoot();
        BuildMainScreen();
        BuildResultScreen();
        BuildSummonReveal();
        ShowMainScreen();
    }

    public void OpenMainScreen()
    {
        if (mainScreen && resultScreen)
        {
            ShowMainScreen();
            RefreshFromServer();
        }
    }

    private void OnEnable()
    {
        if (mainScreen) RefreshAll();
    }

    private async void RefreshFromServer()
    {
        if (loading) return;
        loading = true;
        SetStatus("Loading banners and wallet...");
        try
        {
            string previousCode = activeBanner?.code;
            banners = await gacha.GetBannersAsync() ?? new List<GachaBannerSummaryResponse>();
            if (!this) return;
            if (banners.Count == 0)
            {
                activeBanner = null;
                pity = null;
                SetStatus("No active gacha banners are available.");
                RefreshAll();
                return;
            }
            int previousIndex = banners.FindIndex(item => item.code == previousCode);
            int standardIndex = banners.FindIndex(item =>
                string.Equals(item.code, "STANDARD_TICKET_BANNER", StringComparison.OrdinalIgnoreCase));
            bannerIndex = previousIndex >= 0 ? previousIndex : standardIndex >= 0 ? standardIndex : 0;
            await LoadSelectedBannerAsync();
            UserMeResponse user = await new UserService().GetMeAsync();
            if (!this) return;
            PlayerAuthService.ApplyApiUser(user);
            wallet = new GachaWalletResponse
            {
                golds = user.wallet?.golds ?? 0,
                diamonds = user.wallet?.diamonds ?? 0,
                tickets = user.wallet?.tickets ?? 0
            };
            RefreshAll();
            SetStatus("Pick a banner. Your next surprise is waiting.");
        }
        catch (Exception error)
        {
            if (this) SetStatus("Could not load gacha: " + PlayerNotificationText.FromException(error));
        }
        finally
        {
            if (this) { loading = false; RefreshButtons(); }
        }
    }

    private async Task LoadSelectedBannerAsync()
    {
        string code = banners[bannerIndex].code;
        activeBanner = await gacha.GetBannerAsync(code);
        pity = await gacha.GetPityAsync(code);
        if (this) RefreshAll();
    }

    private void NextBanner() => ChangeBanner(1);

    private async void ChangeBanner(int direction)
    {
        if (loading || summonInProgress || banners.Count < 2) return;
        if (pendingRequestId != Guid.Empty)
        {
            SetStatus("Retry the pending summon before changing banners.");
            return;
        }
        loading = true;
        int previousIndex = bannerIndex;
        GachaBannerDetailResponse previousBanner = activeBanner;
        GachaPityResponse previousPity = pity;
        bannerIndex = (bannerIndex + direction + banners.Count) % banners.Count;
        SetStatus("Loading banner...");
        try
        {
            await LoadSelectedBannerAsync();
            if (this) SetStatus("Showing " + activeBanner.name);
        }
        catch (Exception error)
        {
            bannerIndex = previousIndex;
            activeBanner = previousBanner;
            pity = previousPity;
            if (this)
            {
                RefreshAll();
                SetStatus("Could not load banner: " + PlayerNotificationText.FromException(error));
            }
        }
        finally
        {
            if (this) { loading = false; RefreshButtons(); }
        }
    }

    private void ShowBannerDetails()
    {
        if (!bannerDetailPanel || activeBanner == null || loading) return;
        HideHistory();
        var text = new StringBuilder();
        text.AppendLine(activeBanner.name);
        text.AppendLine(activeBanner.description);
        text.AppendLine();
        text.AppendLine("SUMMON COST");
        if (activeBanner.costs != null)
            foreach (GachaCostResponse cost in activeBanner.costs)
                text.AppendLine("x" + cost.rollCount + "  /  " + cost.amount.ToString("N0") + " " + cost.currency.ToLowerInvariant());
        text.AppendLine();
        text.AppendLine("CHANCES & PITY");
        text.AppendLine("Common " + activeBanner.rarityRates?.common + "%   |   Rare " + activeBanner.rarityRates?.rare +
            "%   |   Epic " + activeBanner.rarityRates?.epic + "%   |   Legendary " + activeBanner.rarityRates?.legendary + "%");
        text.AppendLine("Epic pity: " + activeBanner.pityLimit?.epic + " draws.  Legendary pity: " + activeBanner.pityLimit?.legendary + " draws.");
        text.AppendLine();
        text.AppendLine("IN THIS BANNER");
        if (activeBanner.poolItems != null)
            foreach (GachaPoolItemResponse item in activeBanner.poolItems)
                text.AppendLine(item.name + "  /  " + item.rarity);
        text.AppendLine();
        text.AppendLine("ALREADY OWN IT? HERE'S YOUR BONUS");
        if (activeBanner.duplicateRewards != null)
            foreach (var reward in activeBanner.duplicateRewards)
                text.AppendLine(reward.Key + "  /  +" + reward.Value.amount.ToString("N0") + " " + reward.Value.currency.ToLowerInvariant());
        bannerDetailText.text = text.ToString();
        float height = Mathf.Max(571, bannerDetailText.GetPreferredValues(text.ToString(), 1083, 0).y + 30);
        bannerDetailText.rectTransform.sizeDelta = new Vector2(1083, height);
        bannerDetailText.rectTransform.anchoredPosition = new Vector2(551.5f, -height * .5f);
        detailsContent.sizeDelta = new Vector2(0, height);
        bannerDetailPanel.gameObject.SetActive(true);
        bannerDetailPanel.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        detailsScroll.verticalNormalizedPosition = 1f;
    }

    private void HideBannerDetails()
    {
        if (bannerDetailPanel) bannerDetailPanel.gameObject.SetActive(false);
    }

    private async void StartSummon(int count)
    {
        if (loading)
        {
            SetStatus("Wait for the banner to finish loading.");
            return;
        }
        if (summonInProgress)
        {
            if (summonReveal && summonReveal.IsPlaying) summonReveal.RequestSkip();
            return;
        }
        if (activeBanner == null || string.IsNullOrWhiteSpace(activeBanner.code))
        {
            SetStatus("No active banner is loaded.");
            return;
        }
        if (activeBanner.costs == null || !activeBanner.costs.Exists(cost => cost.rollCount == count))
        {
            SetStatus("This banner does not offer that summon count.");
            return;
        }
        if (pendingRequestId != Guid.Empty &&
            (pendingBannerCode != activeBanner.code || pendingCount != count))
        {
            SetStatus("Retry the pending x" + pendingCount + " summon before choosing another count.");
            return;
        }

        HideHistory();
        HideBannerDetails();
        summonInProgress = true;
        SetStatus("Opening your surprise...");
        try
        {
            if (pendingRequestId == Guid.Empty || pendingBannerCode != activeBanner.code || pendingCount != count)
            {
                pendingRequestId = Guid.NewGuid();
                pendingBannerCode = activeBanner.code;
                pendingCount = count;
            }
            GachaRollResponse roll = await gacha.RollAsync(activeBanner.code, count, pendingRequestId);
            if (!this || roll == null || roll.results == null || roll.wallet == null || roll.pity == null)
                throw new ApiException("The server returned an incomplete gacha result.");
            wallet = roll.wallet;
            pity = roll.pity;
            pendingResults = new List<GachaReward>(roll.results.Count);
            foreach (GachaRewardResponse result in roll.results)
                pendingResults.Add(ToVisualReward(result));
            if (pendingResults.Count != count)
                throw new ApiException("The server returned the wrong number of rewards.");
            pendingRequestId = Guid.Empty;

            PlayerProfileStore.SetCurrency(ToLocalAmount(wallet.golds), ToLocalAmount(wallet.diamonds), ToLocalAmount(wallet.tickets));
            UserMeResponse user = PlayerAuthService.CurrentApiUser;
            if (user?.wallet != null)
            {
                user.wallet.golds = ToLocalAmount(wallet.golds);
                user.wallet.diamonds = ToLocalAmount(wallet.diamonds);
                user.wallet.tickets = ToLocalAmount(wallet.tickets);
            }
            RefreshAll();
            PlayPendingReveal();
        }
        catch (Exception error)
        {
            if (this)
            {
                if (error is ApiException apiError && apiError.StatusCode >= 400 && apiError.StatusCode < 500)
                    pendingRequestId = Guid.Empty;
                summonInProgress = false;
                SetStatus("Summon failed: " + PlayerNotificationText.FromException(error) + " You can retry this summon safely.");
            }
        }
    }

    private static int ToLocalAmount(long amount) =>
        amount < 0 ? 0 : amount > int.MaxValue ? int.MaxValue : (int)amount;

    private static GachaReward ToVisualReward(GachaRewardResponse item)
    {
        int rarity = RarityLevel(item?.rarity);
        return new GachaReward(GachaRewardType.Skin, 1, rarity, rarity >= 5,
            item?.itemName ?? item?.itemCode ?? "Unknown item", item?.isDuplicate ?? false,
            item?.duplicateReward == null ? "" :
                "+" + item.duplicateReward.amount.ToString("N0") + " " + item.duplicateReward.currency);
    }

    private static int RarityLevel(string rarity)
    {
        switch (rarity?.Trim().ToUpperInvariant())
        {
            case "LEGENDARY": return 5;
            case "EPIC": return 4;
            case "RARE": return 3;
            default: return 2;
        }
    }

    private static string SummarizeRewards(List<GachaReward> rewards)
    {
        if (rewards == null || rewards.Count == 0) return "No rewards returned.";
        int duplicates = rewards.FindAll(item => item.isDuplicate).Count;
        return rewards.Count + (rewards.Count == 1 ? " find" : " finds") + " for your collection" +
            (duplicates > 0 ? "  /  " + duplicates + " duplicate bonuses" : "  /  all new!");
    }
    private void ShowPendingResults()
    {
        summonInProgress = false;
        PopulateResultGrid();
        resultSummaryLabel.text = SummarizeRewards(pendingResults);
        resultScreen.gameObject.SetActive(true);
        mainScreen.gameObject.SetActive(false);
        SetStatus(string.Empty);
    }

    private void BuildSummonReveal()
    {
        summonReveal = contentRoot.gameObject.AddComponent<GachaSummonRevealController>();
        summonReveal.Initialize(contentRoot);
    }

    private void PlayPendingReveal()
    {
        mainScreen.gameObject.SetActive(false);
        resultScreen.gameObject.SetActive(false);
        if (summonReveal)
            summonReveal.PlaySequence(pendingResults, GetRewardSprite, ShowPendingResults);
        else
            ShowPendingResults();
    }

    private void PopulateResultGrid()
    {
        ClearRows(resultGrid);
        int count = pendingResults.Count;
        for (int i = 0; i < count; i++)
        {
            Rect area = count == 1 ? new Rect(505, 6, 470, 434) : new Rect((i % 5) * 300, (i / 5) * 241, 280, 218);
            var reward = pendingResults[i];
            Color tint = reward.rarity >= 5 ? UI.Yellow : reward.rarity == 4 ? UI.Lavender : reward.rarity == 3 ? UI.Blue : UI.Paper;
            var card = UI.Card(resultGrid, "Reward " + (i + 1), area, UI.White, i % 2 == 0 ? -.65f : .65f).rectTransform;
            UI.Text(card, "Rarity", RarityName(reward.rarity), new Rect(16, 13, area.width - 32, 27), 20, UI.Muted, TextAlignmentOptions.Center);
            float iconWidth = count == 1 ? 100 : 60;
            UI.Doodle(card, "Reward Doodle", new Rect((area.width - iconWidth) * .5f, count == 1 ? 89 : 53, iconWidth, count == 1 ? 127 : 66), SketchbookDoodle.Shape.Pawn, UI.Ink, -6);
            UI.Doodle(card, "Reward Star", new Rect(area.width - 70, count == 1 ? 91 : 57, 32, 32), SketchbookDoodle.Shape.Star, tint, 12);
            UI.Text(card, "Item Name", reward.displayName, new Rect(18, count == 1 ? 248 : 121, area.width - 36, count == 1 ? 66 : 46), count == 1 ? 34 : 24, UI.Ink, TextAlignmentOptions.Center, true);
            var badge = UI.Card(card, "Ownership Label", new Rect(17, area.height - 44, area.width - 34, 38), tint, 0, false, 5).rectTransform;
            UI.Text(badge, "Ownership", reward.isDuplicate ? "DUPLICATE  " + reward.duplicateText : "NEW FIND!", new Rect(7, 7, area.width - 48, 24), 17, UI.Ink, TextAlignmentOptions.Center);
        }
    }

    private static string RarityName(int rarity) => rarity >= 5 ? "LEGENDARY" : rarity == 4 ? "EPIC" : rarity == 3 ? "RARE" : "COMMON";

    private static void ClearRows(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
    }

    private void ShowMainScreen()
    {
        HideHistory();
        HideBannerDetails();
        mainScreen.gameObject.SetActive(true);
        resultScreen.gameObject.SetActive(false);
        RefreshAll();
    }

    private void BackToModeSelection()
    {
        HideHistory();
        HideBannerDetails();
        backToModeSelection?.Invoke();
    }

    private async void ShowHistory()
    {
        if (!historyPanel || loading || historyLoading || summonInProgress) return;
        historyLoading = true;
        HideBannerDetails();
        historyPanel.gameObject.SetActive(true);
        historyPanel.SetAsLastSibling();
        previousHistoryButton.interactable = nextHistoryButton.interactable = false;
        historyPageLabel.text = "Opening your lucky trail...";
        try
        {
            history = await gacha.GetHistoryAsync(historyPage, 8);
            if (!this || !historyPanel) return;
            RenderHistory();
        }
        catch (Exception error)
        {
            if (this) historyPageLabel.text = "Couldn't load history. " + PlayerNotificationText.FromException(error);
        }
        finally
        {
            historyLoading = false;
            if (this) UpdateHistoryButtons();
        }
    }

    private void RenderHistory()
    {
        ClearRows(historyContent);
        int pages = history == null ? 1 : Mathf.Max(1, (int)Math.Ceiling(history.total / 8d));
        historyPageLabel.text = "Page " + historyPage + " / " + pages;
        float y = 4;
        if (history?.items == null || history.items.Count == 0)
        {
            UI.Doodle(historyContent, "Empty History Star", new Rect(524, 115, 70, 70), SketchbookDoodle.Shape.Star, UI.Yellow, 10);
            UI.Text(historyContent, "History Empty", "A blank page, for now.\nYour first summon will appear here.", new Rect(130, 210, 860, 100), 31, UI.Muted, TextAlignmentOptions.Center, true);
        }
        else foreach (var roll in history.items)
        {
            var names = new List<string>();
            if (roll.results != null)
                foreach (GachaRewardResponse item in roll.results)
                    names.Add(item.itemName + (item.isDuplicate ? " (duplicate" + (item.duplicateReward == null ? "" : ", +" + item.duplicateReward.amount + " " + item.duplicateReward.currency.ToLowerInvariant()) + ")" : ""));
            string date = DateTimeOffset.TryParse(roll.createdAt, out var time) ? time.ToLocalTime().ToString("dd MMM  HH:mm") : "--";
            float height = roll.count > 1 ? 183 : 112;
            var row = UI.Card(historyContent, "History Entry", new Rect(4, y, 1112, height), UI.Paper, 0, false, 14).rectTransform;
            UI.Text(row, "History Meta", date + "  /  x" + roll.count + "  /  " + roll.totalCost.ToString("N0") + " " + roll.currency.ToLowerInvariant(), new Rect(20, 12, 1068, 31), 22, UI.Muted);
            var namesLabel = UI.Text(row, "History Items", string.Join(", ", names), new Rect(20, 48, 1068, height - 60), 25, UI.Ink, TextAlignmentOptions.TopLeft, true);
            namesLabel.enableAutoSizing = false;
            float textHeight = namesLabel.GetPreferredValues(namesLabel.text, 1068, 0).y;
            if (textHeight > height - 60)
            {
                height = textHeight + 67;
                row.sizeDelta = new Vector2(1112, height);
                row.anchoredPosition = new Vector2(560, -y - height * .5f);
                namesLabel.rectTransform.sizeDelta = new Vector2(1068, textHeight);
                namesLabel.rectTransform.anchoredPosition = new Vector2(554, -48 - textHeight * .5f);
            }
            y += height + 14;
        }
        historyContent.sizeDelta = new Vector2(0, Mathf.Max(520, y));
        Canvas.ForceUpdateCanvases();
        historyScroll.verticalNormalizedPosition = 1;
        UpdateHistoryButtons();
    }

    private void UpdateHistoryButtons()
    {
        previousHistoryButton.interactable = !historyLoading && historyPage > 1;
        nextHistoryButton.interactable = !historyLoading && history != null && (long)historyPage * 8 < history.total;
    }

    private void PreviousHistoryPage()
    {
        if (historyLoading || historyPage <= 1) return;
        historyPage--;
        ShowHistory();
    }

    private void NextHistoryPage()
    {
        if (historyLoading || history == null || (long)historyPage * 8 >= history.total) return;
        historyPage++;
        ShowHistory();
    }
    private void HideHistory()
    {
        if (historyPanel)
            historyPanel.gameObject.SetActive(false);
    }

    private void RefreshAll()
    {
        for (int i = 0; i < goldLabels.Count; i++)
            goldLabels[i].text = wallet?.golds.ToString("N0") ?? "--";
        for (int i = 0; i < diamondLabels.Count; i++)
            diamondLabels[i].text = wallet?.diamonds.ToString("N0") ?? "--";
        for (int i = 0; i < ticketLabels.Count; i++)
            ticketLabels[i].text = wallet?.tickets.ToString("N0") ?? "--";

        if (bannerLabel) bannerLabel.text = activeBanner?.name ?? "A new surprise awaits";
        if (selectorLabel) selectorLabel.text = activeBanner?.name ?? "No banner loaded";
        if (bannerDescription) bannerDescription.text = activeBanner?.description ?? "Choose a banner to discover your next chess skin.";
        if (bannerTag) bannerTag.text = activeBanner?.costs?.Count > 0 ? activeBanner.costs[0].currency.ToUpperInvariant() + " SUMMONS" : "A LITTLE MYSTERY";
        if (bannerPageLabel) bannerPageLabel.text = banners.Count == 0 ? "-- / --" : (bannerIndex + 1) + " / " + banners.Count;
        bool premium = activeBanner?.costs?.Exists(cost => cost.currency == "DIAMONDS") ?? false;
        if (heroCard) heroCard.Configure(premium ? new Color(.94f, .92f, .99f) : UI.White, UI.Ink, 22, 2.6f, 1.8f, 13);
        string[] names = { "COMMON", "RARE", "EPIC", "LEGENDARY" };
        decimal?[] rates = { activeBanner?.rarityRates?.common, activeBanner?.rarityRates?.rare, activeBanner?.rarityRates?.epic, activeBanner?.rarityRates?.legendary };
        for (int i = 0; i < rateLabels.Length; i++) if (rateLabels[i]) rateLabels[i].text = names[i] + " " + (rates[i]?.ToString("0.##") ?? "--") + "%";
        for (int i = 0; i < summonCosts.Count; i++) summonCosts[i].text = CostLabel(i % 2 == 0 ? 1 : 10);
        if (pityLabel) pityLabel.text = pity?.legendary == null ? "Pity -- / --" : $"Pity {pity.legendary.current} / {pity.legendary.limit}";
        if (epicPityLabel) epicPityLabel.text = pity?.epic == null ? "-- / --" : $"{pity.epic.current} / {pity.epic.limit}";
        SetPityFill(pityFill, pity?.legendary, 158);
        SetPityFill(epicPityFill, pity?.epic, 235);
        RefreshButtons();
    }

    private static void SetPityFill(RectTransform fill, GachaPityTierResponse tier, float y)
    {
        if (!fill) return;
        float width = tier == null || tier.limit <= 0 ? 0 : 270 * Mathf.Clamp01(tier.current / (float)tier.limit);
        fill.gameObject.SetActive(width >= 6);
        fill.sizeDelta = new Vector2(width, 12);
        fill.anchoredPosition = new Vector2(28 + width * .5f, -y);
        var graphic = fill.GetComponent<HandDrawnRoundedGraphic>();
        graphic.Configure(graphic.color, Color.clear, 2, 0, 0, 1);
    }

    private void RefreshButtons()
    {
        if (nextBannerButton) nextBannerButton.interactable = !loading && !summonInProgress && banners.Count > 1 && pendingRequestId == Guid.Empty;
        if (previousBannerButton) previousBannerButton.interactable = nextBannerButton && nextBannerButton.interactable;
        for (int i = 0; i < summonButtons.Count; i++)
        {
            int count = i % 2 == 0 ? 1 : 10;
            summonButtons[i].interactable = !loading && !summonInProgress && activeBanner != null &&
                (activeBanner.costs?.Exists(cost => cost.rollCount == count) ?? false) &&
                (pendingRequestId == Guid.Empty || pendingCount == count);
        }
    }

    private string CostLabel(int count)
    {
        GachaCostResponse cost = activeBanner?.costs?.Find(item => item.rollCount == count);
        return cost == null ? "--" : cost.amount.ToString("N0") + " " + cost.currency.ToLowerInvariant();
    }

    private void SetStatus(string message)
    {
        if (statusLabel)
            statusLabel.text = message;
        if (resultStatusLabel) resultStatusLabel.text = message;
        RefreshButtons();
    }

    private RectTransform CreateLayer(string name)
    {
        RectTransform layer = CreateChild(contentRoot ? contentRoot : root, name, Vector2.zero, new Vector2(DesignWidth, DesignHeight));
        Stretch(layer);
        return layer;
    }

    private RectTransform CreateChild(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private Sprite GetRewardSprite(GachaReward reward)
    {
        GachaRewardType type = reward.type;
        if (type == GachaRewardType.Gold)
            return GetSprite("GoldPR.png");
        if (type == GachaRewardType.Diamond)
            return GetSprite("DiamondPR.png");
        if (type == GachaRewardType.Ticket)
            return GetSprite("SummonTicketPR.png");
        return reward.rarity >= 5 ? GetSprite("Skin5StarPR.png") : null;
    }

    private Sprite GetSprite(string fileName)
    {
        if (sprites.TryGetValue(fileName, out Sprite sprite))
            return sprite;
        sprite = LoadSprite(fileName, true);
        sprites[fileName] = sprite;
        return sprite;
    }

    private void LoadSprites()
    {
        // Only reward artwork is used by the sketchbook layout and reveal.
        LoadSpriteToCache("GoldPR.png", true);
        LoadSpriteToCache("DiamondPR.png", true);
        LoadSpriteToCache("SummonTicketPR.png", true);
        LoadSpriteToCache("Skin5StarPR.png", true);
    }

    private void LoadSpriteToCache(string fileName, bool trimTransparent)
    {
        sprites[fileName] = LoadSprite(fileName, trimTransparent);
    }

    private Sprite LoadSprite(string fileName, bool trimTransparent)
    {
        string path = FullAssetPath(fileName);
        if (CoreArtworkCache.GetSprite(path, trimTransparent) is Sprite prepared) return prepared;
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[GachaMenu] Missing asset: {fileName}");
            return null;
        }

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        if (!texture.LoadImage(bytes))
        {
            Destroy(texture);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(fileName);
        Rect rect = trimTransparent ? MenuArtworkBounds.GetRect(AssetFolder + "/" + fileName, texture) : new Rect(0f, 0f, texture.width, texture.height);
        Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
        MenuTextureSampling.FinishRuntimeTexture(texture);
        runtimeSprites.Add(sprite);
        return sprite;
    }

    private static string FullAssetPath(string fileName)
    {
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), AssetFolder, fileName));
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < runtimeSprites.Count; i++)
        {
            if (!runtimeSprites[i])
                continue;
            Texture2D texture = runtimeSprites[i].texture;
            if (Application.isPlaying)
            {
                Destroy(runtimeSprites[i]);
                if (texture) Destroy(texture);
            }
            else
            {
                DestroyImmediate(runtimeSprites[i]);
                if (texture) DestroyImmediate(texture);
            }
        }
    }

}

public sealed class GachaContentRootFitter : MonoBehaviour
{
    private RectTransform rectTransform;
    private float referenceWidth = 1920f;
    private float referenceHeight = 1080f;

    public void Configure(float width, float height)
    {
        referenceWidth = Mathf.Max(1f, width);
        referenceHeight = Mathf.Max(1f, height);
        Apply();
    }

    private void Awake()
    {
        rectTransform = transform as RectTransform;
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        Apply();
    }

    private void Apply()
    {
        if (!rectTransform)
            rectTransform = transform as RectTransform;
        if (!rectTransform)
            return;

        RectTransform parent = rectTransform.parent as RectTransform;
        if (!parent)
            return;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(referenceWidth, referenceHeight);

        float scale = Mathf.Min(parent.rect.width / referenceWidth, parent.rect.height / referenceHeight);
        if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale))
            scale = 1f;
        rectTransform.localScale = new Vector3(scale, scale, 1f);
    }
}

public enum GachaRewardType
{
    Gold,
    Diamond,
    Ticket,
    Skin
}

public readonly struct GachaReward
{
    public readonly GachaRewardType type;
    public readonly int amount;
    public readonly int rarity;
    public readonly bool isTopReward;
    public readonly string itemName;
    public readonly bool isDuplicate;
    public readonly string duplicateText;

    public string displayName => string.IsNullOrWhiteSpace(itemName) ? type.ToString() : itemName;
    public string AmountText => type == GachaRewardType.Skin
        ? displayName + (isDuplicate ? " (duplicate " + duplicateText + ")" : "")
        : $"+{amount:N0}";

    public GachaReward(GachaRewardType type, int amount, int rarity, bool isTopReward,
        string itemName = null, bool isDuplicate = false, string duplicateText = null)
    {
        this.type = type;
        this.amount = amount;
        this.rarity = rarity;
        this.isTopReward = isTopReward;
        this.itemName = itemName;
        this.isDuplicate = isDuplicate;
        this.duplicateText = duplicateText;
    }
}
