using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RegisterController : MonoBehaviour
{
    [SerializeField] private TMP_InputField usernameField;
    [SerializeField] private TMP_InputField emailField;
    [SerializeField] private TMP_InputField passwordField;
    [SerializeField] private TMP_InputField confirmPasswordField;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button registerButton;
    private readonly AuthService auth = new AuthService();
    private bool busy;

    private void OnEnable()
    {
        if (registerButton) registerButton.onClick.AddListener(Register);
    }

    private void OnDisable()
    {
        if (registerButton) registerButton.onClick.RemoveListener(Register);
    }

    public async void Register()
    {
        if (busy) return;
        string username = usernameField ? usernameField.text.Trim() : "";
        string email = emailField ? emailField.text.Trim() : "";
        string password = passwordField ? passwordField.text : "";
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Show("Enter username, email and password.");
            return;
        }
        if (confirmPasswordField && password != confirmPasswordField.text)
        {
            Show("Passwords do not match.");
            return;
        }

        busy = true;
        if (registerButton) registerButton.interactable = false;
        Show("Creating account...");
        try
        {
            await auth.RegisterAsync(username, email, password);
            if (this != null) Show("Account created. Log in with your email.");
        }
        catch (Exception error)
        {
            if (this != null) Show(PlayerNotificationText.FromException(error));
        }
        finally
        {
            if (this != null)
            {
                busy = false;
                if (registerButton) registerButton.interactable = true;
            }
        }
    }

    private void Show(string message)
    {
        if (messageText) messageText.text = message;
        Debug.Log("[RegisterController] " + message);
    }
}
