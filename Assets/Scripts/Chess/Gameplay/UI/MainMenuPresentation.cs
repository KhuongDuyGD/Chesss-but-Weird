using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI = SketchbookUI;

/// <summary>The first screen, in a 1920 x 1080 safe design frame. Text and controls remain editable Unity UI.</summary>
public sealed class MainMenuPresentation : MonoBehaviour
{
    private static readonly Color Ink = new Color(.15f, .145f, .17f);
    private static readonly Color QuietInk = new Color(.46f, .435f, .42f);
    private static readonly Color Blue = new Color(.22f, .39f, .73f);
    private Button startButton, settingsButton, creditsButton, logoutButton, closeCreditsButton;
    private RectTransform creditsOverlay;
    private CanvasGroup menuControls, creditsGroup;
    private Coroutine creditsTransition;
    private GameObject previousSelection;
    private Button[] navigationRing;

    public void Build(Sprite logo, UnityAction start, UnityAction settings, UnityAction logout)
    {
        var page = (RectTransform)transform;
        // Quiet page corners reinforce the sketchbook without filling the background with repeats.
        InkDoodle(page, "Top Left Page Corner", new Rect(50, 40, 34, 34), MainMenuInkGraphic.Drawing.Corner, new Color(.36f, .32f, .28f, .24f));
        var bottomCorner = InkDoodle(page, "Bottom Right Page Corner", new Rect(1836, 1005, 34, 34), MainMenuInkGraphic.Drawing.Corner, new Color(.36f, .32f, .28f, .24f));
        bottomCorner.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
        UI.Doodle(page, "Chess Club Checker Mark", new Rect(103, 63, 30, 30), SketchbookDoodle.Shape.Board, QuietInk, -5);
        Label(page, "Chess Club Caption", "a very questionable chess club", new Rect(148, 55, 435, 43), 27, QuietInk);
        UI.Card(page, "Early Access Label", new Rect(1400, 59, 193, 40), new Color(.92f, .90f, .83f), -2, false, 6)
            .Configure(new Color(.92f, .90f, .83f), Color.clear, 6, 0, .7f, 11);
        Label(page, "Early Access Text", "EARLY ACCESS", new Rect(1407, 61, 177, 35), 23, QuietInk, TextAlignmentOptions.Center);

        // Only this small child canvas animates continuously; the page and controls stay cached at rest.
        var title = UI.Node(page, "Main Menu Logo Animation", new Rect(490, 118, 940, 470));
        title.gameObject.AddComponent<Canvas>();
        var titleShadow = UI.Image(title, "Main Menu Logo Soft Shadow", logo, new Rect(2, 4, 940, 470));
        titleShadow.color = new Color(.31f, .26f, .21f, .04f);
        var titleArtwork = UI.Image(title, "Main Menu Logo", logo, new Rect(0, 0, 940, 470));
        // A sub-pixel UI ink pass improves small-screen clarity without repainting the drawing or lettering.
        var inkEdge = titleArtwork.gameObject.AddComponent<Outline>();
        inkEdge.effectColor = new Color(.08f, .07f, .06f, .35f);
        inkEdge.effectDistance = new Vector2(.65f, -.65f);
        inkEdge.useGraphicAlpha = true;
        var wobble = title.gameObject.AddComponent<HandDrawnIdleWiggle>();
        wobble.Configure(.7f, .24f, .0015f, .07f);
        Label(page, "Main Menu Tagline", "Same board. Questionable decisions.", new Rect(600, 580, 720, 48), 34, QuietInk, TextAlignmentOptions.Center);

        var controls = UI.Node(page, "Main Menu Controls", new Rect(0, 0, 1920, 1080));
        menuControls = controls.gameObject.AddComponent<CanvasGroup>();
        startButton = MenuButton(controls, "Start", "START", new Rect(641, 663, 638, 119), new Color(1f, .82f, .30f), start, 65f, true);
        settingsButton = MenuButton(controls, "Settings", "SETTINGS", new Rect(641, 815, 306, 85), new Color(.80f, .87f, .69f), settings, 35f);
        creditsButton = MenuButton(controls, "Credits", "CREDITS", new Rect(975, 815, 304, 85), new Color(.85f, .80f, .93f), ShowCredits, 35f);
        logoutButton = MenuButton(controls, "Logout", "LOG OUT", new Rect(1644, 57, 164, 47), new Color(.97f, .954f, .914f), logout, 23f);

        // Four intentional side accents, kept outside the central reading corridor.
        UI.Doodle(page, "Yellow Margin Star", new Rect(400, 305, 33, 33), SketchbookDoodle.Shape.Star, new Color(.94f, .73f, .23f), -14);
        InkDoodle(page, "Blue Margin Spark", new Rect(1469, 417, 35, 35), MainMenuInkGraphic.Drawing.Spark, new Color(.35f, .50f, .73f, .65f), 2.4f);
        var crown = InkDoodle(page, "Daydreaming Crown", new Rect(231, 719, 121, 112), MainMenuInkGraphic.Drawing.Crown, new Color(.42f, .39f, .36f, .58f), 3.2f);
        crown.rectTransform.localRotation = Quaternion.Euler(0, 0, 9);
        var crownCaption = Label(page, "Crown Margin Note", "royally unqualified.", new Rect(168, 853, 258, 36), 26, QuietInk, TextAlignmentOptions.Center);
        crownCaption.rectTransform.localRotation = Quaternion.Euler(0, 0, 6);
        UI.Doodle(page, "Confused Chessboard", new Rect(1586, 791, 79, 79), SketchbookDoodle.Shape.Board, new Color(.46f, .44f, .41f, .50f), -9);
        Label(page, "Chessboard Question", "?", new Rect(1660, 742, 62, 91), 52, Blue, TextAlignmentOptions.Center);
        InkDoodle(page, "Chessboard Pencil Underline", new Rect(1569, 879, 137, 19), MainMenuInkGraphic.Drawing.Underline, new Color(.36f, .48f, .70f, .35f), 2f);

        Label(page, "Friendly Footer", "Play well. Respect others. Stay weird.", new Rect(104, 987, 580, 39), 25, QuietInk);
        Label(page, "Main Menu Version", "v" + Application.version, new Rect(1597, 987, 206, 39), 25, QuietInk, TextAlignmentOptions.Right);
        BuildCredits(page);
        LinkNavigation();
        navigationRing = new[] { startButton, settingsButton, creditsButton, logoutButton };
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string text, Rect area, float size,
        Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var label = UI.Text(parent, name, text, area, size, color, alignment);
        label.enableAutoSizing = false;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }

    private static MainMenuInkGraphic InkDoodle(Transform parent, string name, Rect area,
        MainMenuInkGraphic.Drawing drawing, Color ink, float line = 2.5f)
    {
        var doodle = UI.Node(parent, name, area).gameObject.AddComponent<MainMenuInkGraphic>();
        doodle.Configure(drawing, ink, line);
        return doodle;
    }

    private static Button MenuButton(Transform parent, string name, string caption, Rect area,
        Color fill, UnityAction action, float textSize, bool primary = false)
    {
        // Shadows travel with the button, so its hover never separates from the paper depth.
        var holder = UI.Node(parent, name, area);
        for (int i = 2; i >= 0; i--)
        {
            float spread = i * 3f;
            var shadow = UI.Card(holder, name + " Soft Shadow " + i,
                new Rect(-spread, 7 + i * 2f, area.width + spread * 2, area.height + spread), Color.clear, 0, false, primary ? 20 : 15);
            shadow.Configure(new Color(.29f, .235f, .17f, .028f + (2 - i) * .025f), Color.clear, primary ? 20 : 15, 0, .8f, 7);
        }
        var face = UI.Card(holder, name + " Hand Drawn Face", new Rect(0, 0, area.width, area.height), fill, 0, false, primary ? 19 : 14);
        face.Configure(fill, Ink, primary ? 19 : 14, primary ? 3.7f : 2.5f, primary ? 1.9f : 1.3f, name.Length * 7);
        face.raycastTarget = true;
        var button = holder.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, .94f);
        colors.selectedColor = new Color(1f, 1f, .94f);
        colors.pressedColor = new Color(.93f, .90f, .82f);
        colors.disabledColor = new Color(.75f, .75f, .75f, .65f);
        colors.fadeDuration = .12f;
        button.colors = colors;
        button.onClick.AddListener(action);
        var text = Label(holder, name + " Label", caption, new Rect(primary ? 91 : 48, 2, area.width - (primary ? 182 : 76), area.height - 4), textSize, Ink, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = primary ? 7f : 2f;
        if (primary)
        {
            InkDoodle(holder, "Start Arrow", new Rect(area.width - 94, 37, 46, 43), MainMenuInkGraphic.Drawing.Arrow, Ink, 3.7f);
            InkDoodle(holder, "Start Pencil Highlight", new Rect(35, 14, area.width - 70, 7), MainMenuInkGraphic.Drawing.Underline, new Color(1f, 1f, 1f, .43f), 1.6f);
        }
        else if (name == "Settings") InkDoodle(holder, "Settings Gear", new Rect(23, 28, 29, 29), MainMenuInkGraphic.Drawing.Gear, Ink, 2f);
        else if (name == "Credits") InkDoodle(holder, "Credits Heart", new Rect(25, 29, 29, 28), MainMenuInkGraphic.Drawing.Heart, Ink, 2f);
        holder.gameObject.AddComponent<MainMenuButtonMotion>().Configure();
        return button;
    }

    private void LinkNavigation()
    {
        SetNavigation(startButton, logoutButton, settingsButton, null, null);
        SetNavigation(settingsButton, startButton, null, creditsButton, creditsButton);
        SetNavigation(creditsButton, startButton, null, settingsButton, settingsButton);
        SetNavigation(logoutButton, null, startButton, null, null);
        SetNavigation(closeCreditsButton, closeCreditsButton, closeCreditsButton, closeCreditsButton, closeCreditsButton);
    }

    private static void SetNavigation(Button button, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        button.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right };
    }

    private void BuildCredits(RectTransform page)
    {
        creditsOverlay = UI.Node(page, "Main Menu Credits Overlay", new Rect(0, 0, 1920, 1080));
        creditsGroup = creditsOverlay.gameObject.AddComponent<CanvasGroup>();
        var veil = creditsOverlay.gameObject.AddComponent<AntialiasedMenuImage>();
        veil.color = new Color(.23f, .21f, .19f, .27f);
        UI.Card(creditsOverlay, "Credits Paper Card", new Rect(529, 235, 862, 640), new Color(1f, .988f, .949f), -1, true, 26);
        Label(creditsOverlay, "Credits Title", "THE WEIRD PEOPLE", new Rect(599, 280, 722, 72), 50, Ink, TextAlignmentOptions.Center);
        InkDoodle(creditsOverlay, "Credits Title Underline", new Rect(796, 363, 328, 15), MainMenuInkGraphic.Drawing.Underline, new Color(.80f, .69f, .91f), 3.3f);
        TextAsset copy = Resources.Load<TextAsset>("MainMenu/MainMenuCredits");
        var body = Label(creditsOverlay, "Credits Copy", copy ? copy.text.Trim() : "Chess But Weird\n\nThanks for playing. Stay weird.",
            new Rect(600, 403, 720, 302), 28, QuietInk, TextAlignmentOptions.Center);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Ellipsis;
        closeCreditsButton = MenuButton(creditsOverlay, "Close Credits", "BACK TO THE WEIRD", new Rect(738, 749, 444, 73), new Color(1f, .82f, .30f), HideCredits, 29f);
        creditsOverlay.gameObject.SetActive(false);
    }

    private void ShowCredits()
    {
        if (creditsTransition != null) StopCoroutine(creditsTransition);
        previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        menuControls.interactable = false;
        menuControls.blocksRaycasts = false;
        creditsOverlay.gameObject.SetActive(true);
        creditsGroup.blocksRaycasts = creditsGroup.interactable = true;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(closeCreditsButton.gameObject);
        if (!Application.isPlaying) { creditsGroup.alpha = 1f; return; }
        creditsTransition = StartCoroutine(FadeCredits(true));
    }

    private void HideCredits()
    {
        if (creditsTransition != null) StopCoroutine(creditsTransition);
        creditsGroup.interactable = false;
        if (!Application.isPlaying)
        {
            creditsOverlay.gameObject.SetActive(false);
            menuControls.interactable = menuControls.blocksRaycasts = true;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(previousSelection ? previousSelection : creditsButton.gameObject);
            return;
        }
        creditsTransition = StartCoroutine(FadeCredits(false));
    }

    private IEnumerator FadeCredits(bool show)
    {
        float from = show ? 0f : creditsGroup.alpha;
        for (float elapsed = 0; elapsed < .18f; elapsed += Time.unscaledDeltaTime)
        {
            float progress = elapsed / .18f;
            float ease = 1f - Mathf.Pow(1f - progress, 3);
            creditsGroup.alpha = Mathf.Lerp(from, show ? 1f : 0f, ease);
            yield return null;
        }
        creditsGroup.alpha = show ? 1f : 0f;
        if (!show)
        {
            creditsOverlay.gameObject.SetActive(false);
            menuControls.interactable = menuControls.blocksRaycasts = true;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(previousSelection ? previousSelection : creditsButton.gameObject);
        }
        creditsTransition = null;
    }

    private void Update()
    {
        if (!creditsOverlay) return;
        if (creditsOverlay.gameObject.activeSelf)
        {
            if (creditsGroup.interactable && ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame))) HideCredits();
        }
        else if (EventSystem.current && menuControls.interactable)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame &&
                (!selected || !selected.activeInHierarchy || selected.transform.IsChildOf(menuControls.transform)))
            {
                int index = System.Array.FindIndex(navigationRing, button => button.gameObject == selected);
                bool reverse = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
                int next = index < 0 ? 0 : (index + (reverse ? -1 : 1) + navigationRing.Length) % navigationRing.Length;
                EventSystem.current.SetSelectedGameObject(navigationRing[next].gameObject);
            }
            else if ((!selected || !selected.activeInHierarchy) &&
                ((Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)) ||
                 (Gamepad.current != null && (Gamepad.current.dpad.ReadValue().sqrMagnitude > .1f || Gamepad.current.leftStick.ReadValue().sqrMagnitude > .3f))))
                EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }
    }

    private void OnDisable()
    {
        if (creditsTransition != null) StopCoroutine(creditsTransition);
        creditsTransition = null;
        if (creditsOverlay) creditsOverlay.gameObject.SetActive(false);
        if (menuControls) menuControls.interactable = menuControls.blocksRaycasts = true;
        if (EventSystem.current && EventSystem.current.currentSelectedGameObject &&
            EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
            EventSystem.current.SetSelectedGameObject(null);
    }
}
