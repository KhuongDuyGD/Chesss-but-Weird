using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UI = SketchbookUI;

public enum PlayModeOption { PveBotRush, ChaosNormal, ClassicNormal, CustomCreateRoom, CustomBrowserRoom, ChaosRanked, ClassicRanked }

public static class PlayModeNames
{
    public static string Label(PlayModeOption mode) => mode == PlayModeOption.PveBotRush ? "PVE / BOT RUSH" :
        mode == PlayModeOption.ChaosRanked ? "CHAOS / RANKED" : mode == PlayModeOption.ClassicRanked ? "CLASSIC / RANKED" :
        mode == PlayModeOption.ChaosNormal ? "CHAOS / NORMAL" : mode == PlayModeOption.ClassicNormal ? "CLASSIC / NORMAL" :
        mode == PlayModeOption.CustomCreateRoom ? "CUSTOM / CREATE ROOM" : "CUSTOM / BROWSER ROOM";
    public static bool IsCustom(PlayModeOption mode) => mode == PlayModeOption.CustomCreateRoom || mode == PlayModeOption.CustomBrowserRoom;
    public static bool IsRanked(PlayModeOption mode) => mode == PlayModeOption.ChaosRanked || mode == PlayModeOption.ClassicRanked;
    public static bool IsChaos(string mode) => string.Equals(mode, "Chaos", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "Aram", StringComparison.OrdinalIgnoreCase);
    public static string Backend(PlayModeOption mode) => mode == PlayModeOption.ChaosNormal || mode == PlayModeOption.ChaosRanked ? "Chaos" : "Classic";
    public static string DisplayText(string value) => (value ?? "").Replace("ARAM", "CHAOS").Replace("Aram", "Chaos");
    public static string DisplayBackend(string mode) => IsChaos(mode) ? "Chaos" : mode ?? "Classic";
}

/// <summary>Expanding mode cards; a category and an available row must both be selected.</summary>
public sealed class PlayModeSelectionView : MonoBehaviour
{
    private readonly CanvasGroup[] rows = new CanvasGroup[4];
    private readonly Button[] cards = new Button[4];
    private readonly Coroutine[] reveals = new Coroutine[4];
    private readonly Color[] colors = { UI.Yellow, UI.Lavender, UI.Blue, UI.Green };
    private Button play;
    private TextMeshProUGUI chosen;
    private int category = -1;
    public PlayModeOption? SelectedMode { get; private set; }

    public void Build(UnityAction back, Action<PlayModeOption> openLobby)
    {
        var root = (RectTransform)transform;
        UI.Background(root);
        var content = UI.Node(root, "Play Mode Content", new Rect(0, 0, 1672, 941));
        content.gameObject.AddComponent<InventoryContentRootFitter>().Configure(1672, 941, .94f);
        UI.Text(content, "Eyebrow", "CHESS BUT WEIRD / PLAY", new Rect(64, 25, 1450, 32), 21, UI.Muted);
        UI.Text(content, "Title", "Choose your game", new Rect(64, 75, 1450, 82), 62);
        UI.Text(content, "Hint", "Open a category, tick a mode, then press Play.", new Rect(64, 167, 1450, 44), 27, UI.Muted);
        var group = content.gameObject.AddComponent<ToggleGroup>();
        group.allowSwitchOff = true;
        string[] titles = { "PVE", "CHAOS", "CLASSIC", "CUSTOM" };
        string[] captions = { "A rival at every level.", "Chess with a twist.", "The familiar battle.", "Your room, your rules." };
        for (int i = 0; i < cards.Length; i++)
        {
            int value = i;
            var frame = UI.Node(content, titles[i] + " Category", new Rect(64 + i * 393, 255, 365, 505));
            cards[i] = UI.Button(frame, titles[i] + " Card", "", new Rect(0, 0, 365, 505), UI.White, () => OpenCategory(value));
            UI.Text(frame, "Category Title", titles[i], new Rect(20, 25, 325, 75), 43, UI.Ink, TextAlignmentOptions.Center);
            UI.Doodle(frame, "Category Doodle", new Rect(137, 111, 90, 90),
                i == 0 ? SketchbookDoodle.Shape.Pawn : i == 1 ? SketchbookDoodle.Shape.Star : i == 3 ? SketchbookDoodle.Shape.Ticket : SketchbookDoodle.Shape.Board, UI.Ink);
            UI.Text(frame, "Caption", captions[i], new Rect(12, 212, 341, 40), 22, UI.Muted, TextAlignmentOptions.Center);
            UI.Card(frame, "Pencil Rule", new Rect(25, 271, 315, 3), colors[i], 0, false, 1);
            var list = UI.Node(frame, "Mode Rows", new Rect(17, 299, 331, 178));
            rows[i] = list.gameObject.AddComponent<CanvasGroup>();
            rows[i].alpha = 0; rows[i].interactable = rows[i].blocksRaycasts = false;
            var option = i == 3 ? PlayModeOption.CustomCreateRoom : (PlayModeOption)i;
            string caption = i == 0 ? "Bot Rush" : i == 3 ? "Create room" : "Normal";
            AddOption(list, titles[i] + " " + caption, caption, 0, option, group);
            if (i == 3) AddOption(list, "CUSTOM Browser room", "Browser room", 91, PlayModeOption.CustomBrowserRoom, group);
            else if (i != 0) AddOption(list, titles[i] + " Ranked", "Ranked", 91, i == 1 ? PlayModeOption.ChaosRanked : PlayModeOption.ClassicRanked, group);
        }
        chosen = UI.Text(content, "Selected Mode", "Choose a mode to continue", new Rect(64, 789, 1050, 50), 28, UI.Muted);
        UI.Button(content, "Back", "<  BACK", new Rect(64, 865, 235, 66), UI.Pink, back, 26);
        play = UI.Button(content, "Play Selected Mode", "PLAY  >", new Rect(1280, 850, 328, 82), UI.Yellow,
            () => { if (SelectedMode.HasValue) openLobby(SelectedMode.Value); }, 32);
        play.interactable = false;
    }

    private void AddOption(Transform parent, string name, string caption, float y, PlayModeOption option, ToggleGroup group)
    {
        ModeRow(parent, name, caption, new Rect(0, y, 331, 72), true, group, active =>
        {
            if (active) { SelectedMode = option; chosen.text = PlayModeNames.Label(option); }
            else if (SelectedMode == option) { SelectedMode = null; chosen.text = "Choose a mode to continue"; }
            play.interactable = SelectedMode.HasValue;
        });
    }

    private static Toggle ModeRow(Transform parent, string name, string caption, Rect area, bool available,
        ToggleGroup group, UnityAction<bool> changed)
    {
        var face = UI.Card(parent, name, area, available ? UI.White : UI.Paper, 0, false, 12);
        face.raycastTarget = true;
        var toggle = face.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = face; toggle.group = group; toggle.interactable = available;
        UI.Card(face.transform, "Checkbox", new Rect(15, 20, 30, 30), UI.White, 0, false, 4);
        // A vector check remains visible even when the project's font has no checkmark glyph.
        var tick = UI.Doodle(face.transform, "Tick", new Rect(15, 20, 30, 30), SketchbookDoodle.Shape.Check, UI.Ink);
        toggle.graphic = tick; toggle.toggleTransition = Toggle.ToggleTransition.None; toggle.isOn = false;
        UI.Text(face.transform, "Mode Name", caption, new Rect(58, 12, area.width - 66, 48), available ? 26 : 21, available ? UI.Ink : UI.Muted);
        if (changed != null) toggle.onValueChanged.AddListener(changed);
        return toggle;
    }

    private void OpenCategory(int value)
    {
        category = value;
        for (int i = 0; i < rows.Length; i++)
        {
            bool open = i == value;
            cards[i].targetGraphic.color = open ? colors[i] : UI.White;
            if (!open) foreach (var option in rows[i].GetComponentsInChildren<Toggle>(true)) option.isOn = false;
            if (reveals[i] != null) StopCoroutine(reveals[i]);
            rows[i].interactable = rows[i].blocksRaycasts = open;
            if (!Application.isPlaying || UserSettings.Presentation.ReducedMotion) rows[i].alpha = open ? 1 : 0;
            else reveals[i] = StartCoroutine(Reveal(rows[i], open));
        }
    }

    private static IEnumerator Reveal(CanvasGroup row, bool open)
    {
        float from = row.alpha, elapsed = 0;
        while (elapsed < .18f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .18f));
            row.alpha = Mathf.Lerp(from, open ? 1 : 0, t);
            ((RectTransform)row.transform).localScale = Vector3.one * Mathf.Lerp(.98f, 1f, row.alpha);
            yield return null;
        }
        row.alpha = open ? 1 : 0;
        ((RectTransform)row.transform).localScale = Vector3.one;
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        for (int i = 0; i < rows.Length; i++)
        {
            reveals[i] = null;
            if (rows[i]) { rows[i].alpha = i == category ? 1 : 0; ((RectTransform)rows[i].transform).localScale = Vector3.one; }
        }
    }
}

