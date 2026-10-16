using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UI = SketchbookUI;

public partial class ChessLanController
{
    private sealed class NetworkLobbyUiController
    {
        private readonly ChessLanController owner;
        private GameObject canvasRoot;
        private RectTransform contentRoot;
        private NetworkLobbyUiMode currentMode;
        private int builtRevision = -1;
        private TextMeshProUGUI statusLabel, connectionLabel, roomCodeLabel, hostLabel, guestLabel;
        private TMP_InputField joinInput;
        private MatchLoadoutController loadoutPicker;
        private MatchFoundConfirmationView confirmation;
        private string builtLoadoutMode;
        private int builtLoadoutVersion = -1, builtLifecycle = -1;
        private readonly List<(Button button, Func<bool> enabled)> controls = new List<(Button, Func<bool>)>();
        public bool IsCodeFocused => joinInput && joinInput.isFocused;

        public NetworkLobbyUiController(ChessLanController controller)
        {
            owner = controller;
            canvasRoot = new GameObject("Online Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 64;
            ResponsiveUi.ConfigureCanvasScaler(canvasRoot.GetComponent<CanvasScaler>(), new Vector2(1672, 941));
            var paper = canvasRoot.AddComponent<AntialiasedMenuImage>(); paper.color = UI.Paper; paper.raycastTarget = true;
            contentRoot = MenuDesignFrame.Create(canvasRoot.transform, "Online Menu", new Vector2(1672, 941));
            canvasRoot.SetActive(false);
        }
        public void Show(NetworkLobbyUiMode mode)
        { currentMode = mode; canvasRoot.SetActive(true); Refresh(); }
        public void Hide() { if (canvasRoot) canvasRoot.SetActive(false); }
        public void Destroy() { if (canvasRoot) UnityEngine.Object.Destroy(canvasRoot); }
        public void Refresh()
        {
            if (!canvasRoot || !canvasRoot.activeSelf) return;
            if (builtRevision != owner.menuRevision)
            {
                if (loadoutPicker && IsLoadoutPage && builtLoadoutMode == owner.requestedGameMode &&
                    builtLoadoutVersion == owner.loadoutOpenVersion && builtLifecycle == owner.lifecycle) builtRevision = owner.menuRevision;
                else Rebuild();
            }
            if (loadoutPicker)
            {
                loadoutPicker.SetOnlineContext(() => owner.CanEditLoadout && !owner.forgetCancelledMatch && !owner.findMatchAfterRequest && owner.online?.Connected == true && owner.online?.Recovering != true,
                    owner.MatchConnectionState + "  /  " + PlayModeNames.DisplayText(owner.statusMessage));
                bool searching = owner.menuPage == OnlineMenuPage.Searching;
                loadoutPicker.SetMatchmakingState(owner.HasLobbyReservation || searching || owner.menuPage == OnlineMenuPage.Accept || owner.findMatchAfterRequest,
                    searching, owner.SearchElapsed, owner.requestInFlight || owner.online?.Connected != true || owner.online?.Recovering == true,
                    owner.RequestCancelSearch);
            }
            if (confirmation) confirmation.SetState(owner.menuPage == OnlineMenuPage.Accept, owner.FoundOpponent,
                owner.AcceptanceSeconds, owner.LocalAccepted, owner.requestInFlight, owner.online?.Connected == true);
            if (statusLabel) statusLabel.text = owner.requestInFlight ? "Working...  " + owner.statusMessage : owner.statusMessage;
            if (connectionLabel) connectionLabel.text = owner.MatchConnectionState;
            if (roomCodeLabel) roomCodeLabel.text = owner.GetLobbyRoomCode();
            if (hostLabel) hostLabel.text = owner.GetHostPlayerLine();
            if (guestLabel) guestLabel.text = owner.GetGuestPlayerLine();
            if (joinInput) joinInput.interactable = !owner.requestInFlight;
            foreach (var control in controls) if (control.button) control.button.interactable = control.enabled();
        }
        private void Rebuild()
        {
            // Disable before deferred destruction so old controls cannot take a click.
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                var child = contentRoot.GetChild(i).gameObject; child.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(child); else UnityEngine.Object.DestroyImmediate(child);
            }
            controls.Clear(); joinInput = null;
            loadoutPicker = null; confirmation = null;
            roomCodeLabel = hostLabel = guestLabel = null;
            builtRevision = owner.menuRevision;
            statusLabel = connectionLabel = null;
            if (IsLoadoutPage)
            { BuildLoadout(); BuildAcceptancePopup(); return; }
            UI.Background(contentRoot);
            UI.Doodle(contentRoot, "Corner Star", new Rect(1480, 55, 75, 75), SketchbookDoodle.Shape.Star, UI.Yellow, 12);
            ActionButton(contentRoot, "Back", new Rect(64, 54, 155, 64), UI.White, owner.BackOnlineMenu);
            connectionLabel = UI.Text(contentRoot, "Connection", "", new Rect(1210, 62, 250, 44), 23, UI.Muted, TextAlignmentOptions.Right);
            UI.Text(contentRoot, "Page Title", Title(), new Rect(64, 144, 1430, 80), 57);
            switch (owner.menuPage)
            {
                case OnlineMenuPage.Home: BuildHome(); break;
                case OnlineMenuPage.Rooms: BuildRooms(); break;
                case OnlineMenuPage.FindRoom: BuildRoomBrowser(); break;
                case OnlineMenuPage.Room: BuildRoom(); break;
            }
            statusLabel = UI.Text(contentRoot, "Status", "", new Rect(64, 860, 1544, 57), 23, UI.Muted, TextAlignmentOptions.MidlineLeft, true);
        }
        private string Title()
        {
            switch (owner.menuPage)
            {
                case OnlineMenuPage.Home: return "ONLINE";
                case OnlineMenuPage.Rooms: return owner.customRoomEntry.HasValue ? "CUSTOM / CREATE ROOM" : "PLAY WITH FRIENDS";
                case OnlineMenuPage.FindRoom: return "CUSTOM / BROWSER ROOM";
                case OnlineMenuPage.Room: return "YOUR ROOM";
                case OnlineMenuPage.Loadout: return "CHOOSE YOUR LOADOUT";
                case OnlineMenuPage.Searching: return "FINDING OPPONENT";
                default: return "MATCH FOUND!";
            }
        }
        private Button ActionButton(Transform parent, string label, Rect rect, Color color, UnityAction action, Func<bool> enabled = null, float fontSize = 28)
        {
            var button = UI.Button(parent, label, label, rect, color, action, fontSize);
            controls.Add((button, enabled ?? (() => !owner.requestInFlight)));
            return button;
        }
        private void ChoiceCard(float x, string title, string description, string actionLabel, Color color, SketchbookDoodle.Shape shape, UnityAction action)
        {
            var button = ActionButton(contentRoot, "", new Rect(x, 298, 740, 462), color, action);
            button.name = title;
            var parent = button.transform;
            UI.Tape(parent, 310, -14);
            UI.Doodle(parent, title + " Doodle", new Rect(40, 35, 108, 108), shape, UI.Ink, -5);
            UI.Text(parent, "Title", title, new Rect(40, 165, 655, 64), 43);
            UI.Text(parent, "Description", description, new Rect(40, 244, 655, 98), 27, UI.Muted, TextAlignmentOptions.TopLeft, true);
            UI.Text(parent, "Action", actionLabel + "  >", new Rect(40, 373, 655, 48), 30);
        }
        private void BuildHome()
        {
            UI.Text(contentRoot, "Intro", "A friendly room, or a fresh opponent. Pick your next game.", new Rect(64, 235, 1480, 45), 27, UI.Muted);
            ChoiceCard(64, "ROOMS", "Create a room and share its code, or find a room to join.", "Create / Find Room", UI.Blue, SketchbookDoodle.Shape.Board, owner.OpenRooms);
            ChoiceCard(868, "MATCHMAKING", "Choose your loadout and find an opponent. Accept together to begin.", "Choose Loadout", UI.Yellow, SketchbookDoodle.Shape.Pawn, owner.OpenMatchmaking);
        }
        private void ModePicker(float y)
        {
            UI.Text(contentRoot, "Mode", "GAME MODE", new Rect(64, y, 188, 54), 24, UI.Muted);
            ActionButton(contentRoot, "Classic", new Rect(270, y, 174, 54), IsAramGameMode(owner.requestedGameMode) ? UI.White : UI.Yellow,
                () => owner.ChangeOnlineMode("Classic"), () => owner.CanEditLoadout);
            ActionButton(contentRoot, "CHAOS", new Rect(466, y, 174, 54), IsAramGameMode(owner.requestedGameMode) ? UI.Yellow : UI.White,
                () => owner.ChangeOnlineMode("Aram"), () => owner.CanEditLoadout);
            UI.Text(contentRoot, "Settings", $"{owner.onlineRegion}  /  {owner.onlineInitialSeconds / 60} min + {owner.onlineIncrementSeconds} sec", new Rect(985, y, 615, 54), 26, UI.Muted, TextAlignmentOptions.Right);
        }
        private void BuildRooms()
        {
            ModePicker(235);
            if (owner.customRoomEntry.HasValue)
            {
                ChoiceCard(466, "CREATE ROOM", "Make a room for two players. Share its code with your friend once it is created.", "Create Room", UI.Green, SketchbookDoodle.Shape.Board, owner.RequestCreateRoom);
                return;
            }
            ChoiceCard(64, "CREATE ROOM", "Make a room for two players. Your room code appears as soon as it is created.", "Create Room", UI.Green, SketchbookDoodle.Shape.Board, owner.RequestCreateRoom);
            ChoiceCard(868, "FIND ROOM", "Browse open rooms or enter a room code to join your friend.", "Browse Rooms", UI.Blue, SketchbookDoodle.Shape.Ticket, owner.OpenRoomBrowser);
        }
        private void BuildRoomBrowser()
        {
            ModePicker(235);
            var card = UI.Card(contentRoot, "Join By Code", new Rect(64, 310, 1544, 92), UI.Blue).rectTransform;
            UI.Text(card, "Code Caption", "ROOM CODE", new Rect(24, 16, 225, 60), 26);
            joinInput = CodeInput(card, new Rect(264, 14, 690, 64));
            ActionButton(card, "Join", new Rect(982, 14, 230, 64), UI.Yellow, () => owner.RequestJoinRoom(joinInput.text),
                () => !owner.requestInFlight && !string.IsNullOrWhiteSpace(owner.roomCodeInput));
            ActionButton(card, "Refresh", new Rect(1232, 14, 283, 64), UI.White, owner.RefreshRoomBrowser);
            var content = UI.ScrollArea(contentRoot, "Open Rooms", new Rect(64, 421, 1544, 295), out _);
            var rooms = owner.roomListing?.items;
            if (rooms == null || rooms.Count == 0)
                UI.Text(content, "Empty Rooms", owner.roomListError != null ? "Couldn't load rooms. Press Refresh to try again." : owner.roomsLoading ? "Loading rooms..." : "No rooms yet. Create a room, or join using a code.",
                    new Rect(35, 75, 1465, 145), 32, UI.Muted, TextAlignmentOptions.Center, true);
            else
            {
                content.sizeDelta = new Vector2(0, Mathf.Max(295, rooms.Count * 91));
                for (int i = 0; i < rooms.Count; i++)
                {
                    var room = rooms[i]; bool full = room.memberCount >= room.capacity;
                    var row = ActionButton(content, "", new Rect(4, i * 91 + 4, 1532, 78), full ? UI.Paper : UI.White,
                        () => owner.RequestJoinRoom(room.code), () => !owner.requestInFlight && !full && !owner.HasLobbyReservation);
                    row.name = "Room " + room.code;
                    UI.Text(row.transform, "Code", room.code, new Rect(25, 10, 255, 56), 33);
                    UI.Text(row.transform, "Details", $"{PlayModeNames.DisplayBackend(room.settings.mode)}   /   {room.settings.initialSeconds / 60} min + {room.settings.incrementSeconds} sec   /   {room.settings.region}", new Rect(295, 10, 835, 56), 27, UI.Muted);
                    UI.Text(row.transform, "Capacity", $"{room.memberCount}/{room.capacity}", new Rect(1160, 10, 115, 56), 28);
                    UI.Text(row.transform, "Join", full ? "Full" : "Join  >", new Rect(1300, 10, 204, 56), 29);
                }
            }
            ActionButton(contentRoot, "Previous", new Rect(64, 752, 215, 60), UI.White, () => owner.ChangeRoomListPage(-1), () => !owner.requestInFlight && owner.roomListPage > 1);
            UI.Text(contentRoot, "Page", $"Page {owner.roomListPage}   /   {owner.roomListing?.total ?? 0} rooms", new Rect(322, 752, 750, 60), 26, UI.Muted);
            ActionButton(contentRoot, "Next", new Rect(1393, 752, 215, 60), UI.White, () => owner.ChangeRoomListPage(1),
                () => !owner.requestInFlight && owner.roomListPage * 8 < (owner.roomListing?.total ?? 0));
        }
        private TMP_InputField CodeInput(Transform parent, Rect area)
        {
            var background = UI.Card(parent, "Room Code Input", area, UI.White, 0, false, 10);
            background.raycastTarget = true;
            var field = background.gameObject.AddComponent<TMP_InputField>();
            var viewport = UI.Node(background.transform, "Text Viewport", new Rect(18, 0, area.width - 36, area.height));
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = UI.Text(viewport, "Text", "", new Rect(0, 0, area.width - 36, area.height), 29);
            var placeholder = UI.Text(viewport, "Placeholder", "Enter room code", new Rect(0, 0, area.width - 36, area.height), 27, UI.Muted);
            field.targetGraphic = background; field.textViewport = viewport; field.textComponent = text; field.placeholder = placeholder;
            field.contentType = TMP_InputField.ContentType.Alphanumeric; field.characterLimit = 8; field.lineType = TMP_InputField.LineType.SingleLine;
            field.SetTextWithoutNotify(owner.roomCodeInput);
            field.onValueChanged.AddListener(value => owner.roomCodeInput = NormalizeRoomCode(value));
            field.onSubmit.AddListener(value => { if (!owner.requestInFlight && !string.IsNullOrWhiteSpace(value)) owner.RequestJoinRoom(value); });
            return field;
        }
        private void BuildRoom()
        {
            var code = UI.Card(contentRoot, "Room Code Card", new Rect(64, 260, 650, 457), UI.Blue).rectTransform;
            UI.Tape(code, 270, -14);
            UI.Text(code, "Share", "SHARE THIS ROOM CODE", new Rect(34, 45, 580, 50), 26, UI.Muted);
            roomCodeLabel = UI.Text(code, "Code", "", new Rect(34, 120, 580, 115), 78, UI.Ink, TextAlignmentOptions.Center);
            ActionButton(code, "Copy Code", new Rect(124, 307, 402, 78), UI.White, owner.RequestCopyRoomCode);
            var players = UI.Card(contentRoot, "Players Card", new Rect(752, 260, 856, 457), UI.White).rectTransform;
            UI.Text(players, "Heading", "PLAYERS  /  2", new Rect(36, 26, 760, 56), 31);
            hostLabel = UI.Text(players, "Host", "", new Rect(36, 105, 760, 64), 34);
            guestLabel = UI.Text(players, "Guest", "", new Rect(36, 188, 760, 64), 34);
            UI.Text(players, "Clock", "CLOCK", new Rect(36, 279, 160, 52), 24, UI.Muted);
            foreach (var preset in new[] { (300, 0, "5 + 0"), (600, 5, "10 + 5"), (900, 10, "15 + 10") })
            {
                int x = preset.Item1 == 300 ? 192 : preset.Item1 == 600 ? 396 : 600;
                ActionButton(players, preset.Item3, new Rect(x, 279, 182, 52), owner.online?.Room?.settings?.initialSeconds == preset.Item1 ? UI.Yellow : UI.Paper,
                    () => owner.RequestChangeTimeControl(preset.Item1, preset.Item2), () => !owner.requestInFlight && owner.online?.Room?.ownerId == PlayerAuthService.UserId);
            }
            UI.Text(players, "Hint", "The room owner starts when both players have joined.", new Rect(36, 365, 780, 54), 25, UI.Muted, TextAlignmentOptions.MidlineLeft, true);
            ActionButton(contentRoot, "Leave Room", new Rect(64, 756, 285, 76), UI.Pink, owner.BackOnlineMenu);
            ActionButton(contentRoot, "Start Game", new Rect(1038, 756, 570, 76), UI.Yellow, owner.RequestStartGame,
                () => !owner.requestInFlight && owner.online?.Room?.ownerId == PlayerAuthService.UserId && owner.online.Room.members.Count == 2);
        }
        private bool IsLoadoutPage => owner.menuPage == OnlineMenuPage.Loadout || owner.menuPage == OnlineMenuPage.Searching || owner.menuPage == OnlineMenuPage.Accept;
        private void BuildLoadout()
        {
            builtLoadoutMode = owner.requestedGameMode;
            builtLoadoutVersion = owner.loadoutOpenVersion; builtLifecycle = owner.lifecycle;
            var screen = UI.Node(contentRoot, "Shared Match Loadout", new Rect(0, 0, 1672, 941));
            var picker = screen.gameObject.AddComponent<MatchLoadoutController>();
            loadoutPicker = picker;
            picker.Initialize(screen, LoadingManager.For(owner.chessGame));
            picker.OpenOnline(selection =>
            {
                if (owner.HasLobbyReservation) return;
                owner.loadoutLoaded = true;
                var next = owner.loadoutContinuation ?? owner.RequestFindMatch;
                if (next == (Action)owner.RequestFindMatch && owner.requestInFlight) owner.findMatchAfterRequest = true;
                else next.Invoke();
            }, owner.BackOnlineMenu, owner.loadoutActionLabel);
            picker.ConfigureLobby(owner.customRoomEntry.HasValue ? PlayModeNames.Label(owner.customRoomEntry.Value) :
                PlayModeNames.DisplayBackend(owner.requestedGameMode).ToUpperInvariant() + " / NORMAL", owner.loadoutActionLabel);
        }
        private void BuildAcceptancePopup()
        {
            var overlay = UI.Node(contentRoot, "Match Found Popup", new Rect(0, 0, 1672, 941));
            confirmation = overlay.gameObject.AddComponent<MatchFoundConfirmationView>();
            confirmation.Build(owner.AcceptFoundMatch,
                () => owner.DeclineFoundMatch("Match declined. Check your loadout and try again."));
        }
    }
}
