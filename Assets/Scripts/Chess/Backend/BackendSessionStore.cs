using System;
using UnityEngine;

public static class BackendSessionStore
{
    private const string TokenKey = "backend.token";
    private const string TokenExpiryKey = "backend.token.expiryUtc";
    private const string UserIdKey = "backend.user.id";
    private const string UsernameKey = "backend.user.username";
    private const string EloKey = "backend.user.elo";
    private const string CreatedAtKey = "backend.user.createdAt";

    public static string Token
    {
        get => PlayerPrefs.GetString(TokenKey, string.Empty);
        private set => PlayerPrefs.SetString(TokenKey, value ?? string.Empty);
    }

    public static DateTime TokenExpiryUtc
    {
        get
        {
            string raw = PlayerPrefs.GetString(TokenExpiryKey, string.Empty);
            return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed)
                ? parsed
                : DateTime.MinValue;
        }
        private set => PlayerPrefs.SetString(TokenExpiryKey, value == DateTime.MinValue ? string.Empty : value.ToString("O"));
    }

    public static bool HasToken => !string.IsNullOrWhiteSpace(Token);
    public static bool IsTokenExpired => TokenExpiryUtc != DateTime.MinValue && DateTime.UtcNow >= TokenExpiryUtc;

    public static void SaveAuth(BackendAuthResponseDto auth)
    {
        if (auth == null || auth.user == null)
            return;

        Token = auth.token ?? string.Empty;
        TokenExpiryUtc = DateTime.UtcNow.AddSeconds(Math.Max(0, auth.expiresInSeconds - 5));
        PlayerPrefs.SetString(UserIdKey, auth.user.id ?? string.Empty);
        PlayerPrefs.SetString(UsernameKey, auth.user.username ?? string.Empty);
        PlayerPrefs.SetInt(EloKey, auth.user.elo);
        PlayerPrefs.SetString(CreatedAtKey, auth.user.createdAt ?? string.Empty);
        PlayerPrefs.Save();
    }

    public static BackendUserProfileDto GetStoredUser()
    {
        string userId = PlayerPrefs.GetString(UserIdKey, string.Empty);
        string username = PlayerPrefs.GetString(UsernameKey, string.Empty);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(username))
            return null;

        return new BackendUserProfileDto
        {
            id = userId,
            username = username,
            elo = PlayerPrefs.GetInt(EloKey, 1200),
            createdAt = PlayerPrefs.GetString(CreatedAtKey, string.Empty)
        };
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.DeleteKey(TokenExpiryKey);
        PlayerPrefs.DeleteKey(UserIdKey);
        PlayerPrefs.DeleteKey(UsernameKey);
        PlayerPrefs.DeleteKey(EloKey);
        PlayerPrefs.DeleteKey(CreatedAtKey);
        PlayerPrefs.Save();
    }
}
