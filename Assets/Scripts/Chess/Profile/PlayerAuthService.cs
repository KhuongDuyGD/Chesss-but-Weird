using System;
using System.Security.Cryptography;
using UnityEngine;

public static class PlayerAuthService
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int HashIterations = 12000;
    private const int MinimumUsernameLength = 3;
    private const int MinimumPasswordLength = 6;

    public static PlayerProfile CurrentProfile { get; private set; }
    public static bool IsAuthenticated => CurrentProfile != null;
    public static string CurrentDisplayName => IsAuthenticated ? CurrentProfile.displayName : Environment.MachineName;

    public static bool Register(string username, string password, out string message)
    {
        username = ProfileStore.NormalizeUsername(username);
        if (!ValidateCredentials(username, password, out message))
            return false;

        if (ProfileStore.FindAccount(username) != null)
        {
            message = "Username already exists.";
            return false;
        }

        string salt = CreateSalt();
        AuthAccount account = new AuthAccount
        {
            username = username,
            passwordSalt = salt,
            passwordHash = HashPassword(password, salt),
            profile = PlayerProfile.Create(username)
        };

        ProfileStore.Data.accounts.Add(account);
        ProfileStore.Data.lastUsername = username;
        ProfileStore.Save();

        CurrentProfile = account.profile;
        message = $"Registered as {username}.";
        Debug.Log($"[ChessAuth] Registered profile '{username}'. Store={ProfileStore.StorePath}");
        return true;
    }

    public static bool Login(string username, string password, out string message)
    {
        username = ProfileStore.NormalizeUsername(username);
        AuthAccount account = ProfileStore.FindAccount(username);
        if (account == null)
        {
            message = "Account not found.";
            return false;
        }

        string attemptedHash = HashPassword(password, account.passwordSalt);
        if (!SlowEquals(attemptedHash, account.passwordHash))
        {
            message = "Incorrect password.";
            return false;
        }

        account.profile.lastLoginAtUtc = DateTime.UtcNow.ToString("O");
        ProfileStore.Data.lastUsername = account.username;
        ProfileStore.Save();

        CurrentProfile = account.profile;
        message = $"Welcome, {account.profile.displayName}.";
        Debug.Log($"[ChessAuth] Logged in as '{account.username}'.");
        return true;
    }

    public static void Logout()
    {
        if (CurrentProfile != null)
            Debug.Log($"[ChessAuth] Logged out '{CurrentProfile.username}'.");

        CurrentProfile = null;
    }

    public static string GetLastUsername()
    {
        return ProfileStore.Data.lastUsername ?? string.Empty;
    }

    public static void RecordGameResult(bool won, bool lost, bool draw)
    {
        if (CurrentProfile == null)
            return;

        if (won)
            CurrentProfile.wins++;
        else if (lost)
            CurrentProfile.losses++;
        else if (draw)
            CurrentProfile.draws++;

        CurrentProfile.totalGames++;
        ProfileStore.Save();
    }

    private static bool ValidateCredentials(string username, string password, out string message)
    {
        if (username.Length < MinimumUsernameLength)
        {
            message = $"Username must be at least {MinimumUsernameLength} characters.";
            return false;
        }

        if (password == null || password.Length < MinimumPasswordLength)
        {
            message = $"Password must be at least {MinimumPasswordLength} characters.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static string CreateSalt()
    {
        byte[] salt = new byte[SaltBytes];
        using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
        {
            generator.GetBytes(salt);
        }

        return Convert.ToBase64String(salt);
    }

    private static string HashPassword(string password, string saltBase64)
    {
        byte[] salt = Convert.FromBase64String(saltBase64);
        using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, salt, HashIterations))
        {
            return Convert.ToBase64String(pbkdf2.GetBytes(HashBytes));
        }
    }

    private static bool SlowEquals(string left, string right)
    {
        if (left == null || right == null)
            return false;

        byte[] leftBytes = Convert.FromBase64String(left);
        byte[] rightBytes = Convert.FromBase64String(right);
        int diff = leftBytes.Length ^ rightBytes.Length;
        int length = Math.Min(leftBytes.Length, rightBytes.Length);

        for (int i = 0; i < length; i++)
            diff |= leftBytes[i] ^ rightBytes[i];

        return diff == 0;
    }
}
