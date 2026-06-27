using System;
using UnityEngine;

public class AuthController : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    private ChessGame chessGame;
    private Action authenticatedCallback;
    private HandDrawnMenuAssets menuAssets;
    private string username = string.Empty;
    private string password = string.Empty;
    private string confirmPassword = string.Empty;
    private string serverHost = string.Empty;
    private string serverPort = string.Empty;
    private string statusMessage = "Login or create an account on the backend server.";
    private bool registerMode;
    private bool requestInFlight;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle statusStyle;

    public static AuthController Create(ChessGame game, Action onAuthenticated)
    {
        GameObject root = new GameObject("Chess Auth Controller");
        AuthController controller = root.AddComponent<AuthController>();
        controller.chessGame = game;
        controller.authenticatedCallback = onAuthenticated;
        controller.username = PlayerAuthService.GetLastUsername();
        controller.serverHost = BackendConfig.Host;
        controller.serverPort = BackendConfig.Port.ToString();
        controller.menuAssets = root.AddComponent<HandDrawnMenuAssets>();
        controller.menuAssets.LoadFromResources();
        return controller;
    }

    private void OnGUI()
    {
        EnsureStyles();

        Matrix4x4 previousMatrix = GUI.matrix;
        float scale = GetGuiScale();
        float guiWidth = Screen.width / scale;
        float guiHeight = Screen.height / scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        try
        {
            DrawMenuBackground(guiWidth, guiHeight);

            float panelWidth = Mathf.Clamp(guiWidth * 0.46f, 700f, 900f);
            float panelHeight = registerMode ? 620f : 550f;
            Rect panelRect = new Rect(
                (guiWidth - panelWidth) * 0.5f,
                (guiHeight - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            GUI.Box(panelRect, string.Empty);
            GUI.Label(new Rect(panelRect.x + 32f, panelRect.y + 24f, panelRect.width - 64f, 54f), registerMode ? "Register" : "Login", titleStyle);

            GUI.Label(new Rect(panelRect.x + 40f, panelRect.y + 92f, 170f, 36f), "Server URL / Host", bodyStyle);
            serverHost = GUI.TextField(new Rect(panelRect.x + 220f, panelRect.y + 88f, panelRect.width - 260f, 42f), serverHost, 128);

            GUI.Label(new Rect(panelRect.x + 40f, panelRect.y + 146f, 170f, 36f), "Port (host only)", bodyStyle);
            serverPort = GUI.TextField(new Rect(panelRect.x + 220f, panelRect.y + 142f, 180f, 42f), serverPort, 8);

            GUI.Label(new Rect(panelRect.x + 40f, panelRect.y + 200f, 150f, 36f), "Username", bodyStyle);
            username = GUI.TextField(new Rect(panelRect.x + 220f, panelRect.y + 196f, panelRect.width - 260f, 42f), username, 50);

            GUI.Label(new Rect(panelRect.x + 40f, panelRect.y + 254f, 150f, 36f), "Password", bodyStyle);
            password = GUI.PasswordField(new Rect(panelRect.x + 220f, panelRect.y + 250f, panelRect.width - 260f, 42f), password, '*', 72);

            float nextY = panelRect.y + 308f;
            if (registerMode)
            {
                GUI.Label(new Rect(panelRect.x + 40f, nextY, 150f, 36f), "Confirm", bodyStyle);
                confirmPassword = GUI.PasswordField(new Rect(panelRect.x + 220f, nextY - 4f, panelRect.width - 260f, 42f), confirmPassword, '*', 72);
                nextY += 58f;
            }

            GUI.Label(new Rect(panelRect.x + 40f, nextY, panelRect.width - 80f, 48f), "Use host + port for local testing, or paste a public backend URL like https://body-unstamped-decimeter.ngrok-free.dev", statusStyle);
            GUI.Label(new Rect(panelRect.x + 40f, nextY + 52f, panelRect.width - 80f, 82f), statusMessage, statusStyle);

            bool previousEnabled = GUI.enabled;
            GUI.enabled = !requestInFlight;

            float buttonY = panelRect.y + panelRect.height - 104f;
            Rect primaryButton = new Rect(panelRect.x + 40f, buttonY, 220f, 58f);
            Rect secondaryButton = new Rect(panelRect.x + 280f, buttonY, 220f, 58f);
            Rect switchButton = new Rect(panelRect.x + panelRect.width - 260f, buttonY, 220f, 58f);

            if (GUI.Button(primaryButton, registerMode ? "Sign Up" : "Login", buttonStyle))
                Submit();

            if (GUI.Button(secondaryButton, "Clear", buttonStyle))
                ClearInput();

            if (GUI.Button(switchButton, registerMode ? "Login" : "Sign Up", buttonStyle))
                ToggleMode();

            GUI.enabled = previousEnabled;
        }
        finally
        {
            GUI.matrix = previousMatrix;
        }
    }

    private void Submit()
    {
        if (!TryApplyServerConfig())
            return;

        if (registerMode && password != confirmPassword)
        {
            statusMessage = "Passwords do not match.";
            return;
        }

        requestInFlight = true;
        statusMessage = registerMode ? "Creating account..." : "Logging in...";

        BackendAuthRequest request = new BackendAuthRequest
        {
            username = username.Trim(),
            password = password
        };

        string path = registerMode ? "/api/auth/signup" : "/api/auth/login";
        StartCoroutine(BackendRestClient.Send<BackendAuthResponseDto>(
            "POST",
            path,
            request,
            false,
            HandleAuthSuccess,
            HandleAuthFailure));
    }

    private bool TryApplyServerConfig()
    {
        if (string.IsNullOrWhiteSpace(serverHost))
        {
            statusMessage = "Server host or URL is required.";
            return false;
        }

        string trimmedHost = serverHost.Trim();
        if (Uri.TryCreate(trimmedHost, UriKind.Absolute, out Uri absoluteUri))
        {
            if (!string.Equals(absoluteUri.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(absoluteUri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                statusMessage = "Server URL must start with http:// or https://.";
                return false;
            }

            BackendConfig.Host = absoluteUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            BackendConfig.Scheme = absoluteUri.Scheme;
            if (!absoluteUri.IsDefaultPort)
                BackendConfig.Port = absoluteUri.Port;
            BackendConfig.Save();
            return true;
        }

        if (!int.TryParse(serverPort, out int parsedPort) || parsedPort < 1 || parsedPort > 65535)
        {
            statusMessage = "Server port must be between 1 and 65535.";
            return false;
        }

        BackendConfig.Host = trimmedHost;
        BackendConfig.Port = parsedPort;
        BackendConfig.Scheme = "http";
        BackendConfig.Save();
        return true;
    }

    private void HandleAuthSuccess(BackendApiResponse<BackendAuthResponseDto> response)
    {
        requestInFlight = false;
        PlayerAuthService.ApplyAuthResponse(response.result);
        statusMessage = string.IsNullOrWhiteSpace(response.message) ? "Authenticated." : response.message;
        authenticatedCallback?.Invoke();
        Destroy(gameObject);
    }

    private void HandleAuthFailure(string message, BackendApiResponse<object> response)
    {
        requestInFlight = false;
        statusMessage = message;
        if (response != null && response.errors != null && response.errors.Count > 0)
        {
            foreach (var entry in response.errors)
            {
                statusMessage = $"{entry.Key}: {entry.Value}";
                break;
            }
        }
    }

    private void ToggleMode()
    {
        registerMode = !registerMode;
        password = string.Empty;
        confirmPassword = string.Empty;
        statusMessage = registerMode
            ? "Create an account on the Spring Boot backend."
            : "Login with an existing backend account.";
    }

    private void ClearInput()
    {
        username = string.Empty;
        password = string.Empty;
        confirmPassword = string.Empty;
        statusMessage = "Login or create an account on the backend server.";
    }

    private void DrawMenuBackground(float width, float height)
    {
        Color previousColor = GUI.color;
        GUI.color = new Color(0.985f, 0.965f, 0.91f, 1f);
        GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (menuAssets != null && menuAssets.HasRequiredSprites)
        {
            DrawSprite(menuAssets.mascot, new Vector2(-830f, 410f), new Vector2(225f, 175f), 0.95f);
            DrawSprite(menuAssets.logo, new Vector2(0f, 300f), new Vector2(720f, 360f), 1f);
            DrawSprite(menuAssets.crown, new Vector2(-790f, -390f), new Vector2(210f, 160f), 0.85f);
            DrawSprite(menuAssets.hearts, new Vector2(-475f, -305f), new Vector2(92f, 82f), 0.75f);
            DrawSprite(menuAssets.hearts, new Vector2(475f, -305f), new Vector2(100f, 90f), 0.75f);
            DrawSprite(menuAssets.stars, new Vector2(-480f, -70f), new Vector2(86f, 58f), 0.75f);
            DrawSprite(menuAssets.stars, new Vector2(480f, -95f), new Vector2(86f, 58f), 0.75f);
            DrawSprite(menuAssets.punchDoodle, new Vector2(-780f, 110f), new Vector2(220f, 142f), 0.28f);
            DrawSprite(menuAssets.punchDoodle, new Vector2(780f, 120f), new Vector2(220f, 142f), 0.22f);
            DrawSprite(menuAssets.earlyAccess, new Vector2(700f, -405f), new Vector2(320f, 145f), 0.95f);
        }

        GUI.color = new Color(0f, 0f, 0f, 0.08f);
        GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private void DrawSprite(Sprite sprite, Vector2 centerOffset, Vector2 size, float alpha)
    {
        if (!sprite || !sprite.texture)
            return;

        Rect rect = new Rect(
            (ReferenceWidth - size.x) * 0.5f + centerOffset.x,
            (ReferenceHeight - size.y) * 0.5f - centerOffset.y,
            size.x,
            size.y);
        Rect textureRect = sprite.rect;
        Rect texCoords = new Rect(
            textureRect.x / sprite.texture.width,
            textureRect.y / sprite.texture.height,
            textureRect.width / sprite.texture.width,
            textureRect.height / sprite.texture.height);

        Color previousColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTextureWithTexCoords(rect, sprite.texture, texCoords, true);
        GUI.color = previousColor;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
            return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 38,
            fontStyle = FontStyle.Bold
        };

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold
        };

        statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Italic,
            wordWrap = true
        };
    }

    private static float GetGuiScale()
    {
        float widthScale = Screen.width / ReferenceWidth;
        float heightScale = Screen.height / ReferenceHeight;
        return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 1f, 2f);
    }
}
