using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AuthController : MonoBehaviour
{
    private Action authenticatedCallback;
    private MainMenuAuthUI mainMenuAuthUI;
    private bool requestInFlight;
    private MainMenuAuthUI.AuthMode requestMode;

    public static AuthController Create(ChessGame game, Action onAuthenticated)
    {
        GameMusicManager.PlayAuthMusic();
        GameObject root = new GameObject("Chess Auth Controller");
        AuthController controller = root.AddComponent<AuthController>();
        controller.authenticatedCallback = onAuthenticated;
        controller.mainMenuAuthUI = root.AddComponent<MainMenuAuthUI>();
        controller.mainMenuAuthUI.Initialize(null, PlayerAuthService.GetLastUsername(), string.Empty);
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
        requestMode = mainMenuAuthUI.CurrentMode;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowFailure("Enter your username or email address.");
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowFailure("Enter your password.");
            return;
        }

        if (mainMenuAuthUI.CurrentMode == MainMenuAuthUI.AuthMode.SignUp && password != confirmPassword)
        {
            ShowFailure("Your passwords do not match. Please enter them again.");
            return;
        }

        requestInFlight = true;
        mainMenuAuthUI.SetInteractable(false);
        AuthNotificationView.Show(requestMode == MainMenuAuthUI.AuthMode.SignUp ? "Creating your account..." : "Logging in...",
            "Please wait a moment.", AuthNotificationView.ResultKind.Info);

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
            ShowFailure(string.IsNullOrWhiteSpace(sessionError)
                ? "The server returned an invalid login session. Please try again."
                : sessionError);
            return;
        }

        bool signedUp = requestMode == MainMenuAuthUI.AuthMode.SignUp;
        AuthNotificationView.Show(signedUp ? "Sign-up successful!" : "Login successful!",
            signedUp ? "Your account is ready. Let the games begin!" : "Welcome back! You're ready to play.",
            AuthNotificationView.ResultKind.Success);
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

        string reason = message;
        if (response != null && response.errors != null && response.errors.Count > 0)
        {
            foreach (var entry in response.errors)
            {
                if (string.IsNullOrWhiteSpace(entry.Value)) continue;
                reason = $"{entry.Key}: {entry.Value}";
                break;
            }
        }

        if (string.Equals(message, "Cannot connect to destination host", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(message, "Cannot resolve destination host", StringComparison.OrdinalIgnoreCase))
            reason = "We couldn't reach the server. Check your connection and try again.";

        ShowFailure(reason);
        if (mainMenuAuthUI != null)
            mainMenuAuthUI.ClearSensitiveFields();
    }

    private void ShowFailure(string reason)
    {
        AuthNotificationView.Show(requestMode == MainMenuAuthUI.AuthMode.SignUp ? "Sign-up unsuccessful" : "Login unsuccessful",
            string.IsNullOrWhiteSpace(reason) ? "The server couldn't complete your request. Please try again." : reason,
            AuthNotificationView.ResultKind.Error);
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
        AuthNotificationView.Show("Playing as Guest", "Local play is ready. Log in to unlock account features.", AuthNotificationView.ResultKind.Info);
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
        // Buttons handle Submit themselves, including the banner's close button.
        // Pressing Enter there must not also submit the authentication form.
        var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (selected && selected.GetComponent<Button>()) return false;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
    }
}
