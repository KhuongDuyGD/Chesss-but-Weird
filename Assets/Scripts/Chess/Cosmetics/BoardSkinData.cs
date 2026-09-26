using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Chess/Cosmetics/Board Skin")]
public sealed class BoardSkinData : ScriptableObject
{
    public string boardId = CosmeticSelection.LowPolyId;
    public string displayName = "Tazji's Low Poly Arena";
    public AssetReferenceSprite preview;
    public AssetReferenceGameObject boardPrefab;
    public Vector3 localPosition;
    public Vector3 localRotation;
    public Vector3 localScale = Vector3.one;
}
