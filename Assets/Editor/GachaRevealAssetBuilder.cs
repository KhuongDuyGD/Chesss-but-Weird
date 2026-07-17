using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GachaRevealAssetBuilder
{
    private const string GachaAssetFolder = "Assets/Materials/Gacha_menu";
    private const string DemoScenePath = "Assets/Scenes/GachaRevealDemo.unity";

    [MenuItem("Chess But Weird/Gacha/Generate Reveal Assets")]
    public static void GenerateRevealAssets()
    {
        Directory.CreateDirectory(GachaAssetFolder);
        SaveTexture("GachaRevealRing.png", CreateRingTexture(512));
        SaveTexture("GachaRevealSpark.png", CreateSparkTexture(128));
        SaveTexture("GachaRevealRay.png", CreateRayTexture(768));
        SaveTexture("GachaRevealCardFrame.png", CreateCardFrameTexture(256, 340));
        SaveTexture("GachaRevealGlyph1.png", CreateGlyphTexture(160, GlyphType.Pawn));
        SaveTexture("GachaRevealGlyph2.png", CreateGlyphTexture(160, GlyphType.Knight));
        SaveTexture("GachaRevealGlyph3.png", CreateGlyphTexture(160, GlyphType.Bishop));
        SaveTexture("GachaRevealGlyph4.png", CreateGlyphTexture(160, GlyphType.Rook));
        SaveTexture("GachaRevealGlyph5.png", CreateGlyphTexture(160, GlyphType.Queen));
        SaveTexture("GachaRevealGlyph6.png", CreateGlyphTexture(160, GlyphType.King));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Chess But Weird/Gacha/Create Reveal Demo Scene")]
    public static void CreateRevealDemoScene()
    {
        GenerateRevealAssets();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "GachaRevealDemo";

        GameObject canvasObject = new GameObject("Gacha Reveal Demo Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1672f, 941f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        GameObject rootObject = new GameObject("Gacha Reveal Preview Root", typeof(RectTransform), typeof(Image));
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(canvasRect, false);
        Stretch(root);
        Image backing = rootObject.GetComponent<Image>();
        backing.color = new Color(0.015f, 0.013f, 0.025f, 1f);

        GachaSummonRevealController reveal = rootObject.AddComponent<GachaSummonRevealController>();
        reveal.Initialize(root);
        GachaSummonRevealDemo demo = rootObject.AddComponent<GachaSummonRevealDemo>();
        SerializedObject serializedDemo = new SerializedObject(demo);
        serializedDemo.FindProperty("revealRoot").objectReferenceValue = root;
        serializedDemo.FindProperty("previewRewardIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{GachaAssetFolder}/GachaRevealGlyph5.png");
        serializedDemo.ApplyModifiedPropertiesWithoutUndo();
        GachaSummonRevealDemo.BuildPreviewHud(root, demo);

        Directory.CreateDirectory(Path.GetDirectoryName(DemoScenePath));
        EditorSceneManager.SaveScene(scene, DemoScenePath);
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath);
    }

    private static Texture2D CreateRingTexture(int size)
    {
        Texture2D texture = CreateTransparentTexture(size, size);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        Color32 gold = new Color(1f, 0.88f, 0.32f, 1f);
        Color32 blue = new Color(0.26f, 0.8f, 1f, 0.75f);

        DrawRing(texture, center, size * 0.39f, 7f, gold);
        DrawRing(texture, center, size * 0.3f, 3f, blue);
        DrawRing(texture, center, size * 0.2f, 2f, new Color(1f, 1f, 1f, 0.38f));

        for (int i = 0; i < 32; i++)
        {
            float angle = i * Mathf.PI * 2f / 32f;
            float inner = i % 4 == 0 ? size * 0.33f : size * 0.355f;
            float outer = i % 4 == 0 ? size * 0.43f : size * 0.405f;
            DrawLine(texture, center + Direction(angle) * inner, center + Direction(angle) * outer, i % 4 == 0 ? 4f : 2f, gold);
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D CreateSparkTexture(int size)
    {
        Texture2D texture = CreateTransparentTexture(size, size);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        Color32 core = new Color(1f, 0.97f, 0.58f, 1f);
        Color32 glow = new Color(0.35f, 0.82f, 1f, 0.45f);

        FillDiamond(texture, center, size * 0.42f, glow);
        FillDiamond(texture, center, size * 0.24f, core);
        DrawLine(texture, new Vector2(center.x, 8f), new Vector2(center.x, size - 8f), 5f, core);
        DrawLine(texture, new Vector2(8f, center.y), new Vector2(size - 8f, center.y), 5f, core);
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateRayTexture(int size)
    {
        Texture2D texture = CreateTransparentTexture(size, size);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        for (int i = 0; i < 24; i++)
        {
            float angle = i * Mathf.PI * 2f / 24f;
            Color color = i % 2 == 0 ? new Color(1f, 0.86f, 0.25f, 0.5f) : new Color(0.24f, 0.8f, 1f, 0.28f);
            DrawLine(texture, center, center + Direction(angle) * size * 0.47f, i % 2 == 0 ? 10f : 5f, color);
        }
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateCardFrameTexture(int width, int height)
    {
        Texture2D texture = CreateTransparentTexture(width, height);
        Color32 fill = new Color(1f, 0.94f, 0.68f, 0.92f);
        Color32 edge = new Color(0.34f, 0.18f, 0.04f, 1f);
        Color32 shine = new Color(1f, 1f, 1f, 0.4f);

        FillRoundedRect(texture, new Rect(14f, 14f, width - 28f, height - 28f), 24f, fill);
        DrawRoundedRect(texture, new Rect(14f, 14f, width - 28f, height - 28f), 24f, 7f, edge);
        DrawRoundedRect(texture, new Rect(29f, 30f, width - 58f, height - 60f), 16f, 3f, shine);
        DrawRing(texture, new Vector2(width * 0.5f, height * 0.63f), 78f, 4f, new Color(0.38f, 0.18f, 0.04f, 0.5f));
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateGlyphTexture(int size, GlyphType glyph)
    {
        Texture2D texture = CreateTransparentTexture(size, size);
        Color32 glow = new Color(0.25f, 0.82f, 1f, 0.25f);
        Color32 ink = new Color(1f, 0.93f, 0.45f, 0.96f);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        FillCircle(texture, center, size * 0.45f, glow);
        DrawRing(texture, center, size * 0.43f, 3f, ink);

        switch (glyph)
        {
            case GlyphType.Pawn:
                FillCircle(texture, new Vector2(80f, 52f), 23f, ink);
                FillRoundedRect(texture, new Rect(58f, 72f, 44f, 42f), 10f, ink);
                FillRoundedRect(texture, new Rect(42f, 110f, 76f, 14f), 6f, ink);
                break;
            case GlyphType.Knight:
                FillRoundedRect(texture, new Rect(54f, 42f, 48f, 80f), 14f, ink);
                FillCircle(texture, new Vector2(92f, 54f), 21f, ink);
                DrawLine(texture, new Vector2(76f, 62f), new Vector2(118f, 83f), 16f, ink);
                FillRoundedRect(texture, new Rect(42f, 112f, 76f, 13f), 6f, ink);
                break;
            case GlyphType.Bishop:
                FillCircle(texture, new Vector2(80f, 61f), 28f, ink);
                DrawLine(texture, new Vector2(68f, 43f), new Vector2(94f, 78f), 7f, new Color(0.05f, 0.03f, 0.01f, 0.55f));
                FillRoundedRect(texture, new Rect(56f, 88f, 48f, 31f), 8f, ink);
                FillRoundedRect(texture, new Rect(40f, 116f, 80f, 12f), 6f, ink);
                break;
            case GlyphType.Rook:
                FillRoundedRect(texture, new Rect(50f, 52f, 60f, 66f), 4f, ink);
                FillRoundedRect(texture, new Rect(42f, 43f, 18f, 20f), 2f, ink);
                FillRoundedRect(texture, new Rect(71f, 43f, 18f, 20f), 2f, ink);
                FillRoundedRect(texture, new Rect(100f, 43f, 18f, 20f), 2f, ink);
                FillRoundedRect(texture, new Rect(40f, 114f, 80f, 13f), 6f, ink);
                break;
            case GlyphType.Queen:
                FillCircle(texture, new Vector2(49f, 49f), 10f, ink);
                FillCircle(texture, new Vector2(80f, 38f), 11f, ink);
                FillCircle(texture, new Vector2(111f, 49f), 10f, ink);
                DrawLine(texture, new Vector2(49f, 56f), new Vector2(62f, 103f), 15f, ink);
                DrawLine(texture, new Vector2(80f, 49f), new Vector2(80f, 106f), 15f, ink);
                DrawLine(texture, new Vector2(111f, 56f), new Vector2(98f, 103f), 15f, ink);
                FillRoundedRect(texture, new Rect(46f, 102f, 68f, 19f), 7f, ink);
                break;
            case GlyphType.King:
                DrawLine(texture, new Vector2(80f, 37f), new Vector2(80f, 76f), 9f, ink);
                DrawLine(texture, new Vector2(61f, 53f), new Vector2(99f, 53f), 8f, ink);
                FillRoundedRect(texture, new Rect(56f, 75f, 48f, 43f), 10f, ink);
                FillRoundedRect(texture, new Rect(41f, 114f, 78f, 13f), 6f, ink);
                break;
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D CreateTransparentTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, 0);
        texture.SetPixels32(pixels);
        return texture;
    }

    private static void SaveTexture(string fileName, Texture2D texture)
    {
        string path = $"{GachaAssetFolder}/{fileName}";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static void DrawRing(Texture2D texture, Vector2 center, float radius, float thickness, Color color)
    {
        float outer = radius + thickness * 0.5f;
        float inner = radius - thickness * 0.5f;
        for (int y = Mathf.FloorToInt(center.y - outer); y <= Mathf.CeilToInt(center.y + outer); y++)
        {
            for (int x = Mathf.FloorToInt(center.x - outer); x <= Mathf.CeilToInt(center.x + outer); x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance >= inner && distance <= outer)
                    BlendPixel(texture, x, y, color);
            }
        }
    }

    private static void FillCircle(Texture2D texture, Vector2 center, float radius, Color color)
    {
        for (int y = Mathf.FloorToInt(center.y - radius); y <= Mathf.CeilToInt(center.y + radius); y++)
            for (int x = Mathf.FloorToInt(center.x - radius); x <= Mathf.CeilToInt(center.x + radius); x++)
                if (Vector2.Distance(new Vector2(x, y), center) <= radius)
                    BlendPixel(texture, x, y, color);
    }

    private static void FillDiamond(Texture2D texture, Vector2 center, float radius, Color color)
    {
        for (int y = Mathf.FloorToInt(center.y - radius); y <= Mathf.CeilToInt(center.y + radius); y++)
            for (int x = Mathf.FloorToInt(center.x - radius); x <= Mathf.CeilToInt(center.x + radius); x++)
                if (Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y) <= radius)
                    BlendPixel(texture, x, y, color);
    }

    private static void DrawLine(Texture2D texture, Vector2 start, Vector2 end, float thickness, Color color)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(start, end) * 1.5f);
        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = Vector2.Lerp(start, end, i / (float)Mathf.Max(1, steps));
            FillCircle(texture, point, thickness * 0.5f, color);
        }
    }

    private static void FillRoundedRect(Texture2D texture, Rect rect, float radius, Color color)
    {
        for (int y = Mathf.FloorToInt(rect.yMin); y <= Mathf.CeilToInt(rect.yMax); y++)
        {
            for (int x = Mathf.FloorToInt(rect.xMin); x <= Mathf.CeilToInt(rect.xMax); x++)
            {
                if (IsInRoundedRect(new Vector2(x, y), rect, radius))
                    BlendPixel(texture, x, y, color);
            }
        }
    }

    private static void DrawRoundedRect(Texture2D texture, Rect rect, float radius, float thickness, Color color)
    {
        Rect inner = new Rect(rect.x + thickness, rect.y + thickness, rect.width - thickness * 2f, rect.height - thickness * 2f);
        for (int y = Mathf.FloorToInt(rect.yMin); y <= Mathf.CeilToInt(rect.yMax); y++)
        {
            for (int x = Mathf.FloorToInt(rect.xMin); x <= Mathf.CeilToInt(rect.xMax); x++)
            {
                Vector2 point = new Vector2(x, y);
                if (IsInRoundedRect(point, rect, radius) && !IsInRoundedRect(point, inner, Mathf.Max(1f, radius - thickness)))
                    BlendPixel(texture, x, y, color);
            }
        }
    }

    private static bool IsInRoundedRect(Vector2 point, Rect rect, float radius)
    {
        Vector2 inner = new Vector2(
            Mathf.Clamp(point.x, rect.xMin + radius, rect.xMax - radius),
            Mathf.Clamp(point.y, rect.yMin + radius, rect.yMax - radius));
        return rect.Contains(point) && Vector2.Distance(point, inner) <= radius;
    }

    private static Vector2 Direction(float radians)
    {
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private static void BlendPixel(Texture2D texture, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
            return;

        Color existing = texture.GetPixel(x, y);
        float alpha = color.a + existing.a * (1f - color.a);
        if (alpha <= 0f)
            return;

        Color blended = (color * color.a + existing * existing.a * (1f - color.a)) / alpha;
        blended.a = alpha;
        texture.SetPixel(x, y, blended);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private enum GlyphType
    {
        Pawn,
        Knight,
        Bishop,
        Rook,
        Queen,
        King
    }
}
