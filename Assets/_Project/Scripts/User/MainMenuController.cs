using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private TMP_Text usernameText;
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private TMP_Text eloText;
    [SerializeField] private TMP_Text goldsText;
    [SerializeField] private TMP_Text diamondsText;
    [SerializeField] private TMP_Text ticketsText;
    [SerializeField] private TMP_Text chessSkinIdText;
    [SerializeField] private TMP_Text boardSkinIdText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button refreshProfileButton;
    [SerializeField] private Button refreshTokenButton;
    [SerializeField] private Button autoRefreshTestButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private string loginSceneName = "";
    private readonly UserService users = new UserService();
    private readonly AuthService auth = new AuthService();
    private bool busy;

    private void OnEnable()
    {
        if (refreshProfileButton) refreshProfileButton.onClick.AddListener(LoadProfile);
        if (refreshTokenButton) refreshTokenButton.onClick.AddListener(RefreshToken);
        if (autoRefreshTestButton) autoRefreshTestButton.onClick.AddListener(TestAutoRefresh);
        if (logoutButton) logoutButton.onClick.AddListener(Logout);
        LoadProfile();
    }

    private void OnDisable()
    {
        if (refreshProfileButton) refreshProfileButton.onClick.RemoveListener(LoadProfile);
        if (refreshTokenButton) refreshTokenButton.onClick.RemoveListener(RefreshToken);
        if (autoRefreshTestButton) autoRefreshTestButton.onClick.RemoveListener(TestAutoRefresh);
        if (logoutButton) logoutButton.onClick.RemoveListener(Logout);
    }

    public async void LoadProfile()
    {
        if (busy) return;
        SetBusy(true);
        try
        {
            UserMeResponse user = await users.GetMeAsync();
            if (this == null) return;
            PlayerAuthService.ApplyApiUser(user);
            Set(usernameText, user.username);
            Set(displayNameText, user.profile?.displayName);
            Set(eloText, user.stats?.elo.ToString());
            Set(goldsText, user.wallet?.golds.ToString());
            Set(diamondsText, user.wallet?.diamonds.ToString());
            Set(ticketsText, user.wallet?.tickets.ToString());
            Set(chessSkinIdText, user.equipped?.chessSkinId);
            Set(boardSkinIdText, user.equipped?.boardSkinId);
            Show("Profile loaded.");
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

    public async void RefreshToken()
    {
        if (busy) return;
        SetBusy(true);
        try
        {
            await auth.RefreshAsync();
            if (this != null) Show("Tokens refreshed.");
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

    // Test-only: force a 401 while retaining the valid refresh token.
    public async void TestAutoRefresh()
    {
        if (busy || !AuthStorage.HasSession()) return;
        SetBusy(true);
        try
        {
            AuthStorage.SaveTokens("expired-access-token", AuthStorage.GetRefreshToken(),
                AuthStorage.AccessTokenExpiresAt, AuthStorage.RefreshTokenExpiresAt);
            UserMeResponse user = await users.GetMeAsync();
            if (this == null) return;
            PlayerAuthService.ApplyApiUser(user);
            Show("401 -> refresh -> /users/me retry succeeded.");
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
    public async void Logout()
    {
        if (busy) return;
        SetBusy(true);
        try
        {
            await auth.LogoutAsync();
            if (this == null) return;
            Show("Logged out.");
            if (!string.IsNullOrWhiteSpace(loginSceneName))
                SceneManager.LoadScene(loginSceneName);
        }
        catch (Exception error)
        {
            if (this != null) Show("Local session cleared. " + PlayerNotificationText.FromException(error));
        }
        finally
        {
            if (this != null) SetBusy(false);
        }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        if (refreshProfileButton) refreshProfileButton.interactable = !value;
        if (refreshTokenButton) refreshTokenButton.interactable = !value;
        if (autoRefreshTestButton) autoRefreshTestButton.interactable = !value;
        if (logoutButton) logoutButton.interactable = !value;
    }

    private static void Set(TMP_Text label, string value)
    {
        if (label) label.text = value ?? "";
    }

    private void Show(string message)
    {
        if (messageText) messageText.text = message;
        Debug.Log("[MainMenuController] " + message);
    }
}
