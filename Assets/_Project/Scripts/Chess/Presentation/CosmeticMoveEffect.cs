using System.Collections;
using UnityEngine;

internal static class CosmeticMoveEffect
{
    public static void Play(MonoBehaviour owner, Transform piece, Vector3 position)
    {
        string id = CosmeticSelection.Load().moveEffectId;
        if (!owner || id == "classic" || UserSettings.Presentation.ReducedMotion) return;
        owner.StartCoroutine(Burst(owner.transform, piece, position, id == "confetti"));
    }

    private static IEnumerator Burst(Transform parent, Transform piece, Vector3 center, bool confetti)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (!shader) shader = Shader.Find("Sprites/Default");
        if (!shader) yield break;
        var material = new Material(shader);
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
        }
        var root = new GameObject("Cosmetic Move Burst");
        root.transform.SetParent(parent, false);
        root.transform.position = center;
        root.transform.rotation = Quaternion.identity;
        // Tie the lifetime to a component, so cancelling a match also releases the material.
        root.AddComponent<CosmeticEffectLifetime>().material = material;
        Object.Destroy(root, .8f);
        Vector3 up = piece.up;
        Vector3 right = piece.right;
        Vector3 forward = piece.forward;
        float size = Mathf.Max(.12f, piece.lossyScale.x * .35f);
        int count = UserSettings.Presentation.Effects == 0 ? 3 : UserSettings.Presentation.Effects == 1 ? 5 : 9;
        var lines = new LineRenderer[count];
        var directions = new Vector3[count];
        var colors = new Color[count];
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            directions[i] = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
            colors[i] = confetti ? (i % 3 == 0 ? SketchbookUI.Pink : i % 3 == 1 ? SketchbookUI.Blue : SketchbookUI.Yellow) : SketchbookUI.Yellow;
            var child = new GameObject("Doodle Spark", typeof(LineRenderer));
            child.transform.SetParent(root.transform, false);
            var line = child.GetComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = !confetti;
            line.positionCount = confetti ? 2 : 10;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            line.widthMultiplier = size * .07f;
            if (confetti)
            {
                line.SetPosition(0, -right * size * .12f);
                line.SetPosition(1, right * size * .12f + up * size * .07f);
            }
            else for (int p = 0; p < 10; p++)
            {
                float a = p * Mathf.PI / 5;
                float radius = size * (p % 2 == 0 ? .16f : .07f);
                line.SetPosition(p, (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
            }
            lines[i] = line;
        }
        float elapsed = 0;
        while (root && elapsed < .55f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / .55f);
            for (int i = 0; i < count; i++)
            {
                lines[i].transform.localPosition = directions[i] * size * (.3f + t) + up * size * (.2f + Mathf.Sin(t * Mathf.PI) * .8f);
                Color color = colors[i]; color.a = 1f - t;
                lines[i].startColor = lines[i].endColor = color;
            }
            yield return null;
        }
        if (root) Object.Destroy(root);
    }
}

internal sealed class CosmeticEffectLifetime : MonoBehaviour
{
    internal Material material;
    private void OnDestroy() { if (material) Destroy(material); }
}
