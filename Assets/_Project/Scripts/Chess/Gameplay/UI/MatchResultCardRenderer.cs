using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using ChessButWeird.Application;
using UI = SketchbookUI;

/// <summary>A scoped camera renders only the share card, independent of screen resolution.</summary>
public sealed class MatchResultCardRenderer : IDisposable
{
    public const int Width = 1200, Height = 720;
    private GameObject stage;
    private Camera camera;
    private RenderTexture target;
    private TMP_FontAsset textFallback;
    public RenderTexture Texture => target;

    public void Build(ChessGame game)
    {
        Dispose();
        stage = new GameObject("Result Card Render Stage");
        stage.transform.position = new Vector3(24000, 24000, 24000);
        var cameraObject = new GameObject("Result Card Camera");
        cameraObject.transform.SetParent(stage.transform, false);
        cameraObject.transform.localPosition = new Vector3(0, 0, -10);
        camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = Height / 200f;
        camera.nearClipPlane = .1f; camera.farClipPlane = 20;
        camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = UI.Paper; camera.allowHDR = false; camera.allowMSAA = false;
        var urp = camera.GetUniversalAdditionalCameraData();
        urp.renderPostProcessing = false; urp.antialiasing = AntialiasingMode.None;
        urp.renderShadows = false; urp.volumeLayerMask = 0;
        urp.requiresDepthTexture = false; urp.requiresColorTexture = false;
        target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
        { name = "Match Result Card", antiAliasing = 1, useMipMap = false };
        if (!target.Create()) throw new InvalidOperationException("The result image could not be allocated.");
        camera.targetTexture = target;
        var card = new GameObject("Share Card Only", typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
        card.SetParent(stage.transform, false); card.sizeDelta = new Vector2(Width, Height);
        card.localScale = Vector3.one * .01f;
        var canvas = card.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        Compose(card, game);
        foreach (Transform node in stage.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
        Canvas.ForceUpdateCanvases();
        foreach (var label in card.GetComponentsInChildren<TextMeshProUGUI>())
        {
            label.text = label.text.Normalize(NormalizationForm.FormC);
            if (label.font && !label.font.HasCharacters(label.text, out uint[] missing, true, true))
            {
                // Keep the established font wherever it covers the text. In particular,
                // player names must not silently lose Vietnamese or other Latin accents.
                if (!textFallback) textFallback = TMP_FontAsset.CreateFontAsset(ChessFontCatalog.ResultFallbackFont);
                if (textFallback) label.font = textFallback;
            }
            label.ForceMeshUpdate();
        }
    }

    private static void Compose(RectTransform root, ChessGame game)
    {
        UI.Card(root, "Paper", new Rect(0, 0, Width, Height), UI.Paper, 0, false, 0);
        UI.Text(root, "Brand", "CHESS BUT WEIRD", new Rect(48, 30, 780, 43), 32);
        UI.Text(root, "Tagline", "A GOOD GAME. A WEIRD STORY.", new Rect(50, 78, 760, 24), 18, UI.Muted);
        UI.Doodle(root, "Brand Doodle", new Rect(1070, 28, 70, 70), SketchbookDoodle.Shape.Star, UI.Ink, -9);
        var sheet = UI.Card(root, "Result Note", new Rect(40, 132, 592, 534), UI.White, 0, true, 20).rectTransform;
        UI.Tape(sheet, 185, -12, 180, -3);
        string mode = StandardChessNotation.SingleLine(game.StatisticsMode);
        UI.Text(sheet, "Mode", mode.ToUpperInvariant(), new Rect(28, 32, 536, 34), 25, UI.Muted);
        bool draw = game.Status == ChessGame.ChessGameStatus.Draw;
        bool victory = game.WinningTeam == game.PlayerTeam;
        string outcome = draw ? "IT'S A DRAW" : game.IsHotseatGame ? game.WinningTeam.ToString().ToUpperInvariant() + " WINS" : victory ? "VICTORY!" : "DEFEAT";
        Color accent = draw ? UI.Lavender : game.IsHotseatGame || victory ? UI.Yellow : UI.Pink;
        var banner = UI.Card(sheet, "Outcome Banner", new Rect(26, 84, 540, 90), accent, -1, false, 12).rectTransform;
        UI.Text(banner, "Outcome", outcome, new Rect(12, 6, 516, 78), 53, UI.Ink, TextAlignmentOptions.Center);
        UI.Text(sheet, "White Caption", "WHITE", new Rect(30, 198, 120, 26), 19, UI.Muted);
        UI.Text(sheet, "White Player", StandardChessNotation.SingleLine(game.StatisticsPlayerName(PieceTeam.White)), new Rect(30, 229, 532, 62), 34, null, TextAlignmentOptions.MidlineLeft, true);
        UI.Text(sheet, "Black Caption", "BLACK", new Rect(30, 307, 120, 26), 19, UI.Muted);
        UI.Text(sheet, "Black Player", StandardChessNotation.SingleLine(game.StatisticsPlayerName(PieceTeam.Black)), new Rect(30, 338, 532, 62), 34, null, TextAlignmentOptions.MidlineLeft, true);
        string reason = StandardChessNotation.SingleLine(game.ResultTerminationReason);
        string score = draw ? "1/2–1/2" : game.WinningTeam == PieceTeam.White ? "1–0" : "0–1";
        UI.Text(sheet, "Reason", score + (reason.Length > 0 ? "  ·  " + reason : "  ·  Match complete"), new Rect(30, 426, 532, 38), 25);
        string details = "";
        if (game.StatisticsTimeKnown)
        {
            int seconds = Mathf.Max(0, Mathf.FloorToInt(game.MatchElapsedSeconds));
            details = $"{seconds / 60:00}:{seconds % 60:00} played";
        }
        if (game.ResultMoveCount.HasValue)
            details += (details.Length > 0 ? "  /  " : "") + game.ResultMoveCount.Value + " moves played";
        UI.Text(sheet, "Reliable Statistics", details, new Rect(30, 478, 532, 29), 21, UI.Muted);
        DrawBoard(root, game.CaptureResultBoard());
        UI.Text(root, "Footer", "YOUR MATCH. YOUR MOMENT.", new Rect(48, 684, 1100, 23), 17, UI.Muted);
    }

    private static void DrawBoard(RectTransform root, char[] board)
    {
        UI.Text(root, "Board Heading", "THE FINAL BOARD", new Rect(680, 133, 464, 35), 25, UI.Ink, TextAlignmentOptions.Center);
        var frame = UI.Card(root, "Board Frame", new Rect(664, 180, 488, 488), UI.Ink, 0, false, 10).rectTransform;
        if (board == null || board.Length != 64)
        {
            UI.Text(frame, "Board Unavailable", "Board snapshot\nunavailable", new Rect(25, 150, 438, 160), 32, UI.White, TextAlignmentOptions.Center, true);
            return;
        }
        const float tile = 56;
        for (int row = 0; row < 8; row++) for (int x = 0; x < 8; x++)
        {
            int y = 7 - row;
            float left = 20 + x * tile, top = 20 + row * tile;
            var square = UI.Node(frame, "Square " + x + y, new Rect(left, top, tile, tile));
            var fill = square.gameObject.AddComponent<Image>(); fill.raycastTarget = false;
            fill.color = (x + y) % 2 == 0 ? new Color(.53f, .63f, .50f) : new Color(.91f, .91f, .80f);
            char piece = board[y * 8 + x];
            if (piece == '\0') continue;
            bool white = char.IsUpper(piece);
            var token = UI.Card(square, "Piece Token", new Rect(6, 5, 44, 44), white ? UI.White : UI.Ink, 0, false, 18).rectTransform;
            token.GetComponent<HandDrawnRoundedGraphic>().Configure(white ? UI.White : UI.Ink, white ? UI.Ink : UI.White, 18, 1.5f, 0, 0);
            UI.Text(token, "Piece", char.ToUpperInvariant(piece).ToString(), new Rect(0, 0, 44, 43), 31, white ? UI.Ink : UI.White, TextAlignmentOptions.Center);
        }
        for (int x = 0; x < 8; x++)
            UI.Text(frame, "File", ((char)('a' + x)).ToString(), new Rect(20 + x * tile, 468, tile, 18), 14, UI.White, TextAlignmentOptions.Center);
        for (int row = 0; row < 8; row++)
            UI.Text(frame, "Rank", (8 - row).ToString(), new Rect(0, 20 + row * tile, 20, tile), 14, UI.White, TextAlignmentOptions.Center);
    }

    public byte[] EncodePng()
    {
        if (!target || !target.IsCreated()) throw new InvalidOperationException("Preview the result before saving it.");
        RenderTexture previous = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            RenderTexture.active = target;
            pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false); pixels.Apply(false);
            return pixels.EncodeToPNG();
        }
        finally { RenderTexture.active = previous; Release(pixels); }
    }

    public void Freeze() { if (camera) camera.enabled = false; }

    public void Dispose()
    {
        if (camera) camera.targetTexture = null;
        if (stage) { stage.SetActive(false); Release(stage); }
        if (target) { target.Release(); Release(target); }
        Release(textFallback); textFallback = null;
        stage = null; camera = null; target = null;
    }
    private static void Release(UnityEngine.Object value)
    { if (value) { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); } }
}
