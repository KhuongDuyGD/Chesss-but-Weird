using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

// A presentation scope, never an inventory/economic resolver. Only exact local catalog hits load.
public sealed class CosmeticModelPreview : MonoBehaviour
{
    private RawImage image;
    private GameObject stage, visual;
    private Camera cameraView;
    private RenderTexture target;
    private AssetLoader assets;
    private Coroutine operation;
    private int version;
    public bool HasModel => visual && image && image.enabled;
    public string Status { get; private set; } = "Artwork unavailable";

    public void Initialize(RectTransform parent)
    {
        var node = new GameObject("Exact Reward Model", typeof(RectTransform), typeof(RawImage));
        node.transform.SetParent(parent, false);
        var rect = (RectTransform)node.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(0, 42); rect.sizeDelta = new Vector2(240, 190);
        image = node.GetComponent<RawImage>(); image.raycastTarget = false; image.enabled = false;
    }
    public void Show(GachaReward reward)
    {
        Clear();
        if (!isActiveAndEnabled || !image || string.IsNullOrWhiteSpace(reward.itemId) ||
            CosmeticItemKinds.KnownMissingCode(reward.itemCode)) return;
        if (reward.rawItemType != CosmeticItemKinds.Board &&
            !CosmeticItemKinds.TryPiece(reward.rawItemType, out _, out _)) return;
        // No key substitution. Default aliases are explicitly category scoped.
        string key = reward.unityAssetKey;
        if (key == "default") key = CosmeticSelection.LowPolyId;
        if (string.IsNullOrWhiteSpace(key)) return;
        assets = new AssetLoader(); Status = "Loading reward model";
        operation = StartCoroutine(Load(reward, key, version));
    }
    private IEnumerator Load(GachaReward reward, string key, int epoch)
    {
        var scope = assets;
        CosmeticCatalog catalog = null;
        yield return scope.Load<CosmeticCatalog>(CosmeticCatalog.Address, value => catalog = value);
        if (epoch != version) yield break;
        if (!catalog) { Status = "Artwork unavailable"; yield break; }
        bool board = reward.rawItemType == CosmeticItemKinds.Board;
        var entry = board ? catalog.FindBoard(key) : catalog.FindPiece(key);
        if (entry == null) { Status = "Artwork unavailable"; yield break; }
        GameObject prefab = null; PieceSkinData piece = null;
        Vector3 rotation = Vector3.zero, scale = Vector3.one;
        var team = PieceTeam.White; var kind = PieceType.King;
        if (board)
        {
            BoardSkinData data = null;
            yield return scope.Load<BoardSkinData>(entry.data, value => data = value);
            if (data && epoch == version)
            {
                rotation = data.localRotation; scale = data.localScale;
                yield return scope.Load<GameObject>(data.boardPrefab, value => prefab = value);
            }
        }
        else
        {
            yield return scope.Load<PieceSkinData>(entry.data, value => piece = value);
            bool single = CosmeticItemKinds.TryPiece(reward.rawItemType, out var side, out var type);
            if (single) { team = side; kind = type; }
            if (piece && epoch == version && single)
            {
                rotation = piece.GetTuning(kind).RotationEuler;
                yield return scope.Load<GameObject>(piece.GetPrefab(kind, team), value => prefab = value);
            }
            // Whole sets are not silently represented as a single-piece reward.
        }
        if (epoch != version || !prefab) { Status = "Artwork unavailable"; yield break; }
        EnsureStage();
        visual = new GameObject("Reward " + reward.itemId); visual.transform.SetParent(stage.transform, false);
        visual.transform.localRotation = Quaternion.Euler(rotation); visual.transform.localScale = scale;
        CopyMeshes(prefab.transform, visual.transform);
        var renderers = visual.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) { Clear(); yield break; }
        if (piece && piece.applyTeamColor)
        {
            var block = new MaterialPropertyBlock();
            var color = team == PieceTeam.White ? piece.whiteColor : piece.blackColor;
            block.SetColor("_BaseColor", color); block.SetColor("_Color", color);
            foreach (var renderer in renderers) renderer.SetPropertyBlock(block);
        }
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        visual.transform.position -= bounds.center - stage.transform.position;
        float radius = Mathf.Max(.01f, bounds.extents.magnitude);
        float distance = radius / Mathf.Sin(cameraView.fieldOfView * Mathf.Deg2Rad / 2) * 1.15f;
        cameraView.transform.localPosition = board ? new Vector3(0, distance * .9f, -distance * .7f) : new Vector3(0, distance * .4f, -distance);
        cameraView.transform.LookAt(stage.transform.position);
        if (board)
        {
            // Fit the projected bounds rather than leaving a flat board small inside a bounding sphere.
            cameraView.orthographic = true;
            float halfWidth = 0, halfHeight = 0;
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            {
                var corner = Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(cameraView.transform.right, corner)));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(cameraView.transform.up, corner)));
            }
            cameraView.orthographicSize = Mathf.Max(halfHeight, halfWidth / (240f / 190f)) * 1.12f;
        }
        cameraView.nearClipPlane = Mathf.Max(.001f, distance * .01f); cameraView.farClipPlane = distance * 4 + radius;
        image.enabled = true; Status = "Exact model"; operation = null;
    }
    private static void CopyMeshes(Transform source, Transform parent)
    {
        // Copy render-only transforms. Do not instantiate gameplay scripts, cameras, colliders or lights.
        var node = new GameObject(source.name); node.layer = 31; node.transform.SetParent(parent, false);
        node.transform.localPosition = source.localPosition; node.transform.localRotation = source.localRotation; node.transform.localScale = source.localScale;
        var filter = source.GetComponent<MeshFilter>(); var renderer = source.GetComponent<MeshRenderer>();
        if (filter && filter.sharedMesh && renderer)
        {
            node.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            node.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
        }
        foreach (Transform child in source) if (child.gameObject.activeSelf) CopyMeshes(child, node.transform);
    }
    private void EnsureStage()
    {
        if (stage) return;
        stage = new GameObject("Scoped Reward Preview"); stage.transform.position = new Vector3(10000, 10000, 10000);
        var cameraObject = new GameObject("Reward Camera"); cameraObject.transform.SetParent(stage.transform, false);
        cameraView = cameraObject.AddComponent<Camera>(); cameraView.cullingMask = 1 << 31;
        cameraView.clearFlags = CameraClearFlags.SolidColor; cameraView.backgroundColor = Color.clear;
        cameraView.fieldOfView = 30; cameraView.allowHDR = false; cameraView.allowMSAA = false;
        var urp = cameraView.GetUniversalAdditionalCameraData();
        urp.renderPostProcessing = false; urp.antialiasing = AntialiasingMode.None; urp.volumeLayerMask = 0;
        urp.renderShadows = false; urp.requiresDepthTexture = false; urp.requiresColorTexture = false;
        target = new RenderTexture(480, 380, 24, RenderTextureFormat.ARGB32) { name = "Reward Preview RT", antiAliasing = 1, useMipMap = false };
        target.Create(); cameraView.targetTexture = target; image.texture = target;
        for (int i = 0; i < 2; i++)
        {
            var lamp = new GameObject("Reward Light"); lamp.transform.SetParent(stage.transform, false);
            lamp.transform.localRotation = Quaternion.Euler(i == 0 ? 40 : 25, i == 0 ? -30 : 150, 0);
            var light = lamp.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = i == 0 ? 1.4f : .6f;
            light.cullingMask = 1 << 31; light.shadows = LightShadows.None;
        }
    }
    public void SetAlpha(float alpha) { if (image) image.color = new Color(1, 1, 1, alpha); }
    public void Clear()
    {
        version++;
        if (operation != null) StopCoroutine(operation); operation = null;
        if (image) { image.enabled = false; image.texture = null; }
        if (cameraView) cameraView.targetTexture = null;
        // Detach shared meshes before releasing their Addressables handles (Destroy is deferred).
        if (visual) foreach (var filter in visual.GetComponentsInChildren<MeshFilter>()) filter.sharedMesh = null;
        if (stage) { stage.SetActive(false); Destroy(stage); } stage = null; visual = null;
        if (target) { target.Release(); Destroy(target); } target = null;
        assets?.Dispose(); assets = null; Status = "Artwork unavailable";
    }
    private void OnDisable() => Clear();
    private void OnDestroy() => Clear();
}
