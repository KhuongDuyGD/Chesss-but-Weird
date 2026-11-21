using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI = SketchbookUI;

/// <summary>One paper navigation shell; public editorial content and private server mail have separate state.</summary>
public sealed class CommunityMenuController : MonoBehaviour
{
    private RectTransform root, list, detail, filterBar;
    private TextMeshProUGUI heading, counters, status;
    private Button backButton, switchButton, retryButton, previousButton, nextButton, allButton;
    private ScrollRect listScroll, detailScroll;
    private UnityAction back, login;
    private bool isMail, refreshing, claimBusy, disposed, built;
    private int page = 1;
    private string filter = "All", selectedMailId, selectedNewsId, mailError;
    private CommunityMailPage mailbox;
    private CommunityMail selectedMail;
    private CommunityNewsReadSet newsRead;
    private string readScope;
    private GameObject lastSelection;
    private readonly CommunityRequestGate session = new CommunityRequestGate();
    private readonly HashSet<string> unresolvedClaims = new HashSet<string>(StringComparer.Ordinal);
    private long detailRequest;
    private sealed class Entry
    {
        public TextMeshProUGUI Label, Count;
        public GameObject UnreadHint, GiftHint;
        public bool Mailbox;
    }
    private readonly List<Entry> entries = new List<Entry>();
    public ICommunityMailboxGateway Gateway { get; set; } = new CommunityMailboxGateway();

    public void Build(RectTransform pageRoot, UnityAction onBack, UnityAction onLogin)
    {
        root = pageRoot; back = onBack; login = onLogin;
        UI.Card(root, "Community Paper", new Rect(62, 142, 1796, 844), UI.Paper, 0, true, 20);
        backButton = UI.Button(root, "Back", "BACK", new Rect(80, 56, 238, 64), UI.White, () => back?.Invoke(), 25);
        heading = Label(root, "Community Heading", "NEWS", new Rect(355, 47, 870, 78), 50);
        switchButton = UI.Button(root, "Switch Destination", "MAILBOX >", new Rect(1490, 56, 338, 64), UI.Blue, () => Open(!isMail), 26);
        counters = Label(root, "Independent Counters", "", new Rect(104, 163, 1600, 44), 25, UI.Muted);
        filterBar = UI.Node(root, "Filters", new Rect(104, 222, 1300, 56));
        retryButton = UI.Button(root, "Refresh", "REFRESH", new Rect(1590, 221, 228, 54), UI.White, RefreshMailbox, 24);
        list = UI.ScrollArea(root, "Articles and Mail List", new Rect(104, 304, 594, 567), out listScroll);
        UI.Card(root, "Letter Sheet", new Rect(732, 300, 1080, 620), UI.White, -.25f, false, 12);
        UI.Tape(root, 1180, 287, 160, -3);
        detail = UI.ScrollArea(root, "Readable Detail", new Rect(766, 329, 1012, 555), out detailScroll);
        AddScrollbar(root, detailScroll, new Rect(1788, 337, 9, 538));
        AddScrollbar(root, listScroll, new Rect(703, 310, 9, 555));
        status = Label(root, "Community Status", "", new Rect(104, 939, 1690, 43), 24, UI.Muted);
        previousButton = UI.Button(root, "Previous Page", "<", new Rect(104, 889, 112, 40), UI.White, () => { if (page > 1) { page--; RefreshMailbox(); } }, 26);
        nextButton = UI.Button(root, "Next Page", ">", new Rect(586, 889, 112, 40), UI.White, () => { page++; RefreshMailbox(); }, 26);
        Label(root, "News Local Read Policy", "Development notes / Read history stays on this device / " + CommunityContent.VersionLabel,
            new Rect(104, 1009, 1630, 36), 22, UI.Muted);
        allButton = UI.Button(root, "All Action", "READ ALL", new Rect(1410, 158, 408, 54), UI.Yellow, AllAction, 23);
        built = true;
        PlayerAuthService.StateChanged += OnSessionChanged;
        ResetReadScope();
        UpdateCounters();
    }
    public void BindEntry(Button button, bool mail)
    {
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        CommunityTypography.Apply(label);
        // Separate vector icon from text so unavailable Unicode emoji cannot become empty squares.
        var icon = UI.Node(button.transform, "Destination Icon", new Rect(15, 17, 32, 32)).gameObject.AddComponent<CommunityDestinationIcon>();
        icon.Configure(mail);
        label.rectTransform.offsetMin += new Vector2(25, 0);
        var unread = UI.Card(button.transform, "Unread Hint", new Rect(mail ? 256 : 308, -17, 36, 36), UI.Pink, 0, false, 17).rectTransform;
        var count = Label(unread, "Unread Count", "", new Rect(0, 1, 36, 34), 20); count.alignment = TextAlignmentOptions.Center;
        var gift = UI.Card(button.transform, "Gift Hint", new Rect(308, -19, 40, 40), UI.Yellow, 4, false, 12).rectTransform;
        UI.Node(gift, "Wrapped Present", new Rect(6, 4, 28, 31)).gameObject.AddComponent<CommunityDestinationIcon>().ConfigureGift();
        entries.Add(new Entry { Label = label, Mailbox = mail, UnreadHint = unread.gameObject, GiftHint = gift.gameObject, Count = count }); UpdateCounters();
    }
    public void Open(bool mailboxPage)
    {
        isMail = mailboxPage; filter = "All";
        ResetReadScope(); Render(resetList: true, resetDetail: true);
        if (isMail) RefreshMailbox();
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(backButton.gameObject);
    }
    private void OnSessionChanged()
    {
        session.Advance(); detailRequest++; mailbox = null; selectedMail = null; selectedMailId = null;
        unresolvedClaims.Clear();
        refreshing = claimBusy = false; page = 1; mailError = null;
        ResetReadScope(); UpdateCounters();
        if (built && root.gameObject.activeInHierarchy) Render();
        if (PlayerAuthService.IsAuthenticated) RefreshMailbox();
    }
    private bool Accept(long request, long epoch, string userId) => !disposed && this && session.Accept(request) &&
        AuthStorage.SessionVersion == epoch && PlayerAuthService.IsAuthenticated && PlayerAuthService.UserId == userId;
    public async void RefreshMailbox()
    {
        if (!built || disposed || refreshing || claimBusy) return;
        if (!PlayerAuthService.IsAuthenticated) { mailbox = null; UpdateCounters(); if (root.gameObject.activeInHierarchy && isMail) Render(); return; }
        long request = session.Current, epoch = AuthStorage.SessionVersion;
        string userId = PlayerAuthService.UserId;
        refreshing = true; mailError = null;
        if (root.gameObject.activeInHierarchy && isMail) Render();
        try
        {
            // This validates the account with /me before lazy welcome enrollment, for old and new accounts alike.
            var me = await Gateway.Me(epoch);
            if (!Accept(request, epoch, userId) || me == null || me.userId != userId) return;
            PlayerAuthService.ApplyApiUser(me);
            var enrolled = await Gateway.Enroll(epoch);
            if (!Accept(request, epoch, userId)) return;
            var result = await Gateway.List(page, epoch);
            if (!Accept(request, epoch, userId)) return;
            var currentWallet = await Gateway.Me(epoch);
            if (!Accept(request, epoch, userId) || currentWallet == null || currentWallet.userId != userId) return;
            PlayerAuthService.ApplyApiUser(currentWallet);
            mailbox = result;
            foreach (var item in result.items) unresolvedClaims.Remove(item.mailId);
            if (!string.IsNullOrEmpty(selectedMailId)) selectedMail = result.items.Find(m => m.mailId == selectedMailId);
            var welcome = enrolled?.mail;
            string toastKey = "community.welcome.toast." + CommunityNewsReadSet.Scope(userId, false);
            if (welcome != null && welcome.IsClaimable && !PlayerPrefs.HasKey(toastKey))
            {
                PlayerPrefs.SetInt(toastKey, 1); PlayerPrefs.Save();
                AuthNotificationView.Show("You have new mail", "A welcome gift is waiting in your Mailbox. Open the letter whenever you are ready.", AuthNotificationView.ResultKind.Info);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (Accept(request, epoch, userId)) mailError = "Could not load mail. Check your connection and select REFRESH."; }
        finally
        {
            if (Accept(request, epoch, userId)) { refreshing = false; UpdateCounters(); if (root.gameObject.activeInHierarchy && isMail) Render(); }
        }
    }
    private void ResetReadScope()
    {
        string scope = CommunityNewsReadSet.Scope(PlayerAuthService.UserId, !PlayerAuthService.IsAuthenticated);
        if (scope == readScope) return;
        readScope = scope; newsRead = new CommunityNewsReadSet(PlayerPrefs.GetString(scope, "[]")); selectedNewsId = null;
    }
    private List<CommunityNewsArticle> News() => CommunityContent.Catalog.PublicArticles(DateTimeOffset.UtcNow).ToList();
    private void UpdateCounters()
    {
        int unreadNews = News().Count(a => !newsRead.IsRead(a.id));
        foreach (var entry in entries)
        {
            if (!entry.Label) continue;
            entry.Label.text = entry.Mailbox ? "MAILBOX" : "NEWS";
            bool gift = PlayerAuthService.IsAuthenticated && (mailbox?.unclaimedCount ?? 0) > 0;
            bool unreadMail = PlayerAuthService.IsAuthenticated && (mailbox?.unreadCount ?? 0) > 0;
            entry.UnreadHint.SetActive(entry.Mailbox ? unreadMail : unreadNews > 0);
            entry.GiftHint.SetActive(entry.Mailbox && gift);
            long count = entry.Mailbox ? mailbox?.unreadCount ?? 0 : unreadNews;
            entry.Count.text = count > 99 ? "99+" : count.ToString();
        }
        if (counters) counters.text = isMail ? $"UNREAD MAIL  {mailbox?.unreadCount ?? 0}     /     UNCLAIMED GIFTS  {mailbox?.unclaimedCount ?? 0}" : $"UNREAD NEWS  {unreadNews}     /     DEVELOPMENT NOTES";
    }
    private void Render(bool resetList = false, bool resetDetail = false)
    {
        if (!built || disposed) return;
        float listOffset = list.anchoredPosition.y, detailOffset = detail.anchoredPosition.y;
        Clear(list); Clear(detail); Clear(filterBar);
        heading.text = isMail ? "MAILBOX" : "NEWS";
        SetButtonText(switchButton, isMail ? "NEWS >" : "MAILBOX >");
        ShowButton(retryButton, isMail && PlayerAuthService.IsAuthenticated);
        ShowButton(previousButton, isMail && PlayerAuthService.IsAuthenticated);
        ShowButton(nextButton, isMail && PlayerAuthService.IsAuthenticated);
        previousButton.interactable = page > 1 && !refreshing;
        nextButton.interactable = mailbox != null && page * 20L < mailbox.total && !refreshing;
        retryButton.interactable = !refreshing && !claimBusy;
        ShowButton(allButton, !isMail || PlayerAuthService.IsAuthenticated);
        SetButtonText(allButton, isMail ? claimBusy ? "CONFIRMING..." : "CLAIM ALL" : "READ ALL");
        allButton.interactable = isMail ? !refreshing && !claimBusy && (mailbox?.unclaimedCount ?? 0) > 0 : News().Any(a => !newsRead.IsRead(a.id));
        status.text = isMail ? mailError ?? (refreshing ? "Loading your mailbox..." : "Reading does not claim rewards. Your welcome gift never expires.") : CommunityContent.Error ?? "News and updates from the Chess But Weird team.";
        BuildFilters(); UpdateCounters();
        if (isMail) RenderMailbox(); else RenderNews();
        RestoreScroll(listScroll, listOffset, resetList);
        RestoreScroll(detailScroll, detailOffset, resetDetail);
        foreach (var scroll in new[] { listScroll, detailScroll })
            if (scroll.verticalScrollbar) scroll.verticalScrollbar.gameObject.SetActive(scroll.content.rect.height > scroll.viewport.rect.height + 1);
    }
    private void BuildFilters()
    {
        var filters = isMail ? new[] { "All", "Unread", "Rewards" } : new[] { "All", "Updates", "Modes", "Cosmetics", "Events", "Maintenance", "Banners" };
        for (int i = 0; i < filters.Length; i++)
        {
            string value = filters[i];
            var button = UI.Button(filterBar, value, value, new Rect(i * 179, 0, 163, 52), filter == value ? UI.Yellow : UI.White,
                () => { filter = value; Render(resetList: true, resetDetail: true); }, 23);
            CommunityTypography.Apply(button.GetComponentInChildren<TextMeshProUGUI>());
        }
    }
    private void RenderMailbox()
    {
        if (!PlayerAuthService.IsAuthenticated)
        {
            IllustratedHeader(detail, "mail", "A LETTER JUST FOR YOU", UI.Blue);
            Body(detail, "Sign in to read your mail and claim gifts. News is available to everyone.", 175, 930, 30);
            ActionButton(detail, "SIGN IN", 330, login, UI.Yellow);
            detail.sizeDelta = new Vector2(0, 500); return;
        }
        var items = (mailbox?.items ?? new List<CommunityMail>()).Where(m => filter != "Unread" || m.IsUnread).Where(m => filter != "Rewards" || m.attachments.Count > 0).ToList();
        float y = 4;
        foreach (var m in items)
        {
            var captured = m;
            var button = UI.Button(list, "Mail " + m.mailId, "", new Rect(5, y, 573, 229), selectedMailId == m.mailId ? UI.Yellow : UI.White,
                () => SelectMail(captured), 25);
            Label(button.transform, "Mail Title", (m.IsUnread ? "NEW  " : "") + m.title, new Rect(19, 13, 532, 65), 27, UI.Ink, true);
            Label(button.transform, "Mail Sender", m.sender + "  /  " + Date(m.sentAtUtc), new Rect(19, 82, 535, 33), 22, UI.Muted);
            Label(button.transform, "Mail Preview", m.preview, new Rect(19, 122, 532, 51), 22, UI.Muted, true);
            Label(button.transform, "Mail Status", State(m), new Rect(19, 181, 532, 36), 24, UI.Ink);
            y += 245;
        }
        list.sizeDelta = new Vector2(0, Mathf.Max(y, 567));
        if (items.Count == 0) Body(list, refreshing ? "Loading..." : mailError ?? "No letters in this section yet.", 30, 540, 28);
        if (selectedMail == null) { IllustratedHeader(detail, "mail", "A LITTLE LETTER", UI.Blue); Body(detail, "Choose a letter to read its full message and see any attached rewards.", 180, 930, 30); detail.sizeDelta = new Vector2(0, 520); return; }
        Label(detail, "Letter Title", selectedMail.title, new Rect(8, 0, 970, 108), 39, UI.Ink, true);
        Label(detail, "Letter Sender", selectedMail.sender + "  /  " + Date(selectedMail.sentAtUtc), new Rect(8, 118, 970, 40), 24, UI.Muted);
        float bottom = Body(detail, selectedMail.body, 174, 965, 28);
        foreach (var reward in selectedMail.attachments)
        {
            var row = UI.Card(detail, "Reward " + reward.currency, new Rect(12, bottom, 942, 67), RewardColor(reward.currency), 0, false, 10).transform;
            UI.Doodle(row, "Currency Icon", new Rect(15, 12, 42, 42), reward.currency == "GOLDS" ? SketchbookDoodle.Shape.Coin : reward.currency == "DIAMONDS" ? SketchbookDoodle.Shape.Diamond : SketchbookDoodle.Shape.Ticket, UI.Ink);
            Label(row, "Reward Amount", RewardName(reward.currency) + " x " + reward.amount.ToString("N0"), new Rect(77, 11, 835, 45), 29);
            bottom += 82;
        }
        Label(detail, "Expiry", selectedMail.expiresAtUtc == null ? "NEVER EXPIRES" : "Expires: " + Date(selectedMail.expiresAtUtc), new Rect(12, bottom, 900, 38), 23, UI.Muted); bottom += 52;
        if (selectedMail.attachments.Count > 0)
        {
            var claim = ActionButton(detail, selectedMail.IsClaimed ? "CLAIMED" : claimBusy ? "CONFIRMING..." : selectedMail.IsClaimable ? "CLAIM GIFT" : "EXPIRED", bottom, ClaimSelected, UI.Yellow);
            claim.interactable = selectedMail.IsClaimable && !claimBusy && !unresolvedClaims.Contains(selectedMail.mailId);
            bottom += 90;
        }
        detail.sizeDelta = new Vector2(0, Mathf.Max(bottom + 30, 555));
    }
    private async void SelectMail(CommunityMail item)
    {
        long request = session.Current, epoch = AuthStorage.SessionVersion, selection = ++detailRequest;
        string userId = PlayerAuthService.UserId;
        selectedMailId = item.mailId; selectedMail = item; mailError = null; Render(); detailScroll.verticalNormalizedPosition = 1;
        try
        {
            var result = await Gateway.Read(item.mailId, epoch);
            if (!Accept(request, epoch, userId) || selection != detailRequest) return;
            if (item.IsUnread && mailbox != null) mailbox.unreadCount = Math.Max(0, mailbox.unreadCount - 1);
            if (mailbox != null) { int index = mailbox.items.FindIndex(m => m.mailId == item.mailId); if (index >= 0) mailbox.items[index] = result; }
            selectedMail = result; UpdateCounters(); if (isMail && root.gameObject.activeInHierarchy) Render();
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (Accept(request, epoch, userId) && selection == detailRequest) status.text = "Read status could not be saved. Select the letter again after reconnecting."; }
    }
    private async void ClaimSelected()
    {
        if (claimBusy || selectedMail == null || !selectedMail.IsClaimable || unresolvedClaims.Contains(selectedMail.mailId)) return;
        long request = session.Current, epoch = AuthStorage.SessionVersion;
        string userId = PlayerAuthService.UserId, mailId = selectedMail.mailId;
        unresolvedClaims.Add(mailId);
        claimBusy = true; Render();
        bool confirmed = false;
        try
        {
            try { await Gateway.Claim(mailId, epoch); }
            catch (OperationCanceledException) { return; }
            catch (Exception) { /* Both outcomes require authoritative recovery; never locally add currency. */ }
            if (!Accept(request, epoch, userId)) return;
            var recoveredMail = await Gateway.Get(mailId, epoch);
            if (!Accept(request, epoch, userId)) return;
            var me = await Gateway.Me(epoch);
            if (!Accept(request, epoch, userId) || me == null || me.userId != userId) return;
            PlayerAuthService.ApplyApiUser(me);
            unresolvedClaims.Remove(mailId);
            confirmed = recoveredMail.IsClaimed;
            if (selectedMailId == mailId) selectedMail = recoveredMail;
            if (mailbox != null) { int index = mailbox.items.FindIndex(m => m.mailId == mailId); if (index >= 0) mailbox.items[index] = recoveredMail; }
            mailError = confirmed ? null : "The server has not confirmed a claim. It is safe to try again.";
            if (confirmed) AuthNotificationView.Show("Gift claimed", "Your wallet has been refreshed from the server. Have fun!", AuthNotificationView.ResultKind.Success);
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (Accept(request, epoch, userId)) mailError = "The outcome is not confirmed. Select REFRESH to recover your mail and wallet before retrying."; }
        finally
        {
            if (Accept(request, epoch, userId))
            {
                claimBusy = false; if (isMail && root.gameObject.activeInHierarchy) Render();
                if (confirmed) RefreshMailbox();
            }
        }
    }
    private void AllAction()
    {
        if (isMail) { ClaimAll(); return; }
        foreach (var article in News()) newsRead.Mark(article.id);
        PlayerPrefs.SetString(readScope, newsRead.Serialize()); PlayerPrefs.Save();
        Render();
    }
    private async void ClaimAll()
    {
        if (claimBusy || refreshing || !PlayerAuthService.IsAuthenticated || (mailbox?.unclaimedCount ?? 0) == 0) return;
        long request = session.Current, epoch = AuthStorage.SessionVersion;
        string userId = PlayerAuthService.UserId;
        detailRequest++; // Discard a pre-claim Read response that could repaint an unclaimed letter.
        claimBusy = true; Render();
        try
        {
            var receipt = await Gateway.ClaimAll(epoch);
            if (!Accept(request, epoch, userId)) return;
            var me = await Gateway.Me(epoch);
            if (!Accept(request, epoch, userId) || me == null || me.userId != userId) return;
            PlayerAuthService.ApplyApiUser(me);
            var result = await Gateway.List(page, epoch);
            if (!Accept(request, epoch, userId)) return;
            mailbox = result;
            selectedMail = result.items.Find(m => m.mailId == selectedMailId);
            mailError = null;
            string rewards = string.Join("\n", receipt.rewards.Select(a => $"{a.amount:N0} {a.currency}"));
            AuthNotificationView.Show(receipt.claimedCount > 0 ? "All gifts claimed" : "No gifts remaining",
                receipt.claimedCount > 0 ? $"Claimed gifts from {receipt.claimedCount} letters and marked them as read.\n{rewards}" : "Your mailbox and wallet are up to date.",
                AuthNotificationView.ResultKind.Success);
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (Accept(request, epoch, userId)) mailError = "Claim outcome could not be confirmed. REFRESH to recover your mailbox and wallet. Retrying will never grant the same gift twice.";
        }
        finally
        {
            if (Accept(request, epoch, userId)) { claimBusy = false; UpdateCounters(); if (isMail && root.gameObject.activeInHierarchy) Render(); }
        }
    }
    private void RenderNews()
    {
        var articles = News().Where(a => filter == "All" || a.category == filter).ToList();
        float y = 4;
        foreach (var article in articles)
        {
            var a = article;
            var button = UI.Button(list, "News " + a.id, "", new Rect(5, y, 573, a.featured ? 225 : 179), selectedNewsId == a.id ? UI.Yellow : a.featured ? UI.Pink : UI.White,
                () => SelectNews(a), 25);
            Label(button.transform, "Article Version", (newsRead.IsRead(a.id) ? "" : "NEW  ") + "v." + a.version + "  /  " + a.category, new Rect(19, 12, 533, 35), 23, UI.Muted);
            Label(button.transform, "Article Title", a.title, new Rect(19, 54, 532, a.featured ? 104 : 87), 28, UI.Ink, true);
            if (a.featured) Label(button.transform, "Featured", "FEATURED  /  " + Date(a.publishAtUtc), new Rect(19, 172, 532, 32), 22, UI.Muted);
            y += a.featured ? 242 : 195;
        }
        list.sizeDelta = new Vector2(0, Mathf.Max(567, y));
        if (articles.Count == 0) Body(list, CommunityContent.Error ?? "No articles in this category yet.", 30, 540, 28);
        var selected = News().Find(a => a.id == selectedNewsId);
        if (selected == null)
        {
            var hero = News().FirstOrDefault(a => a.featured) ?? News().FirstOrDefault();
            IllustratedHeader(detail, hero?.artKey ?? "news", hero == null ? "THE NEWS DESK" : hero.artKey == "chaos" ? "35 BUFFS. ENDLESS POSSIBILITIES." : "FEATURED / v." + hero.version, UI.Pink);
            float end = Body(detail, hero?.summary ?? "Choose a story to read the full development update.", 186, 955, 31);
            if (hero != null) ActionButton(detail, "READ FEATURED", end + 36, () => SelectNews(hero), UI.Yellow);
            detail.sizeDelta = new Vector2(0, 555); return;
        }
        IllustratedHeader(detail, selected.artKey, "v." + selected.version + "  /  " + selected.category, UI.Pink);
        var title = Label(detail, "Article Full Title", selected.title, new Rect(8, 166, 970, 150), 37, UI.Ink, true);
        float bottom = 178 + title.GetPreferredValues(selected.title, 970, 0).y;
        Label(detail, "Publication Date", "Published: " + Date(selected.publishAtUtc), new Rect(8, bottom + 8, 970, 38), 23, UI.Muted);
        bottom = Body(detail, selected.body, bottom + 67, 965, 29);
        detail.sizeDelta = new Vector2(0, Mathf.Max(555, bottom + 35));
    }
    private void SelectNews(CommunityNewsArticle article)
    {
        bool changed = selectedNewsId != article.id;
        selectedNewsId = article.id; newsRead.Mark(article.id);
        PlayerPrefs.SetString(readScope, newsRead.Serialize()); PlayerPrefs.Save();
        Render(resetDetail: changed);
    }
    private static void RestoreScroll(ScrollRect scroll, float offset, bool reset)
    {
        scroll.StopMovement();
        var position = scroll.content.anchoredPosition;
        position.y = reset ? 0 : Mathf.Clamp(offset, 0, Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height));
        scroll.content.anchoredPosition = position;
    }
    private static string Date(string value) => DateTimeOffset.TryParse(value, out var date) ? date.ToString("dd/MM/yyyy") : "-";
    private static string State(CommunityMail m) => m.IsClaimed ? "CLAIMED" : m.status == "Expired" ? "EXPIRED" : m.IsClaimable ? "UNCLAIMED GIFTS" : "SYSTEM LETTER";
    private static string RewardName(string currency) => currency == "GOLDS" ? "Gold" : currency == "DIAMONDS" ? "Diamonds" : "Tickets";
    private static Color RewardColor(string currency) => currency == "GOLDS" ? UI.Yellow : currency == "DIAMONDS" ? UI.Blue : UI.Lavender;
    private static TextMeshProUGUI Label(Transform parent, string name, string value, Rect rect, float size, Color? color = null, bool wrap = false)
    {
        var text = UI.Text(parent, name, value, rect, size, color, TextAlignmentOptions.TopLeft, wrap);
        CommunityTypography.Apply(text); return text;
    }
    private static float Body(Transform parent, string value, float y, float width, float size)
    {
        var text = Label(parent, "Body", value, new Rect(8, y, width, 100), size, UI.Ink, true);
        text.enableAutoSizing = false; text.overflowMode = TextOverflowModes.Overflow;
        float height = text.GetPreferredValues(value, width, 0).y + 18;
        text.rectTransform.sizeDelta = new Vector2(width, height);
        text.rectTransform.anchoredPosition = new Vector2(8 + width * .5f, -y - height * .5f);
        return y + height;
    }
    private static Button ActionButton(Transform parent, string text, float y, UnityAction action, Color color)
    {
        var button = UI.Button(parent, text, text, new Rect(12, y, 940, 65), color, action, 28);
        CommunityTypography.Apply(button.GetComponentInChildren<TextMeshProUGUI>()); return button;
    }
    private static void IllustratedHeader(Transform parent, string art, string caption, Color color)
    {
        var paper = UI.Card(parent, "Illustrated Cover " + art, new Rect(10, 5, 956, 144), color, 0, false, 14).transform;
        var shape = art == "shop" ? SketchbookDoodle.Shape.Coin : art == "ranked" ? SketchbookDoodle.Shape.Trophy :
            art == "chaos" ? SketchbookDoodle.Shape.Star : art == "kingdoms" ? SketchbookDoodle.Shape.Queen : art == "loadout" ? SketchbookDoodle.Shape.Knight : SketchbookDoodle.Shape.Board;
        UI.Doodle(paper, "Cover Pencil Illustration", new Rect(30, 15, 112, 112), shape, UI.Ink, -6);
        UI.Doodle(paper, "Cover Pencil Accent", new Rect(834, 33, 65, 65), SketchbookDoodle.Shape.Star, UI.Muted, 12);
        Label(paper, "Cover Caption", caption, new Rect(166, 43, 650, 69), 31, UI.Ink, true);
    }
    private static void Clear(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--) { var child = parent.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
    }
    private static void SetButtonText(Button button, string value) { var text = button.GetComponentInChildren<TextMeshProUGUI>(); text.text = value; CommunityTypography.Apply(text); }
    private static void ShowButton(Button button, bool visible)
    {
        button.gameObject.SetActive(visible);
        var shadow = button.transform.parent.Find(button.name + " Pencil Shadow");
        if (shadow) shadow.gameObject.SetActive(visible);
    }
    private static void AddScrollbar(Transform parent, ScrollRect scroll, Rect area)
    {
        var track = UI.Node(parent, scroll.name + " Scroll Track", area);
        var background = track.gameObject.AddComponent<Image>(); background.color = new Color(.35f, .30f, .28f, .10f);
        var handle = UI.Node(track, "Pencil Scroll Handle", new Rect(0, 0, area.width, area.height));
        handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one;
        handle.offsetMin = handle.offsetMax = Vector2.zero;
        var ink = handle.gameObject.AddComponent<Image>(); ink.color = new Color(.35f, .30f, .28f, .45f);
        var bar = track.gameObject.AddComponent<Scrollbar>(); bar.handleRect = handle; bar.targetGraphic = ink;
        bar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }
    private void Update()
    {
        if (!built || !root.gameObject.activeInHierarchy) return;
        var input = root.GetComponent<CanvasGroup>();
        if (input && !input.interactable) return;
        if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)) { back?.Invoke(); return; }
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame && EventSystem.current)
        {
            var buttons = root.GetComponentsInChildren<Button>().Where(b => b.IsActive() && b.IsInteractable()).ToArray();
            if (buttons.Length == 0) return;
            int index = Array.FindIndex(buttons, b => b.gameObject == EventSystem.current.currentSelectedGameObject);
            bool reverse = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            EventSystem.current.SetSelectedGameObject(buttons[(index + (reverse ? buttons.Length - 1 : 1) + buttons.Length) % buttons.Length].gameObject);
        }
        if (Gamepad.current != null && detailScroll) detailScroll.verticalNormalizedPosition = Mathf.Clamp01(detailScroll.verticalNormalizedPosition + Gamepad.current.rightStick.ReadValue().y * Time.unscaledDeltaTime * .7f);
        var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != lastSelection) { lastSelection = selected; RevealSelection(listScroll, selected); RevealSelection(detailScroll, selected); }
    }
    private static void RevealSelection(ScrollRect scroll, GameObject selected)
    {
        if (!selected || !scroll || !selected.transform.IsChildOf(scroll.content) || !(selected.transform is RectTransform rect)) return;
        var corners = new Vector3[4]; var viewport = new Vector3[4];
        rect.GetWorldCorners(corners); scroll.viewport.GetWorldCorners(viewport);
        float delta = corners[0].y < viewport[0].y ? viewport[0].y - corners[0].y : corners[2].y > viewport[2].y ? viewport[2].y - corners[2].y : 0;
        var position = scroll.content.anchoredPosition;
        position.y = Mathf.Clamp(position.y + scroll.content.InverseTransformVector(new Vector3(0, delta, 0)).y, 0, Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height));
        scroll.content.anchoredPosition = position;
    }
    public void Dispose() { if (disposed) return; disposed = true; session.Advance(); PlayerAuthService.StateChanged -= OnSessionChanged; }
    private void OnDestroy() => Dispose();
}
