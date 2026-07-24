using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AramBuffDraftView : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    private RectTransform canvasRoot;
    private CanvasGroup draftGroup;
    private RectTransform draftPanel;
    private RectTransform cardRoot;
    private RectTransform hudPanel;
    private RectTransform targetPromptPanel;
    private Button continueButton;
    private TextMeshProUGUI continueButtonLabel;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI subtitleLabel;
    private TextMeshProUGUI hudWhiteLabel;
    private TextMeshProUGUI hudBlackLabel;
    private TextMeshProUGUI targetPromptLabel;
    private TextMeshProUGUI toastLabel;
    private readonly List<BuffCardView> cards = new List<BuffCardView>();
    private readonly List<AramBuffDefinition> whiteOptions = new List<AramBuffDefinition>();
    private readonly List<AramBuffDefinition> blackOptions = new List<AramBuffDefinition>();
    private readonly List<AramBuffDefinition> whiteSelection = new List<AramBuffDefinition>();
    private readonly List<AramBuffDefinition> blackSelection = new List<AramBuffDefinition>();
    private Action<List<AramBuffDefinition>, List<AramBuffDefinition>> completion;
    private PieceTeam choosingTeam = PieceTeam.White;
    private bool privateNetworkDraft;
    private float toastUntil;

    public void ShowDraft(List<AramBuffDefinition> pool, Action<List<AramBuffDefinition>, List<AramBuffDefinition>> onCompleted)
    {
        EnsureBuilt();
        completion = onCompleted;
        privateNetworkDraft = false;
        choosingTeam = PieceTeam.White;
        whiteOptions.Clear();
        blackOptions.Clear();
        if (pool != null)
        {
            whiteOptions.AddRange(pool);
            blackOptions.AddRange(pool);
        }

        whiteSelection.Clear();
        blackSelection.Clear();
        draftGroup.alpha = 1f;
        draftGroup.blocksRaycasts = true;
        draftGroup.interactable = true;
        draftPanel.gameObject.SetActive(true);
        hudPanel.gameObject.SetActive(false);
        SetContinueButtonVisible(false);
        RebuildCards(pool);
        RefreshDraftCopy();
    }

    public void ShowPracticeRoll(List<AramBuffDefinition> rolledWhiteOptions, List<AramBuffDefinition> rolledBlackOptions, Action<List<AramBuffDefinition>, List<AramBuffDefinition>> onCompleted)
    {
        EnsureBuilt();
        completion = onCompleted;
        privateNetworkDraft = false;
        choosingTeam = PieceTeam.White;
        whiteOptions.Clear();
        blackOptions.Clear();
        if (rolledWhiteOptions != null)
            whiteOptions.AddRange(rolledWhiteOptions);
        if (rolledBlackOptions != null)
            blackOptions.AddRange(rolledBlackOptions);

        whiteSelection.Clear();
        blackSelection.Clear();
        draftGroup.alpha = 1f;
        draftGroup.blocksRaycasts = true;
        draftGroup.interactable = true;
        draftPanel.gameObject.SetActive(true);
        hudPanel.gameObject.SetActive(false);
        SetContinueButtonVisible(false);
        RebuildCards(whiteOptions);
        RefreshDraftCopy();
    }

    public void ShowNetworkRoll(PieceTeam localTeam, List<AramBuffDefinition> localOptions, Action<AramBuffDefinition> onCompleted)
    {
        EnsureBuilt();
        privateNetworkDraft = true;
        choosingTeam = localTeam;
        whiteOptions.Clear();
        blackOptions.Clear();
        if (localTeam == PieceTeam.White)
            whiteOptions.AddRange(localOptions ?? new List<AramBuffDefinition>());
        else
            blackOptions.AddRange(localOptions ?? new List<AramBuffDefinition>());

        whiteSelection.Clear();
        blackSelection.Clear();
        completion = (white, black) =>
        {
            IReadOnlyList<AramBuffDefinition> selected = localTeam == PieceTeam.White ? white : black;
            onCompleted?.Invoke(selected != null && selected.Count > 0 ? selected[0] : null);
        };
        draftGroup.alpha = 1f;
        draftGroup.blocksRaycasts = true;
        draftGroup.interactable = true;
        draftPanel.gameObject.SetActive(true);
        hudPanel.gameObject.SetActive(false);
        SetContinueButtonVisible(false);
        RebuildCards(localTeam == PieceTeam.White ? whiteOptions : blackOptions);
        RefreshDraftCopy();
    }

    public void ShowHud(IReadOnlyList<AramBuffDefinition> whiteBuffs, IReadOnlyList<AramBuffDefinition> blackBuffs)
    {
        EnsureBuilt();
        draftPanel.gameObject.SetActive(false);
        targetPromptPanel.gameObject.SetActive(false);
        draftGroup.blocksRaycasts = false;
        draftGroup.interactable = false;
        draftGroup.alpha = 1f;
        hudPanel.gameObject.SetActive(true);
        RefreshHud(whiteBuffs, blackBuffs);
    }

    public void ShowPrivateHud(PieceTeam localTeam, IReadOnlyList<AramBuffDefinition> localBuffs)
    {
        EnsureBuilt();
        draftPanel.gameObject.SetActive(false);
        targetPromptPanel.gameObject.SetActive(false);
        draftGroup.blocksRaycasts = false;
        draftGroup.interactable = false;
        draftGroup.alpha = 1f;
        hudPanel.gameObject.SetActive(true);
        hudWhiteLabel.text = localTeam == PieceTeam.White ? $"Your buffs: {FormatBuffList(localBuffs)}" : "White buffs: Hidden";
        hudBlackLabel.text = localTeam == PieceTeam.Black ? $"Your buffs: {FormatBuffList(localBuffs)}" : "Black buffs: Hidden";
    }

    public void ShowTargetPrompt(PieceTeam team, string message, AramBuffDefinition source)
    {
        EnsureBuilt();
        draftPanel.gameObject.SetActive(false);
        hudPanel.gameObject.SetActive(false);
        targetPromptPanel.gameObject.SetActive(true);
        draftGroup.blocksRaycasts = false;
        draftGroup.interactable = false;
        draftGroup.alpha = 1f;

        Color color = source ? source.AccentColor : new Color(1f, 0.88f, 0.34f, 1f);
        targetPromptLabel.color = new Color(color.r, color.g, color.b, 1f);
        targetPromptLabel.text = message;
    }

    public void RefreshHud(IReadOnlyList<AramBuffDefinition> whiteBuffs, IReadOnlyList<AramBuffDefinition> blackBuffs)
    {
        if (!hudPanel)
            return;

        hudWhiteLabel.text = $"White: {FormatBuffList(whiteBuffs)}";
        hudBlackLabel.text = $"Black: {FormatBuffList(blackBuffs)}";
    }

    public void ShowBuffToast(PieceTeam team, string message, AramBuffDefinition source)
    {
        EnsureBuilt();
        Color color = source ? source.AccentColor : new Color(1f, 0.88f, 0.34f, 1f);
        toastLabel.color = new Color(color.r, color.g, color.b, 1f);
        toastLabel.text = $"{team}: {message}";
        toastUntil = Time.unscaledTime + 2.4f;
        toastLabel.gameObject.SetActive(true);
    }

    public void HideAll()
    {
        if (!canvasRoot)
            return;

        draftPanel.gameObject.SetActive(false);
        hudPanel.gameObject.SetActive(false);
        targetPromptPanel.gameObject.SetActive(false);
        toastLabel.gameObject.SetActive(false);
        draftGroup.blocksRaycasts = false;
        draftGroup.interactable = false;
    }

    private void Update()
    {
        if (cards.Count > 0)
        {
            float time = Time.unscaledTime;
            for (int i = 0; i < cards.Count; i++)
                cards[i].Tick(time);
        }

        if (toastLabel && toastLabel.gameObject.activeSelf && Time.unscaledTime >= toastUntil)
            toastLabel.gameObject.SetActive(false);
    }

    private void RebuildCards(List<AramBuffDefinition> pool)
    {
        ClearCards();

        int count = pool == null ? 0 : pool.Count;
        int columns = 3;
        Vector2 cardSize = new Vector2(430f, 196f);
        Vector2 gap = new Vector2(38f, 34f);
        float totalWidth = columns * cardSize.x + (columns - 1) * gap.x;
        int rows = Mathf.CeilToInt(Mathf.Max(1, count) / (float)columns);
        float totalHeight = rows * cardSize.y + (rows - 1) * gap.y;

        for (int i = 0; i < count; i++)
        {
            AramBuffDefinition definition = pool[i];
            int col = i % columns;
            int row = i / columns;
            Vector2 position = new Vector2(
                -totalWidth * 0.5f + cardSize.x * 0.5f + col * (cardSize.x + gap.x),
                totalHeight * 0.5f - cardSize.y * 0.5f - row * (cardSize.y + gap.y));
            cards.Add(CreateCard(definition, position, cardSize, i));
        }
    }

    private BuffCardView CreateCard(AramBuffDefinition definition, Vector2 position, Vector2 size, int index, bool selectable = true, string headingOverride = null)
    {
        RectTransform card = CreateRect(cardRoot, definition ? definition.ShortName : $"Buff {index + 1}", position, size);
        Image background = card.gameObject.AddComponent<Image>();
        Color accent = definition ? definition.AccentColor : new Color(1f, 0.9f, 0.4f, 1f);
        background.color = new Color(0.06f, 0.055f, 0.075f, 0.93f);
        Outline outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = accent;
        outline.effectDistance = new Vector2(3f, -3f);

        if (selectable)
        {
            Button button = card.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = background;
            button.onClick.AddListener(() => SelectBuff(definition));
        }

        TextMeshProUGUI tier = AddText(card, "Tier", new Vector2(-155f, 64f), new Vector2(100f, 28f), 21f, TextAlignmentOptions.Left, accent);
        tier.text = definition
            ? $"{(string.IsNullOrEmpty(headingOverride) ? string.Empty : headingOverride + " - ")}{definition.Tier.ToString().ToUpperInvariant()}"
            : (string.IsNullOrEmpty(headingOverride) ? "BUFF" : headingOverride);
        TextMeshProUGUI name = AddText(card, "Name", new Vector2(30f, 46f), new Vector2(320f, 54f), 30f, TextAlignmentOptions.Left, Color.white);
        name.text = definition ? definition.DisplayName : "ARAM Buff";
        TextMeshProUGUI desc = AddText(card, "Description", new Vector2(0f, -34f), new Vector2(360f, 82f), 21f, TextAlignmentOptions.TopLeft, new Color(0.93f, 0.92f, 0.86f, 1f));
        desc.text = definition ? definition.Description : string.Empty;
        desc.textWrappingMode = TextWrappingModes.Normal;
        return new BuffCardView(card, background, accent, index);
    }

    private void ClearCards()
    {
        for (int i = cardRoot.childCount - 1; i >= 0; i--)
            Destroy(cardRoot.GetChild(i).gameObject);
        cards.Clear();
    }

    private void SelectBuff(AramBuffDefinition definition)
    {
        if (!definition)
            return;

        if (privateNetworkDraft)
        {
            if (choosingTeam == PieceTeam.White)
                whiteSelection.Add(definition);
            else
                blackSelection.Add(definition);
            CompleteSelection();
            return;
        }

        if (choosingTeam == PieceTeam.White)
        {
            whiteSelection.Clear();
            whiteSelection.Add(definition);
            choosingTeam = PieceTeam.Black;
            RebuildCards(blackOptions);
            RefreshDraftCopy();
            return;
        }

        blackSelection.Clear();
        blackSelection.Add(definition);
        CompleteSelection();
    }

    private void RefreshDraftCopy()
    {
        if (privateNetworkDraft)
        {
            List<AramBuffDefinition> options = choosingTeam == PieceTeam.White ? whiteOptions : blackOptions;
            titleLabel.text = "YOUR PRIVATE ARAM ROLL";
            subtitleLabel.text = $"Choose 1 {FormatTierList(options)} buff. Your opponent cannot see this buff.";
            return;
        }

        titleLabel.text = choosingTeam == PieceTeam.White ? "WHITE ARAM ROLL" : "BLACK ARAM ROLL";
        subtitleLabel.text = choosingTeam == PieceTeam.White
            ? $"Choose 1 buff from {FormatTierList(whiteOptions)} options."
            : $"White picked {FormatBuffList(whiteSelection)}. Choose 1 buff from {FormatTierList(blackOptions)} options.";
    }

    private void CompleteSelection()
    {
        SetContinueButtonVisible(false);
        draftPanel.gameObject.SetActive(false);
        Action<List<AramBuffDefinition>, List<AramBuffDefinition>> callback = completion;
        completion = null;
        privateNetworkDraft = false;
        callback?.Invoke(new List<AramBuffDefinition>(whiteSelection), new List<AramBuffDefinition>(blackSelection));
    }

    private static string FormatBuffList(IReadOnlyList<AramBuffDefinition> buffs)
    {
        if (buffs == null || buffs.Count == 0)
            return "None";

        List<string> names = new List<string>();
        for (int i = 0; i < buffs.Count; i++)
            if (buffs[i])
                names.Add(buffs[i].ShortName);
        return names.Count == 0 ? "None" : string.Join(", ", names);
    }

    private static string FormatTierList(IReadOnlyList<AramBuffDefinition> buffs)
    {
        if (buffs == null || buffs.Count == 0)
            return "rolled";

        AramBuffDefinition first = null;
        for (int i = 0; i < buffs.Count && !first; i++)
            first = buffs[i];

        return first ? first.Tier.ToString() : "rolled";
    }

    private void EnsureBuilt()
    {
        if (canvasRoot)
            return;

        GameObject rootObject = new GameObject("ARAM Buff Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot = rootObject.GetComponent<RectTransform>();
        canvasRoot.SetParent(transform, false);
        Stretch(canvasRoot);

        Canvas canvas = rootObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 62;
        ResponsiveUi.ConfigureCanvasScaler(rootObject.GetComponent<CanvasScaler>(), new Vector2(ReferenceWidth, ReferenceHeight));

        draftGroup = rootObject.AddComponent<CanvasGroup>();
        draftPanel = CreateRect(canvasRoot, "Draft Panel", Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight));
        Image dim = draftPanel.gameObject.AddComponent<Image>();
        dim.color = new Color(0.015f, 0.012f, 0.022f, 0.88f);

        titleLabel = AddText(draftPanel, "Draft Title", new Vector2(0f, 390f), new Vector2(900f, 72f), 54f, TextAlignmentOptions.Center, new Color(1f, 0.88f, 0.36f, 1f));
        subtitleLabel = AddText(draftPanel, "Draft Subtitle", new Vector2(0f, 330f), new Vector2(1180f, 44f), 26f, TextAlignmentOptions.Center, new Color(0.88f, 0.92f, 1f, 0.92f));
        cardRoot = CreateRect(draftPanel, "Draft Cards", new Vector2(0f, -40f), new Vector2(1440f, 520f));
        CreateContinueButton(draftPanel);

        hudPanel = CreateRect(canvasRoot, "ARAM HUD", new Vector2(-685f, 410f), new Vector2(500f, 126f));
        Image hud = hudPanel.gameObject.AddComponent<Image>();
        hud.color = new Color(0.025f, 0.023f, 0.035f, 0.76f);
        Outline hudOutline = hudPanel.gameObject.AddComponent<Outline>();
        hudOutline.effectColor = new Color(1f, 0.82f, 0.26f, 0.55f);
        hudOutline.effectDistance = new Vector2(2f, -2f);
        AddText(hudPanel, "HUD Title", new Vector2(0f, 38f), new Vector2(460f, 30f), 24f, TextAlignmentOptions.Center, new Color(1f, 0.88f, 0.34f, 1f)).text = "ARAM BUFFS";
        hudWhiteLabel = AddText(hudPanel, "HUD White", new Vector2(0f, 4f), new Vector2(452f, 28f), 20f, TextAlignmentOptions.Left, Color.white);
        hudBlackLabel = AddText(hudPanel, "HUD Black", new Vector2(0f, -28f), new Vector2(452f, 28f), 20f, TextAlignmentOptions.Left, new Color(0.86f, 0.92f, 1f, 1f));
        hudPanel.gameObject.SetActive(false);

        targetPromptPanel = CreateRect(canvasRoot, "ARAM Target Prompt", new Vector2(0f, 410f), new Vector2(1120f, 76f));
        Image targetPromptBackground = targetPromptPanel.gameObject.AddComponent<Image>();
        targetPromptBackground.color = new Color(0.025f, 0.023f, 0.035f, 0.82f);
        targetPromptBackground.raycastTarget = false;
        Outline targetPromptOutline = targetPromptPanel.gameObject.AddComponent<Outline>();
        targetPromptOutline.effectColor = new Color(1f, 0.82f, 0.26f, 0.55f);
        targetPromptOutline.effectDistance = new Vector2(2f, -2f);
        targetPromptLabel = AddText(targetPromptPanel, "Target Prompt Label", Vector2.zero, new Vector2(1060f, 54f), 28f, TextAlignmentOptions.Center, new Color(1f, 0.88f, 0.34f, 1f));
        targetPromptPanel.gameObject.SetActive(false);

        toastLabel = AddText(canvasRoot, "ARAM Toast", new Vector2(0f, 356f), new Vector2(920f, 54f), 30f, TextAlignmentOptions.Center, new Color(1f, 0.88f, 0.3f, 1f));
        toastLabel.gameObject.SetActive(false);
        SetContinueButtonVisible(false);
    }

    private void CreateContinueButton(Transform parent)
    {
        RectTransform buttonRect = CreateRect(parent, "Practice Continue", new Vector2(0f, -365f), new Vector2(330f, 76f));
        Image image = buttonRect.gameObject.AddComponent<Image>();
        image.color = new Color(1f, 0.78f, 0.24f, 0.96f);
        Outline outline = buttonRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.08f, 0.06f, 0.05f, 0.95f);
        outline.effectDistance = new Vector2(3f, -3f);

        continueButton = buttonRect.gameObject.AddComponent<Button>();
        continueButton.transition = Selectable.Transition.None;
        continueButton.targetGraphic = image;
        continueButton.onClick.AddListener(CompleteSelection);
        continueButtonLabel = AddText(buttonRect, "Label", Vector2.zero, new Vector2(300f, 54f), 30f, TextAlignmentOptions.Center, new Color(0.11f, 0.08f, 0.04f, 1f));
        continueButtonLabel.text = "Start Practice";
    }

    private void SetContinueButtonVisible(bool visible)
    {
        if (continueButton)
            continueButton.gameObject.SetActive(visible);
    }

    private static TextMeshProUGUI AddText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = CreateRect(parent, name, position, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(12f, fontSize * 0.62f);
        text.fontSizeMax = fontSize;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private readonly struct BuffCardView
    {
        private readonly RectTransform rect;
        private readonly Image image;
        private readonly Color accent;
        private readonly float offset;

        public BuffCardView(RectTransform rect, Image image, Color accent, int index)
        {
            this.rect = rect;
            this.image = image;
            this.accent = accent;
            offset = index * 0.42f;
        }

        public void Tick(float time)
        {
            if (!rect || !image)
                return;

            float pulse = 0.5f + Mathf.Sin(time * 2.8f + offset) * 0.5f;
            rect.localScale = Vector3.one * (1f + pulse * 0.012f);
            image.color = Color.Lerp(new Color(0.055f, 0.052f, 0.072f, 0.93f), new Color(accent.r * 0.18f, accent.g * 0.18f, accent.b * 0.18f, 0.95f), pulse * 0.32f);
        }
    }
}
