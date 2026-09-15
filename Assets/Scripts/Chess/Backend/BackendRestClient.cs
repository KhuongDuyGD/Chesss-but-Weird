using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

public static class BackendRestClient
{
    public static IEnumerator Send<T>(
        string method,
        string path,
        object body,
        bool authorized,
        Action<BackendApiResponse<T>> onSuccess,
        Action<string, BackendApiResponse<object>> onError)
    {
        // Guest sessions have no account token. Reject locally without sending a
        // request whose 401 response could otherwise clear the guest profile.
        if (authorized && PlayerAuthService.IsGuestSession)
        {
            onError?.Invoke("This feature requires an account. You can continue playing as Guest.", null);
            yield break;
        }

        string url = BackendConfig.BaseUrl + path;
        UnityWebRequest request = new UnityWebRequest(url, method);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Accept", "application/json");

        if (body != null)
        {
            string requestJson = JsonConvert.SerializeObject(body);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestJson));
            request.SetRequestHeader("Content-Type", "application/json");
        }

        if (authorized && !string.IsNullOrWhiteSpace(BackendSessionStore.Token))
            request.SetRequestHeader("Authorization", $"Bearer {BackendSessionStore.Token}");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

        if (request.result == UnityWebRequest.Result.Success)
        {
            BackendApiResponse<T> response = SafeDeserialize<BackendApiResponse<T>>(responseText);
            if (response == null)
            {
                onError?.Invoke("Unable to parse server response.", null);
                yield break;
            }

            onSuccess?.Invoke(response);
            yield break;
        }

        BackendApiResponse<object> errorResponse = SafeDeserialize<BackendApiResponse<object>>(responseText);
        if (authorized && request.responseCode == 401 && !PlayerAuthService.IsGuestSession)
            PlayerAuthService.Logout();

        string message = errorResponse != null && !string.IsNullOrWhiteSpace(errorResponse.message)
            ? errorResponse.message
            : string.IsNullOrWhiteSpace(request.error)
                ? $"HTTP {(int)request.responseCode}"
                : request.error;

        onError?.Invoke(message, errorResponse);
    }

    private static T SafeDeserialize<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[BackendRestClient] JSON parse failed: {exception.Message}");
            return null;
        }
    }
}
