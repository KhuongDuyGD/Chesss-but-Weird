using System;

[Serializable]
public sealed class ItemCatalogResponse
{
    public string itemId;
    public string code;
    public string name;
    public string type;
    public string rarity;
    public string unityAssetKey;
    public string[] tags;
    public bool isDefault;
    public bool isActive;
}

[Serializable]
public sealed class InventoryItemResponse
{
    public string playerItemId;
    public string itemId;
    public string code;
    public string name;
    public string type;
    public string rarity;
    public string unityAssetKey;
    public string[] tags;
    public bool isDefault;
    public bool isEquipped;
    public string acquiredSource;
    public string acquiredAt;
}

[Serializable]
public sealed class EquipItemRequest
{
    public string itemId;
}

[Serializable]
public sealed class EquipPieceRequest { public string slot, itemId; }
[Serializable]
public sealed class EquipLoadoutRequest
{
    public string pieceMode = "SET", chessSkinId, boardSkinId;
    public System.Collections.Generic.List<EquipPieceRequest> pieces = new System.Collections.Generic.List<EquipPieceRequest>();
}

[Serializable]
public sealed class EquipItemResponse
{
    public bool success;
    public string message;
    public UserEquippedResponse equipped;
}
