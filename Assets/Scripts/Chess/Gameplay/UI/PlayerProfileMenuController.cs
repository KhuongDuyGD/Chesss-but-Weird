using System;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UI = SketchbookUI;

public sealed class PlayerProfileMenuController : MonoBehaviour
{
    private RectTransform root;
    private TextMeshProUGUI nameLabel, usernameLabel, emailLabel, idLabel, joinedLabel, avatarLabel;
    private TextMeshProUGUI chessLabel, boardLabel, statusLabel;
    private readonly TextMeshProUGUI[] stats = new TextMeshProUGUI[5];
    private readonly TextMeshProUGUI[] wallet = new TextMeshProUGUI[3];
    private Button refreshButton;
    private UnityAction closeAction;
    private bool loadingProfile;
    private Texture2D avatarTexture;
    private Sprite avatarSprite;

    public void Initialize(RectTransform newRoot, UnityAction newCloseAction)
    {
        root = newRoot;
        closeAction = newCloseAction;
        Build();
        Refresh(PlayerAuthService.CurrentApiUser);
    }

    public async void Open()
    {
        if (loadingProfile) return;
        loadingProfile = true;
        refreshButton.interactable = false;
        Refresh(PlayerAuthService.CurrentApiUser);
        SetStatus("Opening your notebook...");
        try
        {
            UserMeResponse user = await new UserService().GetMeAsync();
            if (!this || !root) return;
            PlayerAuthService.ApplyApiUser(user);
            Refresh(user);
            SetStatus("All caught up. Ready for your next move!");
        }
        catch (Exception error)
        {
            if (this) SetStatus("Couldn't update your profile. " + PlayerNotificationText.FromException(error));
        }
        finally
        {
            loadingProfile = false;
            if (refreshButton) refreshButton.interactable = true;
        }
    }

    private void Build()
    {
        var backdrop = root.gameObject.GetComponent<Image>() ?? root.gameObject.AddComponent<AntialiasedMenuImage>();
        backdrop.color = UI.Paper;
        var page = UI.Node(root, "Player Notebook", new Rect(0, 0, 1672, 941));
        page.gameObject.AddComponent<InventoryContentRootFitter>().Configure(1672, 941, 1f);
        UI.Background(page);
        UI.Text(page, "Eyebrow", "CHESS BUT WEIRD  /  MY LITTLE CORNER", new Rect(94, 44, 760, 30), 21, UI.Muted);
        UI.Doodle(page, "Title Highlight", new Rect(90, 123, 442, 26), SketchbookDoodle.Shape.Scribble, UI.Yellow, -1f);
        UI.Text(page, "Title", "Player notebook", new Rect(90, 74, 900, 92), 66);
        UI.Doodle(page, "Title Star", new Rect(633, 85, 45, 45), SketchbookDoodle.Shape.Star, UI.Pink, -14);
        refreshButton = UI.Button(page, "Refresh Profile", "REFRESH", new Rect(1350, 85, 225, 66), UI.Blue, Open, 26);

        var identity = UI.Card(page, "Player Card", new Rect(92, 217, 425, 584), UI.White, .7f).rectTransform;
        UI.Tape(identity, 141, -10, 140, -6);
        UI.Image(identity, "Avatar", LoadAvatar(), new Rect(128, 43, 170, 170));
        UI.Doodle(identity, "Avatar Star", new Rect(300, 68, 44, 44), SketchbookDoodle.Shape.Star, UI.Yellow, 12);
        avatarLabel = UI.Text(identity, "Avatar Label", "DEFAULT AVATAR", new Rect(38, 215, 349, 28), 18, UI.Muted, TextAlignmentOptions.Center);
        nameLabel = UI.Text(identity, "Display Name", "--", new Rect(27, 261, 371, 66), 42, UI.Ink, TextAlignmentOptions.Center);
        usernameLabel = UI.Text(identity, "Username", "--", new Rect(28, 331, 369, 35), 25, UI.Muted, TextAlignmentOptions.Center);
        UI.Doodle(identity, "Name Underline", new Rect(98, 376, 230, 12), SketchbookDoodle.Shape.Scribble, UI.Blue);
        joinedLabel = UI.Text(identity, "Member Since", "--", new Rect(32, 409, 361, 32), 22, UI.Muted, TextAlignmentOptions.Center);
        UI.Text(identity, "Email Caption", "EMAIL", new Rect(32, 456, 100, 24), 18, UI.Muted);
        emailLabel = UI.Text(identity, "Email", "--", new Rect(32, 480, 361, 31), 24);
        UI.Text(identity, "Id Caption", "PLAYER ID", new Rect(32, 520, 110, 23), 18, UI.Muted);
        idLabel = UI.Text(identity, "Player ID", "--", new Rect(141, 520, 253, 28), 19);

        var score = UI.Card(page, "Scorecard", new Rect(555, 217, 1024, 219), UI.White).rectTransform;
        UI.Text(score, "Scorecard Heading", "My scorecard", new Rect(30, 18, 600, 40), 31);
        UI.Text(score, "Scorecard Note", "every move tells a story", new Rect(637, 25, 354, 30), 21, UI.Muted, TextAlignmentOptions.Right);
        string[] captions = { "ELO", "WINS", "LOSSES", "DRAWS", "MATCHES" };
        Color[] colors = { UI.Blue, UI.Green, UI.Pink, UI.Lavender, UI.Yellow };
        for (int i = 0; i < stats.Length; i++)
        {
            float x = 23 + i * 196;
            UI.Doodle(score, captions[i] + " Highlight", new Rect(x + 31, 132, 128, 20), SketchbookDoodle.Shape.Scribble, colors[i]);
            stats[i] = UI.Text(score, captions[i] + " Value", "--", new Rect(x, 73, 192, 72), 52, UI.Ink, TextAlignmentOptions.Center);
            UI.Text(score, captions[i] + " Caption", captions[i], new Rect(x, 159, 192, 30), 22, UI.Muted, TextAlignmentOptions.Center);
        }
        wallet[0] = UI.Wallet(page, "Gold", new Rect(555, 472, 324, 126), UI.Yellow, SketchbookDoodle.Shape.Coin);
        wallet[1] = UI.Wallet(page, "Diamonds", new Rect(905, 472, 324, 126), UI.Blue, SketchbookDoodle.Shape.Diamond);
        wallet[2] = UI.Wallet(page, "Tickets", new Rect(1255, 472, 324, 126), UI.Pink, SketchbookDoodle.Shape.Ticket);

        var equipment = UI.Card(page, "Equipped Items", new Rect(555, 638, 1024, 163), UI.White).rectTransform;
        UI.Text(equipment, "Equipment Heading", "Ready for the next match", new Rect(28, 14, 800, 42), 30);
        UI.Doodle(equipment, "Chess Piece", new Rect(33, 67, 66, 74), SketchbookDoodle.Shape.Pawn, UI.Ink, -5);
        UI.Text(equipment, "Chess Set Caption", "CHESS SET", new Rect(115, 72, 360, 25), 18, UI.Muted);
        chessLabel = UI.Text(equipment, "Chess Set", "--", new Rect(115, 100, 370, 39), 27);
        UI.Doodle(equipment, "Chess Board", new Rect(544, 75, 62, 62), SketchbookDoodle.Shape.Board, UI.Ink, 4);
        UI.Text(equipment, "Board Caption", "ARENA", new Rect(626, 72, 365, 25), 18, UI.Muted);
        boardLabel = UI.Text(equipment, "Board", "--", new Rect(626, 100, 365, 39), 27);

        UI.Button(page, "Back", "<  BACK TO MENU", new Rect(92, 841, 425, 65), UI.White, () => closeAction?.Invoke(), 26);
        statusLabel = UI.Text(page, "Profile Status", "", new Rect(572, 853, 998, 41), 23, UI.Muted, TextAlignmentOptions.Center);
    }

    private void Refresh(UserMeResponse user)
    {
        nameLabel.text = string.IsNullOrWhiteSpace(user?.profile?.displayName) ? user?.username ?? "--" : user.profile.displayName;
        usernameLabel.text = string.IsNullOrWhiteSpace(user?.username) ? "--" : "@" + user.username;
        emailLabel.text = user?.email ?? "--";
        idLabel.text = user?.userId ?? "--";
        avatarLabel.text = string.IsNullOrWhiteSpace(user?.profile?.avatarId) ? "DEFAULT AVATAR" : "AVATAR  " + user.profile.avatarId;
        joinedLabel.text = DateTimeOffset.TryParse(user?.createdAt, out var joined)
            ? "Here since " + joined.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) : "Member since --";
        int?[] values = { user?.stats?.elo, user?.stats?.wins, user?.stats?.losses, user?.stats?.draws, user?.stats?.gamesPlayed };
        for (int i = 0; i < stats.Length; i++) stats[i].text = values[i]?.ToString("N0", CultureInfo.InvariantCulture) ?? "--";
        wallet[0].text = user?.wallet?.golds.ToString("N0", CultureInfo.InvariantCulture) ?? "--";
        wallet[1].text = user?.wallet?.diamonds.ToString("N0", CultureInfo.InvariantCulture) ?? "--";
        wallet[2].text = user?.wallet?.tickets.ToString("N0", CultureInfo.InvariantCulture) ?? "--";
        chessLabel.text = user?.equipped?.chessSkinId == "6aacc041e50e17c25a690158" ? "Tazji's Low Poly" : user?.equipped?.chessSkinId ?? "--";
        boardLabel.text = user?.equipped?.boardSkinId == "6aacc04be50e17c25a69015a" ? "Tazji's Low Poly Arena" : user?.equipped?.boardSkinId ?? "--";
    }

    private void SetStatus(string message) { if (statusLabel) statusLabel.text = message ?? string.Empty; }

    private Sprite LoadAvatar()
    {
        const string path = "Assets/Materials/PlayerProfile/Avatar0.png";
        Sprite prepared = CoreArtworkCache.GetSprite(path, true);
        if (prepared) return prepared;
        if (!File.Exists(path)) return null;
        avatarTexture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = "Profile Avatar" };
        if (!avatarTexture.LoadImage(File.ReadAllBytes(path))) return null;
        avatarSprite = Sprite.Create(avatarTexture, MenuArtworkBounds.GetRect(path, avatarTexture), new Vector2(.5f, .5f), 100);
        MenuTextureSampling.FinishRuntimeTexture(avatarTexture);
        return avatarSprite;
    }

    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
            if (avatarSprite) Destroy(avatarSprite);
            if (avatarTexture) Destroy(avatarTexture);
        }
        else
        {
            if (avatarSprite) DestroyImmediate(avatarSprite);
            if (avatarTexture) DestroyImmediate(avatarTexture);
        }
    }
}
