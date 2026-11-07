using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Chess/Cosmetics/Environment")]
public sealed class EnvironmentData : ScriptableObject
{
    public string environmentId;
    public string displayName;
    public AssetReferenceSprite preview;
    public bool useBoardScale;
    public AssetReferenceGameObject environmentPrefab;
    public Vector3 localPosition;
    public Vector3 localRotation;
    public Vector3 localScale = Vector3.one;
    [Tooltip("Opaque horizon used beyond this environment, replacing the original scene skybox only while equipped.")]
    public Color horizonColor = new Color(.19f, .28f, .14f, 1f);
}
