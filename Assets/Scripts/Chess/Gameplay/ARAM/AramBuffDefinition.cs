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
    // Stable serialized IDs; the Domain adapter maps each ID to its corresponding flag bit.
    CommandantPawn = 0,
    StrongFortress = 1,
    FreestyleLeap = 2,
    Doppelganger = 3,
    SuicideBomber = 4,
    FlyingThunderGod = 5,
    NobleSacrifice = 6, AbsoluteSniper = 7, GamblingLeadsToMisery = 8, LootBox = 9,
    PeaceTShirt = 10, RiseOfPawn = 11, MobileFortress = 12, HidingKing = 13, GachaBanner = 14,
    SubstituteNinjutsu = 15, HighTechEra = 16, QueensBetrayal = 17, DefinitionOfAram = 18,
    RngFiesta = 19, IFrameRoll = 20, GhostArmy = 21, PlagueTown = 22, CustomizeArmy = 23,
    PawnsRevolution = 24, OneManArmy = 25
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
                "Choose 3 allied Pawns. They may advance two squares throughout the match if both squares are empty. This move cannot capture or jump over pieces.",
                new Color(0.76f, 0.82f, 0.94f, 1f)),
            Create(
                AramBuffId.StrongFortress,
                AramBuffTier.Silver,
                "Strong Fortress",
                "Fortress",
                "Ignore one type of castling restriction: a moved King, a moved Rook, being in check, or crossing an attacked square. Two restriction types block castling. The path must be clear and the destination safe.",
                new Color(0.64f, 0.84f, 1f, 1f)),
            Create(
                AramBuffId.FreestyleLeap,
                AramBuffTier.Silver,
                "Freestyle Leap",
                "Leap",
                "Your Knights may land on any square along any of their L-shaped routes, including intermediate squares. They still jump over pieces. Two-by-two diagonal jumps are not included.",
                new Color(0.74f, 1f, 0.78f, 1f)),
            Create(
                AramBuffId.Doppelganger,
                AramBuffTier.Gold,
                "The Doppelganger",
                "Swap Kit",
                "Choose one Knight and one Bishop to exchange movement. Every 5 of your turns, you may swap both movement sets again without spending your move, or keep them.",
                new Color(1f, 0.78f, 0.24f, 1f)),
            Create(
                AramBuffId.SuicideBomber,
                AramBuffTier.Gold,
                "Suicide Bomber",
                "Bomber",
                "Once per match, your original Queen explodes when captured. All non-King pieces in the 3x3 area are destroyed, including the capturer and friendly pieces. Promoted Queens do not explode.",
                new Color(1f, 0.48f, 0.32f, 1f)),
            Create(
                AramBuffId.FlyingThunderGod,
                AramBuffTier.Diamond,
                "Flying Thunder God",
                "Teleport",
                "Your original Queen may teleport to any empty square as her move, provided your King stays safe. Maximum 5 teleports; 5 of your turns to recharge. Promoted Queens cannot teleport.",
                new Color(0.58f, 0.94f, 1f, 1f)),
            Entry(AramBuffId.NobleSacrifice, AramBuffTier.Silver, "Noble Sacrifice", "Sacrifice",
                "A Pawn may capture an allied Pawn diagonally forward. The survivor becomes Bloodthirsty: move or capture into any of the three squares directly ahead, or retreat one empty square."),
            Entry(AramBuffId.AbsoluteSniper, AramBuffTier.Silver, "Absolute Sniper", "Sniper",
                "A Bishop that captures an enemy non-Pawn from at least 4 squares away gains one ranged capture without moving. The charge expires after 5 of your turns."),
            Entry(AramBuffId.GamblingLeadsToMisery, AramBuffTier.Silver, "Gambling Leads to Misery", "Extra Turn",
                "Whenever your Pawn captures an enemy, it has a 30% chance to move again immediately. Only that Pawn may act. Maximum 2 extra turns per match."),
            Entry(AramBuffId.LootBox, AramBuffTier.Silver, "Loot Box", "Supply Drop",
                "Supply crates land on empty squares in rounds 25 and 50. Collect one to deploy a Rook, then a Queen, on an empty square in your half. Enemies destroy your crates by entering their square."),
            Entry(AramBuffId.PeaceTShirt, AramBuffTier.Gold, "Peace T-shirt", "Peace",
                "Keep your King out of check for your first 15 turns. Then recruit one enemy other than a King or Queen and deploy it on an empty square in your first three ranks."),
            Entry(AramBuffId.RiseOfPawn, AramBuffTier.Gold, "Rise of Pawn", "Pawn Mines",
                "Your Pawns may retreat one empty square. A Pawn retreating onto your back rank sacrifices itself and leaves a mine. An enemy entering the mine is destroyed with it."),
            Entry(AramBuffId.MobileFortress, AramBuffTier.Gold, "Mobile Fortress", "Transport",
                "A Rook may carry one allied Pawn without spending a move. If the Rook is destroyed, deploy its passenger on an empty square within the surrounding 3x3 area. Landing on the far rank promotes it."),
            Entry(AramBuffId.HidingKing, AramBuffTier.Gold, "Hiding King", "King Swap",
                "Swap your King with an allied piece on your back rank without spending a move. Available at the start and every 10 of your turns. Your King must end on a safe square."),
            Entry(AramBuffId.GachaBanner, AramBuffTier.Gold, "Gacha Banner", "Battle Gacha",
                "Captures grant tickets: Pawn 1, Knight/Bishop/Rook 2, Queen 3. Spend up to 2 tickets per turn to drop random allies on empty squares: Pawn 70%, Knight 10%, Bishop 10%, Rook 9%, Queen 1%."),
            Entry(AramBuffId.SubstituteNinjutsu, AramBuffTier.Diamond, "Substitute Ninjutsu", "Substitute",
                "The first time your King is checked, teleport it to a safe empty square in your half and leave a decoy. The decoy moves like a King and transforms into the first piece it captures."),
            Entry(AramBuffId.HighTechEra, AramBuffTier.Diamond, "The High-tech Era", "Cannon Rooks",
                "Your starting Rooks retain normal movement and may capture over exactly one intervening piece. Each Rook permanently loses its cannon after 10 of your turns without firing."),
            Entry(AramBuffId.QueensBetrayal, AramBuffTier.Diamond, "The Queen's Betrayal", "Unstable Queen",
                "Gain an extra Queen on your third rank. At the end of each owner's turn, she has a 10% chance to change sides."),
            Entry(AramBuffId.DefinitionOfAram, AramBuffTier.Diamond, "The Definition of ARAM", "Collapsing Board",
                "At round 10 the outermost files collapse. At round 15 the next pair collapses, leaving a 4x8 board. Pieces on collapsed squares are destroyed without capture rewards."),
            Entry(AramBuffId.RngFiesta, AramBuffTier.Diamond, "RNG Fiesta", "Army Reroll",
                "At the start, every allied piece except your King and Queen transforms randomly into a Pawn, Knight, Bishop, or Rook."),
            Entry(AramBuffId.IFrameRoll, AramBuffTier.Diamond, "I-Frame Roll", "Last Escape",
                "When checkmated, choose a safe empty square within a 5x5 area centered on your King to escape without spending a move. Maximum 3 escapes per match."),
            Entry(AramBuffId.GhostArmy, AramBuffTier.Legendary, "Ghost Army", "Phasing",
                "Your pieces may move through allied pieces but cannot share their destination. Enemy pieces still block paths. Phasing through allies cannot be used to check the enemy King."),
            Entry(AramBuffId.PlagueTown, AramBuffTier.Legendary, "Plague Town", "Plague",
                "Any enemy that captures one of your pieces becomes infected and dies after its next 4 owner turns. Kings and Queens are not immune."),
            Entry(AramBuffId.CustomizeArmy, AramBuffTier.Legendary, "Customize Army", "Formation",
                "Spend up to 2 minutes rearranging your army within your half, then confirm. If the final formation is unchanged, replace this buff with a random Gold buff."),
            Entry(AramBuffId.PawnsRevolution, AramBuffTier.Legendary, "Pawn's Revolution", "Pawn Army",
                "Sacrifice all your pieces except the King, then fill every other square in your half with allied Pawns."),
            Entry(AramBuffId.OneManArmy, AramBuffTier.Legendary, "One Man Army", "King's Rifle",
                "Keep only your King, who may also move two squares orthogonally. Start with a rifle shot and recharge every 3 turns. Aim in first person for 15 seconds; a miss or hitting a King/Queen ends the shot. Shots do not stack.")
        };
    }

    private static AramBuffDefinition Entry(AramBuffId id, AramBuffTier tier, string name, string shortName, string description)
    {
        Color color = tier == AramBuffTier.Silver ? new Color(.76f,.82f,.94f) :
            tier == AramBuffTier.Gold ? new Color(1f,.78f,.24f) :
            tier == AramBuffTier.Diamond ? new Color(.58f,.94f,1f) : new Color(.85f,.5f,1f);
        return Create(id,tier,name,shortName,description,color);
    }

    public static AramBuffDefinition FindById(IReadOnlyList<AramBuffDefinition> definitions, AramBuffId id)
    {
        if (definitions == null)
            return null;

        for (int i = 0; i < definitions.Count; i++)
        {
            AramBuffDefinition definition = definitions[i];
            if (definition && definition.Id == id)
                return definition;
        }

        return null;
    }

    public static bool TryParseId(string value, out AramBuffId id)
    {
        return System.Enum.TryParse(value, true, out id) && System.Enum.IsDefined(typeof(AramBuffId), id);
    }

    private static AramBuffDefinition Create(AramBuffId id, AramBuffTier tier, string displayName, string shortName, string description, Color accentColor)
    {
        AramBuffDefinition definition = ScriptableObject.CreateInstance<AramBuffDefinition>();
        definition.name = $"ARAM {id}";
        definition.Configure(id, tier, displayName, shortName, description, accentColor);
        return definition;
    }
}
