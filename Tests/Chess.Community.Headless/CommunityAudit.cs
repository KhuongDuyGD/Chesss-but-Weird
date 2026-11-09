using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static class CommunityAudit
{
    private static readonly List<string> report = new List<string>();
    private static int checks, ticks;
    private static string output;
    private static GameObject canvasObject;
    private static RectTransform page;
    private static CommunityMenuController controller;
    public static void Run()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').Contains("/Logs/community-qa/")) throw new Exception("Isolated QA project required.");
        output = Path.GetDirectoryName(Application.dataPath);
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorApplication.update += Start;
        EditorApplication.EnterPlaymode();
    }
    private static async void Start()
    {
        if (!EditorApplication.isPlaying || ++ticks < 12) return;
        EditorApplication.update -= Start;
        try
        {
            await Audit(); report.Add("PASS " + checks + " assertions using production UI/content with isolated transport/auth/audio ports.");
            report.Add("Graphics: " + SystemInfo.graphicsDeviceType + "; captures are actual Unity camera renders of this fixture, not live server/two-client play.");
            File.WriteAllLines(Path.Combine(output, "audit-result.txt"), report); EditorApplication.Exit(0);
        }
        catch (Exception ex) { report.Add("FAIL " + ex); File.WriteAllLines(Path.Combine(output, "audit-result.txt"), report); EditorApplication.Exit(1); }
    }
    private static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    private static void Reject(Action action, string label) { bool rejected = false; try { action(); } catch (FormatException) { rejected = true; } catch (Newtonsoft.Json.JsonException) { rejected = true; } Check(rejected, label); }
    private static IEnumerable<Button> Buttons() => page.GetComponentsInChildren<Button>().Where(b => b.gameObject.activeInHierarchy);
    private static Button Button(string name) => Buttons().First(b => b.name == name);
    private static void Click(string name) { var button = Button(name); Check(button.IsInteractable(), "Button disabled: " + name); button.onClick.Invoke(); Canvas.ForceUpdateCanvases(); }
    private static bool Text(string value) => page.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains(value));
    private static async Task Audit()
    {
        var catalog = CommunityContent.Catalog;
        Check(CommunityContent.Error == null, "Bundled catalog failed"); Check(catalog.articles.Count == 8, "Expected eight verified source articles");
        Check(CommunityContent.VersionLabel == "v.0.7.0", "Shared product version");
        Check(catalog.articles.OrderBy(a => DateTimeOffset.Parse(a.sourceCommitDateUtc)).First().version == "0.1.0", "First update version");
        Check(catalog.articles.All(a => a.locale == "en-US" && (a.title + a.summary + a.body).All(c => c < 128)), "News must stay English with existing font");
        var news = catalog.PublicArticles(DateTimeOffset.UtcNow).ToList();
        Check(news.Count == 7 && news[0].id == "NEWS_CHAOS_VFX_20261109", "Rights gate / fallback hero / ordering");
        Check(!news.Any(a => a.rightsClearanceRequired), "Uncleared rights leaked publicly");
        Check(!CommunityNewsCatalog.TrustedSource("https://github.com.evil.example/KhuongDuyGD/Chesss-but-Weird/commit/" + new string('a', 40)), "URL host spoof allowed");
        Check(!CommunityNewsCatalog.TrustedSource("javascript:alert(1)"), "Unsafe URL allowed");
        Reject(() => CommunityNewsCatalog.Parse("{broken"), "Broken catalog accepted");
        Reject(() => CommunityNewsCatalog.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(new CommunityNewsCatalog { schemaVersion = 88, currentVersion = "0.6.0" })), "Unsupported schema accepted");
        var modified = Newtonsoft.Json.JsonConvert.DeserializeObject<CommunityNewsCatalog>(Newtonsoft.Json.JsonConvert.SerializeObject(catalog));
        modified.articles.First(a => a.rightsClearanceRequired).editorialStatus = "Published"; Reject(() => CommunityNewsCatalog.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(modified)), "Rights validation missing");
        var aRead = new CommunityNewsReadSet("[]"); aRead.Mark(news[0].id);
        Check(new CommunityNewsReadSet(aRead.Serialize()).IsRead(news[0].id), "Read state did not persist");
        Check(!new CommunityNewsReadSet("[]").IsRead(news[0].id), "Read state leaked");
        Check(CommunityNewsReadSet.Scope("a", false) != CommunityNewsReadSet.Scope("b", false) && CommunityNewsReadSet.Scope("a", true) != CommunityNewsReadSet.Scope("a", false), "Read scopes collide");
        Check(!new CommunityNewsReadSet("{bad").IsRead(news[0].id), "Corrupt read state failure");
        var gate = new CommunityRequestGate(); long old = gate.Advance(); gate.Advance(); Check(!gate.Accept(old), "Stale gate accepted");
        new GameObject("Event System", typeof(EventSystem));
        canvasObject = new GameObject("Community QA Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
        var paper = canvasObject.AddComponent<MainMenuPaperGraphic>(); paper.raycastTarget = false;
        page = SketchbookUI.Node(canvas.transform, "Community", new Rect(0, 0, 1920, 1080));
        controller = page.gameObject.AddComponent<CommunityMenuController>();
        var gateway = new FixtureMailboxGateway(); controller.Gateway = gateway;
        bool login = false; int backs = 0; controller.Build(page, () => backs++, () => login = true);
        var hintHost = SketchbookUI.Node(canvas.transform, "Inactive Hint Probes", new Rect(0, 0, 360, 180));
        var mailEntry = SketchbookUI.Button(hintHost, "Mail Probe", "MAILBOX", new Rect(0, 0, 360, 70), SketchbookUI.Blue, null);
        var newsEntry = SketchbookUI.Button(hintHost, "News Probe", "NEWS", new Rect(0, 90, 360, 70), SketchbookUI.Pink, null);
        controller.BindEntry(mailEntry, true); controller.BindEntry(newsEntry, false); hintHost.gameObject.SetActive(false);
        controller.Open(true); Check(gateway.Enrolls == 0 && Text("Sign in to read your mail"), "Guest envelope must be login CTA only"); Click("SIGN IN"); Check(login, "Login CTA not wired");
        controller.Open(false); Check(Text("35 BUFFS"), "Guest news hero missing");
        Check(newsEntry.transform.Find("Unread Hint").gameObject.activeSelf && !mailEntry.transform.Find("Gift Hint").gameObject.activeSelf, "Guest hint state incorrect");
        Capture("news-overview.png");
        var newsList = page.GetComponentsInChildren<ScrollRect>().First(s => s.name != "Readable Detail");
        var newsDetail = page.GetComponentsInChildren<ScrollRect>().First(s => s.name == "Readable Detail");
        newsList.verticalNormalizedPosition = .2f; Canvas.ForceUpdateCanvases();
        float listOffset = newsList.content.anchoredPosition.y;
        Check(listOffset > 100, "Fixture must select an article from a scrolled list");
        Click("News NEWS_SETTINGS_20261007"); Check(Text("v.0.1.0"), "Article version not shown");
        Check(Mathf.Abs(newsList.content.anchoredPosition.y - listOffset) < 1, "Article selection reset news list immediately");
        await Task.Yield(); await Task.Yield(); Canvas.ForceUpdateCanvases();
        Check(Mathf.Abs(newsList.content.anchoredPosition.y - listOffset) < 1, "News list jumped after layout/frame update");
        Check(!page.GetComponentsInChildren<TMP_Text>().Any(t => t.richText && t.name == "Body"), "Unsafe rich body text");
        Capture("news-article.png");
        Check(!Buttons().Any(b => b.name == "VIEW SOURCE COMMIT" || b.name == "OPEN BROWSER"), "Source-code actions still visible");
        Check(!Text("Source commit:"), "Source-code metadata still visible");
        newsDetail.verticalNormalizedPosition = .4f; Canvas.ForceUpdateCanvases();
        float detailOffset = newsDetail.content.anchoredPosition.y;
        Check(detailOffset > 0, "Fixture article must overflow for reselect regression");
        Click("News NEWS_SETTINGS_20261007");
        await Task.Yield(); await Task.Yield(); Canvas.ForceUpdateCanvases();
        Check(Mathf.Abs(newsDetail.content.anchoredPosition.y - detailOffset) < 1, "Reselecting the same article lost reading position");
        Click("News NEWS_CHAOS35_20261108");
        await Task.Yield(); await Task.Yield(); Canvas.ForceUpdateCanvases();
        Check(Mathf.Abs(newsDetail.content.anchoredPosition.y) < 1, "New article did not start at its beginning");
        Check(Mathf.Abs(newsList.content.anchoredPosition.y - listOffset) < 1, "Changing articles reset news list");
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        EventSystem.current.SetSelectedGameObject(Button("Back").gameObject);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); InputSystem.Update(); Check(keyboard.tabKey.isPressed, "Fixture Tab event not applied"); controller.SendMessage("Update");
        Check(EventSystem.current.currentSelectedGameObject != Button("Back").gameObject, "Tab did not advance focus");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update(); controller.SendMessage("Update"); Check(backs == 1, "Escape did not return");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        var gamepad = InputSystem.AddDevice<Gamepad>();
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East)); InputSystem.Update(); controller.SendMessage("Update");
        Check(backs == 2, "Controller B did not return"); InputSystem.RemoveDevice(gamepad); InputSystem.RemoveDevice(keyboard);
        foreach (var size in new[] { new Vector2(1920, 1080), new Vector2(1280, 800), new Vector2(960, 540), new Vector2(800, 600) })
        {
            ((RectTransform)canvas.transform).sizeDelta = size;
            var frame = MenuDesignFrame.Create(canvas.transform, "Safe Layout", new Vector2(1920, 1080));
            frame.gameObject.GetComponent<MenuDesignFrame>().Configure(new Vector2(1920, 1080));
            page.SetParent(frame, false); Canvas.ForceUpdateCanvases();
            foreach (var viewport in page.GetComponentsInChildren<ScrollRect>()) Within(viewport.viewport, (RectTransform)canvas.transform, "Viewport outside safe window " + size);
            Within((RectTransform)Button("Back").transform, (RectTransform)canvas.transform, "Back outside window " + size);
            Within((RectTransform)Button("Switch Destination").transform, (RectTransform)canvas.transform, "Destination outside window " + size);
            if (size.x == 960 || size.x == 1280) Capture("news-window-" + (int)size.x + "x" + (int)size.y + ".png");
            page.SetParent(canvas.transform, false);
            UnityEngine.Object.Destroy(frame.parent.gameObject);
        }
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
        PlayerAuthService.Switch("account-A"); controller.Open(true);
        Check(Text("UNCLAIMED GIFTS") && gateway.Enrolls > 0, "Authenticated welcome missing");
        Check(mailEntry.transform.Find("Unread Hint").gameObject.activeSelf && mailEntry.transform.Find("Gift Hint").gameObject.activeSelf, "Unread and gift icons not visible");
        var pendingHub = SketchbookUI.Node(canvas.transform, "Pending Hub", new Rect(0, 0, 1920, 1080));
        var pendingMode = pendingHub.gameObject.AddComponent<ModeSelectionPresentation>(); pendingMode.Build(Resources.Load<Sprite>("MainMenu/MainMenuLogoOriginal"), new ModeSelectionPresentation.Actions());
        controller.BindEntry(pendingMode.MailboxButton, true); controller.BindEntry(pendingMode.NewsButton, false);
        page.gameObject.SetActive(false); Capture("main-menu-2-pending.png"); pendingHub.gameObject.SetActive(false); page.gameObject.SetActive(true);
        Check(PlayerAuthService.CurrentApiUser.wallet.golds == 1000, "Opening hub granted gift");
        Click("Mail welcome-account-A"); Check(!gateway.Letters["account-A"].IsUnread && !gateway.Letters["account-A"].IsClaimed && gateway.Claims == 0, "Reading consumed reward");
        Check(!mailEntry.transform.Find("Unread Hint").gameObject.activeSelf && mailEntry.transform.Find("Gift Hint").gameObject.activeSelf, "Reading hid the unclaimed gift icon");
        Check(Text("2,000") || Text("2.000"), "Reward amount missing");
        Check(page.GetComponentsInChildren<TMP_Text>().All(t => t.font == ChessFontCatalog.TmpFont), "Community changed the game font");
        var letterBody = page.GetComponentsInChildren<TMP_Text>().First(t => t.name == "Body");
        var sender = page.GetComponentsInChildren<TMP_Text>().First(t => t.name == "Letter Sender");
        Check(Bounds(letterBody.rectTransform).yMax < Bounds(sender.rectTransform).yMin, "Long body moved up over sender/title");
        Capture("mail-welcome.png");
        var scroll = page.GetComponentsInChildren<ScrollRect>().First(s => s.name == "Readable Detail");
        scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases(); Capture("mail-rewards.png"); scroll.verticalNormalizedPosition = 1;
        gateway.LoseClaimResponse = true; Click("CLAIM GIFT");
        Check(gateway.Claims == 1 && PlayerAuthService.CurrentApiUser.wallet.golds == 3000 && Text("CLAIMED"), "Timeout recovery failed");
        Check(!Button("CLAIMED").interactable, "Claimed CTA enabled");
        Check(AuthNotificationView.Successes == 1, "Claim success not confirmed exactly once");
        Check(!mailEntry.transform.Find("Gift Hint").gameObject.activeSelf, "Claim did not clear gift icon");
        Capture("mail-claimed.png");
        gateway.Offline = true; controller.RefreshMailbox(); Check(Text("Could not load mail"), "Offline retry state missing"); gateway.Offline = false;
        // Hold A's list across an account switch. Its eventual completion must never repaint A's letter into B.
        gateway.PauseList = true; controller.RefreshMailbox(); var pending = gateway.PendingList; var snapshotA = gateway.Snapshot();
        gateway.PauseList = false; PlayerAuthService.Switch("account-B"); controller.Open(true); pending.SetResult(snapshotA); await Task.Yield();
        Check(!Buttons().Any(b => b.name == "Mail welcome-account-A") && Buttons().Any(b => b.name == "Mail welcome-account-B"), "Old account mail leaked");
        Click("Mail welcome-account-B"); gateway.FailMe = true; Click("CLAIM GIFT");
        Check(!Button("CLAIM GIFT").interactable && Text("The outcome is not confirmed"), "Uncertain wallet outcome enabled retry/success");
        Check(AuthNotificationView.Successes == 1, "False success before wallet recovery");
        gateway.FailMe = false; controller.RefreshMailbox(); Check(Text("CLAIMED"), "Refresh failed to recover committed claim");
        PlayerAuthService.Switch(null); controller.Open(true); Check(!Buttons().Any(b => b.name.StartsWith("Mail welcome-")), "Logout left private letter visible");
        controller.Open(false); foreach (var article in news) Click("News " + article.id);
        Check(!newsEntry.transform.Find("Unread Hint").gameObject.activeSelf, "Read news kept its unread hint");
        controller.Dispose(); page.gameObject.SetActive(false);
        // Both menus share a version; community entry points belong to Main Menu 2 only.
        var main = SketchbookUI.Node(canvas.transform, "Main", new Rect(0, 0, 1920, 1080));
        bool openedMail = false, openedNews = false;
        var presentation = main.gameObject.AddComponent<MainMenuPresentation>();
        presentation.Build(Resources.Load<Sprite>("MainMenu/MainMenuLogoOriginal"), () => { }, () => { }, () => { });
        Check(!main.GetComponentsInChildren<Button>(true).Any(b => b.name == "Mailbox" || b.name == "News"), "Community entry leaked into Main Menu 1");
        Check(main.GetComponentsInChildren<TMP_Text>().Any(t => t.name == "Main Menu Version" && t.text == CommunityContent.VersionLabel), "Main version mismatch");
        Capture("main-menu-1.png"); main.gameObject.SetActive(false);
        var hub = SketchbookUI.Node(canvas.transform, "Hub", new Rect(0, 0, 1920, 1080));
        var mode = hub.gameObject.AddComponent<ModeSelectionPresentation>(); mode.Build(Resources.Load<Sprite>("MainMenu/MainMenuLogoOriginal"),
            new ModeSelectionPresentation.Actions { Mailbox = () => openedMail = true, News = () => openedNews = true });
        controller.BindEntry(mode.MailboxButton, true); controller.BindEntry(mode.NewsButton, false);
        mode.MailboxButton.onClick.Invoke(); mode.NewsButton.onClick.Invoke(); Check(openedMail && openedNews, "Main Menu 2 community callbacks disconnected");
        Check(hub.GetComponentsInChildren<TMP_Text>().Any(t => t.name == "Mode Hub Version" && t.text == CommunityContent.VersionLabel), "Hub version mismatch");
        var music = hub.GetComponentsInChildren<Button>().First(b => b.name == "Music Record");
        Check(!Bounds((RectTransform)music.transform).Overlaps(Bounds((RectTransform)mode.MailboxButton.transform)), "Music/mail buttons overlap");
        Check(!Bounds((RectTransform)mode.MailboxButton.transform).Overlaps(Bounds((RectTransform)mode.NewsButton.transform)), "Mail/news buttons overlap");
        Capture("main-menu-2.png");
    }
    private static Rect Bounds(RectTransform rect) { var c = new Vector3[4]; rect.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
    private static void Within(RectTransform child, RectTransform parent, string label)
    {
        var c = Bounds(child); var p = Bounds(parent);
        Check(c.xMin >= p.xMin - 1 && c.yMin >= p.yMin - 1 && c.xMax <= p.xMax + 1 && c.yMax <= p.yMax + 1, label);
    }
    private static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var bar in canvasObject.GetComponentsInChildren<Scrollbar>()) Within(bar.handleRect, (RectTransform)bar.transform, "Scrollbar handle escaped its track");
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var camera = new GameObject("Capture Camera", typeof(Camera)).GetComponent<Camera>();
        var size = ((RectTransform)canvasObject.transform).rect.size;
        int width = Mathf.RoundToInt(size.x), height = Mathf.RoundToInt(size.y);
        camera.orthographic = true; camera.orthographicSize = height * .5f; camera.transform.position = new Vector3(0, 0, -100);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = SketchbookUI.Paper;
        var target = new RenderTexture(width, height, 24); camera.targetTexture = target; camera.Render();
        var prior = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG()); RenderTexture.active = prior;
        UnityEngine.Object.Destroy(texture); camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(camera.gameObject);
    }
}
