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
        public UnityAction Play, Local, Online, Aram, Shop, Inventory, Gacha, Profile, Settings, Back, Logout;
    }

    private static readonly Color Ink = new Color(.15f, .145f, .17f);
    private static readonly Color QuietInk = new Color(.40f, .375f, .37f);
    private CanvasGroup controls;
    private Button local, shop, inventory, gacha, profile, settings, back, logout, music;
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
        Label(page, "Mode Hub Title", "YOUR NEXT MOVE", new Rect(537, 69, 935, 114), 66, Ink).characterSpacing = 2f;
        Label(page, "Mode Hub Welcome", "Play a little. Get weird a lot.", new Rect(539, 186, 852, 48), 31, QuietInk);

        Label(page, "Modes Section", "MAKE YOUR MOVE", new Rect(124, 278, 1500, 43), 30, Ink);
        var content = UI.Node(page, "Mode Hub Controls", new Rect(0, 0, 1920, 1080));
        controls = content.gameObject.AddComponent<CanvasGroup>();
        local = ModeCard(content, "Play", "PLAY", "Pick your next battle", "PVE · CHAOS · CLASSIC", new Rect(120, 350, 536, 485),
            UI.Yellow, ModeMenuDoodleGraphic.Drawing.Local, actions.Play ?? actions.Local);
        gacha = ModeCard(content, "Gacha", "GACHA", "Unbox something strange", "Discover your next favourite.", new Rect(692, 350, 536, 485),
            UI.Lavender, ModeMenuDoodleGraphic.Drawing.Gacha, actions.Gacha);
        inventory = ModeCard(content, "Inventory", "INVENTORY", "Your collected treasures", "Board, chess and move effects.", new Rect(1264, 350, 536, 485),
            UI.Blue, ModeMenuDoodleGraphic.Drawing.Inventory, actions.Inventory);
        profile = ButtonFace(content, "Profile", new Rect(448, 872, 310, 70), UI.White, actions.Profile, false);
        Label(profile.transform, "Profile Label", "PROFILE", new Rect(12, 8, 286, 54), 29, Ink, TextAlignmentOptions.Center);
        settings = ButtonFace(content, "Settings", new Rect(805, 872, 310, 70), UI.White, actions.Settings, false);
        Label(settings.transform, "Settings Label", "SETTINGS", new Rect(12, 8, 286, 54), 29, Ink, TextAlignmentOptions.Center);
        shop = ButtonFace(content, "Shop", new Rect(1162, 872, 310, 70), UI.Paper, actions.Shop, false);
        Label(shop.transform, "Shop Label", "SHOP · COMING SOON", new Rect(12, 8, 286, 54), 23, Ink, TextAlignmentOptions.Center);

        back = ButtonFace(content, "Back", new Rect(122, 976, 184, 58), new Color(.983f, .972f, .946f), actions.Back, false);
        Label(back.transform, "Back Label", "BACK", new Rect(16, 6, 152, 45), 29, Ink, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        logout = ButtonFace(content, "Logout", new Rect(1644, 57, 164, 47), new Color(.97f, .954f, .914f), actions.Logout, false);
        Label(logout.transform, "Logout Label", "LOG OUT", new Rect(8, 3, 148, 40), 23, Ink, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        Label(page, "Mode Hub Footer", "Play well. Respect others. Stay weird.", new Rect(361, 985, 897, 42), 26, QuietInk);
        Label(page, "Mode Hub Version", "v" + Application.version, new Rect(1597, 987, 206, 39), 25, QuietInk, TextAlignmentOptions.Right);

        music = page.gameObject.AddComponent<MenuMusicWidget>().Build(page, content, this);

        LinkNavigation();
        navigationRing = new[] { local, gacha, inventory, profile, settings, shop, back, logout, music };
    }

    private static Button ModeCard(Transform parent, string name, string title, string description, string note,
        Rect area, Color fill, ModeMenuDoodleGraphic.Drawing drawing, UnityAction action)
    {
        var button = ButtonFace(parent, name, area, fill, action, true);
        var caption = Label(button.transform, name + " Title", title, new Rect(28, 28, area.width - 56, 82), title.Length > 7 ? 43 : 61, Ink);
        caption.fontStyle = FontStyles.Bold;
        caption.characterSpacing = 2f;
        Label(button.transform, name + " Description", description, new Rect(30, 119, area.width - 60, 43), 27, Ink);
        Doodle(button.transform, name + " Illustration", new Rect((area.width - 164) / 2, 206, 164, 164), drawing);
        Label(button.transform, name + " Note", note, new Rect(30, 420, area.width - 60, 35), 23, QuietInk);
        Accent(button.transform, name + " Pencil Highlight", new Rect(28, 14, area.width - 56, 7), MainMenuInkGraphic.Drawing.Underline, new Color(1f, 1f, 1f, .40f), 1.6f);
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
        Nav(local, logout, profile, null, gacha); Nav(gacha, logout, settings, local, inventory);
        Nav(inventory, music, shop, gacha, null);
        Nav(profile, local, back, null, settings); Nav(settings, gacha, back, profile, shop);
        Nav(shop, inventory, back, settings, null); Nav(back, profile, null, null, settings);
        Nav(logout, null, local, inventory, music); Nav(music, logout, inventory, gacha, null);
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
