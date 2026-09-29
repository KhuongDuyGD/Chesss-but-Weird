using UnityEngine;

/// <summary>Preserves the original drawing pixel for pixel; trims only its transparent export padding.</summary>
public static class MainMenuLogoOriginal
{
    public const string ResourcePath = "MainMenu/MainMenuLogoOriginal";

    // The same bounds used by the original GameLogo.png menu sprite, in its 1448 x 1086 export.
    public static Sprite LoadSprite()
    {
        Texture2D texture = Resources.Load<Texture2D>(ResourcePath);
        if (!texture) return null;
        MenuTextureSampling.Configure(texture);
        float scaleX = texture.width / 1448f, scaleY = texture.height / 1086f;
        var rect = new Rect(82f * scaleX, 223f * scaleY, 1294f * scaleX, 715f * scaleY);
        var sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        sprite.name = "MainMenuLogoOriginal";
        return sprite;
    }
}
