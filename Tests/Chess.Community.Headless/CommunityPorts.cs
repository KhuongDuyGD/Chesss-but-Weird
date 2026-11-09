// Test-only transport/auth/audio ports in an isolated Unity project. No real account, save or backend configuration.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public static class AuthStorage { public static long SessionVersion; }
public sealed class UserMeResponse { public string userId; public UserWalletResponse wallet; }
public sealed class UserWalletResponse { public int golds, diamonds, tickets; }
public static class PlayerAuthService
{
    public static event Action StateChanged;
    public static bool IsAuthenticated { get; private set; }
    public static string UserId { get; private set; }
    public static UserMeResponse CurrentApiUser;
    public static void Switch(string id) { AuthStorage.SessionVersion++; UserId = id; IsAuthenticated = id != null; CurrentApiUser = null; StateChanged?.Invoke(); }
    public static void ApplyApiUser(UserMeResponse user) { CurrentApiUser = user; }
}
public static class AuthNotificationView
{
    public enum ResultKind { Info, Success, Error }
    public static int Successes, Infos;
    public static void Show(string title, string text, ResultKind kind) { if (kind == ResultKind.Success) Successes++; else Infos++; }
}
public sealed class ApiClient
{
    public static ApiClient Shared = new ApiClient();
    public Task<T> SendAsync<T>(string method, string path, object body, bool authorized, long? epoch) => throw new InvalidOperationException("Fixture forbids real HTTP.");
    public Task<T> PostForSessionAsync<T>(string path, object body, long epoch) => throw new InvalidOperationException("Fixture forbids real HTTP.");
}
public static class UserSettings
{
    public static class Presentation { public static bool ReducedMotion = true; }
}
public enum GameMusicPack { Official, Fantasy }
public static class GameMusicManager
{
    public static GameMusicPack ActivePack;
    public static void SetActivePack(GameMusicPack pack) { ActivePack = pack; }
    public static string GetPackDisplayName(GameMusicPack pack) => pack.ToString();
}
public static class GameRuntimeSettings { public static float MusicVolume01 => 0; }

public sealed class FixtureMailboxGateway : ICommunityMailboxGateway
{
    public readonly Dictionary<string, CommunityMail> Letters = new Dictionary<string, CommunityMail>();
    public int Claims, Enrolls;
    public bool Offline, LoseClaimResponse, FailMe, PauseList;
    public TaskCompletionSource<CommunityMailPage> PendingList;
    private string Id => PlayerAuthService.UserId;
    public Task<UserMeResponse> Me(long epoch)
    {
        if (Offline || FailMe) throw new Exception("offline");
        bool claimed = Letters.ContainsKey(Id) && Letters[Id].IsClaimed;
        return Task.FromResult(new UserMeResponse { userId = Id, wallet = new UserWalletResponse { golds = claimed ? 3000 : 1000, diamonds = claimed ? 100 : 0, tickets = claimed ? 13 : 10 } });
    }
    public Task<CommunityMailEnrollment> Enroll(long epoch)
    {
        if (Offline) throw new Exception("offline"); Enrolls++;
        bool created = !Letters.ContainsKey(Id);
        if (created) Letters[Id] = new CommunityMail { mailId = "welcome-" + Id, userId = Id, campaignId = "WELCOME_2026_V1", schemaVersion = 1,
            sender = "Chess But Weird Team", title = "Welcome to Chess But Weird!", preview = "A small gift from the team to help you begin your journey.",
            body = "Dear Tactician,\n\nThank you for joining Chess But Weird! We are excited to share matches full of strategy and surprises with you.\n\nTo help you get started, the team has sent you 2,000 Gold, 100 Diamonds and 3 Summon Tickets. Claim your gift right here in this letter!\n\nMay your moves be creative, your matches memorable and your adventures full of fun.\n\nWith thanks,\nThe Chess But Weird Team",
            sentAtUtc = "2026-11-09T00:00:00Z", status = "Claimable", category = "Reward",
            attachments = new List<CommunityMailAttachment> { new CommunityMailAttachment { currency = "GOLDS", amount = 2000 }, new CommunityMailAttachment { currency = "DIAMONDS", amount = 100 }, new CommunityMailAttachment { currency = "TICKETS", amount = 3 } } };
        return Task.FromResult(new CommunityMailEnrollment { created = created, mail = Copy(Letters[Id]) });
    }
    public CommunityMailPage Snapshot() => new CommunityMailPage { page = 1, pageSize = 20, total = 1, unreadCount = Letters[Id].IsUnread ? 1 : 0,
        unclaimedCount = Letters[Id].IsClaimable ? 1 : 0, items = new List<CommunityMail> { Copy(Letters[Id]) } };
    public Task<CommunityMailPage> List(int page, long epoch)
    {
        if (Offline) throw new Exception("offline");
        if (PauseList) { PendingList = new TaskCompletionSource<CommunityMailPage>(); return PendingList.Task; }
        return Task.FromResult(Snapshot());
    }
    public Task<CommunityMail> Get(string id, long epoch) { if (Offline) throw new Exception("offline"); return Task.FromResult(Copy(Letters[Id])); }
    public Task<CommunityMail> Read(string id, long epoch) { Letters[Id].readAtUtc = "2026-11-09T00:01:00Z"; return Get(id, epoch); }
    public Task<CommunityMailReceipt> Claim(string id, long epoch)
    {
        Claims++; Letters[Id].claimedAtUtc = "2026-11-09T00:02:00Z"; Letters[Id].status = "Claimed";
        if (LoseClaimResponse) throw new Exception("response lost after commit");
        return Task.FromResult(new CommunityMailReceipt { mailId = id, receiptId = "mail:" + id, claimedAtUtc = Letters[Id].claimedAtUtc });
    }
    private static CommunityMail Copy(CommunityMail mail) => Newtonsoft.Json.JsonConvert.DeserializeObject<CommunityMail>(Newtonsoft.Json.JsonConvert.SerializeObject(mail));
}
