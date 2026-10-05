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
    PawnsRevolution = 24, OneManArmy = 25,
    EveryManForHimself = 26, SacUrQueen = 27, AutoCastling = 28,
    UltimateQuest = 29, MonsterTruck = 30, CreditCard = 31,
    KingIsPoorPiece = 32, DoubleEdgedTrap = 33, Paratrooper = 34
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
            Entry(AramBuffId.CommandantPawn, AramBuffTier.Silver, "Commandant Pawn", "Commandant",
                "Choose 3 Pawns. They can advance two empty squares throughout the match, without capturing or jumping. Selected Pawns have a small ring."),
            Entry(AramBuffId.StrongFortress, AramBuffTier.Silver, "Strong Fortress", "Fortress",
                "Castle even while checked or across attacked squares. The destination must be safe and the path clear. After one wing, your King may castle on the other wing from the back rank, if its Rook is unmoved."),
            Entry(AramBuffId.FreestyleLeap, AramBuffTier.Silver, "Freestyle Leap", "Freestyle",
                "Choose one Knight. It may land on any intermediate square along its L-shaped routes, and still jumps over pieces. Two-by-two diagonals are excluded."),
            Entry(AramBuffId.Doppelganger, AramBuffTier.Gold, "The Doppelganger", "Doppelganger",
                "Choose a Knight and Bishop to exchange movement. Every 4 moves by either side, keep or swap both together, or choose a new pair, without spending a move. If either piece type disappears from your army, restore normal movement. Your opponent sees a different Gold buff in the HUD."),
            Entry(AramBuffId.SuicideBomber, AramBuffTier.Gold, "Suicide Bomber", "Bomber",
                "Once per match, your original Queen explodes when captured. All non-King pieces in the surrounding 3x3 area die, including the capturer and allies. Promoted Queens do not explode."),
            Entry(AramBuffId.FlyingThunderGod, AramBuffTier.Diamond, "Flying Thunder God", "Teleport",
                "Your original Queen may teleport to any empty square as her move, if your King remains safe. Recharge after 5 of your turns; maximum 5 uses. Promoted Queens cannot teleport."),
            Entry(AramBuffId.NobleSacrifice, AramBuffTier.Silver, "Noble Sacrifice", "Sacrifice",
                "A Pawn may capture an allied Pawn diagonally ahead and become Bloodthirsty. It can then move or capture into any of the three squares ahead, but cannot eat another allied Pawn."),
            Entry(AramBuffId.AbsoluteSniper, AramBuffTier.Silver, "Absolute Sniper", "Sniper",
                "A Bishop capturing an enemy non-Pawn from 4 or more squares away empowers its next diagonal capture: it automatically shoots without moving or confirmation. The shot spends your move. The charge expires after the next 4 moves by either side."),
            Entry(AramBuffId.GamblingLeadsToMisery, AramBuffTier.Silver, "Gambling Leads to Misery", "Gambling",
                "A Pawn capturing an enemy has a 25% chance of an immediate extra move with that Pawn. Maximum 3 extra moves per match."),
            Entry(AramBuffId.LootBox, AramBuffTier.Silver, "Loot Box", "Loot Box",
                "At combined move 25 and 50, a crate lands on a random square and captures its occupant. Kings are protected at 25, but not at 50. Collect it to place a Rook, then a Queen, in your half; enemies destroy your crates."),
            Entry(AramBuffId.PeaceTShirt, AramBuffTier.Gold, "Peace T-shirt", "Peace",
                "Keep your King out of check for the first 14 combined moves. Recruit one enemy other than a King or Queen into your first three ranks. If those ranks are full, you may replace a Pawn there."),
            Entry(AramBuffId.RiseOfPawn, AramBuffTier.Gold, "Devolution of Pawn?", "Pawn Mines",
                "All your Pawns can retreat one empty square. Retreating onto your back rank sacrifices the Pawn and leaves a mine. An enemy entering it dies and consumes the mine."),
            Entry(AramBuffId.MobileFortress, AramBuffTier.Gold, "Mobile Fortress", "Transport",
                "A Rook can carry one allied Pawn from its surrounding 3x3 area. Unload into an empty 3x3 square from your next turn. A destroyed carrier parachutes its Pawn nearby; landing on the far rank promotes it. These actions spend no move."),
            Entry(AramBuffId.HidingKing, AramBuffTier.Gold, "Hiding King", "King Swap",
                "At the start, swap your King unconditionally with an ally on your back rank. Afterward, recharge every 10 combined moves. Both pieces must then be on your back rank and your King must end safe."),
            Entry(AramBuffId.GachaBanner, AramBuffTier.Gold, "Gacha Banner", "Battle Gacha",
                "Capture an enemy non-Pawn for 1 ticket. Spend 1 ticket to drop a random ally at most once per turn: Pawn 70%, Knight 10%, Bishop 10%, Rook 9%, Queen 1%. The drop cannot check the opposing King."),
            Entry(AramBuffId.SubstituteNinjutsu, AramBuffTier.Diamond, "Substitute Ninjutsu", "Substitute",
                "On your first check, teleport your King to a safe empty home square and leave a decoy. The decoy moves like a King and transforms into the first enemy type it captures."),
            Entry(AramBuffId.HighTechEra, AramBuffTier.Diamond, "The High-tech Era", "Cannon Rooks",
                "Your two initial Rooks keep normal movement and can capture over exactly one piece. Each cannon expires after 10 combined moves without firing."),
            Entry(AramBuffId.QueensBetrayal, AramBuffTier.Diamond, "The Queen's Betrayal", "Betrayal",
                "Gain an extra Queen on your third rank. After each combined move, her betrayal chance starts at 10% and rises by 5 percentage points. On betrayal, the opponent places her in their first three ranks."),
            Entry(AramBuffId.DefinitionOfAram, AramBuffTier.Diamond, "The Definition of ARAM", "Collapse",
                "After your 10th turn, the outermost files collapse and destroy their pieces without capture points. Five combined moves later the next pair collapses, leaving a 4x8 board."),
            Entry(AramBuffId.RngFiesta, AramBuffTier.Diamond, "RNG Fiesta", "RNG Fiesta",
                "At the start, transform all your pieces except Kings and Queens randomly into Pawns, Knights, Bishops or Rooks."),
            Entry(AramBuffId.IFrameRoll, AramBuffTier.Diamond, "I-Frame Roll", "Last Escape",
                "When checkmated, choose a safe empty square within the 5x5 area centered on your King. Spend no move; maximum 3 escapes per match."),
            Entry(AramBuffId.GhostArmy, AramBuffTier.Legendary, "Ghost Army", "Ghost Army",
                "Your pieces can pass through allies but cannot share a destination. Enemies still block paths. Phasing cannot be used to check the opposing King."),
            Entry(AramBuffId.PlagueTown, AramBuffTier.Legendary, "Plague Town", "Plague",
                "An enemy capturing your piece becomes infected and dies after the next 4 combined moves. Kings are immune."),
            Entry(AramBuffId.CustomizeArmy, AramBuffTier.Legendary, "Customize Army", "Formation",
                "Rearrange your army within your half for up to 2 minutes, then confirm. An unchanged formation refunds this buff as a random Gold buff."),
            Entry(AramBuffId.PawnsRevolution, AramBuffTier.Legendary, "Pawn's Revolution", "Pawn Army",
                "Sacrifice every piece except your King, then fill every empty square in your half with Pawns."),
            Entry(AramBuffId.OneManArmy, AramBuffTier.Legendary, "One Man Army", "King's Rifle",
                "Keep only your King, who can also move two squares orthogonally. A rifle shot is available immediately and recharges every 3 of your turns, without stacking. Aim for 15 seconds; a miss or King/Queen hit ends the shot. You can still make your normal move."),
            Entry(AramBuffId.EveryManForHimself, AramBuffTier.Silver, "Every Man for Himself", "Army Quest",
                "Capture a matching enemy type with each of Pawn, Knight, Bishop, Rook and Queen, and promote a Pawn once. Then promote 2 surviving Pawns to Queens, or place 1 Queen anywhere if fewer than 2 remain. Any draw becomes a loss."),
            Entry(AramBuffId.SacUrQueen, AramBuffTier.Silver, "Sac ur Queen!!!", "Queen Refund",
                "Replace your initial Queen with your choice of Rook, Bishop or Knight. After 10 of your turns, transform one ally other than a King or promoted Queen into a Queen."),
            Entry(AramBuffId.AutoCastling, AramBuffTier.Silver, "Auto Castling", "Auto Castle",
                "Immediately castle kingside without conditions. Allies occupying the King and Rook destinations swap into the vacated squares, preserving your army."),
            Entry(AramBuffId.UltimateQuest, AramBuffTier.Gold, "Ultimate Quest", "Queen Mastery",
                "A non-Queen that captures any enemy Queen permanently gains Queen movement in addition to its own movement."),
            Entry(AramBuffId.MonsterTruck, AramBuffTier.Gold, "Monster Truck", "Monster Truck",
                "Your Rooks can move through allies into empty squares. This extension cannot capture or check through a blocker."),
            Entry(AramBuffId.CreditCard, AramBuffTier.Gold, "Credit Card", "Credit Card",
                "Earn normal material points from captures. Buy one piece per turn for your half without spending a move: Pawn 2, Knight/Bishop 4, Rook 5, Queen 10. You may buy on credit while solvent; no purchases while your balance is negative."),
            Entry(AramBuffId.KingIsPoorPiece, AramBuffTier.Diamond, "The King Is a Poor Piece", "Kingless",
                "Replace your King with a Queen. Lose if your whole army is destroyed, if you have not won by checkmate after your 25th turn, or if the game is drawn."),
            Entry(AramBuffId.DoubleEdgedTrap, AramBuffTier.Diamond, "Double-edged Trap", "Promotion Trap",
                "Enemy Pawns cannot promote. A Pawn reaching its far rank permanently changes to one-square orthogonal movement and captures, while remaining a Pawn."),
            Entry(AramBuffId.Paratrooper, AramBuffTier.Diamond, "Paratrooper", "Paratrooper",
                "After promoting, choose any empty active square to deploy that piece, if your King remains safe. Placement does not spend an extra move."),
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
