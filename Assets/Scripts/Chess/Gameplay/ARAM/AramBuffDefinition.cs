using System.Collections.Generic;
using UnityEngine;

public enum AramBuffTier
{
    Silver,
    Gold,
    Diamond,
    Legendary
}

public enum AramBuffId
{
    CommandantPawn,
    StrongFortress,
    FreestyleLeap,
    Doppelganger,
    SuicideBomber,
    FlyingThunderGod
}

[CreateAssetMenu(fileName = "AramBuffDefinition", menuName = "Chess/ARAM/Buff Definition")]
public sealed class AramBuffDefinition : ScriptableObject
{
    [SerializeField] private AramBuffId id;
    [SerializeField] private AramBuffTier tier;
    [SerializeField] private string displayName;
    [SerializeField] private string shortName;
    [SerializeField, TextArea(2, 5)] private string description;
    [SerializeField] private Color accentColor = new Color(1f, 0.82f, 0.28f, 1f);

    public AramBuffId Id => id;
    public AramBuffTier Tier => tier;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id.ToString() : displayName;
    public string ShortName => string.IsNullOrWhiteSpace(shortName) ? DisplayName : shortName;
    public string Description => description ?? string.Empty;
    public Color AccentColor => accentColor;

    public void Configure(AramBuffId newId, AramBuffTier newTier, string newDisplayName, string newShortName, string newDescription, Color newAccentColor)
    {
        id = newId;
        tier = newTier;
        displayName = newDisplayName;
        shortName = newShortName;
        description = newDescription;
        accentColor = newAccentColor;
    }
}

public static class AramBuffLibrary
{
    public static List<AramBuffDefinition> CreateBuiltInDefinitions()
    {
        return new List<AramBuffDefinition>
        {
            Create(
                AramBuffId.CommandantPawn,
                AramBuffTier.Silver,
                "Commandant Pawn",
                "Commandant",
                "Ba Tot gan trung tam duoc phep di 2 o tien len vinh vien neu duong di trong.",
                new Color(0.76f, 0.82f, 0.94f, 1f)),
            Create(
                AramBuffId.StrongFortress,
                AramBuffTier.Silver,
                "Strong Fortress",
                "Fortress",
                "Vua duoc nhap thanh ngay ca khi dang bi chieu hoac di qua o bi tan cong.",
                new Color(0.64f, 0.84f, 1f, 1f)),
            Create(
                AramBuffId.FreestyleLeap,
                AramBuffTier.Silver,
                "Freestyle Leap",
                "Leap",
                "Ma mo rong buoc nhay chu L, co them cac diem nhay cheo 2x2.",
                new Color(0.74f, 1f, 0.78f, 1f)),
            Create(
                AramBuffId.Doppelganger,
                AramBuffTier.Gold,
                "The Doppelganger",
                "Swap Kit",
                "Mot Ma va mot Tuong dau tran hoan doi bo ky nang di chuyen cho nhau.",
                new Color(1f, 0.78f, 0.24f, 1f)),
            Create(
                AramBuffId.SuicideBomber,
                AramBuffTier.Gold,
                "Suicide Bomber",
                "Bomber",
                "Hau goc khi bi an se no quanh o do mot lan, pha huy quan xung quanh.",
                new Color(1f, 0.48f, 0.32f, 1f)),
            Create(
                AramBuffId.FlyingThunderGod,
                AramBuffTier.Diamond,
                "Flying Thunder God",
                "Teleport",
                "Hau goc co the dich chuyen toi bat ky o trong, toi da 5 lan, hoi 5 luot cua phe do.",
                new Color(0.58f, 0.94f, 1f, 1f))
        };
    }

    private static AramBuffDefinition Create(AramBuffId id, AramBuffTier tier, string displayName, string shortName, string description, Color accentColor)
    {
        AramBuffDefinition definition = ScriptableObject.CreateInstance<AramBuffDefinition>();
        definition.name = $"ARAM {id}";
        definition.Configure(id, tier, displayName, shortName, description, accentColor);
        return definition;
    }
}
