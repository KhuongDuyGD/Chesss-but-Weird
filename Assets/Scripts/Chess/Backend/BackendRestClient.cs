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
        string url = BackendConfig.BaseUrl + path;
        UnityWebRequest request = new UnityWebRequest(url, method);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Accept", "application/json");

        if (body != null)
        {
            string json = JsonConvert.SerializeObject(body);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.SetRequestHeader("Content-Type", "application/json");
        }

        if (authorized && !string.IsNullOrWhiteSpace(BackendSessionStore.Token))
            request.SetRequestHeader("Authorization", $"Bearer {BackendSessionStore.Token}");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            BackendApiResponse<T> response = SafeDeserialize<BackendApiResponse<T>>(request.downloadHandler.text);
            if (response == null)
            {
                onError?.Invoke("Unable to parse server response.", null);
                yield break;
            }

            onSuccess?.Invoke(response);
            yield break;
        }

        BackendApiResponse<object> errorResponse = SafeDeserialize<BackendApiResponse<object>>(request.downloadHandler.text);
        if (request.responseCode == 401)
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
