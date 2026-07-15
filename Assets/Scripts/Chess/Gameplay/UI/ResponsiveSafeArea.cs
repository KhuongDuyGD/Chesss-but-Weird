using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ResponsiveSafeArea : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
    private Vector2Int lastScreenSize = new Vector2Int(-1, -1);
    private float nextSafeAreaCheckAt;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextSafeAreaCheckAt)
            return;

        nextSafeAreaCheckAt = Time.unscaledTime + 0.25f;
        if (lastSafeArea != Screen.safeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
            Apply();
    }

    private void Apply()
    {
        if (!rectTransform || Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect safeArea = Screen.safeArea;
        rectTransform.anchorMin = new Vector2(safeArea.xMin / Screen.width, safeArea.yMin / Screen.height);
        rectTransform.anchorMax = new Vector2(safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        nextSafeAreaCheckAt = Time.unscaledTime + 0.25f;
    }
}

public static class ResponsiveUi
{
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;

    public static void ConfigureCanvasScaler(CanvasScaler scaler, Vector2 referenceResolution)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.referencePixelsPerUnit = 100f;
    }

    public static float GetFitScale(float referenceWidth = ReferenceWidth, float referenceHeight = ReferenceHeight)
    {
        if (Screen.width <= 0 || Screen.height <= 0)
            return 1f;

        float widthScale = Screen.width / referenceWidth;
        float heightScale = Screen.height / referenceHeight;
        return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 0.35f, 2.5f);
    }
}
