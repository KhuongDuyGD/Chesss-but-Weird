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

    public string whiteSkinId = LowPolyId;
    public string blackSkinId = LowPolyId;
    public string boardId = LowPolyId;
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
            environmentId = "";
            return;
        }

        whiteSkinId = ReplaceRemovedPieceId(whiteSkinId);
        blackSkinId = ReplaceRemovedPieceId(blackSkinId);
        if (string.IsNullOrWhiteSpace(boardId) ||
            string.Equals(boardId, "default", StringComparison.OrdinalIgnoreCase))
            boardId = LowPolyId;
        environmentId = environmentId ?? "";
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
