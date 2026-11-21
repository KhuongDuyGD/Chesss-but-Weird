using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class CosmeticSelection
{
    private const string GuestSaveKey = "chess.cosmetics.selection.v1";
    private const string AccountSaveKeyPrefix = "chess.cosmetics.selection.v2.";
    public const string LowPolyId = "tazji_lowpoly";
    public const string KingdomRushId = "kingdom_rush";
    public const string KingdomRushDenasId = "kingdom_rush_denas";
    public const string KingdomRushVeznanId = "kingdom_rush_veznan";
    public static bool IsKingdomRushShopOnly(string id) =>
        string.Equals(id, KingdomRushId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(id, KingdomRushDenasId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(id, KingdomRushVeznanId, StringComparison.OrdinalIgnoreCase);
    private const string DefaultChessItemId = "6aacc041e50e17c25a690158";
    private const string DefaultBoardItemId = "6aacc04be50e17c25a69015a";

    public string whiteSkinId = LowPolyId;
    public string blackSkinId = LowPolyId;
    public string boardId = LowPolyId;
    public string environmentId = "classic";
    public string moveEffectId = "classic";
    public bool customPieces;
    public List<PieceCosmeticChoice> pieces = new List<PieceCosmeticChoice>();
    public PieceCosmeticChoice GetPiece(PieceTeam team, PieceType type) =>
        pieces?.FirstOrDefault(p => p != null && p.slot == CosmeticItemKinds.Slot(team, type));
    public void SetPiece(PieceTeam team, PieceType type, string skinId, string itemId)
    {
        pieces = pieces ?? new List<PieceCosmeticChoice>();
        string slot = CosmeticItemKinds.Slot(team, type);
        pieces.RemoveAll(p => p == null || p.slot == slot);
        if (!string.IsNullOrWhiteSpace(skinId)) pieces.Add(new PieceCosmeticChoice { slot = slot, skinId = skinId, itemId = itemId });
    }

    public static bool HasSavedSelection => PlayerPrefs.HasKey(CurrentSaveKey);

    public CosmeticSelection Copy() => new CosmeticSelection
    {
        whiteSkinId = whiteSkinId,
        blackSkinId = blackSkinId,
        boardId = boardId,
        environmentId = environmentId,
        moveEffectId = moveEffectId,
        customPieces = customPieces,
        pieces = pieces?.Where(p => p != null).Select(p => p.Copy()).ToList() ?? new List<PieceCosmeticChoice>()
    };

    public void Save()
    {
        NormalizeForCurrentSession();
        PlayerPrefs.SetString(CurrentSaveKey, JsonUtility.ToJson(this));
        PlayerPrefs.Save();
    }

    public static CosmeticSelection Load()
    {
        CosmeticSelection selection = null;
        try
        {
            string json = PlayerPrefs.GetString(CurrentSaveKey, "");
            if (!string.IsNullOrWhiteSpace(json))
                selection = JsonUtility.FromJson<CosmeticSelection>(json);
        }
        catch (Exception error)
        {
            Debug.LogWarning("[Cosmetics] Invalid saved selection: " + error.Message);
        }

        selection = selection ?? CreateForCurrentAccount();
        selection.NormalizeForCurrentSession();
        return selection;
    }

    private void NormalizeForCurrentSession()
    {
        if (PlayerAuthService.IsGuestSession)
        {
            whiteSkinId = blackSkinId = boardId = LowPolyId;
            environmentId = "classic";
            moveEffectId = "classic";
            customPieces = false;
            pieces = new List<PieceCosmeticChoice>();
            return;
        }

        whiteSkinId = ReplaceRemovedPieceId(whiteSkinId);
        pieces = (pieces ?? new List<PieceCosmeticChoice>()).Where(p => p != null && CosmeticItemKinds.TrySlot(p.slot, out _, out _))
            .GroupBy(p => p.slot).Select(g => g.Last()).ToList();
        blackSkinId = ReplaceRemovedPieceId(blackSkinId);
        if (string.IsNullOrWhiteSpace(boardId) ||
            string.Equals(boardId, "default", StringComparison.OrdinalIgnoreCase))
            boardId = LowPolyId;
        environmentId = string.IsNullOrWhiteSpace(environmentId) || environmentId == KingdomRushId ? "classic" : environmentId;
        if (moveEffectId != "ink_sparks" && moveEffectId != "confetti") moveEffectId = "classic";
    }


    private static string ReplaceRemovedPieceId(string id) =>
        string.IsNullOrWhiteSpace(id) ||
        string.Equals(id, "default", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(id, "dc", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(id, "corn", StringComparison.OrdinalIgnoreCase)
            ? LowPolyId : id;

    private static CosmeticSelection CreateForCurrentAccount() =>
        FromEquipped(PlayerAuthService.CurrentApiUser?.equipped);

    public static CosmeticSelection FromEquipped(UserEquippedResponse equipped)
    {
        var selection = new CosmeticSelection();
        if (equipped == null) return selection;
        if (string.Equals(equipped.chessSkinId, DefaultChessItemId, StringComparison.OrdinalIgnoreCase))
            selection.whiteSkinId = selection.blackSkinId = LowPolyId;
        if (string.Equals(equipped.boardSkinId, DefaultBoardItemId, StringComparison.OrdinalIgnoreCase))
            selection.boardId = LowPolyId;
        return selection;
    }

    private static string CurrentSaveKey
    {
        get
        {
            string userId = PlayerAuthService.CurrentApiUser?.userId;
            return !PlayerAuthService.IsGuestSession && !string.IsNullOrWhiteSpace(userId)
                ? AccountSaveKeyPrefix + userId
                : GuestSaveKey;
        }
    }
}
