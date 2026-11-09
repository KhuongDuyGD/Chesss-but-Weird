using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

[Serializable] public sealed class CommunityNewsCatalog
{
    public int schemaVersion;
    public string currentVersion;
    public List<CommunityNewsArticle> articles = new List<CommunityNewsArticle>();
    public static readonly string[] Categories = { "Updates", "Modes", "Cosmetics", "Events", "Maintenance", "Banners" };
    public static readonly string[] ArtKeys = { "kingdoms", "chaos", "shop", "loadout", "ranked", "play", "settings" };
    public static CommunityNewsCatalog Parse(string json)
    {
        var catalog = JsonConvert.DeserializeObject<CommunityNewsCatalog>(json) ?? throw new FormatException("News catalog missing.");
        if (catalog.schemaVersion != 1 || !ValidVersion(catalog.currentVersion) || catalog.articles == null)
            throw new FormatException("News schema/version invalid.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in catalog.articles)
        {
            if (a == null || string.IsNullOrWhiteSpace(a.id) || !ids.Add(a.id) || a.schemaVersion != 1 || a.locale != "en-US" ||
                !Categories.Contains(a.category) || !ArtKeys.Contains(a.artKey) || !ValidVersion(a.version) ||
                string.IsNullOrWhiteSpace(a.title) || string.IsNullOrWhiteSpace(a.summary) || string.IsNullOrWhiteSpace(a.body) ||
                !DateTimeOffset.TryParse(a.publishAtUtc, out _) || !DateTimeOffset.TryParse(a.sourceCommitDateUtc, out _) ||
                !string.IsNullOrEmpty(a.expiresAtUtc) && !DateTimeOffset.TryParse(a.expiresAtUtc, out _) ||
                !TrustedSource(a.sourceUrl) || (a.editorialStatus != "Published" && a.editorialStatus != "PendingRights") ||
                a.rightsClearanceRequired && !a.rightsCleared && a.editorialStatus == "Published")
                throw new FormatException("News article invalid: " + a?.id);
        }
        return catalog;
    }
    private static bool ValidVersion(string value) => value != null && System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d+\.\d+\.\d+$") && Version.TryParse(value, out _);
    public IEnumerable<CommunityNewsArticle> PublicArticles(DateTimeOffset now) => articles
        .Where(a => a.editorialStatus == "Published" && (!a.rightsClearanceRequired || a.rightsCleared) &&
            DateTimeOffset.Parse(a.publishAtUtc) <= now && (string.IsNullOrEmpty(a.expiresAtUtc) || DateTimeOffset.TryParse(a.expiresAtUtc, out var expiry) && expiry > now))
        .OrderByDescending(a => a.pinned).ThenByDescending(a => a.priority).ThenByDescending(a => DateTimeOffset.Parse(a.publishAtUtc)).ThenBy(a => a.id, StringComparer.Ordinal);
    public static bool TrustedSource(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
        uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(uri.UserInfo) && uri.IsDefaultPort &&
        System.Text.RegularExpressions.Regex.IsMatch(uri.AbsolutePath, @"^/KhuongDuyGD/Chesss-but-Weird/commit/[a-f0-9]{40}$") &&
        string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
}
[Serializable] public sealed class CommunityNewsArticle
{
    public int schemaVersion = 1, priority;
    public string id, locale, category, version, title, summary, body, artKey, publishAtUtc, sourceCommitDateUtc, sourceUrl, expiresAtUtc, targetBuild, ctaTarget, editorialStatus;
    public bool featured, pinned, rightsClearanceRequired, rightsCleared;
}
[Serializable] public sealed class CommunityMailAttachment { public string currency; public long amount; }
[Serializable] public sealed class CommunityMail
{
    public string mailId, userId, campaignId, category, sender, title, preview, body, sentAtUtc, expiresAtUtc, readAtUtc, claimedAtUtc, status;
    public int schemaVersion;
    public List<CommunityMailAttachment> attachments = new List<CommunityMailAttachment>();
    public bool IsUnread => string.IsNullOrEmpty(readAtUtc);
    public bool IsClaimed => !string.IsNullOrEmpty(claimedAtUtc);
    public bool IsClaimable => status == "Claimable" && !IsClaimed;
}
[Serializable] public sealed class CommunityMailPage
{
    public int page, pageSize;
    public long total, unreadCount, unclaimedCount;
    public List<CommunityMail> items = new List<CommunityMail>();
}
[Serializable] public sealed class CommunityMailEnrollment { public bool created; public CommunityMail mail; }
[Serializable] public sealed class CommunityMailReceipt { public string receiptId, mailId, claimedAtUtc; }

// Each operation also checks ApiClient's auth epoch. This gate rejects out-of-order responses within the same account.
public sealed class CommunityRequestGate
{
    private long generation;
    public long Advance() => ++generation;
    public long Current => generation;
    public bool Accept(long value) => generation == value;
}

public sealed class CommunityNewsReadSet
{
    private readonly HashSet<string> read;
    public CommunityNewsReadSet(string json)
    {
        try { read = new HashSet<string>(JsonConvert.DeserializeObject<string[]>(json ?? "[]") ?? Array.Empty<string>(), StringComparer.Ordinal); }
        catch (JsonException) { read = new HashSet<string>(StringComparer.Ordinal); }
    }
    public bool IsRead(string id) => read.Contains(id);
    public void Mark(string id) { if (!string.IsNullOrEmpty(id)) read.Add(id); }
    public string Serialize() => JsonConvert.SerializeObject(read.OrderBy(id => id, StringComparer.Ordinal));
    public static string Scope(string userId, bool guest)
    {
        if (guest || string.IsNullOrEmpty(userId)) return "community.news.read.guest.v1";
        using (var hash = System.Security.Cryptography.SHA256.Create())
            return "community.news.read.account." + BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(userId))).Replace("-", "");
    }
}
