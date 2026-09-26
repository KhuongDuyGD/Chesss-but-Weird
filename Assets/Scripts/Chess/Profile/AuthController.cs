using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AuthController : MonoBehaviour
{
    private readonly AuthService authService = new AuthService();
    private readonly UserService userService = new UserService();
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
        controller.mainMenuAuthUI.Initialize(null, string.Empty, string.Empty);
        controller.mainMenuAuthUI.SubmitRequested += controller.Submit;
        controller.mainMenuAuthUI.GuestRequested += controller.PlayAsGuest;
        controller.CheckHealth();
        return controller;
    }

    private async void CheckHealth()
    {
        try
        {
            await ApiClient.Shared.CheckHealthAsync();
            if (this != null)
                Debug.Log("[AuthController] Render API health check succeeded.");
        }
        catch (Exception error)
        {
            if (this != null)
                mainMenuAuthUI?.SetStatusMessage("Server may be waking up: " + error.Message);
        }
    }

    private void Update()
    {
        if (!requestInFlight && WasSubmitPressedThisFrame())
            Submit();
    }

    private async void Submit()
    {
        if (requestInFlight || mainMenuAuthUI == null)
            return;

        requestMode = mainMenuAuthUI.CurrentMode;
        string username = mainMenuAuthUI.Username.Trim();
        string email = mainMenuAuthUI.Email.Trim();
        string password = mainMenuAuthUI.Password;
        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
        {
            ShowFailure("Enter a valid email address.");
            return;
        }
        if (requestMode == MainMenuAuthUI.AuthMode.SignUp && string.IsNullOrWhiteSpace(username))
        {
            ShowFailure("Enter a username.");
            return;
        }
        if (string.IsNullOrWhiteSpace(password))
        {
            ShowFailure("Enter your password.");
            return;
        }
        if (requestMode == MainMenuAuthUI.AuthMode.SignUp && password != mainMenuAuthUI.ConfirmPassword)
        {
            ShowFailure("Your passwords do not match.");
            return;
        }

        requestInFlight = true;
        mainMenuAuthUI.SetInteractable(false);
        AuthNotificationView.Show(requestMode == MainMenuAuthUI.AuthMode.SignUp ? "Creating account..." : "Logging in...",
            "Please wait for the server.", AuthNotificationView.ResultKind.Info);

        try
        {
            if (requestMode == MainMenuAuthUI.AuthMode.SignUp)
                await authService.RegisterAsync(username, email, password);
            await authService.LoginAsync(email, password);
            UserMeResponse user = await userService.GetMeAsync();
            if (this == null) return;
            PlayerAuthService.ApplyApiUser(user);
            AuthNotificationView.Show("Account ready", "Welcome, " + PlayerAuthService.CurrentDisplayName + "!",
                AuthNotificationView.ResultKind.Success);
            authenticatedCallback?.Invoke();
            Destroy(gameObject);
        }
        catch (Exception error)
        {
            if (this == null) return;
            requestInFlight = false;
            mainMenuAuthUI.SetInteractable(true);
            mainMenuAuthUI.ClearSensitiveFields();
            ShowFailure(error.Message);
        }
    }

    private void ShowFailure(string reason)
    {
        AuthNotificationView.Show(requestMode == MainMenuAuthUI.AuthMode.SignUp ? "Sign-up unsuccessful" : "Login unsuccessful",
            reason, AuthNotificationView.ResultKind.Error);
    }

    private void PlayAsGuest()
    {
        if (requestInFlight) return;
        PlayerAuthService.BeginGuestSession();
        AuthNotificationView.Show("Playing as Guest", "Local play is ready.", AuthNotificationView.ResultKind.Info);
        authenticatedCallback?.Invoke();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (mainMenuAuthUI == null) return;
        mainMenuAuthUI.SubmitRequested -= Submit;
        mainMenuAuthUI.GuestRequested -= PlayAsGuest;
    }

    private static bool WasSubmitPressedThisFrame()
    {
        var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (selected && selected.GetComponent<Button>()) return false;
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
    }
}
