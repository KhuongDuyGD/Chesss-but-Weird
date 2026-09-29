using UnityEngine;
using UnityEngine.UI;

/// <summary>One static UI mesh: gentle paper grain and baked ambient lighting, without post processing.</summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class MainMenuPaperGraphic : MaskableGraphic
{
    private Texture2D grain;
    public override Texture mainTexture => grain ? grain : Texture2D.whiteTexture;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        // Small, deterministic repeating texture. Generated once, never updated per frame.
        grain = new Texture2D(64, 64, TextureFormat.RGB24, true) {
            name = "MainMenuPaperGrain", filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave
        };
        var pixels = new Color32[64 * 64];
        var random = new System.Random(731);
        for (int i = 0; i < pixels.Length; i++)
        {
            byte value = (byte)random.Next(244, 256);
            pixels[i] = new Color32(value, value, value, 255);
        }
        grain.SetPixels32(pixels);
        grain.Apply(true, true);
        SetMaterialDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        const int columns = 16, rows = 10;
        for (int y = 0; y <= rows; y++)
        for (int x = 0; x <= columns; x++)
        {
            float u = x / (float)columns, v = y / (float)rows;
            float distance = Mathf.Clamp01((u - .50f) * (u - .50f) * 2.1f + (v - .58f) * (v - .58f) * 1.35f);
            Color shade = Color.Lerp(new Color(1f, .99f, .956f), new Color(.925f, .894f, .821f), distance);
            // A restrained warm pool of light behind the title; nothing animates or blurs.
            float light = Mathf.Exp(-((u - .5f) * (u - .5f) * 15f + (v - .64f) * (v - .64f) * 12f));
            shade = Color.Lerp(shade, new Color(1f, .993f, .97f), light * .30f);
            mesh.AddVert(new Vector3(rect.xMin + u * rect.width, rect.yMin + v * rect.height), shade,
                new Vector2(u * rect.width / 96f, v * rect.height / 96f));
        }
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            int i = y * (columns + 1) + x;
            mesh.AddTriangle(i, i + columns + 1, i + 1);
            mesh.AddTriangle(i + 1, i + columns + 1, i + columns + 2);
        }
    }

    protected override void OnDestroy()
    {
        if (grain)
        {
            if (Application.isPlaying) Destroy(grain);
            else DestroyImmediate(grain);
        }
        base.OnDestroy();
    }
}
