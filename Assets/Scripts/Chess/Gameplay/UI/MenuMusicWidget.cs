using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI = SketchbookUI;

public sealed class MenuMusicWidget : MonoBehaviour
{
    private RectTransform record;
    private RectTransform popup;
    private TextMeshProUGUI packName;
    private TextMeshProUGUI[] optionLabels;
    private Button[] options;
    private Button trigger;
    private ModeSelectionPresentation owner;
    private GameMusicPack displayedPack;
    private bool initialized;

    public Button Build(RectTransform page, Transform controls, ModeSelectionPresentation presentation)
    {
        owner = presentation;
        var node = UI.Node(controls, "Music Record", new Rect(1434, 120, 374, 120));
        UI.Text(node, "Now Playing", "NOW PLAYING", new Rect(0, 14, 270, 29), 20, UI.Muted, TextAlignmentOptions.Right);
        packName = UI.Text(node, "Active Music Pack", "CBW Official", new Rect(0, 46, 270, 45), 28, UI.Ink, TextAlignmentOptions.Right);
        record = UI.Node(node, "Spinning Vinyl", new Rect(286, 7, 86, 86));
        var graphic = record.gameObject.AddComponent<MusicRecordGraphic>();
        graphic.raycastTarget = true;
        record.gameObject.AddComponent<Canvas>();
        record.gameObject.AddComponent<GraphicRaycaster>();
        trigger = node.gameObject.AddComponent<Button>();
        trigger.targetGraphic = graphic;
        trigger.transition = Selectable.Transition.None;
        trigger.onClick.AddListener(Open);

        popup = UI.Node(page, "Music Pack Overlay", new Rect(0, 0, 1920, 1080));
        var blocker = popup.gameObject.AddComponent<AntialiasedMenuImage>();
        blocker.color = new Color(0, 0, 0, .13f);
        var dismiss = popup.gameObject.AddComponent<Button>();
        dismiss.targetGraphic = blocker;
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(Close);
        var panel = UI.Card(popup, "Music Pack Picker", new Rect(1264, 112, 548, 636), UI.White, 0, true, 8).rectTransform;
        panel.GetComponent<HandDrawnRoundedGraphic>().raycastTarget = true;
        UI.Text(panel, "Music Heading", "SOUNDTRACK", new Rect(28, 24, 400, 50), 37);
        var close = UI.Button(panel, "Close Music Picker", "x", new Rect(458, 24, 60, 50), UI.Pink, Close, 29);
        var packs = (GameMusicPack[])Enum.GetValues(typeof(GameMusicPack));
        options = new Button[packs.Length];
        optionLabels = new TextMeshProUGUI[packs.Length];
        for (int i = 0; i < packs.Length; i++)
        {
            GameMusicPack pack = packs[i];
            options[i] = UI.Button(panel, "Music Pack " + pack, "", new Rect(28, 106 + i * 98, 490, 78), UI.Paper,
                () => SelectPack(pack), 26);
            UI.Doodle(options[i].transform, "Track Star", new Rect(17, 19, 34, 34), SketchbookDoodle.Shape.Star, UI.Yellow);
            optionLabels[i] = options[i].GetComponentInChildren<TextMeshProUGUI>();
            optionLabels[i].rectTransform.offsetMin = new Vector2(66, optionLabels[i].rectTransform.offsetMin.y);
            optionLabels[i].alignment = TextAlignmentOptions.MidlineLeft;
            options[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = i == 0 ? close : options[i - 1] };
            if (i > 0)
            {
                Navigation nav = options[i - 1].navigation;
                nav.selectOnDown = options[i]; options[i - 1].navigation = nav;
            }
        }
        close.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = options[0], selectOnUp = options[options.Length - 1] };
        popup.gameObject.SetActive(false);
        return trigger;
    }

    private void SelectPack(GameMusicPack pack)
    {
        GameMusicManager.SetActivePack(pack);
        RefreshPack(pack);
    }

    private void Open()
    {
        if (!trigger.IsInteractable()) return;
        owner.SetInputEnabled(false);
        popup.gameObject.SetActive(true);
        RefreshPack(GameMusicManager.ActivePack);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(options[(int)displayedPack].gameObject);
    }

    public void Close()
    {
        if (!popup) return;
        popup.gameObject.SetActive(false);
        owner.SetInputEnabled(true);
        if (EventSystem.current && trigger.gameObject.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(trigger.gameObject);
    }

    private void RefreshPack(GameMusicPack pack)
    {
        initialized = true;
        displayedPack = pack;
        packName.text = GameMusicManager.GetPackDisplayName(pack);
        for (int i = 0; i < options.Length; i++)
        {
            var value = (GameMusicPack)i;
            optionLabels[i].text = GameMusicManager.GetPackDisplayName(value) + (value == pack ? "  /  ON" : "");
            options[i].targetGraphic.color = value == pack ? UI.Green : UI.Paper;
        }
    }

    private void Update()
    {
        if (!record) return;
        GameMusicPack pack = GameMusicManager.ActivePack;
        if (!initialized || displayedPack != pack) RefreshPack(pack);
        if (GameRuntimeSettings.MusicVolume01 > .001f && !UserSettings.Presentation.ReducedMotion)
            SpinRecord(Time.unscaledDeltaTime);
        if (popup.gameObject.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    private void SpinRecord(float deltaTime) => record.Rotate(0, 0, -30f * deltaTime);

    private void OnDisable()
    {
        if (popup) popup.gameObject.SetActive(false);
    }
}
