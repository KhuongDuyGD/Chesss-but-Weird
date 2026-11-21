using UnityEngine;

// Art references only; server offers supply all prices and purchased ItemIds.
[CreateAssetMenu(menuName = "Chess/Shop/Presentation Catalog")]
public sealed class ShopPresentationCatalog : ScriptableObject
{
    public CosmeticCatalog cosmetics;
    public ShopProductCard productCard;
    public string title = "The weird little shop";
    public string subtitle = "A new look. The same wonderfully weird chess.";
}
