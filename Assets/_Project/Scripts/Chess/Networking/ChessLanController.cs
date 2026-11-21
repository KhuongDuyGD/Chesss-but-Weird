using System.IO;
using UnityEngine;

public enum NetworkLobbyUiMode { Lan, Multiplayer }

public partial class ChessLanController : MonoBehaviour
{
    private static GUIStyle GetTitleStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold
        };
    }

    private static GUIStyle GetSectionStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
    }

    private static GUIStyle GetBodyStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            wordWrap = true
        };
    }

    private static GUIStyle GetHudStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
    }

    private static GUIStyle GetStatusStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Italic,
            wordWrap = true
        };
    }

    private static float GetGuiScale()
    {
        return ResponsiveUi.GetFitScale(ReferenceWidth, ReferenceHeight);
    }

    private static Texture2D LoadProjectTexture(string projectRelativePath)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath);
        if (!File.Exists(fullPath))
            return null;

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        if (!texture.LoadImage(bytes))
        {
            UnityEngine.Object.Destroy(texture);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(projectRelativePath);
        MenuTextureSampling.FinishRuntimeTexture(texture);
        return texture;
    }
}
