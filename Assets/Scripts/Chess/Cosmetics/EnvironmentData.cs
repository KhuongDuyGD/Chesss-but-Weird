using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Chess/Cosmetics/Environment")]
public sealed class EnvironmentData : ScriptableObject
{
    public string environmentId;
    public string displayName;
    public AssetReferenceGameObject environmentPrefab;
    public Vector3 localPosition;
    public Vector3 localRotation;
    public Vector3 localScale = Vector3.one;
}
