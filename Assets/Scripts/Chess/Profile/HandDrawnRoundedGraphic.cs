using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A paper card with a soft rounded, lightly uneven ink edge.</summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class HandDrawnRoundedGraphic : AntialiasedUIGraphic
{
    [SerializeField, Min(0f)] private float cornerRadius = 18f;
    [SerializeField, Min(0f)] private float strokeWidth = 3f;
    [SerializeField, Range(0f, 4f)] private float wobble = 1.4f;
    [SerializeField] private Color strokeColor = new Color(0.10f, 0.09f, 0.08f);
    [SerializeField] private int strokeSeed;

    private const int CornerSteps = 8;
    private const int EdgeSteps = 4;

    public void Configure(Color fill, Color stroke, float radius, float width, float irregularity, int seed)
    {
        color = fill;
        strokeColor = stroke;
        cornerRadius = Mathf.Max(0f, radius);
        strokeWidth = Mathf.Max(0f, width);
        wobble = Mathf.Clamp(irregularity, 0f, 4f);
        strokeSeed = seed;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect bounds = GetPixelAdjustedRect();
        float margin = strokeWidth * 0.5f + wobble + 1f;
        bounds.xMin += margin;
        bounds.xMax -= margin;
        bounds.yMin += margin;
        bounds.yMax -= margin;
        if (bounds.width <= strokeWidth * 2f || bounds.height <= strokeWidth * 2f)
            return;

        Rect innerBounds = new Rect(bounds.xMin + strokeWidth, bounds.yMin + strokeWidth,
            bounds.width - strokeWidth * 2f, bounds.height - strokeWidth * 2f);
        float outerRadius = Mathf.Min(cornerRadius, bounds.width * 0.5f, bounds.height * 0.5f);
        float innerRadius = Mathf.Max(0f, outerRadius - strokeWidth);
        List<Vector2> outer = BuildContour(bounds, outerRadius);
        List<Vector2> inner = BuildContour(innerBounds, innerRadius);
        UIEdgeMesh.Panel(helper, outer, inner, color, strokeColor, strokeWidth);
    }

    private List<Vector2> BuildContour(Rect rect, float radius)
    {
        var points = new List<Vector2>(4 * (CornerSteps + EdgeSteps));
        Vector2[] centers =
        {
            new Vector2(rect.xMax - radius, rect.yMax - radius),
            new Vector2(rect.xMax - radius, rect.yMin + radius),
            new Vector2(rect.xMin + radius, rect.yMin + radius),
            new Vector2(rect.xMin + radius, rect.yMax - radius)
        };
        float[] startAngles = { 90f, 0f, -90f, -180f };
        for (int corner = 0; corner < 4; corner++)
        {
            for (int step = 0; step <= CornerSteps; step++)
            {
                float angle = (startAngles[corner] - 90f * step / CornerSteps) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 basePoint = centers[corner] + radius * direction;
                points.Add(basePoint + direction * Jitter(corner * (CornerSteps + EdgeSteps) + step));
            }

            Vector2 edgeStart = points[points.Count - 1];
            int nextCorner = (corner + 1) % 4;
            float nextAngle = startAngles[nextCorner] * Mathf.Deg2Rad;
            Vector2 edgeEnd = centers[nextCorner] +
                radius * new Vector2(Mathf.Cos(nextAngle), Mathf.Sin(nextAngle));
            Vector2 normal = corner == 0 ? Vector2.right :
                corner == 1 ? Vector2.down : corner == 2 ? Vector2.left : Vector2.up;
            for (int step = 1; step <= EdgeSteps; step++)
            {
                float t = step / (float)(EdgeSteps + 1);
                Vector2 basePoint = Vector2.Lerp(edgeStart, edgeEnd, t);
                points.Add(basePoint + normal * Jitter(corner * (CornerSteps + EdgeSteps) + CornerSteps + step));
            }
        }
        return points;
    }

    private float Jitter(int sample)
    {
        float phase = sample * 2.39996f + strokeSeed * 0.73f;
        return wobble * (0.6f * Mathf.Sin(phase) + 0.4f * Mathf.Sin(phase * 0.47f));
    }
}


