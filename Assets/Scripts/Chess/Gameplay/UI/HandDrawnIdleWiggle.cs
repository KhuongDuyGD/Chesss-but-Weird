using UnityEngine;

public class HandDrawnIdleWiggle : MonoBehaviour
{
    [SerializeField] private float positionAmount = 2f;
    [SerializeField] private float rotationAmount = 1.2f;
    [SerializeField] private float scaleAmount = 0.015f;
    [SerializeField] private float interval = 0.09f;

    private RectTransform rectTransform;
    private Vector2 basePosition;
    private Quaternion baseRotation;
    private Vector3 baseScale;
    private float phase;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        if (!rectTransform)
            return;

        basePosition = rectTransform.anchoredPosition;
        baseRotation = rectTransform.localRotation;
        baseScale = rectTransform.localScale;
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void OnEnable()
    {
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void OnDisable()
    {
        if (!rectTransform)
            return;

        rectTransform.anchoredPosition = basePosition;
        rectTransform.localRotation = baseRotation;
        rectTransform.localScale = baseScale;
    }

    private void Update()
    {
        if (!rectTransform)
            return;

        float speed = Mathf.Max(0.01f, interval) * 8f;
        float time = Time.unscaledTime * speed + phase;
        rectTransform.anchoredPosition = basePosition + new Vector2(Mathf.Sin(time), Mathf.Cos(time * 0.83f)) * positionAmount;
        rectTransform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.71f) * rotationAmount);
        float scale = 1f + Mathf.Sin(time * 0.57f) * scaleAmount;
        rectTransform.localScale = baseScale * scale;
    }

    public void Configure(float position, float rotation, float scale, float tick)
    {
        positionAmount = position;
        rotationAmount = rotation;
        scaleAmount = scale;
        interval = tick;
    }
}
