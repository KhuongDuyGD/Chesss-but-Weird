using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class SettingsMenuController : MonoBehaviour
{
    private const float DesignWidth = 1672f;
    private const float DesignHeight = 941f;
    private const float MenuViewportScale = 0.88f;
    private static readonly Vector2 DesignSize = new Vector2(DesignWidth, DesignHeight);
    private static readonly int[] FpsOptions = { 30, 60, 90, 120, 144, 165, 240 };
    private static readonly string[] GraphicOptions = { "Low", "Medium", "High" };
    private static readonly Color Ink = new Color(0.15f, 0.20f, 0.22f);
    private static readonly Color MutedInk = new Color(0.36f, 0.42f, 0.43f);
    private static readonly Color Paper = new Color(1f, 0.985f, 0.94f);
    private static readonly Color Blue = new Color(0.28f, 0.51f, 0.64f);
    private static readonly Color Sage = new Color(0.77f, 0.86f, 0.73f);

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
    private GameObject dropdownSelection;

    public void Initialize(RectTransform newRoot, UnityAction newCloseAction)
    {
        root = newRoot;
        closeAction = newCloseAction;
        Build();
    }

    public void Open()
    {
        HideDropdown();
        GameRuntimeSettings.ApplySaved();
        RefreshFromSettings();
        SetStatus("All changes are saved automatically.");
    }

    private void Build()
    {
        Image blocker = root.gameObject.AddComponent<Image>();
        blocker.color = new Color(0.82f, 0.87f, 0.84f);
        blocker.raycastTarget = true;

        var safeArea = CreateChild(root, "Settings Safe Area", Vector2.zero, Vector2.zero);
        safeArea.anchorMin = Vector2.zero;
        safeArea.anchorMax = Vector2.one;
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        safeArea.gameObject.AddComponent<ResponsiveSafeArea>();
        contentRoot = CreateChild(safeArea, "Settings Content Root", Vector2.zero, DesignSize);
        contentRoot.gameObject.AddComponent<InventoryContentRootFitter>().Configure(DesignWidth, DesignHeight, MenuViewportScale);

        BuildBackdrop();
        BuildPanel();
        popupRoot = CreateChild(contentRoot, "Settings Popup Root", Vector2.zero, DesignSize);
        Image dismissArea = popupRoot.gameObject.AddComponent<Image>();
        dismissArea.color = new Color(0.10f, 0.16f, 0.15f, 0.08f);
        dismissArea.raycastTarget = true;
        Button dismissPopup = popupRoot.gameObject.AddComponent<Button>();
        dismissPopup.transition = Selectable.Transition.None;
        dismissPopup.navigation = new Navigation { mode = Navigation.Mode.None };
        dismissPopup.onClick.AddListener(HideDropdown);
        popupRoot.SetAsLastSibling();
        popupRoot.gameObject.SetActive(false);
    }

    private void BuildBackdrop()
    {
        Color grid = new Color(0.28f, 0.43f, 0.40f, 0.10f);
        for (int x = -816; x <= 816; x += 48)
            AddFrameEdge(contentRoot, "Notebook Grid Vertical", new Vector2(x, 0f), new Vector2(1.5f, DesignHeight), grid);
        for (int y = -456; y <= 456; y += 48)
            AddFrameEdge(contentRoot, "Notebook Grid Horizontal", new Vector2(0f, y), new Vector2(DesignWidth, 1.5f), grid);

        Image spareSheet = AddImage(contentRoot, "Loose Notebook Page", null, new Vector2(-8f, -4f), new Vector2(1480f, 838f));
        spareSheet.color = new Color(0.91f, 0.92f, 0.85f);
        spareSheet.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -1.4f);
        AddSolidFrame(spareSheet.rectTransform, "Loose Page Edge", spareSheet.rectTransform.sizeDelta, 2f, new Color(0.40f, 0.48f, 0.42f, 0.35f));
        AddDoodleStroke(contentRoot, "Blue Margin Marks", new Vector2(-784f, 270f), 12f, Blue);
        AddDoodleStroke(contentRoot, "Green Margin Marks", new Vector2(788f, -280f), 185f, new Color(0.39f, 0.56f, 0.43f));
    }

    private void BuildPanel()
    {
        Image shadow = AddImage(contentRoot, "Settings Paper Shadow", null, new Vector2(12f, -14f), new Vector2(1460f, 820f));
        shadow.color = new Color(0.12f, 0.20f, 0.18f, 0.17f);
        RectTransform panel = CreateChild(contentRoot, "Settings Panel", Vector2.zero, new Vector2(1460f, 820f));
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = Paper;
        panelImage.raycastTarget = true;
        AddSolidFrame(panel, "Settings Panel Frame", panel.sizeDelta, 3f, Ink);

        Image tape = AddImage(panel, "Blue Paper Tape", null, new Vector2(-480f, 408f), new Vector2(180f, 38f));
        tape.color = new Color(0.63f, 0.78f, 0.83f, 0.85f);
        tape.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 3f);
        AddText(panel, "Settings Eyebrow", new Vector2(-400f, 350f), new Vector2(510f, 30f), 21f, TextAlignmentOptions.MidlineLeft, Blue).text = "CHESS, BUT YOUR WAY";
        AddText(panel, "Settings Title", new Vector2(-400f, 294f), new Vector2(510f, 78f), 68f, TextAlignmentOptions.MidlineLeft, Ink).text = "Settings";
        AddText(panel, "Settings Subtitle", new Vector2(-220f, 231f), new Vector2(870f, 40f), 27f, TextAlignmentOptions.MidlineLeft, MutedInk).text = "A little tuning before your next move.";
        AddButton(panel, "Close Settings", new Vector2(656f, 336f), new Vector2(58f, 58f), Close, "X", Paper, 26f);

        RectTransform audio = BuildSection(panel, "Audio", "Set the mood for your match.", new Vector2(-342f, -19f), new Color(0.91f, 0.95f, 0.95f), Blue);
        RectTransform display = BuildSection(panel, "Display", "Find your balance of detail and speed.", new Vector2(342f, -19f), new Color(0.94f, 0.95f, 0.88f), new Color(0.43f, 0.56f, 0.35f));
        musicControl = AddNumberControl(audio, "Music", new Vector2(0f, 14f), 0, 100, OnMusicChanged);
        soundControl = AddNumberControl(audio, "Sound effects", new Vector2(0f, -125f), 0, 100, OnSoundChanged);
        graphicDropdown = AddDropdownControl(display, "Graphics quality", new Vector2(0f, 14f), GraphicOptions, OnGraphicDropdownChanged);
        fpsDropdown = AddDropdownControl(display, "Frame rate limit", new Vector2(0f, -125f), GetFpsLabels(), OnFpsDropdownChanged);

        AddFrameEdge(panel, "Footer Divider", new Vector2(0f, -270f), new Vector2(1310f, 2f), new Color(0.15f, 0.20f, 0.22f, 0.15f));
        statusLabel = AddText(panel, "Settings Status", new Vector2(-315f, -330f), new Vector2(680f, 48f), 24f, TextAlignmentOptions.MidlineLeft, MutedInk);
        AddButton(panel, "Reset Settings", new Vector2(238f, -336f), new Vector2(240f, 72f), ResetDefaults, "Reset defaults", Paper, 27f);
        AddButton(panel, "Done Settings", new Vector2(537f, -336f), new Vector2(240f, 72f), Close, "Done", Sage, 32f);
    }

    private RectTransform BuildSection(RectTransform parent, string title, string subtitle, Vector2 position, Color color, Color accent)
    {
        Image card = AddImage(parent, title + " Card", null, position, new Vector2(636f, 430f));
        card.color = color;
        AddSolidFrame(card.rectTransform, title + " Card Frame", card.rectTransform.sizeDelta, 2f, new Color(Ink.r, Ink.g, Ink.b, 0.28f));
        AddFrameEdge(card.rectTransform, title + " Accent", new Vector2(-314f, 152f), new Vector2(6f, 58f), accent);
        AddText(card.transform, title + " Heading", new Vector2(-44f, 155f), new Vector2(468f, 52f), 39f, TextAlignmentOptions.MidlineLeft, Ink).text = title;
        AddText(card.transform, title + " Description", new Vector2(0f, 108f), new Vector2(556f, 36f), 23f, TextAlignmentOptions.MidlineLeft, MutedInk).text = subtitle;
        for (int i = 0; i < 3; i++)
        {
            float x = 230f + i * 18f;
            AddFrameEdge(card.rectTransform, title + " Dial Line", new Vector2(x, 155f), new Vector2(3f, 36f), accent);
            AddFrameEdge(card.rectTransform, title + " Dial Handle", new Vector2(x, 147f + (i % 2) * 17f), new Vector2(11f, 7f), accent);
        }
        return card.rectTransform;
    }

    private SettingNumberControl AddNumberControl(RectTransform parent, string label, Vector2 position, int min, int max, Action<int> onChanged)
    {
        RectTransform row = CreateChild(parent, $"{label} Row", position, new Vector2(556f, 112f));
        AddText(row, $"{label} Label", new Vector2(-70f, 31f), new Vector2(416f, 44f), 30f, TextAlignmentOptions.MidlineLeft, Ink).text = label;

        Slider slider = CreateSlider(row, $"{label} Slider", new Vector2(-80f, -25f), new Vector2(396f, 54f), min, max);
        TMP_InputField input = CreateInputField(row, $"{label} Input", new Vector2(204f, -25f), new Vector2(92f, 58f), max >= 100 ? 3 : 2);
        AddText(row, $"{label} Percent", new Vector2(269f, -25f), new Vector2(28f, 42f), 22f, TextAlignmentOptions.MidlineLeft, MutedInk).text = "%";
        return new SettingNumberControl(slider, input, min, max, onChanged);
    }

    private SettingDropdownControl AddDropdownControl(RectTransform parent, string label, Vector2 position, string[] options, Action<int> onChanged)
    {
        RectTransform row = CreateChild(parent, $"{label} Row", position, new Vector2(556f, 112f));
        AddText(row, $"{label} Label", new Vector2(0f, 31f), new Vector2(556f, 44f), 30f, TextAlignmentOptions.MidlineLeft, Ink).text = label;

        RectTransform box = CreateChild(row, $"{label} Dropdown", new Vector2(0f, -25f), new Vector2(556f, 58f));
        Image boxImage = box.gameObject.AddComponent<Image>();
        boxImage.color = Paper;
        boxImage.raycastTarget = true;
        AddSolidFrame(box, $"{label} Dropdown Frame", box.sizeDelta, 2f, Ink);
        Button button = box.gameObject.AddComponent<Button>();
        button.targetGraphic = boxImage;
        ConfigureButtonFocus(button);
        HandDrawnPressable pressable = box.gameObject.AddComponent<HandDrawnPressable>();
        pressable.Configure(1.015f, 0.98f, 0.25f, new Color(1f, 0.96f, 0.72f, 1f));

        TextMeshProUGUI valueLabel = AddText(box, $"{label} Value", new Vector2(-18f, 0f), new Vector2(472f, 48f), 29f, TextAlignmentOptions.MidlineLeft, Ink);
        for (int i = 0; i < 2; i++)
        {
            Image stroke = AddImage(box, "Dropdown Chevron", null, new Vector2(236f + i * 10f, 0f), new Vector2(16f, 3f));
            stroke.color = Ink;
            stroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? -40f : 40f);
        }

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
        track.color = new Color(0.75f, 0.82f, 0.82f);
        track.preserveAspect = false;
        track.raycastTarget = true;
        AddSolidFrame(track.rectTransform, "Track Border", track.rectTransform.sizeDelta, 1.5f, new Color(Ink.r, Ink.g, Ink.b, 0.4f));

        RectTransform fillArea = CreateStretchChild(sliderRoot, "Fill Area", new Vector2(19f, 0f), new Vector2(-19f, 0f));
        Image fill = AddImage(fillArea, "Fill", null, Vector2.zero, new Vector2(0f, 16f));
        fill.color = Blue;
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
        handle.color = Paper;
        handle.preserveAspect = false;
        handle.raycastTarget = true;
        AddSolidFrame(handle.rectTransform, "Handle Border", handle.rectTransform.sizeDelta, 2f, Ink);
        AddFrameEdge(handle.rectTransform, "Grip Left", new Vector2(-4f, 0f), new Vector2(2f, 14f), Blue);
        AddFrameEdge(handle.rectTransform, "Grip Right", new Vector2(4f, 0f), new Vector2(2f, 14f), Blue);

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
        background.color = Paper;
        background.raycastTarget = true;
        AddSolidFrame(rect, $"{name} Border", size, 2f, Ink);

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.characterLimit = characterLimit;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;

        TextMeshProUGUI text = AddText(rect, "Text", Vector2.zero, new Vector2(size.x - 20f, size.y - 8f), 28f, TextAlignmentOptions.Center, Ink);
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
        SetStatus($"Saved: music volume {value}%");
    }

    private void OnSoundChanged(int value)
    {
        GameRuntimeSettings.SoundVolumePercent = value;
        SetStatus($"Saved: sound effects {value}%");
    }

    private void OnGraphicDropdownChanged(int index)
    {
        graphicPreset = Mathf.Clamp(index, 0, GraphicOptions.Length - 1);
        GameRuntimeSettings.QualityIndex = PresetToQuality(graphicPreset);
        graphicDropdown.SetSelectedIndex(graphicPreset, false);
        SetStatus($"Saved: {GraphicOptions[graphicPreset].ToLowerInvariant()} graphics quality");
    }

    private void OnFpsDropdownChanged(int index)
    {
        int clamped = Mathf.Clamp(index, 0, FpsOptions.Length - 1);
        fpsValue = FpsOptions[clamped];
        GameRuntimeSettings.TargetFps = fpsValue;
        fpsDropdown.SetSelectedIndex(clamped, false);
        SetStatus($"Saved: frame rate limit {fpsValue} FPS");
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

    private void ShowDropdown(RectTransform anchor, string[] options, int selectedIndex, Action<int> onSelected)
    {
        HideDropdown();
        popupRoot.gameObject.SetActive(true);
        popupRoot.SetAsLastSibling();

        dropdownSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        // Convert the actual anchor bounds, including the nested section and
        // hover scale, into popup space. Open upward when there is no room below.
        var corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector3 bottomLeft = popupRoot.InverseTransformPoint(corners[0]);
        Vector3 topRight = popupRoot.InverseTransformPoint(corners[2]);
        float height = options.Length * 50f + 18f;
        float lowerBound = -DesignHeight * 0.5f + 24f;
        float upperBound = DesignHeight * 0.5f - 24f;
        float y = bottomLeft.y - 8f - height * 0.5f;
        if (y - height * 0.5f < lowerBound)
            y = topRight.y + 8f + height * 0.5f;
        y = Mathf.Clamp(y, lowerBound + height * 0.5f, upperBound - height * 0.5f);
        float x = Mathf.Clamp((bottomLeft.x + topRight.x) * 0.5f,
            -DesignWidth * 0.5f + anchor.sizeDelta.x * 0.5f + 24f,
            DesignWidth * 0.5f - anchor.sizeDelta.x * 0.5f - 24f);
        RectTransform popup = CreateChild(popupRoot, "Dropdown Popup", new Vector2(x, y), new Vector2(anchor.sizeDelta.x, height));
        popup.SetAsLastSibling();
        activeDropdownPopup = popup;
        Image background = popup.gameObject.AddComponent<Image>();
        background.color = Paper;
        background.raycastTarget = true;
        AddSolidFrame(popup, "Dropdown Popup Frame", popup.sizeDelta, 2f, Ink);

        var optionButtons = new Button[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            Color color = i == selectedIndex ? Sage : Paper;
            Button option = AddButton(popup, $"Option {i}", new Vector2(0f, popup.sizeDelta.y * 0.5f - 34f - i * 50f), new Vector2(popup.sizeDelta.x - 24f, 42f), () =>
            {
                onSelected?.Invoke(index);
                HideDropdown();
            }, options[i], color, 24f);
            option.transform.SetAsLastSibling();
            optionButtons[i] = option;
        }
        for (int i = 0; i < optionButtons.Length; i++)
            optionButtons[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = optionButtons[(i + optionButtons.Length - 1) % optionButtons.Length],
                selectOnDown = optionButtons[(i + 1) % optionButtons.Length]
            };
        optionButtons[Mathf.Clamp(selectedIndex, 0, optionButtons.Length - 1)].Select();
    }

    private void HideDropdown()
    {
        if (!popupRoot)
            return;

        if (activeDropdownPopup)
        {
            activeDropdownPopup.gameObject.SetActive(false);
            Destroy(activeDropdownPopup.gameObject);
        }
        activeDropdownPopup = null;
        popupRoot.gameObject.SetActive(false);
        if (EventSystem.current && dropdownSelection && dropdownSelection.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(dropdownSelection);
        dropdownSelection = null;
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (activeDropdownPopup)
            HideDropdown();
        else
        {
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (selected && selected.TryGetComponent<TMP_InputField>(out var input) && input.isFocused) return;
            Close();
        }
    }

    private void OnDisable()
    {
        HideDropdown();
    }

    private Button AddButton(Transform parent, string name, Vector2 position, Vector2 size, UnityAction action, string text, Color color, float fontSize = 30f)
    {
        Image image = AddImage(parent, name, null, position, size);
        image.raycastTarget = true;
        image.color = color;
        image.preserveAspect = false;
        AddSolidFrame(image.rectTransform, $"{name} Border", size, 2f, Ink);

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ConfigureButtonFocus(button);
        button.onClick.AddListener(action);

        HandDrawnPressable pressable = image.gameObject.AddComponent<HandDrawnPressable>();
        pressable.Configure(1.025f, 0.965f, 0.55f, new Color(1f, 0.96f, 0.72f, 1f));

        AddText(image.rectTransform, $"{name} Label", Vector2.zero, size - new Vector2(18f, 8f), fontSize, TextAlignmentOptions.Center, Ink).text = text;
        return button;
    }

    private static void ConfigureButtonFocus(Button button)
    {
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.selectedColor = new Color(0.76f, 0.88f, 1f);
        colors.pressedColor = new Color(0.69f, 0.80f, 0.88f);
        colors.fadeDuration = 0.1f;
        button.colors = colors;
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
            labels[i] = FpsOptions[i].ToString(CultureInfo.InvariantCulture) + " FPS";
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
            menu.ShowDropdown(anchor, options, selectedIndex, index => SetSelectedIndex(index, true));
        }
    }
}
