// Minimal Unity resource/serialization ports for the production localization and definition code.
// This executable never starts Unity or reads/writes the player's preferences.
using System.Text.Json;

public static class UserSettings { public static string LanguageCode = "en"; }
public static class PlayModeNames
{
    public static string DisplayText(string value) => (value ?? "").Replace("ARAM", "CHAOS").Replace("Aram", "Chaos");
}

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static implicit operator bool(Object value) => value != null;
    }
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }
    public readonly struct Color
    {
        public readonly float r, g, b, a;
        public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }
    public sealed class TextAsset : Object { public string text; }
    public static class Resources
    {
        public static readonly Dictionary<string, string> Overrides = new();
        public static T Load<T>(string resource) where T : Object
        {
            if (Overrides.TryGetValue(resource, out var text)) return new TextAsset { text = text } as T;
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", resource.Replace('/', Path.DirectorySeparatorChar) + ".json");
            return File.Exists(path) ? new TextAsset { text = File.ReadAllText(path) } as T : null;
        }
    }
    public static class JsonUtility
    {
        public static T FromJson<T>(string json)
        {
            try { return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { IncludeFields = true }); }
            catch (JsonException ex) { throw new ArgumentException("Invalid JSON", ex); }
        }
    }
    public static class Debug
    {
        public static int Warnings;
        public static void LogWarning(string message) { Warnings++; }
    }
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TextAreaAttribute : Attribute
    {
        public TextAreaAttribute(int min, int max) { }
    }
    [AttributeUsage(AttributeTargets.Class)] public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName, menuName;
    }
}
