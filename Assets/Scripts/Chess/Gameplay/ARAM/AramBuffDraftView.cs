using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AramBuffDraftView : MonoBehaviour
{
    private const float ReferenceWidth = 1280f;
    private const float ReferenceHeight = 720f;

    private RectTransform canvasRoot;
    private CanvasGroup draftGroup;
    private RectTransform draftPanel;
    private RectTransform cardRoot;
    private ScrollRect cardScroll;
    private RectTransform toastPanel;
    private RectTransform targetPromptPanel;
    private Button continueButton;
    private TextMeshProUGUI continueButtonLabel;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI subtitleLabel;
    private TextMeshProUGUI targetPromptLabel;
    private TextMeshProUGUI toastLabel;
    private readonly List<BuffCardView> cards = new List<BuffCardView>();
    private readonly List<AramBuffDefinition> whiteOptions = new List<AramBuffDefinition>();
    private readonly List<AramBuffDefinition> blackOptions = new List<AramBuffDefinition>();
    private readonly List<AramBuffDefinition> whiteSelection = new List<AramBuffDefinition>();
    private readonly List<AramBuffDefinition> blackSelection = new List<AramBuffDefinition>();
    private Action<List<AramBuffDefinition>, List<AramBuffDefinition>> completion;
    private PieceTeam choosingTeam = PieceTeam.White;
    private float toastUntil;
    private AramBuffRuntime runtime;
    public float GameplayHudTop
    {get {return 0;}}
    private RectTransform gameplayFrame;

    public void ShowDraft(List<AramBuffDefinition> pool, Action<List<AramBuffDefinition>, List<AramBuffDefinition>> onCompleted)
    {
        EnsureBuilt();
        completion = onCompleted;
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
        SetContinueButtonVisible(false);
        RebuildCards(pool);
        RefreshDraftCopy();
    }

    public void ShowPracticeRoll(List<AramBuffDefinition> rolledWhiteOptions, List<AramBuffDefinition> rolledBlackOptions, Action<List<AramBuffDefinition>, List<AramBuffDefinition>> onCompleted)
    {
        EnsureBuilt();
        completion = onCompleted;
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
        SetContinueButtonVisible(false);
        RebuildCards(whiteOptions);
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
    }

    public void ShowTargetPrompt(PieceTeam team, string message, AramBuffDefinition source)
    {
        EnsureBuilt();
        draftPanel.gameObject.SetActive(false);
        targetPromptPanel.gameObject.SetActive(true);
        draftGroup.blocksRaycasts = false;
        draftGroup.interactable = false;
        draftGroup.alpha = 1f;

        targetPromptLabel.color = MatchHudStyle.Ink;
        targetPromptLabel.text = message;
    }

    public void ShowBuffToast(PieceTeam team, string message, AramBuffDefinition source)
    {
        EnsureBuilt();
        toastLabel.color = MatchHudStyle.Ink;
        toastLabel.text = $"{team}: {message}";
        toastUntil = Time.unscaledTime + 2.4f;
        toastPanel.gameObject.SetActive(true);
    }

    public void HideAll()
    {
        if (!canvasRoot)
            return;

        draftPanel.gameObject.SetActive(false);
        targetPromptPanel.gameObject.SetActive(false);
        toastPanel.gameObject.SetActive(false);
        draftGroup.blocksRaycasts = false;
        draftGroup.interactable = false;
    }

    private void Update()
    {
        if (toastPanel && toastPanel.gameObject.activeSelf && Time.unscaledTime >= toastUntil)
            toastPanel.gameObject.SetActive(false);
        LayoutGameplayHud();
    }

    private void LayoutGameplayHud()
    {
        if(gameplayFrame)
        {
            float width=Mathf.Min(360,gameplayFrame.rect.width-40);
            MatchHudStyle.Place(toastPanel,Vector2.zero,new Vector2(width,68),new Vector2(20+width/2,50));
            toastLabel.rectTransform.sizeDelta=new Vector2(width-24,62);
            MatchHudStyle.Place(targetPromptPanel,new Vector2(0,1),new Vector2(width,140),new Vector2(20+width/2,-190));
            targetPromptLabel.rectTransform.sizeDelta=new Vector2(width-24,132);
        }
    }

    private void RebuildCards(List<AramBuffDefinition> pool)
    {
        ClearCards();

        int count = pool == null ? 0 : pool.Count;
        int columns = 3;
        Vector2 cardSize = new Vector2(356f, 410f);
        Vector2 gap = new Vector2(24f, 24f);
        float totalWidth = columns * cardSize.x + (columns - 1) * gap.x;
        int rows = Mathf.CeilToInt(Mathf.Max(1, count) / (float)columns);
        float totalHeight = rows * cardSize.y + (rows - 1) * gap.y;
        cardRoot.sizeDelta = new Vector2(1116f, totalHeight);

        for (int i = 0; i < count; i++)
        {
            AramBuffDefinition definition = pool[i];
            int col = i % columns;
            int row = i / columns;
            Vector2 position = new Vector2(
                -totalWidth * 0.5f + cardSize.x * 0.5f + col * (cardSize.x + gap.x),
                -cardSize.y * 0.5f - row * (cardSize.y + gap.y));
            cards.Add(CreateCard(definition, position, cardSize, i));
        }
        cardScroll.vertical = rows > 1;
        cardScroll.verticalNormalizedPosition = 1f;
    }

    private BuffCardView CreateCard(AramBuffDefinition definition, Vector2 position, Vector2 size, int index, bool selectable = true, string headingOverride = null)
    {
        RectTransform card = CreateRect(cardRoot, definition ? definition.ShortName : $"Buff {index + 1}", position, size);
        card.anchorMin=card.anchorMax=new Vector2(.5f,1);
        var background = MatchHudStyle.Surface(card);
        Color accent = definition ? definition.AccentColor : new Color(1f, 0.9f, 0.4f, 1f);
        background.Configure(Color.Lerp(MatchHudStyle.Paper,accent,.12f),MatchHudStyle.Ink,12,1.8f,.3f,0);

        if (selectable)
        {
            Button button = MatchHudStyle.Button(card,"Choose buff","Choose this buff",() => SelectBuff(definition));
            MatchHudStyle.Place((RectTransform)button.transform,Vector2.one*.5f,new Vector2(312,40),new Vector2(0,-175));
            button.GetComponentInChildren<TMP_Text>().rectTransform.sizeDelta=new Vector2(-16,-4);
        }

        TextMeshProUGUI tier = AddText(card, "Tier", new Vector2(0f, 174f), new Vector2(312f, 32f), 20f, TextAlignmentOptions.Left, MatchHudStyle.Accent);
        tier.text = definition
            ? $"{(string.IsNullOrEmpty(headingOverride) ? string.Empty : headingOverride + " - ")}{definition.Tier.ToString().ToUpperInvariant()}"
            : (string.IsNullOrEmpty(headingOverride) ? "BUFF" : headingOverride);
        TextMeshProUGUI name = AddText(card, "Name", new Vector2(0f, 112f), new Vector2(312f, 84f), 28f, TextAlignmentOptions.Left, MatchHudStyle.Ink);
        name.text = definition ? definition.DisplayName : "ARAM Buff";
        var viewport=CreateRect(card,"Description viewport",new Vector2(0,-38),new Vector2(312,208));
        viewport.gameObject.AddComponent<RectMask2D>();
        var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;
        TextMeshProUGUI desc = AddText(viewport, "Description", Vector2.zero, new Vector2(312f, 208f), 20f, TextAlignmentOptions.TopLeft, MatchHudStyle.Ink);
        desc.text = definition ? definition.Description : string.Empty;
        desc.textWrappingMode = TextWrappingModes.Normal;
        desc.rectTransform.anchorMin=desc.rectTransform.anchorMax=desc.rectTransform.pivot=new Vector2(.5f,1);
        desc.rectTransform.sizeDelta=new Vector2(312,Mathf.Max(208,desc.GetPreferredValues(desc.text,312,0).y+8));
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=desc.rectTransform;
        scroll.horizontal=false;scroll.vertical=desc.rectTransform.sizeDelta.y>208;scroll.movementType=ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity=28;scroll.verticalNormalizedPosition=1;
        return new BuffCardView();
    }

    private void ClearCards()
    {
        for (int i = cardRoot.childCount - 1; i >= 0; i--)
        {
            // Destroy is deferred: old cards must stop receiving input immediately.
            cardRoot.GetChild(i).gameObject.SetActive(false);
            Destroy(cardRoot.GetChild(i).gameObject);
        }
        cards.Clear();
    }

    private void SelectBuff(AramBuffDefinition definition)
    {
        if (!definition || completion == null || !draftPanel.gameObject.activeSelf)
            return;

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
        RectTransform layout = MenuDesignFrame.Create(canvasRoot, "ARAM", new Vector2(ReferenceWidth, ReferenceHeight));
        draftPanel = CreateRect(layout, "Draft Panel", Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight));
        Image dim = draftPanel.gameObject.AddComponent<Image>();
        dim.color = new Color(.16f,.14f,.20f,.94f);

        titleLabel = AddText(draftPanel, "Draft Title", new Vector2(0f, 284f), new Vector2(1120f, 60f), 40f, TextAlignmentOptions.Center, MatchHudStyle.Paper);
        subtitleLabel = AddText(draftPanel, "Draft Subtitle", new Vector2(0f, 224f), new Vector2(1120f, 62f), 22f, TextAlignmentOptions.Center, MatchHudStyle.Paper);
        var viewport=CreateRect(draftPanel,"Draft viewport",new Vector2(0,-32),new Vector2(1120,412));
        viewport.gameObject.AddComponent<RectMask2D>();
        var scrollHit=viewport.gameObject.AddComponent<Image>();scrollHit.color=Color.clear;
        cardRoot = CreateRect(viewport, "Draft Cards", Vector2.zero, new Vector2(1116f, 410f));
        cardRoot.anchorMin=cardRoot.anchorMax=cardRoot.pivot=new Vector2(.5f,1);
        cardScroll=viewport.gameObject.AddComponent<ScrollRect>();cardScroll.viewport=viewport;cardScroll.content=cardRoot;
        cardScroll.horizontal=false;cardScroll.movementType=ScrollRect.MovementType.Clamped;cardScroll.scrollSensitivity=40;
        CreateContinueButton(draftPanel);

        gameplayFrame=MatchHudStyle.Rect(canvasRoot,"ARAM Gameplay Safe Area",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        gameplayFrame.gameObject.AddComponent<ResponsiveSafeArea>();
        targetPromptPanel = CreateRect(gameplayFrame, "ARAM Target Prompt", new Vector2(-140f, 197f), new Vector2(840f, 64f));
        MatchHudStyle.Surface(targetPromptPanel,false);
        targetPromptLabel = AddText(targetPromptPanel, "Target Prompt Label", Vector2.zero, new Vector2(800f, 58f), 20f, TextAlignmentOptions.Center, MatchHudStyle.Ink);
        targetPromptPanel.gameObject.SetActive(false);

        toastPanel=CreateRect(gameplayFrame,"ARAM Toast Panel",new Vector2(-140,120),new Vector2(840,64));
        MatchHudStyle.Surface(toastPanel,false);
        toastLabel = AddText(toastPanel, "ARAM Toast", Vector2.zero, new Vector2(800f, 58f), 20f, TextAlignmentOptions.Center, MatchHudStyle.Ink);
        toastPanel.gameObject.SetActive(false);
        SetContinueButtonVisible(false);
        LayoutGameplayHud();
    }

    private void CreateContinueButton(Transform parent)
    {
        continueButton=MatchHudStyle.Button(parent,"Practice Continue","Start Practice",CompleteSelection);
        MatchHudStyle.Place((RectTransform)continueButton.transform,Vector2.one*.5f,new Vector2(300,42),new Vector2(0,-280));
        continueButtonLabel=continueButton.GetComponentInChildren<TextMeshProUGUI>();
        continueButtonLabel.rectTransform.sizeDelta=new Vector2(-16,-4);
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
        text.font=ChessFontCatalog.TmpFont;
        text.enableAutoSizing = false;
        text.richText=false;
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

    private readonly struct BuffCardView { }
}
