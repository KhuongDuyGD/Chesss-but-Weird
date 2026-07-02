using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class AuthController : MonoBehaviour
{
    private Action authenticatedCallback;
    private MainMenuAuthUI mainMenuAuthUI;
    private string statusMessage = "Login or create an account on the backend server.";
    private bool requestInFlight;

    public static AuthController Create(ChessGame game, Action onAuthenticated)
    {
        GameObject root = new GameObject("Chess Auth Controller");
        AuthController controller = root.AddComponent<AuthController>();
        controller.authenticatedCallback = onAuthenticated;
        controller.mainMenuAuthUI = root.AddComponent<MainMenuAuthUI>();
        controller.mainMenuAuthUI.Initialize(null, PlayerAuthService.GetLastUsername(), controller.statusMessage);
        controller.mainMenuAuthUI.SubmitRequested += controller.Submit;
        controller.mainMenuAuthUI.GuestRequested += controller.PlayAsGuest;
        return controller;
    }

    private void Update()
    {
        if (!requestInFlight && WasSubmitPressedThisFrame())
            Submit();
    }

    private void Submit()
    {
        if (requestInFlight || mainMenuAuthUI == null)
            return;

        string username = mainMenuAuthUI.Username.Trim();
        string password = mainMenuAuthUI.Password;
        string confirmPassword = mainMenuAuthUI.ConfirmPassword;

        if (string.IsNullOrWhiteSpace(username))
        {
            SetStatusMessage("Username / Email is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            SetStatusMessage("Password is required.");
            return;
        }

        if (mainMenuAuthUI.CurrentMode == MainMenuAuthUI.AuthMode.SignUp && password != confirmPassword)
        {
            SetStatusMessage("Passwords do not match.");
            return;
        }

        requestInFlight = true;
        mainMenuAuthUI.SetInteractable(false);
        SetStatusMessage(mainMenuAuthUI.CurrentMode == MainMenuAuthUI.AuthMode.SignUp ? "Creating account..." : "Logging in...");

        BackendAuthRequest request = new BackendAuthRequest
        {
            username = username,
            password = password
        };

        string path = mainMenuAuthUI.CurrentMode == MainMenuAuthUI.AuthMode.SignUp ? "/api/auth/signup" : "/api/auth/login";
        StartCoroutine(BackendRestClient.Send<BackendAuthResponseDto>(
            "POST",
            path,
            request,
            false,
            HandleAuthSuccess,
            HandleAuthFailure));
    }

    private void HandleAuthSuccess(BackendApiResponse<BackendAuthResponseDto> response)
    {
        requestInFlight = false;
        string sessionError = "The server returned an invalid login session. Please try again.";
        if (response == null || !PlayerAuthService.TryApplyAuthResponse(response.result, out sessionError))
        {
            mainMenuAuthUI?.SetInteractable(true);
            mainMenuAuthUI?.ClearSensitiveFields();
            SetStatusMessage(string.IsNullOrWhiteSpace(sessionError)
                ? "The server returned an invalid login session. Please try again."
                : sessionError);
            return;
        }

        SetStatusMessage(string.IsNullOrWhiteSpace(response.message) ? "Authenticated." : response.message);
        authenticatedCallback?.Invoke();
        if (mainMenuAuthUI != null)
        {
            mainMenuAuthUI.SubmitRequested -= Submit;
            mainMenuAuthUI.GuestRequested -= PlayAsGuest;
        }
        Destroy(gameObject);
    }

    private void HandleAuthFailure(string message, BackendApiResponse<object> response)
    {
        requestInFlight = false;
        if (mainMenuAuthUI != null)
            mainMenuAuthUI.SetInteractable(true);

        statusMessage = message;
        if (response != null && response.errors != null && response.errors.Count > 0)
        {
            foreach (var entry in response.errors)
            {
                statusMessage = $"{entry.Key}: {entry.Value}";
                break;
            }
        }

        if (string.Equals(message, "Cannot connect to destination host", StringComparison.OrdinalIgnoreCase))
            statusMessage = $"Cannot connect to backend: {BackendConfig.BaseUrl}";

        SetStatusMessage(statusMessage);
        if (mainMenuAuthUI != null)
            mainMenuAuthUI.ClearSensitiveFields();
    }

    private void SetStatusMessage(string message)
    {
        statusMessage = message ?? string.Empty;
        if (mainMenuAuthUI != null)
            mainMenuAuthUI.SetStatusMessage(statusMessage);
    }

    private void OnDestroy()
    {
        if (mainMenuAuthUI != null)
        {
            mainMenuAuthUI.SubmitRequested -= Submit;
            mainMenuAuthUI.GuestRequested -= PlayAsGuest;
        }
    }

    private void PlayAsGuest()
    {
        if (requestInFlight)
            return;

        PlayerAuthService.BeginGuestSession();
        SetStatusMessage("Playing as Guest. Only Local mode is available.");
        authenticatedCallback?.Invoke();
        if (mainMenuAuthUI != null)
        {
            mainMenuAuthUI.SubmitRequested -= Submit;
            mainMenuAuthUI.GuestRequested -= PlayAsGuest;
        }
        Destroy(gameObject);
    }

    private static bool WasSubmitPressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
    }
}
