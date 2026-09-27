using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Offline rendering of the real controllers with fixture DTOs. No auth or roll calls.</summary>
public static class SketchbookLayoutAudit
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string Folder = Path.GetFullPath("Logs/SketchbookUiAudit");
    private static readonly List<string> Checks = new List<string>();

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this fixture in a separate Unity batch process.");
        Directory.CreateDirectory(Folder);
        GameObject root = null;
        try
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            root = new GameObject("Offline sketchbook fixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1672, 941);
            var profileRoot = Panel(root.transform, "Profile");
            var profile = profileRoot.gameObject.AddComponent<PlayerProfileMenuController>();
            profile.Initialize(profileRoot, () => { });
            Call(profile, "Refresh", new UserMeResponse {
                userId = "6aacc041e50e17c25a690158", username = "TaiChess", email = "tai.test@example.com", createdAt = "2026-09-01T12:00:00Z",
                profile = new UserProfileResponse { displayName = "TaiChess" },
                wallet = new UserWalletResponse { golds = 12500, diamonds = 950, tickets = 27 },
                stats = new UserStatsResponse { elo = 1240, wins = 24, losses = 9, draws = 3, gamesPlayed = 36 },
                equipped = new UserEquippedResponse { chessSkinId = "6aacc041e50e17c25a690158", boardSkinId = "6aacc04be50e17c25a69015a" }
            });
            Require(Field<TextMeshProUGUI>(profile, "nameLabel").text == "TaiChess", "Profile uses /me displayName");
            Capture(canvas, "profile");
            profileRoot.gameObject.SetActive(false);

            var gachaRoot = Panel(root.transform, "Gacha");
            var gacha = gachaRoot.gameObject.AddComponent<GachaMenuController>();
            gacha.Initialize(gachaRoot, () => { });
            var standard = Banner("STANDARD_TICKET_BANNER", "Standard Banner", "TICKETS", 1, 10, 80);
            var premium = Banner("PREMIUM_DIAMOND_BANNER", "Premium Banner", "DIAMONDS", 100, 900, 70);
            Set(gacha, "banners", new List<GachaBannerSummaryResponse> { standard, premium });
            Set(gacha, "activeBanner", standard);
            Set(gacha, "wallet", new GachaWalletResponse { golds = 12500, diamonds = 950, tickets = 27 });
            Set(gacha, "pity", new GachaPityResponse { legendary = new GachaPityTierResponse { current = 37, limit = 80 }, epic = new GachaPityTierResponse { current = 7, limit = 10 } });
            Call(gacha, "RefreshAll");
            Require(Field<TextMeshProUGUI>(gacha, "pityLabel").text == "Pity 37 / 80", "Pity uses API current and limit");
            Capture(canvas, "gacha-standard");
            Set(gacha, "activeBanner", premium);
            Set(gacha, "bannerIndex", 1);
            Set(gacha, "pity", new GachaPityResponse { legendary = new GachaPityTierResponse { current = 19, limit = 70 }, epic = new GachaPityTierResponse { current = 2, limit = 10 } });
            Call(gacha, "RefreshAll");
            Require(Field<List<TextMeshProUGUI>>(gacha, "summonCosts")[1].text == "900 diamonds", "Ten summon cost uses configured API total");
            Capture(canvas, "gacha-premium");
            Call(gacha, "ShowBannerDetails");
            Capture(canvas, "gacha-details");
            Call(gacha, "HideBannerDetails");
            Require(!Field<RectTransform>(gacha, "bannerDetailPanel").gameObject.activeSelf, "Details closes");

            var rewards = new List<GachaRewardResponse>();
            var visual = new List<GachaReward>();
            for (int i = 0; i < 10; i++)
            {
                string name = i % 2 == 0 ? "Mid-Autumn Lantern Chess Set" : "Tazji's Low Poly Arena";
                rewards.Add(new GachaRewardResponse { itemName = name, rarity = "EPIC", isDuplicate = i % 3 == 0,
                    duplicateReward = new GachaRewardCurrencyResponse { amount = 100, currency = "GOLDS" } });
                visual.Add(new GachaReward(GachaRewardType.Skin, 1, 2 + i % 4, i % 4 == 3, name, i % 3 == 0, "+100 GOLDS"));
            }
            Set(gacha, "history", new GachaHistoryResponse { page = 1, pageSize = 8, total = 20, items = new List<GachaRollResponse> {
                new GachaRollResponse { count = 10, currency = "DIAMONDS", totalCost = 900, createdAt = "2026-09-27T10:00:00Z", results = rewards },
                new GachaRollResponse { count = 1, currency = "TICKETS", totalCost = 1, createdAt = "2026-09-26T10:00:00Z", results = new List<GachaRewardResponse> { rewards[0] } }
            } });
            Field<RectTransform>(gacha, "historyPanel").gameObject.SetActive(true);
            Call(gacha, "RenderHistory");
            Capture(canvas, "gacha-history");
            Call(gacha, "HideHistory");
            Set(gacha, "pendingResults", visual);
            Call(gacha, "ShowPendingResults");
            Capture(canvas, "gacha-results-ten");
            Set(gacha, "pendingResults", new List<GachaReward> { visual[3] });
            Call(gacha, "ShowPendingResults");
            Capture(canvas, "gacha-results-one");
            Call(gacha, "ShowMainScreen");
            Set(gacha, "loading", true);
            Call(gacha, "RefreshButtons");
            Require(Field<List<Button>>(gacha, "summonButtons").TrueForAll(button => !button.interactable), "No summon during loading");
            Set(gacha, "loading", false);
            Set(gacha, "pendingRequestId", Guid.NewGuid());
            Set(gacha, "pendingCount", 10);
            Call(gacha, "RefreshButtons");
            Require(!Field<Button>(gacha, "nextBannerButton").interactable, "Pending request locks banner selection");
            var buttons = Field<List<Button>>(gacha, "summonButtons");
            Require(!buttons[0].interactable && buttons[1].interactable, "Pending request permits only original count retry");
            var layoutChecks = (List<string>)typeof(MenuLayoutAudit).GetField("checks", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Checks.AddRange(layoutChecks);
            Require(!layoutChecks.Exists(line => line.StartsWith("FAIL")), "All captured buttons stay within viewport");
            Debug.Log("[SketchbookLayoutAudit] PASS: " + Checks.Count + " checks. Fixture screenshots: " + Folder);
        }
        finally
        {
            File.WriteAllLines(Path.Combine(Folder, "checks.txt"), Checks);
            if (root) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GachaBannerDetailResponse Banner(string code, string name, string currency, long one, long ten, int pity) => new GachaBannerDetailResponse {
        code = code, name = name, description = "Collect curious chess sets and arenas. A little luck can change your next game.",
        costs = new List<GachaCostResponse> { new GachaCostResponse { rollCount = 1, currency = currency, amount = one }, new GachaCostResponse { rollCount = 10, currency = currency, amount = ten } },
        rarityRates = new GachaRatesResponse { common = 60, rare = 30, epic = 8, legendary = 2 }, pityLimit = new GachaPityLimitResponse { epic = 10, legendary = pity },
        poolItems = new List<GachaPoolItemResponse> { new GachaPoolItemResponse { name = "Tazji's Low Poly Chess Set", rarity = "COMMON" }, new GachaPoolItemResponse { name = "Mid-Autumn Lantern Arena", rarity = "LEGENDARY" } },
        duplicateRewards = new Dictionary<string, GachaRewardCurrencyResponse> { { "COMMON", new GachaRewardCurrencyResponse { amount = 100, currency = "GOLDS" } }, { "LEGENDARY", new GachaRewardCurrencyResponse { amount = 200, currency = "DIAMONDS" } } }
    };
    private static RectTransform Panel(Transform parent, string name)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static void Capture(Canvas canvas, string name)
    {
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
            typeof(MenuLayoutAudit).GetMethod("Render", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { canvas, size, Path.Combine(Folder, name + "-" + size.x + "x" + size.y + ".png") });
    }
    private static void Require(bool condition, string label) { Checks.Add((condition ? "PASS " : "FAIL ") + label); if (!condition) throw new Exception(label); }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
}
