using UnityEngine;

/// <summary>Fits a fixed artwork coordinate system inside its parent without stretching it.
/// Keep screen transitions on a child so animation never fights layout.</summary>
[DisallowMultipleComponent]
public sealed class MenuDesignFrame : MonoBehaviour
{
    private RectTransform rect;
    private Vector2 designSize = new Vector2(1920f, 1080f);

    public void Configure(Vector2 size)
    {
        designSize = size;
        Apply();
    }

    private void OnEnable() => Apply();
    private void OnRectTransformDimensionsChange() => Apply();
    private void LateUpdate() => Apply();

    private void Apply()
    {
        if (!rect) rect = transform as RectTransform;
        if (!rect || !(rect.parent is RectTransform parent)) return;
        float scale = Mathf.Min(parent.rect.width / designSize.x, parent.rect.height / designSize.y);
        if (scale <= 0f) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = designSize;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = new Vector3(scale, scale, 1f);
    }

    public static RectTransform Create(Transform parent, string name, Vector2 size)
    {
        var safe = new GameObject(name + " Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
        safe.SetParent(parent, false);
        safe.anchorMin = Vector2.zero;
        safe.anchorMax = Vector2.one;
        safe.offsetMin = safe.offsetMax = Vector2.zero;
        safe.gameObject.AddComponent<ResponsiveSafeArea>();
        var frame = new GameObject(name + " Design Frame", typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(safe, false);
        frame.gameObject.AddComponent<MenuDesignFrame>().Configure(size);
        return frame;
    }
}
