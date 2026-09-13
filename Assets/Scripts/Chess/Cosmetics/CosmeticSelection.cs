using System;
using UnityEngine;

[Serializable]
public sealed class CosmeticSelection
{
    private const string SaveKey = "chess.cosmetics.selection.v1";
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
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(this));
        PlayerPrefs.Save();
    }

    public static CosmeticSelection Load()
    {
        try
        {
            var result = JsonUtility.FromJson<CosmeticSelection>(PlayerPrefs.GetString(SaveKey, ""));
            if (result == null) return new CosmeticSelection();
            if (string.IsNullOrWhiteSpace(result.whiteSkinId)) result.whiteSkinId = "default";
            if (string.IsNullOrWhiteSpace(result.blackSkinId)) result.blackSkinId = "default";
            if (string.IsNullOrWhiteSpace(result.boardId)) result.boardId = "default";
            result.environmentId = result.environmentId ?? "";
            return result;
        }
        catch (Exception) { return new CosmeticSelection(); }
    }
}
