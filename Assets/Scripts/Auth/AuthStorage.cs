using System;
using System.Globalization;
using UnityEngine;

// PlayerPrefs is convenient for this prototype, but is not encrypted secure storage.
public static class AuthStorage
{
    private const string AccessKey = "api.auth.accessToken";
    private const string RefreshKey = "api.auth.refreshToken";
    private const string AccessExpiryKey = "api.auth.accessExpiresAt";
    private const string RefreshExpiryKey = "api.auth.refreshExpiresAt";

    public static void SaveTokens(string accessToken, string refreshToken, string accessTokenExpiresAt, string refreshTokenExpiresAt)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("The server returned incomplete tokens.");

        PlayerPrefs.SetString(AccessKey, accessToken);
        PlayerPrefs.SetString(RefreshKey, refreshToken);
        PlayerPrefs.SetString(AccessExpiryKey, accessTokenExpiresAt ?? string.Empty);
        PlayerPrefs.SetString(RefreshExpiryKey, refreshTokenExpiresAt ?? string.Empty);
        PlayerPrefs.Save();
    }

    public static string GetAccessToken() => PlayerPrefs.GetString(AccessKey, string.Empty);
    public static string GetRefreshToken() => PlayerPrefs.GetString(RefreshKey, string.Empty);
    public static string AccessTokenExpiresAt => PlayerPrefs.GetString(AccessExpiryKey, string.Empty);
    public static string RefreshTokenExpiresAt => PlayerPrefs.GetString(RefreshExpiryKey, string.Empty);
    public static bool HasSession() => !string.IsNullOrWhiteSpace(GetRefreshToken()) && !IsExpired(RefreshTokenExpiresAt);

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(AccessKey);
        PlayerPrefs.DeleteKey(RefreshKey);
        PlayerPrefs.DeleteKey(AccessExpiryKey);
        PlayerPrefs.DeleteKey(RefreshExpiryKey);
        PlayerPrefs.Save();
    }

    private static bool IsExpired(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiry)
            && DateTimeOffset.UtcNow >= expiry;
    }
}
