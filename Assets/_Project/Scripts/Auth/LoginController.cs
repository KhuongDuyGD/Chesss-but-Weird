using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LoginController : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailField;
    [SerializeField] private TMP_InputField passwordField;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button healthButton;
    [SerializeField] private string mainMenuSceneName = "";
    private readonly AuthService auth = new AuthService();
    private readonly UserService users = new UserService();
    private bool busy;

    private void OnEnable()
    {
        if (loginButton) loginButton.onClick.AddListener(Login);
        if (healthButton) healthButton.onClick.AddListener(CheckHealth);
    }

    private void OnDisable()
    {
        if (loginButton) loginButton.onClick.RemoveListener(Login);
        if (healthButton) healthButton.onClick.RemoveListener(CheckHealth);
    }

    public async void CheckHealth()
    {
        if (busy) return;
        SetBusy(true);
        try
        {
            await ApiClient.Shared.CheckHealthAsync();
            if (this != null) Show("Server ready.");
        }
        catch (Exception error)
        {
            if (this != null) Show(PlayerNotificationText.FromException(error));
        }
        finally
        {
            if (this != null) SetBusy(false);
        }
    }

    public async void Login()
    {
        if (busy) return;
        string email = emailField ? emailField.text.Trim() : "";
        string password = passwordField ? passwordField.text : "";
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Show("Enter email and password.");
            return;
        }

        SetBusy(true);
        Show("Logging in...");
        try
        {
            await auth.LoginAsync(email, password);
            UserMeResponse user = await users.GetMeAsync();
            if (this == null) return;
            PlayerAuthService.ApplyApiUser(user);
            Show("Welcome, " + PlayerAuthService.CurrentDisplayName + "!");
            if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
                SceneManager.LoadScene(mainMenuSceneName);
        }
        catch (Exception error)
        {
            if (this != null) Show(PlayerNotificationText.FromException(error));
        }
        finally
        {
            if (this != null) SetBusy(false);
        }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        if (loginButton) loginButton.interactable = !value;
        if (healthButton) healthButton.interactable = !value;
    }

    private void Show(string message)
    {
        if (messageText) messageText.text = message;
        Debug.Log("[LoginController] " + message);
    }
}
