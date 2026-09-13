using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Chess/Cosmetics/Board Skin")]
public sealed class BoardSkinData : ScriptableObject
{
    public string boardId = "default";
    public string displayName = "Classic";
    public AssetReferenceSprite preview;
    public AssetReferenceGameObject boardPrefab;
    public Vector3 localPosition;
    public Vector3 localRotation;
    public Vector3 localScale = Vector3.one;
}
