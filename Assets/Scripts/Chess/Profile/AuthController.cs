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
    private string statusMessage = "Login or create a profile to continue.";
    private bool registerMode;
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

            float panelWidth = Mathf.Clamp(guiWidth * 0.42f, 640f, 820f);
            float panelHeight = registerMode ? 520f : 450f;
            Rect panelRect = new Rect(
                (guiWidth - panelWidth) * 0.5f,
                (guiHeight - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            GUI.Box(panelRect, string.Empty);
            GUI.Label(new Rect(panelRect.x + 32f, panelRect.y + 28f, panelRect.width - 64f, 54f), registerMode ? "Create Profile" : "Player Login", titleStyle);
            GUI.Label(new Rect(panelRect.x + 40f, panelRect.y + 96f, 150f, 36f), "Username", bodyStyle);
            username = GUI.TextField(new Rect(panelRect.x + 200f, panelRect.y + 92f, panelRect.width - 240f, 42f), username, 24);

            GUI.Label(new Rect(panelRect.x + 40f, panelRect.y + 154f, 150f, 36f), "Password", bodyStyle);
            password = GUI.PasswordField(new Rect(panelRect.x + 200f, panelRect.y + 150f, panelRect.width - 240f, 42f), password, '*', 64);

            float nextY = panelRect.y + 212f;
            if (registerMode)
            {
                GUI.Label(new Rect(panelRect.x + 40f, nextY + 4f, 150f, 36f), "Confirm", bodyStyle);
                confirmPassword = GUI.PasswordField(new Rect(panelRect.x + 200f, nextY, panelRect.width - 240f, 42f), confirmPassword, '*', 64);
                nextY += 62f;
            }

            GUI.Label(new Rect(panelRect.x + 40f, nextY, panelRect.width - 80f, 62f), statusMessage, statusStyle);

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
        }
        finally
        {
            GUI.matrix = previousMatrix;
        }
    }

    private void Submit()
    {
        if (registerMode)
        {
            if (password != confirmPassword)
            {
                statusMessage = "Passwords do not match.";
                return;
            }

            if (!PlayerAuthService.Register(username, password, out statusMessage))
                return;
        }
        else if (!PlayerAuthService.Login(username, password, out statusMessage))
        {
            return;
        }

        authenticatedCallback?.Invoke();
        Destroy(gameObject);
    }

    private void ToggleMode()
    {
        registerMode = !registerMode;
        password = string.Empty;
        confirmPassword = string.Empty;
        statusMessage = registerMode ? "Create a new local profile." : "Login with your local profile.";
    }

    private void ClearInput()
    {
        username = string.Empty;
        password = string.Empty;
        confirmPassword = string.Empty;
        statusMessage = "Login or create a profile to continue.";
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
