using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class HandDrawnPressable : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.07f;
    [SerializeField] private float pressedScale = 0.94f;
    [SerializeField] private float rotationAmount = 2.2f;
    [SerializeField] private float responseSpeed = 10f;
    [SerializeField] private Graphic tintTarget;
    [SerializeField] private Color hoverTint = new Color(1f, 0.96f, 0.72f, 1f);

    private RectTransform rectTransform;
    private Vector3 baseScale;
    private Quaternion baseRotation;
    private Color baseColor = Color.white;
    private bool hovering;
    private bool pressing;
    private float hoverDirection = 1f;

    public void Configure(float newHoverScale, float newPressedScale, float newRotationAmount, Color newHoverTint)
    {
        hoverScale = newHoverScale;
        pressedScale = newPressedScale;
        rotationAmount = newRotationAmount;
        hoverTint = newHoverTint;
    }

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        baseScale = rectTransform.localScale;
        baseRotation = rectTransform.localRotation;

        if (!tintTarget)
            tintTarget = GetComponent<Graphic>();

        if (tintTarget)
            baseColor = tintTarget.color;
    }

    private void Update()
    {
        float targetScale = pressing ? pressedScale : hovering ? hoverScale : 1f;
        float targetRotation = hovering ? hoverDirection * rotationAmount : 0f;
        float blend = 1f - Mathf.Exp(-responseSpeed * Time.unscaledDeltaTime);

        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, baseScale * targetScale, blend);
        rectTransform.localRotation = Quaternion.Slerp(rectTransform.localRotation, baseRotation * Quaternion.Euler(0f, 0f, targetRotation), blend);

        if (tintTarget)
            tintTarget.color = Color.Lerp(tintTarget.color, hovering ? hoverTint : baseColor, blend);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hoverDirection = Random.value < 0.5f ? -1f : 1f;
        hovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        pressing = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressing = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressing = false;
    }
}
