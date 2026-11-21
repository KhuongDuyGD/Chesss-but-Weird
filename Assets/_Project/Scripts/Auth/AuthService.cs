using System;
using System.Threading.Tasks;

public sealed class AuthService
{
    private readonly ApiClient client;

    public AuthService(ApiClient client = null) => this.client = client ?? ApiClient.Shared;

    public Task<RegisterResponse> RegisterAsync(string username, string email, string password)
    {
        return client.PostAsync<RegisterResponse>("/api/auth/register", new RegisterRequest
        {
            username = username?.Trim(),
            email = email?.Trim(),
            password = password
        });
    }

    public async Task<LoginResponse> LoginAsync(string email, string password)
    {
        LoginResponse result = await client.PostAsync<LoginResponse>("/api/auth/login", new LoginRequest
        {
            email = email?.Trim(),
            password = password
        });
        if (result == null || string.IsNullOrWhiteSpace(result.accessToken) || string.IsNullOrWhiteSpace(result.refreshToken))
            throw new ApiException("Login response did not contain both tokens.");

        AuthStorage.SaveTokens(result.accessToken, result.refreshToken, result.expiresAt, result.refreshTokenExpiresAt);
        return result;
    }

    public async Task<RefreshResponse> RefreshAsync()
    {
        string refreshToken = AuthStorage.GetRefreshToken();
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ApiException("No refresh token is available.", 401);

        RefreshResponse result = await client.PostAsync<RefreshResponse>("/api/auth/refresh", new RefreshRequest { refreshToken = refreshToken });
        if (result == null || string.IsNullOrWhiteSpace(result.accessToken) || string.IsNullOrWhiteSpace(result.refreshToken))
            throw new ApiException("Refresh response did not contain both tokens.");

        AuthStorage.SaveTokens(result.accessToken, result.refreshToken, result.expiresAt, result.refreshTokenExpiresAt);
        return result;
    }

    public async Task LogoutAsync()
    {
        string refreshToken = AuthStorage.GetRefreshToken();
        try
        {
            if (!string.IsNullOrWhiteSpace(refreshToken))
                await client.PostAsync<object>("/api/auth/logout", new LogoutRequest { refreshToken = refreshToken });
        }
        finally
        {
            AuthStorage.Clear();
            PlayerAuthService.Logout();
        }
    }
}
