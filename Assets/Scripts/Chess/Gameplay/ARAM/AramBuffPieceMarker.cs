using UnityEngine;

[DisallowMultipleComponent]
public sealed class AramBuffPieceMarker : MonoBehaviour
{
    private const int RingSegments = 48;
    private const string MarkerRootName = "ARAM Buff Marker";

    private Transform markerRoot;
    private LineRenderer ring;
    private Transform orb;
    private TextMesh label;
    private Material ringMaterial;
    private Material orbMaterial;
    private Color accent = Color.white;
    private float radius = 0.48f;
    private float height = 1.25f;
    private float phase;

    public void Configure(Color color, string markerLabel, float markerRadius = 0.48f, float markerHeight = 1.25f)
    {
        accent = color;
        radius = Mathf.Max(0.18f, markerRadius);
        height = Mathf.Max(0.45f, markerHeight);
        phase = Random.value * 10f;
        Build(markerLabel);
        ApplyColor(1f);
    }

    private void Build(string markerLabel)
    {
        ClearVisuals();
        ClearLegacyChildMarkers();

        GameObject rootObject = new GameObject(MarkerRootName);
        markerRoot = rootObject.transform;
        markerRoot.position = transform.position;
        markerRoot.rotation = Quaternion.identity;
        markerRoot.localScale = Vector3.one;
        SetLayerRecursively(rootObject, LayerMask.NameToLayer("Ignore Raycast"));

        GameObject ringObject = new GameObject("Ring");
        ringObject.transform.SetParent(markerRoot, false);
        ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = RingSegments;
        ring.widthMultiplier = 0.035f;
        ring.numCornerVertices = 4;
        ring.numCapVertices = 4;
        ringMaterial = CreateUnlitMaterial();
        ring.material = ringMaterial;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = i / (float)RingSegments * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.04f, Mathf.Sin(angle) * radius));
        }

        GameObject orbObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orbObject.name = "Orb";
        orbObject.transform.SetParent(markerRoot, false);
        orbObject.transform.localPosition = new Vector3(0f, height, 0f);
        orbObject.transform.localScale = Vector3.one * 0.13f;
        Collider orbCollider = orbObject.GetComponent<Collider>();
        if (orbCollider)
            Destroy(orbCollider);
        orb = orbObject.transform;
        Renderer orbRenderer = orbObject.GetComponent<Renderer>();
        orbMaterial = CreateUnlitMaterial();
        if (orbRenderer)
            orbRenderer.material = orbMaterial;

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(markerRoot, false);
        labelObject.transform.localPosition = new Vector3(0f, height + 0.24f, 0f);
        label = labelObject.AddComponent<TextMesh>();
        label.text = markerLabel;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.characterSize = 0.08f;
        label.fontSize = 34;
        label.color = accent;
        SetLayerRecursively(rootObject, LayerMask.NameToLayer("Ignore Raycast"));
    }

    private void Update()
    {
        if (!markerRoot)
            return;

        float time = Time.unscaledTime + phase;
        float pulse = 0.5f + Mathf.Sin(time * 4.2f) * 0.5f;
        float scale = 1f + pulse * 0.12f;
        markerRoot.position = transform.position;
        markerRoot.localScale = new Vector3(scale, 1f, scale);
        markerRoot.localRotation = Quaternion.Euler(0f, time * 48f, 0f);

        if (orb)
            orb.localPosition = new Vector3(0f, height + Mathf.Sin(time * 3.3f) * 0.08f, 0f);

        ApplyColor(0.58f + pulse * 0.34f);
        FaceCamera();
    }

    private void FaceCamera()
    {
        if (!label)
            return;

        Camera camera = Camera.main;
        if (!camera)
            return;

        Transform labelTransform = label.transform;
        Vector3 direction = labelTransform.position - camera.transform.position;
        if (direction.sqrMagnitude > 0.0001f)
            labelTransform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void ApplyColor(float alpha)
    {
        Color color = accent;
        color.a = Mathf.Clamp01(alpha);
        if (ring)
        {
            ring.startColor = color;
            ring.endColor = color;
        }

        if (ringMaterial)
            ringMaterial.color = color;
        if (orbMaterial)
            orbMaterial.color = color;
        if (label)
            label.color = color;
    }

    private static Material CreateUnlitMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (!shader)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader)
            shader = Shader.Find("Standard");
        return new Material(shader);
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (!target || layer < 0)
            return;

        target.layer = layer;
        Transform targetTransform = target.transform;
        for (int i = 0; i < targetTransform.childCount; i++)
            SetLayerRecursively(targetTransform.GetChild(i).gameObject, layer);
    }

    private void ClearVisuals()
    {
        if (markerRoot)
            Destroy(markerRoot.gameObject);
        if (ringMaterial)
            Destroy(ringMaterial);
        if (orbMaterial)
            Destroy(orbMaterial);
        markerRoot = null;
        ring = null;
        orb = null;
        label = null;
        ringMaterial = null;
        orbMaterial = null;
    }

    private void ClearLegacyChildMarkers()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child && child.name == MarkerRootName)
                Destroy(child.gameObject);
        }
    }

    private void OnDestroy()
    {
        ClearVisuals();
    }
}
