using System.Collections.Generic;
using UnityEngine;

internal static class BotAvatarCatalog
{
    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    internal static Sprite Get(StockfishDifficultyProfile profile)
    {
        if (sprites.TryGetValue(profile.AvatarResource, out var sprite)) return sprite;
        var texture = Resources.Load<Texture2D>(profile.AvatarResource);
        if (!texture) return null;
        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
        sprite.name = profile.BotName + " Avatar";
        sprites.Add(profile.AvatarResource, sprite);
        return sprite;
    }
}
