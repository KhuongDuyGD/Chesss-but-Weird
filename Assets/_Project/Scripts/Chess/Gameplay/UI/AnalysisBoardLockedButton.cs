using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AnalysisBoardLockedButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private const float AnimationDuration = 0.12f;
    private RectTransform rectTransform;
    private RawImage image;
    private GameObject tooltip;
    private Coroutine animationRoutine;
    private Vector3 restingScale;

    public void Initialize(GameObject lockedTooltip)
    {
        rectTransform = transform as RectTransform;
        image = GetComponent<RawImage>();
        tooltip = lockedTooltip;
        restingScale = rectTransform ? rectTransform.localScale : Vector3.one;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        AnimateTo(restingScale * 1.09f, new Color(1f, 1f, 0.86f, 1f));
        if (tooltip)
            tooltip.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateTo(restingScale, Color.white);
        if (tooltip)
            tooltip.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Intentionally locked for now. Keeping the object raycastable preserves hover feedback.
    }

    private void AnimateTo(Vector3 targetScale, Color targetColor)
    {
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        animationRoutine = StartCoroutine(AnimateRoutine(targetScale, targetColor));
    }

    private IEnumerator AnimateRoutine(Vector3 targetScale, Color targetColor)
    {
        Vector3 startScale = rectTransform.localScale;
        Color startColor = image.color;
        float elapsed = 0f;
        while (elapsed < AnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / AnimationDuration));
            rectTransform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
            image.color = Color.LerpUnclamped(startColor, targetColor, t);
            yield return null;
        }

        rectTransform.localScale = targetScale;
        image.color = targetColor;
        animationRoutine = null;
    }
}
