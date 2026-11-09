using System;
using System.Threading.Tasks;
using UnityEngine;

public interface ICommunityMailboxGateway
{
    Task<CommunityMailEnrollment> Enroll(long epoch);
    Task<CommunityMailPage> List(int page, long epoch);
    Task<CommunityMail> Get(string mailId, long epoch);
    Task<CommunityMail> Read(string mailId, long epoch);
    Task<CommunityMailReceipt> Claim(string mailId, long epoch);
    Task<UserMeResponse> Me(long epoch);
}
public sealed class CommunityMailboxGateway : ICommunityMailboxGateway
{
    public Task<CommunityMailEnrollment> Enroll(long epoch) => ApiClient.Shared.PostForSessionAsync<CommunityMailEnrollment>("/api/mailbox/enroll", new { }, epoch);
    public Task<CommunityMailPage> List(int page, long epoch) => ApiClient.Shared.SendAsync<CommunityMailPage>("GET", "/api/mailbox?page=" + page + "&pageSize=20", null, true, epoch);
    public Task<CommunityMail> Get(string id, long epoch) => ApiClient.Shared.SendAsync<CommunityMail>("GET", Path(id), null, true, epoch);
    public Task<CommunityMail> Read(string id, long epoch) => ApiClient.Shared.PostForSessionAsync<CommunityMail>(Path(id) + "/read", new { }, epoch);
    public Task<CommunityMailReceipt> Claim(string id, long epoch) => ApiClient.Shared.PostForSessionAsync<CommunityMailReceipt>(Path(id) + "/claim", new { }, epoch);
    public Task<UserMeResponse> Me(long epoch) => ApiClient.Shared.SendAsync<UserMeResponse>("GET", "/api/users/me", null, true, epoch);
    private static string Path(string id) => "/api/mailbox/" + Uri.EscapeDataString(id);
}

public static class CommunityContent
{
    private static CommunityNewsCatalog catalog;
    public static string Error { get; private set; }
    public static CommunityNewsCatalog Catalog
    {
        get
        {
            if (catalog != null) return catalog;
            try
            {
                var source = Resources.Load<TextAsset>("Community/news.en-US");
                if (!source) throw new FormatException("News catalog not included in build.");
                catalog = CommunityNewsCatalog.Parse(source.text);
            }
            catch (Exception ex) { Error = "Could not read the news catalog. Please update the game."; Debug.LogWarning("[Community] " + ex.Message); catalog = new CommunityNewsCatalog { currentVersion = "0.7.0", schemaVersion = 1 }; }
            return catalog;
        }
    }
    public static string VersionLabel => "v." + Catalog.currentVersion;
}
