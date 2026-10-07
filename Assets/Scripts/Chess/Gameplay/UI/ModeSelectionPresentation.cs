using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI = SketchbookUI;

/// <summary>The mode hub, sharing the original logo, paper and button feedback with Main Menu 1.</summary>
public sealed class ModeSelectionPresentation : MonoBehaviour
{
    public sealed class Actions
    {
        public UnityAction Local, Online, Aram, Shop, Inventory, Gacha, Profile, Settings, Back, Logout;
    }

    private static readonly Color Ink = new Color(.15f, .145f, .17f);
    private static readonly Color QuietInk = new Color(.40f, .375f, .37f);
    private CanvasGroup controls;
    private Button local, online, aram, shop, inventory, gacha, profile, settings, back, logout, music;
    private Button[] navigationRing;
    private GameObject selectionBeforeOverlay;

    public Button LogoutButton => logout;

    public void Build(Sprite logo, Actions actions)
    {
        var page = (RectTransform)transform;
        Accent(page, "Top Left Page Corner", new Rect(50, 40, 34, 34), MainMenuInkGraphic.Drawing.Corner, new Color(.36f, .32f, .28f, .24f));
        var corner = Accent(page, "Bottom Right Page Corner", new Rect(1836, 1005, 34, 34), MainMenuInkGraphic.Drawing.Corner, new Color(.36f, .32f, .28f, .24f));
        corner.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);

        var shadow = UI.Image(page, "Original Logo Soft Shadow", logo, new Rect(117, 62, 348, 174));
        shadow.color = new Color(.31f, .26f, .21f, .05f);
        var artwork = UI.Image(page, "Original Chess But Weird Logo", logo, new Rect(115, 58, 348, 174));
        var edge = artwork.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(.08f, .07f, .06f, .26f);
        edge.effectDistance = new Vector2(.45f, -.45f);
        Label(page, "Mode Hub Title", "CHOOSE YOUR CHAOS", new Rect(537, 69, 935, 114), 66, Ink).characterSpacing = 2f;
        Label(page, "Mode Hub Welcome", "Play a little. Get weird a lot.", new Rect(539, 186, 852, 48), 31, QuietInk);

        Label(page, "Modes Section", "MAKE YOUR MOVE", new Rect(124, 278, 466, 43), 30, Ink);
        Accent(page, "Modes Pencil Rule", new Rect(459, 292, 808, 10), MainMenuInkGraphic.Drawing.Underline, new Color(.44f, .41f, .36f, .18f), 1.7f);
        Label(page, "Tools Section", "YOUR CORNER", new Rect(1372, 278, 402, 43), 30, Ink);

        var toolsPanel = UI.Card(page, "Utility Paper Panel", new Rect(1343, 331, 464, 601), new Color(.993f, .985f, .962f, .70f), 0, true, 27);
        toolsPanel.Configure(new Color(.993f, .985f, .962f, .70f), new Color(.54f, .50f, .46f, .18f), 27, 1.4f, 1.1f, 19);

        var content = UI.Node(page, "Mode Hub Controls", new Rect(0, 0, 1920, 1080));
        controls = content.gameObject.AddComponent<CanvasGroup>();
        local = ModeCard(content, "Local", "LOCAL", "Vs bot or two players", "No travel required.", new Rect(120, 342, 560, 274),
            new Color(1f, .84f, .39f), ModeMenuDoodleGraphic.Drawing.Local, actions.Local);
        online = ModeCard(content, "Online", "ONLINE", "Rooms & multiplayer", "Friends. Rivals. Rematches.", new Rect(712, 342, 560, 274),
            new Color(.80f, .87f, .69f), ModeMenuDoodleGraphic.Drawing.Online, actions.Online);
        aram = ModeCard(content, "Aram", "ARAM", "Buffs. Chaos. Chess-ish.", "Expect the unexpected.", new Rect(120, 648, 560, 274),
            new Color(.85f, .80f, .93f), ModeMenuDoodleGraphic.Drawing.Aram, actions.Aram);
        shop = ModeCard(content, "Shop", "SHOP", "Goodies in the making", "COMING SOON", new Rect(712, 648, 560, 274),
            new Color(.78f, .865f, .965f), ModeMenuDoodleGraphic.Drawing.Shop, actions.Shop);

        inventory = UtilityCard(content, "Inventory", "INVENTORY", "Your collected treasures.", 354, ModeMenuDoodleGraphic.Drawing.Inventory, actions.Inventory);
        gacha = UtilityCard(content, "Gacha", "GACHA", "Unbox something strange.", 494, ModeMenuDoodleGraphic.Drawing.Gacha, actions.Gacha);
        profile = UtilityCard(content, "Profile", "PROFILE", "Your very silly face.", 634, ModeMenuDoodleGraphic.Drawing.Profile, actions.Profile);
        settings = UtilityCard(content, "Settings", "SETTINGS", "Tame the weirdness.", 774, ModeMenuDoodleGraphic.Drawing.Settings, actions.Settings);

        back = ButtonFace(content, "Back", new Rect(122, 976, 184, 58), new Color(.983f, .972f, .946f), actions.Back, false);
        Label(back.transform, "Back Label", "BACK", new Rect(16, 6, 152, 45), 29, Ink, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        logout = ButtonFace(content, "Logout", new Rect(1644, 57, 164, 47), new Color(.97f, .954f, .914f), actions.Logout, false);
        Label(logout.transform, "Logout Label", "LOG OUT", new Rect(8, 3, 148, 40), 23, Ink, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        Label(page, "Mode Hub Footer", "Play well. Respect others. Stay weird.", new Rect(361, 985, 897, 42), 26, QuietInk);
        Label(page, "Mode Hub Version", "v" + Application.version, new Rect(1597, 987, 206, 39), 25, QuietInk, TextAlignmentOptions.Right);

        music = page.gameObject.AddComponent<MenuMusicWidget>().Build(page, content, this);

        LinkNavigation();
        navigationRing = new[] { local, online, aram, shop, inventory, gacha, profile, settings, back, logout, music };
    }

    private static Button ModeCard(Transform parent, string name, string title, string description, string note,
        Rect area, Color fill, ModeMenuDoodleGraphic.Drawing drawing, UnityAction action)
    {
        var button = ButtonFace(parent, name, area, fill, action, true);
        var caption = Label(button.transform, name + " Title", title, new Rect(34, 28, 319, 92), 61, Ink);
        caption.fontStyle = FontStyles.Bold;
        caption.characterSpacing = 4f;
        Label(button.transform, name + " Description", description, new Rect(36, 119, 332, 43), 29, Ink);
        var footnote = Label(button.transform, name + " Note", note, new Rect(36, 216, 476, 34), 24, QuietInk);
        if (name == "Shop") { footnote.fontStyle = FontStyles.Bold; footnote.characterSpacing = 3f; }
        Doodle(button.transform, name + " Illustration", new Rect(372, 57, 157, 151), drawing);
        Accent(button.transform, name + " Pencil Highlight", new Rect(28, 14, area.width - 56, 7), MainMenuInkGraphic.Drawing.Underline, new Color(1f, 1f, 1f, .40f), 1.6f);
        return button;
    }

    private static Button UtilityCard(Transform parent, string name, string title, string description, float y,
        ModeMenuDoodleGraphic.Drawing drawing, UnityAction action)
    {
        var button = ButtonFace(parent, name, new Rect(1362, y, 424, 122), new Color(.997f, .988f, .960f), action, false);
        var caption = Label(button.transform, name + " Title", title, new Rect(116, 14, 284, 56), 35, Ink);
        caption.fontStyle = FontStyles.Bold;
        caption.characterSpacing = 1.5f;
        Label(button.transform, name + " Description", description, new Rect(117, 69, 285, 33), 23, QuietInk);
        var icon = Doodle(button.transform, name + " Upgraded Icon", new Rect(20, 19, 86, 86), drawing);
        if (drawing == ModeMenuDoodleGraphic.Drawing.Gacha)
        {
            // Only this tiny icon canvas moves at idle. The remaining page stays cached.
            icon.gameObject.AddComponent<Canvas>();
            icon.gameObject.AddComponent<HandDrawnIdleWiggle>().Configure(.35f, .32f, .0012f, .08f);
        }
        return button;
    }

    private static Button ButtonFace(Transform parent, string name, Rect area, Color fill, UnityAction action, bool primary)
    {
        var holder = UI.Node(parent, name, area);
        for (int i = 2; i >= 0; i--)
        {
            float spread = i * 2.5f;
            var shadow = UI.Card(holder, name + " Soft Shadow " + i, new Rect(-spread, 6 + i * 2, area.width + spread * 2, area.height + spread), Color.clear, 0, false, primary ? 22 : 15);
            shadow.Configure(new Color(.29f, .235f, .17f, .024f + (2 - i) * .020f), Color.clear, primary ? 22 : 15, 0, .7f, 17);
        }
        var face = UI.Card(holder, name + " Hand Drawn Face", new Rect(0, 0, area.width, area.height), fill, 0, false, primary ? 21 : 14);
        face.Configure(fill, Ink, primary ? 21 : 14, primary ? 3.2f : 2.2f, primary ? 1.6f : 1.1f, name.Length * 11);
        face.raycastTarget = true;
        var button = holder.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.highlightedColor = colors.selectedColor = new Color(1f, 1f, .95f);
        colors.pressedColor = new Color(.93f, .90f, .84f);
        colors.disabledColor = Color.white;
        colors.fadeDuration = .12f;
        button.colors = colors;
        if (action != null) button.onClick.AddListener(action);
        holder.gameObject.AddComponent<MainMenuButtonMotion>().Configure();
        return button;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string text, Rect area, float size, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var label = UI.Text(parent, name, text, area, size, color, alignment);
        label.enableAutoSizing = false;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }

    private static ModeMenuDoodleGraphic Doodle(Transform parent, string name, Rect area, ModeMenuDoodleGraphic.Drawing drawing)
    {
        var icon = UI.Node(parent, name, area).gameObject.AddComponent<ModeMenuDoodleGraphic>();
        icon.Configure(drawing);
        return icon;
    }

    private static MainMenuInkGraphic Accent(Transform parent, string name, Rect area, MainMenuInkGraphic.Drawing drawing, Color color, float line = 2.5f)
    {
        var graphic = UI.Node(parent, name, area).gameObject.AddComponent<MainMenuInkGraphic>();
        graphic.Configure(drawing, color, line);
        return graphic;
    }

    private void LinkNavigation()
    {
        Nav(local, logout, aram, null, online); Nav(online, logout, shop, local, inventory);
        Nav(aram, local, back, null, shop); Nav(shop, online, back, aram, profile);
        Nav(inventory, logout, gacha, online, null); Nav(gacha, inventory, profile, online, null);
        Nav(profile, gacha, settings, shop, null); Nav(settings, profile, back, shop, null);
        Nav(back, aram, null, null, settings); Nav(logout, null, music, online, null);
        Nav(music, logout, inventory, online, null);
    }

    private static void Nav(Button button, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right };
    }

    public void SetInputEnabled(bool enabled)
    {
        if (!controls || controls.interactable == enabled) return;
        if (!enabled && EventSystem.current)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected && selected.transform.IsChildOf(controls.transform))
            {
                selectionBeforeOverlay = selected;
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
        controls.interactable = controls.blocksRaycasts = enabled;
        if (enabled && controls.gameObject.activeInHierarchy && EventSystem.current &&
            (!EventSystem.current.currentSelectedGameObject || !EventSystem.current.currentSelectedGameObject.activeInHierarchy) &&
            selectionBeforeOverlay && selectionBeforeOverlay.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(selectionBeforeOverlay);
        if (enabled) selectionBeforeOverlay = null;
    }

    private void Update()
    {
        if (!controls || !controls.interactable || !local.IsInteractable() || !EventSystem.current) return;
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected && selected.activeInHierarchy && !selected.transform.IsChildOf(controls.transform)) return;
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            int index = System.Array.FindIndex(navigationRing, button => button.gameObject == selected);
            bool reverse = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            int next = index < 0 ? (reverse ? navigationRing.Length - 1 : 0) :
                (index + (reverse ? -1 : 1) + navigationRing.Length) % navigationRing.Length;
            EventSystem.current.SetSelectedGameObject(navigationRing[next].gameObject);
        }
        else if ((!selected || !selected.activeInHierarchy) &&
            ((Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame ||
                Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)) ||
             (Gamepad.current != null && (Gamepad.current.dpad.ReadValue().sqrMagnitude > .1f || Gamepad.current.leftStick.ReadValue().sqrMagnitude > .3f))))
            EventSystem.current.SetSelectedGameObject(local.gameObject);
    }

    private void OnDisable()
    {
        if (EventSystem.current && EventSystem.current.currentSelectedGameObject &&
            EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
            EventSystem.current.SetSelectedGameObject(null);
        selectionBeforeOverlay = null;
        if (controls) controls.interactable = controls.blocksRaycasts = true;
    }
}
