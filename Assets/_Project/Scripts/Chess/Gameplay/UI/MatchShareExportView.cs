using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChessButWeird.Application;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI = SketchbookUI;

/// <summary>Post-match modal. Gameplay, rewards and rematch continue to belong to their existing owners.</summary>
public sealed class MatchShareExportView : MonoBehaviour
{
    private static MatchShareExportView activeView;
    public static bool IsAnyOpen => activeView && activeView.canvasRoot;
    public static int EscapeConsumedFrame { get; private set; } = -1;
    private ChessGame game;
    private GameObject canvasRoot;
    private RawImage preview;
    private TextMeshProUGUI status, notationStatus;
    private RectTransform actions;
    private readonly List<Button> buttons = new List<Button>();
    private readonly List<Button> notationButtons = new List<Button>();
    private MatchResultCardRenderer cardRenderer;
    private CancellationTokenSource imageCopyCancellation;
    private Button back, openExports;
    private GameObject previousSelection;
    private bool busy;
    private int version, statisticsRevision;
    private byte[] png;
    private ChessExportResult pgnResult, fenResult;

    public void Show(ChessGame source)
    {
        if (!MatchResultSharing.CanShare(source)) return;
        Close(); game = source;
        previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        try
        {
            Build(); canvasRoot.SetActive(true); activeView = this;
            StartCoroutine(PreparePreview());
        }
        catch { Close(); throw; }
    }

    private void Build()
    {
        canvasRoot = new GameObject("Match Share Export Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        // This component lives beside HandDrawnMenuView, whose Canvas is hidden
        // during play. Nesting below it inherits its canvas sorting/visibility.
        canvasRoot.transform.SetParent(game.transform, false);
        var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2100;
        ResponsiveUi.ConfigureCanvasScaler(canvasRoot.GetComponent<CanvasScaler>(), new Vector2(1920, 1080));
        canvasRoot.AddComponent<Image>().color = new Color(.10f, .09f, .13f, .85f);
        var frame = MenuDesignFrame.Create(canvasRoot.transform, "Share Export", new Vector2(1920, 1080));
        var sheet = UI.Card(frame, "Share Export Sheet", new Rect(80, 100, 1760, 880), UI.Paper, 0, true, 24).rectTransform;
        UI.Text(sheet, "Heading", "SHARE YOUR GAME", new Rect(50, 30, 1000, 70), 49);
        UI.Text(sheet, "Subtitle", "A keepsake for your collection. A story for your friends.", new Rect(52, 112, 1100, 35), 25, UI.Muted);
        back = UI.Button(sheet, "Close", "BACK", new Rect(1500, 32, 208, 64), UI.White, Close, 26);
        var imageFrame = UI.Card(sheet, "Preview Frame", new Rect(42, 186, 1120, 672), UI.White, 0, false, 10).rectTransform;
        preview = UI.Node(imageFrame, "Result Card Preview", new Rect(10, 6, 1100, 660)).gameObject.AddComponent<RawImage>();
        preview.raycastTarget = false;
        actions = UI.Node(sheet, "Share Actions", new Rect(1200, 184, 510, 672));
        Add("Refresh", "REFRESH", 0, Refresh, UI.Blue);
        Add("Save Image", "SAVE IMAGE", 80, SaveImage, UI.Yellow);
        Add("Copy Image", "COPY IMAGE", 160, CopyImage, UI.Green);
        Add("Copy Result", "COPY RESULT", 240, CopyResult, UI.White);
        // Chaos never instantiates notation controls (or their pencil shadows).
        if (MatchExportPolicy.ShowNotation(game.CaptureExportRecord()))
        {
            AddNotation("Export PGN", "EXPORT PGN", 332, 0, ChessExportKind.Pgn, false);
            AddNotation("Copy PGN", "COPY PGN", 332, 265, ChessExportKind.Pgn, true);
            AddNotation("Export FEN", "EXPORT FEN", 410, 0, ChessExportKind.Fen, false);
            AddNotation("Copy FEN", "COPY FEN", 410, 265, ChessExportKind.Fen, true);
            notationStatus = UI.Text(actions, "Notation Availability", "Checking chess data...", new Rect(0, 488, 510, 62), 19, UI.Muted, TextAlignmentOptions.MidlineLeft, true);
        }
        status = UI.Text(actions, "Action Feedback", "Preparing your result card...", new Rect(0, 550, 510, 80), 20, UI.Muted, TextAlignmentOptions.MidlineLeft, true);
#if UNITY_STANDALONE || UNITY_EDITOR
        openExports = UI.Button(actions, "Open Exports", "OPEN EXPORTS FOLDER", new Rect(0, 636, 510, 36), UI.White, OpenExports, 19);
#endif
        statisticsRevision = -1;
        UpdateButtons();
    }

    private void Add(string name, string label, float y, UnityEngine.Events.UnityAction action, Color fill)
    { buttons.Add(UI.Button(actions, name, label, new Rect(0, y, 510, 64), fill, action, 26)); }

    private void AddNotation(string name, string label, float y, float x, ChessExportKind kind, bool copy)
    {
        var button = UI.Button(actions, name, label, new Rect(x, y, 245, 62), UI.Lavender, () => ExportNotation(kind, copy), 24);
        notationButtons.Add(button); buttons.Add(button); button.interactable = false;
    }

    private IEnumerator PreparePreview()
    {
        if (busy) yield break;
        SetBusy(true); Feedback("Rendering your result card...");
        bool built = false;
        try
        {
            png = null; cardRenderer?.Dispose(); cardRenderer = new MatchResultCardRenderer();
            cardRenderer.Build(game); preview.texture = cardRenderer.Texture; built = true;
        }
        catch (Exception error)
        {
            preview.texture = null; cardRenderer?.Dispose(); cardRenderer = null;
            Feedback("Could not render the card: " + PlayerNotificationText.FromException(error) + " Use Refresh to try again.", true);
        }
        // A normal camera frame renders the isolated canvas under the project's URP settings.
        yield return null; yield return null;
        if (built)
        {
            try { png = cardRenderer.EncodePng(); Feedback("Result ready. Copy or save the image."); }
            catch (Exception error) { Feedback("Image unavailable: " + PlayerNotificationText.FromException(error) + " Use Refresh to try again.", true); }
            cardRenderer.Freeze();
        }
        SetBusy(false); RefreshNotation(); back.Select();
    }

    private async void RefreshNotation()
    {
        if (!notationStatus || !game) return;
        int ticket = version; statisticsRevision = game.Statistics.Revision;
        int revision = statisticsRevision;
        try
        {
            MatchExportRecord record = game.CaptureExportRecord();
            var results = await Task.Run(() => new[] {
                MatchExportService.Export(record, ChessExportKind.Pgn), MatchExportService.Export(record, ChessExportKind.Fen) });
            if (!this || ticket != version || !canvasRoot || revision != statisticsRevision) return;
            pgnResult = results[0]; fenResult = results[1];
            notationStatus.text = !fenResult.Success ? fenResult.Error : !pgnResult.Success ? pgnResult.Error : "PGN: full match  /  FEN: final position";
            UpdateButtons();
        }
        catch (Exception error)
        {
            if (!this || ticket != version || !canvasRoot || revision != statisticsRevision) return;
            pgnResult = fenResult = null;
            notationStatus.text = "Could not validate chess data: " + PlayerNotificationText.FromException(error);
            UpdateButtons();
        }
    }

    private void Refresh()
    {
        if (!busy && MatchResultSharing.CanShare(game)) Show(game);
    }

    private async void SaveImage()
    {
        if (busy || png == null || !MatchResultSharing.CanShare(game)) return;
        SetBusy(true); Feedback("Saving image...");
        int ticket = version;
        try
        {
            string path = await MatchExportFiles.SaveAsync(png, game.StatisticsMode, ".png");
            if (!this || ticket != version) return;
            Complete("Image saved", "Image saved:\n" + path);
        }
        catch (Exception error) { if (this && ticket == version) Complete("Image unavailable", "Could not save image: " + PlayerNotificationText.FromException(error), true); }
        finally { if (this && ticket == version) SetBusy(false); }
    }

    private async void CopyImage()
    {
        if (busy || png == null || !MatchResultSharing.CanShare(game)) return;
        SetBusy(true); Feedback("Copying image..."); int ticket = version;
        var cancellation = new CancellationTokenSource(); imageCopyCancellation = cancellation;
        try
        {
            await MatchImageClipboard.CopyPngAsync(png, cancellation.Token);
            if (this && ticket == version) Complete("Image copied", "Image copied. Paste it into an app that accepts images.");
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (this && ticket == version) Complete("Clipboard unavailable", "Could not copy image: " +
                (error is MatchImageClipboard.CopyException ? error.Message : PlayerNotificationText.FromException(error)), true);
        }
        finally
        {
            if (imageCopyCancellation == cancellation) imageCopyCancellation = null;
            cancellation.Dispose();
            if (this && ticket == version) SetBusy(false);
        }
    }

    private void CopyResult()
    {
        if (busy || !MatchResultSharing.CanShare(game)) return;
        try { GUIUtility.systemCopyBuffer = MatchResultSharing.BuildText(game); Complete("Result copied", "Result copied. Paste it into your chat."); }
        catch (Exception error) { Complete("Clipboard unavailable", "Could not copy result: " + PlayerNotificationText.FromException(error), true); }
    }

    private void OpenExports()
    {
        try
        {
            string directory = MatchExportFiles.ExportDirectory;
            System.IO.Directory.CreateDirectory(directory);
            Application.OpenURL(new Uri(directory + System.IO.Path.DirectorySeparatorChar).AbsoluteUri);
        }
        catch (Exception error) { Complete("Folder unavailable", "Could not open exports: " + PlayerNotificationText.FromException(error), true); }
    }

    private async void ExportNotation(ChessExportKind kind, bool copy)
    {
        if (busy || !game) return;
        SetBusy(true); int ticket = version;
        try
        {
            MatchExportRecord record = game.CaptureExportRecord();
            var result = await Task.Run(() => MatchExportService.Export(record, kind));
            if (!this || ticket != version) return;
            if (!result.Success) { Complete("Export unavailable", result.Error, true); return; }
            if (copy) { GUIUtility.systemCopyBuffer = result.Text; Complete(kind.ToString().ToUpperInvariant() + " copied", kind.ToString().ToUpperInvariant() + " copied."); }
            else
            {
                string path = await MatchExportFiles.SaveAsync(new UTF8Encoding(false).GetBytes(result.Text), record.ModeId, kind == ChessExportKind.Pgn ? ".pgn" : ".fen");
                if (this && ticket == version) Complete(kind.ToString().ToUpperInvariant() + " saved", kind.ToString().ToUpperInvariant() + " saved:\n" + path);
            }
        }
        catch (Exception error) { if (this && ticket == version) Complete("Export failed", "Export failed: " + PlayerNotificationText.FromException(error), true); }
        finally { if (this && ticket == version) SetBusy(false); }
    }

    private void Feedback(string text, bool error = false)
    { if (status) { status.text = text; status.color = error ? new Color(.65f, .12f, .16f) : UI.Muted; } }
    private void Complete(string title, string text, bool error = false)
    {
        Feedback(text, error);
        if (Application.isPlaying) AuthNotificationView.Show(title, text,
            error ? AuthNotificationView.ResultKind.Error : AuthNotificationView.ResultKind.Success);
    }
    private void SetBusy(bool value) { busy = value; UpdateButtons(); }
    private void UpdateButtons()
    {
        foreach (var button in buttons) if (button) button.interactable = !busy;
        for (int i = 0; i < notationButtons.Count; i++)
            if (notationButtons[i]) notationButtons[i].interactable = !busy && (i < 2 ? pgnResult?.Success == true : fenResult?.Success == true);
        if (buttons.Count >= 3) { buttons[1].interactable &= png != null; buttons[2].interactable &= png != null && MatchImageClipboard.IsSupported; }
        // Keep arrow-key navigation inside this modal, including while export
        // controls are disabled. The result/rematch screen remains underneath.
        var navigation = new List<Button>();
        if (back) navigation.Add(back);
        foreach (var button in buttons) if (button && button.interactable) navigation.Add(button);
        if (openExports) navigation.Add(openExports);
        for (int i = 0; i < navigation.Count; i++)
            navigation[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = navigation[(i + navigation.Count - 1) % navigation.Count],
                selectOnDown = navigation[(i + 1) % navigation.Count],
                selectOnLeft = navigation[(i + navigation.Count - 1) % navigation.Count],
                selectOnRight = navigation[(i + 1) % navigation.Count] };
    }

    private void Update()
    {
        if (!canvasRoot) return;
        if (!MatchResultSharing.CanShare(game)) { Close(); return; }
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) { Close(); return; }
        if (!busy && notationStatus && statisticsRevision != game.Statistics.Revision) RefreshNotation();
    }

    public void Close()
    {
        if (activeView == this) { activeView = null; EscapeConsumedFrame = Time.frameCount; }
        version++; StopAllCoroutines(); busy = false; png = null;
        imageCopyCancellation?.Cancel(); imageCopyCancellation = null;
        if (preview) preview.texture = null;
        cardRenderer?.Dispose(); cardRenderer = null;
        if (canvasRoot)
        {
            canvasRoot.SetActive(false);
            if (Application.isPlaying) Destroy(canvasRoot); else DestroyImmediate(canvasRoot);
        }
        canvasRoot = null; buttons.Clear(); notationButtons.Clear(); pgnResult = fenResult = null;
        notationStatus = null;
        if (EventSystem.current && previousSelection && previousSelection.activeInHierarchy) EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }
    private void OnDestroy() => Close();
}
