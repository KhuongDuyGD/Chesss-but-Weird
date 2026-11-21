using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Shared pointer/controller feedback. One damped spring; no allocations, tween library or particles.</summary>
[RequireComponent(typeof(Button), typeof(RectTransform))]
public sealed class MainMenuButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    private RectTransform rect;
    private Button button;
    private Vector2 basePosition;
    private Quaternion baseRotation;
    private Vector3 baseScale;
    private bool hovering, selected, pressing;
    private float lift, velocity, scale = 1f, submitTime;

    public void Configure()
    {
        rect = (RectTransform)transform;
        button = GetComponent<Button>();
        basePosition = rect.anchoredPosition;
        baseRotation = rect.localRotation;
        baseScale = rect.localScale;
    }

    private void Update() => Advance(Time.unscaledDeltaTime, Time.unscaledTime);

    private void Advance(float deltaTime, float time)
    {
        if (!rect || !button) return;
        bool focused = button.IsInteractable() && (hovering || selected);
        bool pressed = button.IsInteractable() && (pressing || submitTime > 0f);
        float dt = Mathf.Clamp(deltaTime, 0f, .033f);
        submitTime = Mathf.Max(0f, submitTime - dt);
        float targetLift = pressed ? -2f : focused ? 5f : 0f;
        if (!focused && !pressed && Mathf.Abs(lift) < .005f && Mathf.Abs(velocity) < .01f && Mathf.Abs(scale - 1f) < .0001f)
        {
            ResetVisuals();
            return;
        }
        // Slightly underdamped for a soft, short bounce, with bounded delta time after a hitch.
        velocity += ((targetLift - lift) * 240f - velocity * 25f) * dt;
        lift += velocity * dt;
        scale = Mathf.Lerp(scale, pressed ? .978f : focused ? 1.025f : 1f, 1f - Mathf.Exp(-19f * dt));
        rect.anchoredPosition = basePosition + Vector2.up * lift;
        rect.localScale = baseScale * scale;
        float wobble = focused && !pressed ? Mathf.Sin(time * 2.4f) * .32f : 0f;
        rect.localRotation = baseRotation * Quaternion.Euler(0, 0, wobble);
    }

    private void ResetVisuals()
    {
        if (!rect) return;
        // Stop writing transforms once settled, so static canvases can remain batched.
        if (rect.anchoredPosition != basePosition) rect.anchoredPosition = basePosition;
        if (rect.localScale != baseScale) rect.localScale = baseScale;
        if (rect.localRotation != baseRotation) rect.localRotation = baseRotation;
        lift = velocity = 0f;
        scale = 1f;
    }

    private void OnDisable()
    {
        hovering = selected = pressing = false;
        submitTime = 0f;
        ResetVisuals();
    }

    public void OnPointerEnter(PointerEventData data) { hovering = true; }
    public void OnPointerExit(PointerEventData data) { hovering = pressing = false; }
    public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) pressing = true; }
    public void OnPointerUp(PointerEventData data) { pressing = false; }
    public void OnSelect(BaseEventData data) { selected = true; }
    public void OnDeselect(BaseEventData data) { selected = pressing = false; }
    public void OnSubmit(BaseEventData data) { if (isActiveAndEnabled && button && button.IsInteractable()) submitTime = .10f; }
}
