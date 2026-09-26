using System;
using UnityEngine;

[Serializable]
public sealed class CosmeticSelection
{
    private const string GuestSaveKey = "chess.cosmetics.selection.v1";
    private const string AccountSaveKeyPrefix = "chess.cosmetics.selection.v2.";
    public const string LowPolyId = "tazji_lowpoly";
    private const string DefaultChessItemId = "6aacc041e50e17c25a690158";
    private const string DefaultBoardItemId = "6aacc04be50e17c25a69015a";

    public string whiteSkinId = "default";
    public string blackSkinId = "default";
    public string boardId = "default";
    public string environmentId = "";

    public CosmeticSelection Copy() => new CosmeticSelection
    {
        whiteSkinId = whiteSkinId,
        blackSkinId = blackSkinId,
        boardId = boardId,
        environmentId = environmentId
    };

    public void Save()
    {
        PlayerPrefs.SetString(CurrentSaveKey, JsonUtility.ToJson(this));
        PlayerPrefs.Save();
    }

    public static CosmeticSelection Load()
    {
        try
        {
            string json = PlayerPrefs.GetString(CurrentSaveKey, "");
            var result = string.IsNullOrWhiteSpace(json)
                ? CreateForCurrentAccount()
                : JsonUtility.FromJson<CosmeticSelection>(json);
            if (result == null) result = CreateForCurrentAccount();
            if (string.IsNullOrWhiteSpace(result.whiteSkinId)) result.whiteSkinId = "default";
            if (string.IsNullOrWhiteSpace(result.blackSkinId)) result.blackSkinId = "default";
            if (string.IsNullOrWhiteSpace(result.boardId)) result.boardId = "default";
            result.environmentId = result.environmentId ?? "";
            return result;
        }
        catch (Exception)
        {
            return CreateForCurrentAccount();
        }
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

    private static CosmeticSelection CreateForCurrentAccount()
    {
        return FromEquipped(PlayerAuthService.CurrentApiUser?.equipped);
    }

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
}
