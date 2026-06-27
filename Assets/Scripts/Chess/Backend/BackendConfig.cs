using System;
using UnityEngine;

public static class BackendConfig
{
    private const string HostKey = "backend.host";
    private const string PortKey = "backend.port";
    private const string SchemeKey = "backend.scheme";
    private const string DefaultHost = "127.0.0.1";
    private const int DefaultPort = 8080;
    private const string DefaultScheme = "http";

    public static string Host
    {
        get => PlayerPrefs.GetString(HostKey, DefaultHost).Trim();
        set => PlayerPrefs.SetString(HostKey, string.IsNullOrWhiteSpace(value) ? DefaultHost : value.Trim());
    }

    public static int Port
    {
        get => PlayerPrefs.GetInt(PortKey, DefaultPort);
        set => PlayerPrefs.SetInt(PortKey, Mathf.Clamp(value, 1, 65535));
    }

    public static string Scheme
    {
        get => PlayerPrefs.GetString(SchemeKey, DefaultScheme);
        set => PlayerPrefs.SetString(SchemeKey, string.IsNullOrWhiteSpace(value) ? DefaultScheme : value.Trim().ToLowerInvariant());
    }

    public static string BaseUrl
    {
        get
        {
            if (TryGetAbsoluteBaseUri(out Uri absoluteUri))
                return absoluteUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');

            return $"{Scheme}://{Host}:{Port}";
        }
    }

    public static string WebSocketUrl
    {
        get
        {
            if (TryGetAbsoluteBaseUri(out Uri absoluteUri))
            {
                string websocketScheme = string.Equals(absoluteUri.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
                string authority = absoluteUri.IsDefaultPort ? absoluteUri.Host : absoluteUri.Authority;
                return $"{websocketScheme}://{authority}/ws/chess";
            }

            string websocketFallbackScheme = Scheme == "https" ? "wss" : "ws";
            return $"{websocketFallbackScheme}://{Host}:{Port}/ws/chess";
        }
    }

    public static bool UsesAbsoluteUrl => TryGetAbsoluteBaseUri(out _);

    private static bool TryGetAbsoluteBaseUri(out Uri uri)
    {
        string rawHost = Host;
        if (Uri.TryCreate(rawHost, UriKind.Absolute, out uri) &&
            (string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        uri = null;
        return false;
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }
}
