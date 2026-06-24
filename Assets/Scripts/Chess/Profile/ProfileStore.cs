using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class ProfileStoreData
{
    public List<AuthAccount> accounts = new List<AuthAccount>();
    public string lastUsername;
}

public static class ProfileStore
{
    private const string FileName = "chess_but_weird_profiles.json";
    private static ProfileStoreData data;

    public static ProfileStoreData Data
    {
        get
        {
            EnsureLoaded();
            return data;
        }
    }

    public static string StorePath => Path.Combine(Application.persistentDataPath, FileName);

    public static void Save()
    {
        EnsureLoaded();

        string directory = Path.GetDirectoryName(StorePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(StorePath, json);
    }

    public static AuthAccount FindAccount(string username)
    {
        EnsureLoaded();
        string normalized = NormalizeUsername(username);
        for (int i = 0; i < data.accounts.Count; i++)
        {
            AuthAccount account = data.accounts[i];
            if (string.Equals(NormalizeUsername(account.username), normalized, StringComparison.OrdinalIgnoreCase))
                return account;
        }

        return null;
    }

    public static string NormalizeUsername(string username)
    {
        return string.IsNullOrWhiteSpace(username) ? string.Empty : username.Trim();
    }

    private static void EnsureLoaded()
    {
        if (data != null)
            return;

        data = new ProfileStoreData();
        if (!File.Exists(StorePath))
            return;

        try
        {
            string json = File.ReadAllText(StorePath);
            ProfileStoreData loaded = JsonUtility.FromJson<ProfileStoreData>(json);
            if (loaded != null)
                data = loaded;

            if (data.accounts == null)
                data.accounts = new List<AuthAccount>();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[ChessAuth] Unable to load profile store: {exception.Message}");
            data = new ProfileStoreData();
        }
    }
}
