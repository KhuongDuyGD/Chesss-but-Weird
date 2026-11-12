using System.Text;
using System.Text.Json;

internal static class LocalizationAudit
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        var expected = new (string Key, string English, string Vietnamese)[]
        {
            ("AbsoluteSniper", "Absolute Sniper", "Absolute Sniper"),
            ("EveryManForHimself", "Every Man for Himself", "Người không vì mình, trời tru đất diệt"),
            ("BrokenLegKnight", "Knightmare on the Chessboard", "Ngựa gãy chân"),
            ("WeaponSupplyPackage", "Tactical Supply Drop", "Gói trang bị vũ khí"),
            ("NewYearFireworks", "New Year, New Boom!", "Pháo hoa chúc mừng năm mới"),
            ("AssassinPawns", "Pawn Identity: Assassin", "Tốt sát thủ"),
            ("MedicalStation", "Emergency Respawn Center", "Trạm y tế"),
            ("WeddingNight", "Checkmate & Consummate", "Động phòng đêm tân hôn"),
            ("ProtectedPawn", "You Wouldn't Hurt a Cute Girl, Would You?", "Bạn sẽ không dám đả thương em gái dễ thương này chứ?"),
            ("Lullaby", "Deadly Lullaby", "Khúc ca ru ngủ"),
            ("GoldenExperienceRequiem", "Golden Experience Requiem", "Golden Experience Requiem"),
            ("QueenCastling", "Royal Family Drama", "Oan gia ngõ hẹp"),
            ("QueenSubordinates", "Death by Queen", "Chết vì gái là một cái chết tê tái"),
            ("Snowman", "Do You Want to Build a Snowman?", "Do you want to build a snowman?"),
            ("DrunkDriving", "Don't Drink and Rook!", "Đã rượu bia thì đừng lái xe"),
            ("MagicCaster", "Magic Caster", "Magic Caster"),
            ("HighTechEra", "High-Tech Era Cannon", "High-tech Era Cannon"),
            ("DefinitionOfAram", "The Chessboard Has Fallen", "Sự sụp đổ của hệ thống bàn cờ"),
        };
        Dictionary<string, JsonElement> Table(string language)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", "Localization", "Settings_" + language + ".json");
            string json = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path));
            using var document = JsonDocument.Parse(json);
            var entries = document.RootElement.GetProperty("entries").EnumerateArray().Select(e => e.Clone()).ToArray();
            Check(entries.Select(e => e.GetProperty("key").GetString()).Distinct().Count() == entries.Length, "Duplicate localization key: " + language);
            foreach (var entry in entries)
            {
                string text = entry.GetProperty("text").GetString();
                Check(!string.IsNullOrWhiteSpace(text) && !text.Contains('\uFFFD') && !text.Contains((char)0) && text.IsNormalized(),
                    "Empty or damaged Unicode: " + entry.GetProperty("key"));
            }
            return entries.ToDictionary(e => e.GetProperty("key").GetString());
        }
        var english = Table("en");
        var vietnamese = Table("vi");
        Check(english.Count == 72 && vietnamese.Count == 36, "Expected 18 English name/short-name/description/summary entries and 18 Vietnamese name/short-name pairs");
        foreach (var buff in expected)
        {
            string prefix = "buff." + buff.Key + ".";
            Check(english[prefix + "name"].GetProperty("text").GetString() == buff.English, "Approved English spelling: " + buff.Key);
            Check(vietnamese[prefix + "name"].GetProperty("text").GetString() == buff.Vietnamese, "Vietnamese name changed: " + buff.Key);
            UserSettings.LanguageCode = "en";
            Check(SettingsLocalization.Text(prefix + "name", "old name") == buff.English, "English table was bypassed: " + buff.Key);
            foreach (string field in new[] { "shortName", "description", "summary" })
            {
                var entry = english[prefix + field];
                string fallback = entry.TryGetProperty("source", out var source) ? source.GetString() : "old text";
                Check(SettingsLocalization.Text(prefix + field, fallback) == entry.GetProperty("text").GetString(), "English lookup failed: " + prefix + field);
                if (entry.TryGetProperty("source", out _))
                    Check(SettingsLocalization.Text(prefix + field, "different server rules") == "different server rules", "Server rule text overwritten: " + prefix + field);
            }
            UserSettings.LanguageCode = "vi";
            Check(SettingsLocalization.Text(prefix + "name", buff.English) == buff.Vietnamese, "Vietnamese lookup failed: " + buff.Key);
            // Only names are translated into Vietnamese; existing English descriptions remain the fallback.
            Check(SettingsLocalization.Text(prefix + "description", "server description") == "server description", "Untranslated description lost fallback");
        }
        UserSettings.LanguageCode = "en";
        Check(SettingsLocalization.Text("missing.key", "Readable English") == "Readable English", "Missing key fallback");
        UserSettings.LanguageCode = "missing-language";
        Check(SettingsLocalization.Text("buff.AbsoluteSniper.name", "Readable English") == "Readable English", "Missing table fallback");
        UnityEngine.Resources.Overrides["Localization/Settings_invalid-json"] = "{broken";
        UserSettings.LanguageCode = "invalid-json";
        Check(SettingsLocalization.Text("key", "Readable English") == "Readable English" && UnityEngine.Debug.Warnings == 1, "Malformed table fallback");
        UnityEngine.Resources.Overrides["Localization/Settings_blank"] = """{"entries":[null,{"key":"empty","text":" "},{"key":"kept","text":"Valid"}]}""";
        UserSettings.LanguageCode = "blank";
        Check(SettingsLocalization.Text("empty", "fallback") == "fallback", "Blank translations should not erase text");
        Check(SettingsLocalization.Text("kept", "fallback") == "Valid", "Valid entry after null/blank entries lost");

        string[] stableIds = {
            "CommandantPawn", "StrongFortress", "FreestyleLeap", "Doppelganger", "SuicideBomber", "FlyingThunderGod",
            "NobleSacrifice", "AbsoluteSniper", "GamblingLeadsToMisery", "LootBox", "PeaceTShirt", "RiseOfPawn",
            "MobileFortress", "HidingKing", "GachaBanner", "SubstituteNinjutsu", "HighTechEra", "QueensBetrayal",
            "DefinitionOfAram", "RngFiesta", "IFrameRoll", "GhostArmy", "PlagueTown", "CustomizeArmy",
            "PawnsRevolution", "OneManArmy", "EveryManForHimself", "SacUrQueen", "AutoCastling", "UltimateQuest",
            "MonsterTruck", "CreditCard", "KingIsPoorPiece", "DoubleEdgedTrap", "Paratrooper"
        };
        Check(Enum.GetValues<AramBuffId>().Length == stableIds.Length, "Localization patch registered or removed a buff");
        for (int i = 0; i < stableIds.Length; i++)
            Check((int)Enum.Parse<AramBuffId>(stableIds[i]) == i, "Serialized buff ID changed: " + stableIds[i]);

        UserSettings.LanguageCode = "en";
        var definitions = AramBuffLibrary.CreateBuiltInDefinitions();
        Check(definitions.Count == 35 && definitions.Select(d => d.Id).Distinct().Count() == 35, "Draft pool changed");
        Check(definitions.Count(d => d.Tier == AramBuffTier.Silver) == 10 &&
              definitions.Count(d => d.Tier == AramBuffTier.Gold) == 10 &&
              definitions.Count(d => d.Tier == AramBuffTier.Diamond) == 10 &&
              definitions.Count(d => d.Tier == AramBuffTier.Legendary) == 5, "Rarity pools changed");
        int integrated = 0;
        foreach (var buff in expected)
        {
            if (!AramBuffLibrary.TryParseId(buff.Key, out var id)) continue;
            integrated++;
            var definition = AramBuffLibrary.FindById(definitions, id);
            string prefix = "buff." + buff.Key + ".";
            Check(definition.DisplayName == buff.English, "Definition/draft name mismatch: " + buff.Key);
            Check(definition.ShortName == english[prefix + "shortName"].GetProperty("text").GetString(), "HUD short-name mismatch: " + buff.Key);
            Check(definition.Description == english[prefix + "description"].GetProperty("text").GetString(), "Tooltip description mismatch: " + buff.Key);
            Check(definition.Summary == english[prefix + "summary"].GetProperty("text").GetString(), "Tooltip summary mismatch: " + buff.Key);
            UserSettings.LanguageCode = "vi";
            Check(definition.DisplayName == buff.Vietnamese && definition.ShortName == buff.Vietnamese, "Live Vietnamese switch: " + buff.Key);
            UserSettings.LanguageCode = "en";
            Check(definition.DisplayName == buff.English, "Live English switch: " + buff.Key);
            definition.Configure(id, definition.Tier, "older server name", "older server short name",
                "Server-specific rule. Do not replace.", definition.AccentColor, "Server-specific summary.");
            Check(definition.DisplayName == buff.English && definition.ShortName == english[prefix + "shortName"].GetProperty("text").GetString(), "Server name bypassed localization");
            Check(definition.Description == "Server-specific rule. Do not replace." && definition.Summary == "Server-specific summary.", "Server rules replaced by practice rules");
            definition.Configure(id, definition.Tier, "old name", "", "First sentence. Second sentence.", definition.AccentColor);
            Check(definition.Summary == "First sentence.", "First-sentence fallback changed");
        }
        Check(integrated == 4, "Review which expansion buffs actually exist before changing integration expectations");
        var untouched = AramBuffLibrary.FindById(definitions, AramBuffId.CommandantPawn);
        Check(untouched.DisplayName == "Commandant Pawn" && untouched.ShortName == "Commandant", "Unrelated buff name changed");
        Check(AramBuffLibrary.TryParseId("DefinitionOfAram", out var collapse) && (int)collapse == 18, "Rename broke saved ID");
        Console.WriteLine($"PASS {checks} localization assertions: 18 approved pairs; 4 integrated buffs; 14 catalog-only buffs; legacy/server fallback; 35 stable IDs.");
        return checks;
    }
}
