using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class SettingsMenuController : MonoBehaviour
{
    private const float DesignWidth = 1672f;
    private const float DesignHeight = 941f;
    private const float MenuViewportScale = 0.88f;
    private static readonly Vector2 DesignSize = new Vector2(DesignWidth, DesignHeight);
    private static readonly int[] FpsOptions = { 30, 60, 90, 120, 144, 165, 240 };
    private static readonly string[] GraphicOptions = { "Low", "Medium", "High" };

    private RectTransform root;
    private RectTransform contentRoot;
    private RectTransform popupRoot;
    private RectTransform activeDropdownPopup;
    private UnityAction closeAction;
    private SettingNumberControl musicControl;
    private SettingNumberControl soundControl;
    private SettingDropdownControl graphicDropdown;
    private SettingDropdownControl fpsDropdown;
    private TextMeshProUGUI statusLabel;
    private int graphicPreset;
    private int fpsValue;

    public void Initialize(RectTransform newRoot, UnityAction newCloseAction)
    {
        root = newRoot;
        closeAction = newCloseAction;
        Build();
    }

    public void Open()
    {
        GameRuntimeSettings.ApplySaved();
        RefreshFromSettings();
        SetStatus("Settings loaded.");
    }

    private void Build()
    {
        Image blocker = root.gameObject.AddComponent<Image>();
        blocker.color = new Color(0.985f, 0.965f, 0.91f, 1f);
        blocker.raycastTarget = true;

        contentRoot = CreateChild(root, "Settings Content Root", Vector2.zero, DesignSize);
        contentRoot.gameObject.AddComponent<InventoryContentRootFitter>().Configure(DesignWidth, DesignHeight, MenuViewportScale);

        BuildPanel();
        popupRoot = CreateChild(contentRoot, "Settings Popup Root", Vector2.zero, DesignSize);
        popupRoot.SetAsLastSibling();
        popupRoot.gameObject.SetActive(false);
    }

    private void BuildPanel()
    {
        RectTransform panel = CreateChild(contentRoot, "Settings Panel", Vector2.zero, new Vector2(1320f, 760f));
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(1f, 0.985f, 0.93f, 1f);
        panelImage.raycastTarget = true;
        AddSolidFrame(panel, "Settings Panel Frame", panel.sizeDelta, 6f, Color.black);

        AddDoodleStroke(panel, "Left Blue Doodle", new Vector2(-515f, 286f), 90f, new Color(0.1f, 0.48f, 1f, 1f));
        AddDoodleStroke(panel, "Right Blue Doodle", new Vector2(515f, 286f), -90f, new Color(0.1f, 0.48f, 1f, 1f));
        AddText(panel, "Settings Title", new Vector2(0f, 286f), new Vector2(720f, 88f), 62f, TextAlignmentOptions.Center, Color.black).text = "Settings UI";

        musicControl = AddNumberControl(panel, "Music", new Vector2(0f, 168f), 0, 100, OnMusicChanged);
        soundControl = AddNumberControl(panel, "Sound", new Vector2(0f, 54f), 0, 100, OnSoundChanged);
        graphicDropdown = AddDropdownControl(panel, "Graphic", new Vector2(0f, -60f), GraphicOptions, OnGraphicDropdownChanged);
        fpsDropdown = AddDropdownControl(panel, "FPS", new Vector2(0f, -174f), GetFpsLabels(), OnFpsDropdownChanged);

        AddButton(panel, "Apply Settings", new Vector2(-290f, -312f), new Vector2(230f, 78f), ApplySettings, "APPLY", new Color(0.86f, 1f, 0.78f, 1f));
        AddButton(panel, "Reset Settings", new Vector2(0f, -312f), new Vector2(230f, 78f), ResetDefaults, "RESET", new Color(1f, 0.96f, 0.72f, 1f));
        AddButton(panel, "Back Settings", new Vector2(290f, -312f), new Vector2(230f, 78f), Close, "BACK", new Color(0.9f, 0.95f, 1f, 1f));
        statusLabel = AddText(panel, "Settings Status", new Vector2(0f, -365f), new Vector2(820f, 36f), 24f, TextAlignmentOptions.Center, new Color(0.18f, 0.14f, 0.1f, 0.82f));
    }

    private SettingNumberControl AddNumberControl(RectTransform parent, string label, Vector2 position, int min, int max, Action<int> onChanged)
    {
        RectTransform row = CreateChild(parent, $"{label} Row", position, new Vector2(1040f, 84f));
        AddText(row, $"{label} Label", new Vector2(-425f, 0f), new Vector2(210f, 54f), 34f, TextAlignmentOptions.MidlineLeft, Color.black).text = $"{label}:";

        Slider slider = CreateSlider(row, $"{label} Slider", new Vector2(-25f, 0f), new Vector2(520f, 34f), min, max);
        TMP_InputField input = CreateInputField(row, $"{label} Input", new Vector2(366f, 0f), new Vector2(126f, 56f), max >= 100 ? 3 : 2);
        AddText(row, $"{label} Percent", new Vector2(454f, 0f), new Vector2(50f, 42f), 24f, TextAlignmentOptions.MidlineLeft, Color.black).text = "%";
        return new SettingNumberControl(slider, input, min, max, onChanged);
    }

    private SettingDropdownControl AddDropdownControl(RectTransform parent, string label, Vector2 position, string[] options, Action<int> onChanged)
    {
        RectTransform row = CreateChild(parent, $"{label} Row", position, new Vector2(1040f, 84f));
        AddText(row, $"{label} Label", new Vector2(-425f, 0f), new Vector2(250f, 54f), 34f, TextAlignmentOptions.MidlineLeft, Color.black).text = $"{label}:";

        RectTransform box = CreateChild(row, $"{label} Dropdown", new Vector2(80f, 0f), new Vector2(520f, 58f));
        Image boxImage = box.gameObject.AddComponent<Image>();
        boxImage.color = new Color(1f, 0.98f, 0.9f, 1f);
        boxImage.raycastTarget = true;
        AddSolidFrame(box, $"{label} Dropdown Frame", box.sizeDelta, 3f, Color.black);
        Button button = box.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = boxImage;
        HandDrawnPressable pressable = box.gameObject.AddComponent<HandDrawnPressable>();
        pressable.Configure(1.015f, 0.98f, 0.25f, new Color(1f, 0.96f, 0.72f, 1f));

        TextMeshProUGUI valueLabel = AddText(box, $"{label} Value", new Vector2(-24f, 0f), new Vector2(400f, 48f), 30f, TextAlignmentOptions.Center, Color.black);
        AddText(box, $"{label} Arrow", new Vector2(220f, 1f), new Vector2(52f, 44f), 29f, TextAlignmentOptions.Center, Color.black).text = "v";

        SettingDropdownControl control = new SettingDropdownControl(this, box, valueLabel, options, onChanged);
        button.onClick.AddListener(control.Toggle);
        return control;
    }

    private Slider CreateSlider(RectTransform parent, string name, Vector2 position, Vector2 size, int min, int max)
    {
        RectTransform sliderRoot = CreateChild(parent, name, position, size);
        Image hitArea = sliderRoot.gameObject.AddComponent<Image>();
        hitArea.color = new Color(1f, 1f, 1f, 0f);
        hitArea.raycastTarget = true;

        Slider slider = sliderRoot.gameObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = true;
        slider.direction = Slider.Direction.LeftToRight;
        slider.transition = Selectable.Transition.None;

        Image track = AddImage(sliderRoot, "Track", null, Vector2.zero, new Vector2(size.x, 16f));
        track.color = new Color(0.12f, 0.1f, 0.08f, 0.18f);
        track.preserveAspect = false;
        track.raycastTarget = true;
        AddSolidFrame(track.rectTransform, "Track Border", track.rectTransform.sizeDelta, 2f, new Color(0.1f, 0.08f, 0.06f, 0.72f));

        RectTransform fillArea = CreateStretchChild(sliderRoot, "Fill Area", new Vector2(19f, 0f), new Vector2(-19f, 0f));
        Image fill = AddImage(fillArea, "Fill", null, Vector2.zero, new Vector2(0f, 16f));
        fill.color = new Color(0.64f, 0.82f, 1f, 0.78f);
        fill.preserveAspect = false;
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(1f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(0f, -8f);
        fillRect.offsetMax = new Vector2(0f, 8f);
        fillRect.anchoredPosition = Vector2.zero;

        RectTransform handleArea = CreateStretchChild(sliderRoot, "Handle Slide Area", new Vector2(19f, 0f), new Vector2(-19f, 0f));
        Image handle = AddImage(handleArea, "Handle", null, Vector2.zero, new Vector2(38f, 38f));
        handle.color = new Color(1f, 0.96f, 0.72f, 1f);
        handle.preserveAspect = false;
        handle.raycastTarget = true;
        AddSolidFrame(handle.rectTransform, "Handle Border", handle.rectTransform.sizeDelta, 3f, Color.black);

        slider.targetGraphic = handle;
        slider.fillRect = fillRect;
        slider.handleRect = handle.rectTransform;
        return slider;
    }

    private TMP_InputField CreateInputField(RectTransform parent, string name, Vector2 position, Vector2 size, int characterLimit)
    {
        GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        RectTransform rect = inputObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image background = inputObject.GetComponent<Image>();
        background.color = Color.white;
        background.raycastTarget = true;
        AddSolidFrame(rect, $"{name} Border", size, 3f, Color.black);

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.characterLimit = characterLimit;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;

        TextMeshProUGUI text = AddText(rect, "Text", Vector2.zero, new Vector2(size.x - 20f, size.y - 8f), 28f, TextAlignmentOptions.Center, Color.black);
        input.textViewport = rect;
        input.textComponent = text;
        return input;
    }

    private void RefreshFromSettings()
    {
        musicControl.SetValue(GameRuntimeSettings.MusicVolumePercent, false);
        soundControl.SetValue(GameRuntimeSettings.SoundVolumePercent, false);
        graphicPreset = QualityToPreset(GameRuntimeSettings.QualityIndex);
        graphicDropdown.SetSelectedIndex(graphicPreset, false);
        fpsValue = ClosestFps(GameRuntimeSettings.TargetFps);
        fpsDropdown.SetSelectedIndex(FpsIndex(fpsValue), false);
    }

    private void OnMusicChanged(int value)
    {
        GameRuntimeSettings.MusicVolumePercent = value;
        SetStatus($"Music volume: {value}%");
    }

    private void OnSoundChanged(int value)
    {
        GameRuntimeSettings.SoundVolumePercent = value;
        SetStatus($"Sound volume: {value}%");
    }

    private void OnGraphicDropdownChanged(int index)
    {
        graphicPreset = Mathf.Clamp(index, 0, GraphicOptions.Length - 1);
        GameRuntimeSettings.QualityIndex = PresetToQuality(graphicPreset);
        graphicDropdown.SetSelectedIndex(graphicPreset, false);
        SetStatus($"Graphic quality: {GraphicOptions[graphicPreset]}");
    }

    private void OnFpsDropdownChanged(int index)
    {
        int clamped = Mathf.Clamp(index, 0, FpsOptions.Length - 1);
        fpsValue = FpsOptions[clamped];
        GameRuntimeSettings.TargetFps = fpsValue;
        fpsDropdown.SetSelectedIndex(clamped, false);
        SetStatus($"FPS cap: {fpsValue}");
    }

    private void ApplySettings()
    {
        GameRuntimeSettings.ApplySaved();
        SetStatus("Applied.");
    }

    private void ResetDefaults()
    {
        GameRuntimeSettings.MusicVolumePercent = 80;
        GameRuntimeSettings.SoundVolumePercent = 100;
        GameRuntimeSettings.ResetPerformanceToRecommended();
        RefreshFromSettings();
        SetStatus("Reset to recommended defaults.");
    }

    private void Close()
    {
        HideDropdown();
        closeAction?.Invoke();
    }

    private void SetStatus(string message)
    {
        if (statusLabel)
            statusLabel.text = message ?? string.Empty;
    }

    private void ShowDropdown(SettingDropdownControl owner, RectTransform anchor, string[] options, int selectedIndex, Action<int> onSelected)
    {
        HideDropdown();
        popupRoot.gameObject.SetActive(true);
        popupRoot.SetAsLastSibling();

        RectTransform row = anchor.parent as RectTransform;
        Vector2 popupPosition = (row ? row.anchoredPosition : Vector2.zero) + anchor.anchoredPosition + new Vector2(0f, -82f);
        RectTransform popup = CreateChild(popupRoot, "Dropdown Popup", popupPosition, new Vector2(anchor.sizeDelta.x, options.Length * 50f + 18f));
        popup.SetAsLastSibling();
        activeDropdownPopup = popup;
        Image background = popup.gameObject.AddComponent<Image>();
        background.color = new Color(1f, 0.985f, 0.93f, 1f);
        background.raycastTarget = true;
        AddSolidFrame(popup, "Dropdown Popup Frame", popup.sizeDelta, 3f, Color.black);

        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            Color color = i == selectedIndex ? new Color(0.86f, 1f, 0.78f, 1f) : new Color(1f, 0.98f, 0.9f, 1f);
            Button option = AddButton(popup, $"Option {i}", new Vector2(0f, popup.sizeDelta.y * 0.5f - 34f - i * 50f), new Vector2(popup.sizeDelta.x - 24f, 42f), () =>
            {
                onSelected?.Invoke(index);
                HideDropdown();
            }, options[i], color, 24f);
            option.transform.SetAsLastSibling();
        }
    }

    private void HideDropdown()
    {
        if (!popupRoot)
            return;

        if (activeDropdownPopup)
            Destroy(activeDropdownPopup.gameObject);
        activeDropdownPopup = null;
        popupRoot.gameObject.SetActive(false);
    }

    private Button AddButton(Transform parent, string name, Vector2 position, Vector2 size, UnityAction action, string text, Color color, float fontSize = 30f)
    {
        Image image = AddImage(parent, name, null, position, size);
        image.raycastTarget = true;
        image.color = color;
        image.preserveAspect = false;
        AddSolidFrame(image.rectTransform, $"{name} Border", size, 3f, Color.black);

        Button button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        HandDrawnPressable pressable = image.gameObject.AddComponent<HandDrawnPressable>();
        pressable.Configure(1.025f, 0.965f, 0.55f, new Color(1f, 0.96f, 0.72f, 1f));

        AddText(image.rectTransform, $"{name} Label", Vector2.zero, size - new Vector2(18f, 8f), fontSize, TextAlignmentOptions.Center, Color.black).text = text;
        return button;
    }

    private Image AddImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI AddText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = ChessFontCatalog.TmpFont != null ? ChessFontCatalog.TmpFont : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(13f, fontSize * 0.6f);
        text.fontSizeMax = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private RectTransform CreateChild(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private RectTransform CreateStretchChild(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    private void AddSolidFrame(RectTransform parent, string name, Vector2 size, float thickness, Color color)
    {
        AddFrameEdge(parent, $"{name} Top", new Vector2(0f, size.y * 0.5f - thickness * 0.5f), new Vector2(size.x, thickness), color);
        AddFrameEdge(parent, $"{name} Bottom", new Vector2(0f, -size.y * 0.5f + thickness * 0.5f), new Vector2(size.x, thickness), color);
        AddFrameEdge(parent, $"{name} Left", new Vector2(-size.x * 0.5f + thickness * 0.5f, 0f), new Vector2(thickness, size.y), color);
        AddFrameEdge(parent, $"{name} Right", new Vector2(size.x * 0.5f - thickness * 0.5f, 0f), new Vector2(thickness, size.y), color);
    }

    private void AddFrameEdge(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject edgeObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = edgeObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = edgeObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void AddDoodleStroke(RectTransform parent, string name, Vector2 position, float rotation, Color color)
    {
        RectTransform rootStroke = CreateChild(parent, name, position, new Vector2(92f, 62f));
        rootStroke.localRotation = Quaternion.Euler(0f, 0f, rotation);
        for (int i = 0; i < 3; i++)
        {
            Image line = AddImage(rootStroke, $"Stroke {i}", null, new Vector2(0f, -20f + i * 20f), new Vector2(62f - i * 8f, 5f));
            line.color = color;
            line.preserveAspect = false;
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -16f + i * 16f);
        }
    }

    private static string[] GetFpsLabels()
    {
        string[] labels = new string[FpsOptions.Length];
        for (int i = 0; i < FpsOptions.Length; i++)
            labels[i] = FpsOptions[i].ToString(CultureInfo.InvariantCulture);
        return labels;
    }

    private static int ClosestFps(int value)
    {
        int best = FpsOptions[0];
        int bestDistance = Mathf.Abs(value - best);
        for (int i = 1; i < FpsOptions.Length; i++)
        {
            int distance = Mathf.Abs(value - FpsOptions[i]);
            if (distance >= bestDistance)
                continue;
            best = FpsOptions[i];
            bestDistance = distance;
        }
        return best;
    }

    private static int FpsIndex(int fps)
    {
        for (int i = 0; i < FpsOptions.Length; i++)
            if (FpsOptions[i] == fps)
                return i;
        return 1;
    }

    private static int QualityToPreset(int qualityIndex)
    {
        int max = Mathf.Max(0, QualitySettings.names.Length - 1);
        if (max <= 1)
            return qualityIndex <= 0 ? 0 : 2;
        float normalized = Mathf.Clamp01(qualityIndex / (float)max);
        if (normalized < 0.34f)
            return 0;
        if (normalized < 0.67f)
            return 1;
        return 2;
    }

    private static int PresetToQuality(int preset)
    {
        int max = Mathf.Max(0, QualitySettings.names.Length - 1);
        if (preset <= 0)
            return 0;
        if (preset == 1)
            return Mathf.RoundToInt(max * 0.5f);
        return max;
    }

    private void OnDestroy()
    {
        HideDropdown();
    }

    private sealed class SettingNumberControl
    {
        private readonly Slider slider;
        private readonly TMP_InputField input;
        private readonly int min;
        private readonly int max;
        private readonly Action<int> onChanged;
        private bool silent;

        public SettingNumberControl(Slider newSlider, TMP_InputField newInput, int newMin, int newMax, Action<int> newOnChanged)
        {
            slider = newSlider;
            input = newInput;
            min = newMin;
            max = newMax;
            onChanged = newOnChanged;
            slider.onValueChanged.AddListener(HandleSliderChanged);
            input.onEndEdit.AddListener(HandleInputEnded);
        }

        public void SetValue(int value, bool notify)
        {
            value = Mathf.Clamp(value, min, max);
            silent = !notify;
            slider.SetValueWithoutNotify(value);
            input.SetTextWithoutNotify(value.ToString(CultureInfo.InvariantCulture));
            silent = false;
            if (notify)
                onChanged?.Invoke(value);
        }

        private void HandleSliderChanged(float value)
        {
            if (silent)
                return;
            SetValue(Mathf.RoundToInt(value), true);
        }

        private void HandleInputEnded(string raw)
        {
            if (silent)
                return;
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                value = Mathf.RoundToInt(slider.value);
            SetValue(value, true);
        }
    }

    private sealed class SettingDropdownControl
    {
        private readonly SettingsMenuController menu;
        private readonly RectTransform anchor;
        private readonly TextMeshProUGUI valueLabel;
        private readonly string[] options;
        private readonly Action<int> onChanged;
        private int selectedIndex;

        public SettingDropdownControl(SettingsMenuController newMenu, RectTransform newAnchor, TextMeshProUGUI newValueLabel, string[] newOptions, Action<int> newOnChanged)
        {
            menu = newMenu;
            anchor = newAnchor;
            valueLabel = newValueLabel;
            options = newOptions;
            onChanged = newOnChanged;
        }

        public void SetSelectedIndex(int index, bool notify)
        {
            selectedIndex = Mathf.Clamp(index, 0, options.Length - 1);
            valueLabel.text = options[selectedIndex];
            if (notify)
                onChanged?.Invoke(selectedIndex);
        }

        public void Toggle()
        {
            menu.ShowDropdown(this, anchor, options, selectedIndex, index => SetSelectedIndex(index, true));
        }
    }
}
