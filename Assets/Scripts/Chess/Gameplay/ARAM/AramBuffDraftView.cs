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
    private Button descriptionButton;
    private TextMeshProUGUI descriptionButtonLabel;
    private bool fullDescriptions;
    private float descriptionDefault;
    private AramBuffDefinition pendingSelection;
    private Action<AramBuffDefinition> onlineSelection;
    private bool onlineCanSelect;
    public bool IsDraftVisible => draftPanel && draftPanel.gameObject.activeInHierarchy;

    // Online options belong to the server and only the local player may pick.
    // Reuse the practice cards without running the local two-player draft.
    public void ShowOnlineDraft(List<AramBuffDefinition> options, PieceTeam team, Action<AramBuffDefinition> onSelected)
    {
        ShowDraft(options, null);
        onlineSelection = onSelected;
        choosingTeam = team;
        titleLabel.text = team.ToString().ToUpperInvariant() + " CHAOS ROLL";
        SetOnlineAvailability(true);
    }

    public void SetOnlineAvailability(bool available)
    {
        onlineCanSelect = available;
        if (!IsDraftVisible || onlineSelection == null) return;
        // Reading remains available while a network selection is pending.
        draftGroup.interactable = true;
        foreach (var card in cards)
        {
            card.Choose.interactable = available;
            card.Motion.SetPending(!available, card.Definition == pendingSelection);
        }
        subtitleLabel.text = available ? "Choose 1 buff for your army. Your opponent chooses separately."
            : "Waiting for the server connection or selection confirmation...";
    }

    public void SetOverlayVisible(bool visible)
    {
        if (canvasRoot) canvasRoot.gameObject.SetActive(visible);
    }

    public bool ContainsPointer(Vector2 screen) => IsDraftVisible &&
        RectTransformUtility.RectangleContainsScreenPoint(draftPanel, screen);
    public float GameplayHudTop
    {get {return 0;}}
    private RectTransform gameplayFrame;

    public void ShowDraft(List<AramBuffDefinition> pool, Action<List<AramBuffDefinition>, List<AramBuffDefinition>> onCompleted)
    {
        EnsureBuilt();
        onlineSelection = null;
        pendingSelection = null;
        descriptionDefault = UserSettings.Get("draft_description");
        fullDescriptions = descriptionDefault > .5f;
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
        onlineSelection = null;
        pendingSelection = null;
        descriptionDefault = UserSettings.Get("draft_description");
        fullDescriptions = descriptionDefault > .5f;
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
        if (IsDraftVisible && !Mathf.Approximately(descriptionDefault, UserSettings.Get("draft_description")))
        {
            descriptionDefault = UserSettings.Get("draft_description");
            fullDescriptions = descriptionDefault > .5f;
            RefreshDescriptions();
        }
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
        Vector2 cardSize = new Vector2(356f, 444f);
        Vector2 gap = new Vector2(24f, 24f);
        float totalWidth = columns * cardSize.x + (columns - 1) * gap.x;
        int rows = Mathf.CeilToInt(Mathf.Max(1, count) / (float)columns);
        float totalHeight = rows * cardSize.y + (rows - 1) * gap.y + 32;
        cardRoot.sizeDelta = new Vector2(1116f, totalHeight);

        for (int i = 0; i < count; i++)
        {
            AramBuffDefinition definition = pool[i];
            int col = i % columns;
            int row = i / columns;
            Vector2 position = new Vector2(
                -totalWidth * 0.5f + cardSize.x * 0.5f + col * (cardSize.x + gap.x),
                -16 - cardSize.y * 0.5f - row * (cardSize.y + gap.y));
            cards.Add(CreateCard(definition, position, cardSize, i));
        }
        cardScroll.vertical = rows > 1;
        cardScroll.verticalNormalizedPosition = 1f;
        RefreshDescriptions();
    }

    private BuffCardView CreateCard(AramBuffDefinition definition, Vector2 position, Vector2 size, int index)
    {
        RectTransform card = CreateRect(cardRoot, definition ? definition.ShortName : $"Buff {index + 1}", position, size);
        card.anchorMin=card.anchorMax=new Vector2(.5f,1);
        var tierValue=definition?definition.Tier:AramBuffTier.Silver;
        Color accent=AramBuffCardTheme.Accent(tierValue), ink=AramBuffCardTheme.Ink(tierValue);
        var background=MatchHudStyle.Surface(card);
        background.Configure(ink,accent,18,2,.22f,index);
        var inset=CreateRect(card,"Inner ink frame",Vector2.zero,size-new Vector2(14,14));
        MatchHudStyle.Surface(inset,false).Configure(Color.clear,new Color(accent.r,accent.g,accent.b,.18f),13,.8f,0,0);
        var filigree=CreateRect(card,"Rarity filigree",Vector2.zero,size).gameObject.AddComponent<AramBuffCardVisual>();
        filigree.ConfigureFrame(tierValue);
        var emblem=CreateRect(card,"Buff emblem",new Vector2(0,126),new Vector2(300,92)).gameObject.AddComponent<AramBuffCardVisual>();
        emblem.Configure(definition);
        var tier=AddText(card,"Tier",new Vector2(0,194),new Vector2(300,28),18,TextAlignmentOptions.Center,accent);
        tier.text=tierValue.ToString().ToUpperInvariant();tier.characterSpacing=6;
        var name=AddText(card,"Name",new Vector2(0,45),new Vector2(300,66),27,TextAlignmentOptions.Center,MatchHudStyle.Paper);
        name.text=definition?definition.DisplayName:"CHAOS Buff";
        var divider=CreateRect(card,"Rule divider",new Vector2(0,0),new Vector2(300,1));
        var line=divider.gameObject.AddComponent<Image>();line.color=new Color(accent.r,accent.g,accent.b,.25f);line.raycastTarget=false;
        var viewport=CreateRect(card,"Description viewport",new Vector2(0,-75),new Vector2(300,126));
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color=Color.clear;
        var desc=AddText(viewport,"Description",Vector2.zero,new Vector2(300,126),19,TextAlignmentOptions.TopLeft,new Color(.87f,.87f,.90f));
        desc.textWrappingMode=TextWrappingModes.Normal;
        desc.rectTransform.anchorMin=desc.rectTransform.anchorMax=desc.rectTransform.pivot=new Vector2(.5f,1);
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=desc.rectTransform;
        scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=28;
        var hint=AddText(card,"Reading hint",new Vector2(-60,-151),new Vector2(180,22),14,TextAlignmentOptions.Left,accent);
        var button=MatchHudStyle.Button(card,"Choose buff","Choose this buff",()=>SelectBuff(definition));
        MatchHudStyle.Place((RectTransform)button.transform,Vector2.one*.5f,new Vector2(300,42),new Vector2(0,-187));
        button.GetComponent<HandDrawnRoundedGraphic>().Configure(AramBuffCardTheme.Paper(tierValue),accent,9,1.2f,.2f,index);
        button.GetComponentInChildren<TMP_Text>().rectTransform.sizeDelta=new Vector2(-16,-4);
        var motion=card.gameObject.AddComponent<AramBuffCardMotion>();motion.Initialize(index);
        var view=new BuffCardView { Definition=definition, Description=desc, Scroll=scroll, Hint=hint, Choose=button, Motion=motion, FullDescription=fullDescriptions };
        var reading=MatchHudStyle.Button(card,"Card reading mode","",()=>
        {
            if(!IsDraftVisible)return;
            view.FullDescription=!view.FullDescription;
            RefreshCardDescription(view);
        });
        MatchHudStyle.Place((RectTransform)reading.transform,Vector2.one*.5f,new Vector2(108,25),new Vector2(96,-151));
        reading.GetComponent<HandDrawnRoundedGraphic>().Configure(new Color(accent.r,accent.g,accent.b,.08f),new Color(accent.r,accent.g,accent.b,.3f),6,.8f,0,0);
        view.ReadingLabel=reading.GetComponentInChildren<TextMeshProUGUI>();
        view.ReadingLabel.color=accent;view.ReadingLabel.fontSize=14;view.ReadingLabel.rectTransform.sizeDelta=new Vector2(-8,-2);
        return view;
    }

    public void ToggleDescriptions()
    {
        if (!IsDraftVisible) return;
        fullDescriptions=!fullDescriptions;
        RefreshDescriptions();
    }

    private void RefreshDescriptions()
    {
        foreach(var card in cards)
        {
            card.FullDescription=fullDescriptions;
            RefreshCardDescription(card);
        }
        if(descriptionButtonLabel)descriptionButtonLabel.text=fullDescriptions?"All cards: Full  /  Summary":"All cards: Summary  /  Full";
    }

    private void RefreshCardDescription(BuffCardView card)
    {
        var definition=card.Definition;
        card.Description.text=definition?(card.FullDescription?definition.Description:definition.Summary):string.Empty;
        float height=Mathf.Max(126,card.Description.GetPreferredValues(card.Description.text,300,0).y+8);
        card.Description.rectTransform.sizeDelta=new Vector2(300,height);
        card.Scroll.vertical=height>127;
        card.Scroll.verticalNormalizedPosition=1;
        card.Hint.text="Scroll to read all";
        card.Hint.gameObject.SetActive(card.Scroll.vertical);
        card.ReadingLabel.text=card.FullDescription?"Summary":"Read full";
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
        if (onlineSelection != null)
        {
            if (definition && onlineCanSelect && IsDraftVisible)
            {
                pendingSelection = definition;
                SetOnlineAvailability(false);
                onlineSelection.Invoke(definition);
            }
            return;
        }
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
        titleLabel.text = choosingTeam == PieceTeam.White ? "WHITE CHAOS ROLL" : "BLACK CHAOS ROLL";
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

        GameObject rootObject = new GameObject("CHAOS Buff Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot = rootObject.GetComponent<RectTransform>();
        canvasRoot.SetParent(transform, false);
        Stretch(canvasRoot);

        Canvas canvas = rootObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 62;
        ResponsiveUi.ConfigureCanvasScaler(rootObject.GetComponent<CanvasScaler>(), new Vector2(ReferenceWidth, ReferenceHeight));

        draftGroup = rootObject.AddComponent<CanvasGroup>();
        RectTransform layout = MenuDesignFrame.Create(canvasRoot, "CHAOS", new Vector2(ReferenceWidth, ReferenceHeight));
        draftPanel = CreateRect(layout, "Draft Panel", Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight));
        Image dim = draftPanel.gameObject.AddComponent<Image>();
        dim.color = new Color(.055f,.065f,.105f,.985f);

        titleLabel = AddText(draftPanel, "Draft Title", new Vector2(0f, 286f), new Vector2(1120f, 60f), 40f, TextAlignmentOptions.Center, MatchHudStyle.Paper);
        subtitleLabel = AddText(draftPanel, "Draft Subtitle", new Vector2(0f, 232f), new Vector2(1120f, 62f), 22f, TextAlignmentOptions.Center, MatchHudStyle.Paper);
        var viewport=CreateRect(draftPanel,"Draft viewport",new Vector2(0,-30),new Vector2(1160,480));
        viewport.gameObject.AddComponent<RectMask2D>();
        var scrollHit=viewport.gameObject.AddComponent<Image>();scrollHit.color=Color.clear;
        cardRoot = CreateRect(viewport, "Draft Cards", Vector2.zero, new Vector2(1116f, 476f));
        cardRoot.anchorMin=cardRoot.anchorMax=cardRoot.pivot=new Vector2(.5f,1);
        cardScroll=viewport.gameObject.AddComponent<ScrollRect>();cardScroll.viewport=viewport;cardScroll.content=cardRoot;
        cardScroll.horizontal=false;cardScroll.movementType=ScrollRect.MovementType.Clamped;cardScroll.scrollSensitivity=40;
        CreateContinueButton(draftPanel);
        descriptionButton=MatchHudStyle.Button(draftPanel,"Draft reading mode","",ToggleDescriptions);
        MatchHudStyle.Place((RectTransform)descriptionButton.transform,Vector2.one*.5f,new Vector2(300,34),new Vector2(408,-310));
        descriptionButton.GetComponent<HandDrawnRoundedGraphic>().Configure(new Color(.14f,.15f,.22f),new Color(.42f,.43f,.53f),8,1,0,0);
        descriptionButtonLabel=descriptionButton.GetComponentInChildren<TextMeshProUGUI>();
        descriptionButtonLabel.color=MatchHudStyle.Paper;descriptionButtonLabel.fontSize=17;
        descriptionButtonLabel.rectTransform.sizeDelta=new Vector2(-16,-4);
        var help=AddText(draftPanel,"Reading preference hint",new Vector2(-210,-310),new Vector2(650,34),17,TextAlignmentOptions.Left,new Color(.61f,.63f,.72f));
        help.text="Default text: Settings / Gameplay / Draft Card Descriptions";

        gameplayFrame=MatchHudStyle.Rect(canvasRoot,"CHAOS Gameplay Safe Area",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        gameplayFrame.gameObject.AddComponent<ResponsiveSafeArea>();
        targetPromptPanel = CreateRect(gameplayFrame, "CHAOS Target Prompt", new Vector2(-140f, 197f), new Vector2(840f, 64f));
        MatchHudStyle.Surface(targetPromptPanel,false);
        targetPromptLabel = AddText(targetPromptPanel, "Target Prompt Label", Vector2.zero, new Vector2(800f, 58f), 20f, TextAlignmentOptions.Center, MatchHudStyle.Ink);
        targetPromptPanel.gameObject.SetActive(false);

        toastPanel=CreateRect(gameplayFrame,"CHAOS Toast Panel",new Vector2(-140,120),new Vector2(840,64));
        MatchHudStyle.Surface(toastPanel,false);
        toastLabel = AddText(toastPanel, "CHAOS Toast", Vector2.zero, new Vector2(800f, 58f), 20f, TextAlignmentOptions.Center, MatchHudStyle.Ink);
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

    private sealed class BuffCardView
    {
        public AramBuffDefinition Definition;
        public TextMeshProUGUI Description, Hint, ReadingLabel;
        public bool FullDescription;
        public ScrollRect Scroll;
        public Button Choose;
        public AramBuffCardMotion Motion;
    }
}
