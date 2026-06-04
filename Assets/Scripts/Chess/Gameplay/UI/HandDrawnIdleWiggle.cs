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
    private float timer;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        if (!rectTransform)
            return;

        basePosition = rectTransform.anchoredPosition;
        baseRotation = rectTransform.localRotation;
        baseScale = rectTransform.localScale;
        timer = Random.Range(0f, interval);
    }

    private void OnEnable()
    {
        timer = Random.Range(0f, interval);
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

        timer -= Time.unscaledDeltaTime;
        if (timer > 0f)
            return;

        timer = interval;
        rectTransform.anchoredPosition = basePosition + Random.insideUnitCircle * positionAmount;
        rectTransform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, Random.Range(-rotationAmount, rotationAmount));
        float scale = 1f + Random.Range(-scaleAmount, scaleAmount);
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
