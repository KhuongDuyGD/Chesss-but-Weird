using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

public sealed class ApiClient
{
    public static readonly ApiClient Shared = new ApiClient();
    private readonly SemaphoreSlim refreshGate = new SemaphoreSlim(1, 1);

    public Task<T> GetAsync<T>(string path, bool authorized = false) => SendAsync<T>("GET", path, null, authorized);
    public Task<T> PostAsync<T>(string path, object body, bool authorized = false) => SendAsync<T>("POST", path, body, authorized);
    public Task<T> PutAsync<T>(string path, object body, bool authorized = false) => SendAsync<T>("PUT", path, body, authorized);
    public Task<T> PatchAsync<T>(string path, object body, bool authorized = false) => SendAsync<T>("PATCH", path, body, authorized);

    public async Task<string> CheckHealthAsync()
    {
        var response = await SendOnceAsync("GET", "/api/health", null, null);
        EnsureSuccess(response);
        return response.Body;
    }

    public async Task<T> SendAsync<T>(string method, string path, object body, bool authorized = false)
    {
        string json = body == null ? null : JsonConvert.SerializeObject(body);
        string token = authorized ? AuthStorage.GetAccessToken() : null;
        var response = await SendOnceAsync(method, path, json, token);

        if (authorized && response.StatusCode == 401 && AuthStorage.HasSession())
        {
            await refreshGate.WaitAsync();
            try
            {
                // A parallel request may already have refreshed this session.
                if (token == AuthStorage.GetAccessToken())
                    await RefreshForRetryAsync();
                token = AuthStorage.GetAccessToken();
            }
            finally
            {
                refreshGate.Release();
            }

            // Retry the original request once, with the exact same method and body.
            response = await SendOnceAsync(method, path, json, token);
        }

        EnsureSuccess(response);
        if (string.IsNullOrWhiteSpace(response.Body))
            return default;

        try
        {
            return JsonConvert.DeserializeObject<T>(response.Body);
        }
        catch (JsonException error)
        {
            throw new ApiException("Invalid JSON response from server.", response.StatusCode, response.Body, error);
        }
    }

    private async Task RefreshForRetryAsync()
    {
        string refreshToken = AuthStorage.GetRefreshToken();
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ApiException("Session expired. Please log in again.", 401);

        var response = await SendOnceAsync("POST", "/api/auth/refresh",
            JsonConvert.SerializeObject(new RefreshRequest { refreshToken = refreshToken }), null);
        if (response.StatusCode == 400 || response.StatusCode == 401 || response.StatusCode == 403)
        {
            AuthStorage.Clear();
            throw new ApiException("Session expired. Please log in again.", response.StatusCode, response.Body);
        }

        EnsureSuccess(response);
        RefreshResponse tokens;
        try
        {
            tokens = JsonConvert.DeserializeObject<RefreshResponse>(response.Body);
        }
        catch (JsonException error)
        {
            throw new ApiException("Invalid refresh response from server.", response.StatusCode, response.Body, error);
        }

        if (tokens == null || string.IsNullOrWhiteSpace(tokens.accessToken) || string.IsNullOrWhiteSpace(tokens.refreshToken))
            throw new ApiException("Refresh response did not contain both tokens.", response.StatusCode, response.Body);

        AuthStorage.SaveTokens(tokens.accessToken, tokens.refreshToken, tokens.expiresAt, tokens.refreshTokenExpiresAt);
    }

    private static async Task<HttpResponse> SendOnceAsync(string method, string path, string json, string accessToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException("API paths must start with one slash.", nameof(path));

        using (var request = new UnityWebRequest(ApiConfig.BaseUrl.TrimEnd('/') + path, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = ApiConfig.TimeoutSeconds;
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrWhiteSpace(accessToken))
                request.SetRequestHeader("Authorization", "Bearer " + accessToken);
            if (json != null)
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));

            var completion = new TaskCompletionSource<bool>();
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            operation.completed += _ => completion.TrySetResult(true);
            await completion.Task;

            return new HttpResponse
            {
                StatusCode = request.responseCode,
                Body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty,
                Error = request.error,
                Failed = request.result != UnityWebRequest.Result.Success
            };
        }
    }

    private static void EnsureSuccess(HttpResponse response)
    {
        if (!response.Failed && response.StatusCode >= 200 && response.StatusCode < 300)
            return;

        string message = response.Error;
        if (!string.IsNullOrWhiteSpace(response.Body))
        {
            try
            {
                var error = JObject.Parse(response.Body);
                message = (string)error["message"] ?? (string)error["title"] ?? message;
            }
            catch (JsonException) { }
        }

        if (string.IsNullOrWhiteSpace(message))
            message = response.StatusCode == 0 ? "Unable to contact server." : "HTTP " + response.StatusCode;
        throw new ApiException(message, response.StatusCode, response.Body);
    }

    private sealed class HttpResponse
    {
        public long StatusCode;
        public string Body;
        public string Error;
        public bool Failed;
    }
}
