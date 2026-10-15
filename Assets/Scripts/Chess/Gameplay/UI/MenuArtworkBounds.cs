// Baked normalized PNG alpha bounds. Update the affected entries when source artwork changes.
using System.Collections.Generic;
using UnityEngine;

public static class MenuArtworkBounds
{
    private static readonly Dictionary<string, Vector4> Bounds = new Dictionary<string, Vector4>
    {
        { "Assets/Materials/Gacha_menu/DiamondPR.png", new Vector4(0.228867624f, 0.228867624f, 0.541467305f, 0.561403509f) },
        { "Assets/Materials/Gacha_menu/GachaRevealCardFrame.png", new Vector4(0.054687500f, 0.041176471f, 0.890625000f, 0.917647059f) },
        { "Assets/Materials/Gacha_menu/GachaRevealGlyph1.png", new Vector4(0.050000000f, 0.050000000f, 0.906250000f, 0.906250000f) },
        { "Assets/Materials/Gacha_menu/GachaRevealGlyph2.png", new Vector4(0.050000000f, 0.050000000f, 0.906250000f, 0.906250000f) },
        { "Assets/Materials/Gacha_menu/GachaRevealGlyph3.png", new Vector4(0.050000000f, 0.050000000f, 0.906250000f, 0.906250000f) },
        { "Assets/Materials/Gacha_menu/GachaRevealGlyph4.png", new Vector4(0.050000000f, 0.050000000f, 0.906250000f, 0.906250000f) },
        { "Assets/Materials/Gacha_menu/GachaRevealGlyph5.png", new Vector4(0.050000000f, 0.050000000f, 0.906250000f, 0.906250000f) },
        { "Assets/Materials/Gacha_menu/GachaRevealGlyph6.png", new Vector4(0.050000000f, 0.050000000f, 0.906250000f, 0.906250000f) },
        { "Assets/Materials/Gacha_menu/GachaRevealRay.png", new Vector4(0.024739583f, 0.024739583f, 0.951822917f, 0.951822917f) },
        { "Assets/Materials/Gacha_menu/GachaRevealRing.png", new Vector4(0.066406250f, 0.066406250f, 0.869140625f, 0.869140625f) },
        { "Assets/Materials/Gacha_menu/GachaRevealSpark.png", new Vector4(0.046875000f, 0.046875000f, 0.914062500f, 0.914062500f) },
        { "Assets/Materials/Gacha_menu/GoldPR.png", new Vector4(0.228070175f, 0.222488038f, 0.536682616f, 0.580542265f) },
        { "Assets/Materials/Gacha_menu/Skin5StarPR.png", new Vector4(0.186602871f, 0.215311005f, 0.629186603f, 0.626794258f) },
        { "Assets/Materials/Gacha_menu/SummonTicketPR.png", new Vector4(0.227272727f, 0.224880383f, 0.545454545f, 0.546251994f) },
        { "Assets/Materials/PlayerProfile/Avatar0.png", new Vector4(0.074218750f, 0.065843621f, 0.876953125f, 0.862139918f) },
        { "Assets/Materials/PlayerProfile/Back.png", new Vector4(0.167127072f, 0.218232044f, 0.666666667f, 0.577348066f) },
        { "Assets/Materials/LANUI/LANUIBlank.png", new Vector4(0.000000000f, 0.000000000f, 1.000000000f, 1.000000000f) },
    };

    public static Rect GetRect(string path, Texture2D texture)
    {
        if (!Bounds.TryGetValue(path, out Vector4 b)) return new Rect(0, 0, texture.width, texture.height);
        return new Rect(b.x * texture.width, b.y * texture.height, b.z * texture.width, b.w * texture.height);
    }
}
