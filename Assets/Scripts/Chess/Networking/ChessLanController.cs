using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChessButWeird.Application;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum NetworkLobbyUiMode
{
    Lan,
    Multiplayer
}

public partial class ChessLanController : MonoBehaviour
{
    private sealed class NetworkLobbyUiController
    {
        private const float DesignWidth = 1672f;
        private const float DesignHeight = 941f;
        private static readonly Vector2 DesignSize = new Vector2(DesignWidth, DesignHeight);

        private readonly ChessLanController owner;
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly List<UnityEngine.Object> runtimeAssets = new List<UnityEngine.Object>();
        private readonly TextMeshProUGUI[] playerLabels = new TextMeshProUGUI[2];
        private readonly TextMeshProUGUI[] recentLabels = new TextMeshProUGUI[3];

        private GameObject canvasRoot;
        private RectTransform contentRoot;
        private TMP_InputField joinInput;
        private TextMeshProUGUI roomCodeLabel;
        private TextMeshProUGUI statusLabel;
        private TextMeshProUGUI serverLabel;
        private NetworkLobbyUiMode currentMode;

        public NetworkLobbyUiController(ChessLanController newOwner)
        {
            owner = newOwner;
            BuildCanvas();
        }

        public void Show(NetworkLobbyUiMode mode)
        {
            currentMode = mode;
            canvasRoot.SetActive(true);
            Rebuild();
            Refresh();
        }

        public void Hide()
        {
            if (canvasRoot)
                canvasRoot.SetActive(false);
        }

        public void Refresh()
        {
            if (!canvasRoot || !canvasRoot.activeSelf)
                return;

            if (roomCodeLabel)
                roomCodeLabel.text = string.IsNullOrWhiteSpace(owner.GetLobbyRoomCode()) ? "--------" : owner.GetLobbyRoomCode();

            if (joinInput && !joinInput.isFocused && string.IsNullOrWhiteSpace(joinInput.text) && !string.IsNullOrWhiteSpace(owner.roomCodeInput))
                joinInput.SetTextWithoutNotify(owner.roomCodeInput);

            if (playerLabels[0])
                playerLabels[0].text = owner.GetHostPlayerLine();
            if (playerLabels[1])
                playerLabels[1].text = owner.GetGuestPlayerLine();
            if (statusLabel)
                statusLabel.text = (IsAramGameMode(owner.requestedGameMode) ? "ARAM - " : string.Empty) + (owner.statusMessage ?? string.Empty);

            if (serverLabel)
            {
                string health = owner.GetServerHealth();
                serverLabel.text = health;
                serverLabel.color = GetServerColor(health);
            }

            for (int i = 0; i < recentLabels.Length; i++)
                if (recentLabels[i])
                    recentLabels[i].text = owner.GetRecentMatchLine(i);
        }

        public void Destroy()
        {
            if (canvasRoot)
                UnityEngine.Object.Destroy(canvasRoot);

            for (int i = 0; i < runtimeAssets.Count; i++)
                if (runtimeAssets[i])
                    UnityEngine.Object.Destroy(runtimeAssets[i]);

            runtimeAssets.Clear();
            sprites.Clear();
        }

        private void BuildCanvas()
        {
            canvasRoot = new GameObject("Network Lobby UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 64;

            ResponsiveUi.ConfigureCanvasScaler(canvasRoot.GetComponent<CanvasScaler>(), DesignSize);
            Image paper = AddImage(canvasRoot.transform, "Lobby Paper", null, Vector2.zero, Vector2.zero);
            paper.color = new Color(.985f, .965f, .91f, 1f);
            paper.raycastTarget = true;
            paper.rectTransform.anchorMin = Vector2.zero;
            paper.rectTransform.anchorMax = Vector2.one;
            paper.rectTransform.offsetMin = paper.rectTransform.offsetMax = Vector2.zero;
            RectTransform frame = MenuDesignFrame.Create(canvasRoot.transform, "Network Lobby", DesignSize);
            contentRoot = CreateChild(frame, "Network Lobby Content", Vector2.zero, DesignSize);
            canvasRoot.SetActive(false);
        }

        private void Rebuild()
        {
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(contentRoot.GetChild(i).gameObject);

            for (int i = 0; i < playerLabels.Length; i++)
                playerLabels[i] = null;
            for (int i = 0; i < recentLabels.Length; i++)
                recentLabels[i] = null;

            joinInput = null;
            roomCodeLabel = null;
            statusLabel = null;
            serverLabel = null;

            if (currentMode == NetworkLobbyUiMode.Lan)
                BuildLan();
            else
                BuildMultiplayer();
        }

        private void BuildLan()
        {
            AddImage(contentRoot, "LAN Background", LoadSprite("LANUIBlank.png"), Vector2.zero, DesignSize);
            AddButton("Host", "HostButton.png", D(410f, 402f), new Vector2(282f, 122f), owner.RequestCreateRoom);
            joinInput = AddInput("Join Code Input", D(815f, 354f), new Vector2(330f, 58f));
            AddButton("Join", "JoinButton.png", D(816f, 448f), new Vector2(312f, 96f), () => owner.RequestJoinRoom(joinInput != null ? joinInput.text : string.Empty));
            roomCodeLabel = AddText("Room Code Value", D(795f, 612f), new Vector2(265f, 60f), 34f, TextAlignmentOptions.Center, Color.black);
            AddButton("Copy", "CopyIcon.png", D(983f, 605f), new Vector2(50f, 57f), owner.RequestCopyRoomCode, 1.08f);
            playerLabels[0] = AddText("Host Player", D(1289f, 349f), new Vector2(330f, 52f), 25f, TextAlignmentOptions.MidlineLeft, Color.black);
            playerLabels[1] = AddText("Guest Player", D(1289f, 445f), new Vector2(330f, 52f), 25f, TextAlignmentOptions.MidlineLeft, Color.black);
            statusLabel = AddText("Lobby Status", D(803f, 752f), new Vector2(700f, 43f), 25f, TextAlignmentOptions.Center, new Color(0.1f, 0.08f, 0.06f, 0.9f));

            AddButton("Ready", "ReadyButton.png", D(325f, 868f), new Vector2(224f, 95f), owner.RequestReady);
            AddButton("Start", "StartButton.png", D(576f, 868f), new Vector2(218f, 95f), owner.RequestStartGame);
            AddButton("Refresh", "RefreshButton.png", D(835f, 868f), new Vector2(228f, 95f), owner.RequestRefreshLobby);
            AddButton("Leave", "LeaveButton.png", D(1094f, 868f), new Vector2(222f, 95f), owner.RequestLeaveToModeSelection);
            AddButton("Back", "BackButton.png", D(1350f, 868f), new Vector2(224f, 95f), owner.RequestBackToMultiplayerChoice);
        }

        private void BuildMultiplayer()
        {
            AddImage(contentRoot, "Multiplayer Background", LoadSprite("MultiplayerUIBlank.png"), Vector2.zero, DesignSize);
            AddButton("Create Room", "CreateRoomButton.png", D(383f, 338f), new Vector2(302f, 120f), owner.RequestCreateRoom);
            AddOnlineTextButton("Find Match", D(383f, 495f), owner.RequestFindMatch);
            AddOnlineTextButton("Cancel Search", D(383f, 558f), owner.RequestCancelSearch);
            AddOnlineTextButton("Close Room", D(383f, 621f), owner.RequestCloseRoom);
            AddOnlineTextButton("Clock 5 min + 0", D(800f, 477f), () => owner.RequestChangeTimeControl(300, 0));
            AddOnlineTextButton("Clock 10 min + 5", D(800f, 540f), () => owner.RequestChangeTimeControl(600, 5));
            joinInput = AddInput("Join Code Input", D(800f, 306f), new Vector2(320f, 54f));
            AddButton("Join Room", "JoinRoomButton.png", D(800f, 393f), new Vector2(330f, 86f), () => owner.RequestJoinRoom(joinInput != null ? joinInput.text : string.Empty));
            roomCodeLabel = AddText("Room Code Value", D(663f, 636f), new Vector2(210f, 56f), 34f, TextAlignmentOptions.Center, Color.black);
            AddButton("Copy", "CopyIcon.png", D(798f, 631f), new Vector2(40f, 48f), owner.RequestCopyRoomCode, 1.08f);
            playerLabels[0] = AddText("Host Player", D(1295f, 303f), new Vector2(292f, 52f), 25f, TextAlignmentOptions.MidlineLeft, Color.black);
            playerLabels[1] = AddText("Guest Player", D(1295f, 392f), new Vector2(292f, 52f), 25f, TextAlignmentOptions.MidlineLeft, Color.black);
            recentLabels[0] = AddText("Recent Match 0", D(1302f, 553f), new Vector2(267f, 42f), 22f, TextAlignmentOptions.Center, Color.black);
            recentLabels[1] = AddText("Recent Match 1", D(1302f, 624f), new Vector2(267f, 42f), 22f, TextAlignmentOptions.Center, Color.black);
            recentLabels[2] = AddText("Recent Match 2", D(1302f, 693f), new Vector2(267f, 42f), 22f, TextAlignmentOptions.Center, Color.black);
            statusLabel = AddText("Lobby Status", D(819f, 774f), new Vector2(1050f, 42f), 25f, TextAlignmentOptions.Center, new Color(0.1f, 0.08f, 0.06f, 0.9f));
            serverLabel = AddText("Server Health", D(1384f, 119f), new Vector2(158f, 34f), 28f, TextAlignmentOptions.Center, Color.black);

            AddButton("Ready", "ReadyButton.png", D(179f, 873f), new Vector2(240f, 92f), owner.RequestReady);
            AddButton("Start Game", "StartGameButton.png", D(488f, 873f), new Vector2(320f, 94f), owner.RequestStartGame);
            AddButton("Refresh", "RefreshButton.png", D(825f, 873f), new Vector2(294f, 94f), owner.RequestRefreshLobby);
            AddButton("Leave Room", "LeaveRoomButton.png", D(1152f, 873f), new Vector2(303f, 94f), owner.RequestLeaveToModeSelection);
            AddButton("Back", "BackButton.png", D(1458f, 873f), new Vector2(247f, 94f), owner.RequestBackToMultiplayerChoice);
        }

        private Button AddButton(string name, string spriteName, Vector2 position, Vector2 size, Action action, float hoverScale = 1.035f)
        {
            Image image = AddImage(contentRoot, name, LoadSprite(spriteName), position, size);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            button.onClick.AddListener(() => action?.Invoke());

            HandDrawnPressable pressable = image.gameObject.AddComponent<HandDrawnPressable>();
            pressable.Configure(hoverScale, 0.965f, 1.1f, new Color(1f, 0.97f, 0.74f, 1f));
            return button;
        }

        private void AddOnlineTextButton(string title, Vector2 position, Action action)
        {
            var image = AddImage(contentRoot, title, null, position, new Vector2(280, 55));
            image.color = new Color(.95f, .88f, .68f); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            var label = AddText(image.transform, "Label", Vector2.zero, new Vector2(260, 49), 28, TextAlignmentOptions.Center, Color.black);
            label.text = title;
        }

        private TMP_InputField AddInput(string name, Vector2 position, Vector2 size)
        {
            GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(AntialiasedMenuImage), typeof(TMP_InputField));
            RectTransform rect = inputObject.GetComponent<RectTransform>();
            rect.SetParent(contentRoot, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image background = inputObject.GetComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.01f);
            background.raycastTarget = true;

            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            input.characterLimit = 8;
            input.contentType = TMP_InputField.ContentType.Alphanumeric;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;

            TextMeshProUGUI text = AddText(rect, "Text", Vector2.zero, size - new Vector2(28f, 8f), 34f, TextAlignmentOptions.Center, Color.black);
            input.textViewport = rect;
            input.textComponent = text;
            input.onValueChanged.AddListener(value =>
            {
                string normalized = NormalizeRoomCode(value);
                if (!string.Equals(value, normalized, StringComparison.Ordinal))
                    input.SetTextWithoutNotify(normalized);
                owner.roomCodeInput = normalized;
            });
            return input;
        }

        private Image AddImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(AntialiasedMenuImage));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private TextMeshProUGUI AddText(string name, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            return AddText(contentRoot, name, position, size, fontSize, alignment, color);
        }

        private TextMeshProUGUI AddText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = ChessFontCatalog.TmpFont != null ? ChessFontCatalog.TmpFont : TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontSizeMin = Mathf.Max(13f, fontSize * 0.58f);
            text.fontSizeMax = fontSize;
            text.enableAutoSizing = true;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private RectTransform CreateChild(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private Sprite LoadSprite(params string[] names)
        {
            string folder = currentMode == NetworkLobbyUiMode.Lan
                ? "Assets/Materials/LANUI"
                : "Assets/Materials/MultiplayerUI";

            for (int i = 0; i < names.Length; i++)
            {
                string key = $"{folder}/{names[i]}";
                if (sprites.TryGetValue(key, out Sprite cached))
                    return cached;

                string fullPath = Path.Combine(Directory.GetCurrentDirectory(), folder, names[i]);
                if (CoreArtworkCache.GetSprite(key, ShouldTrimSprite(names[i])) is Sprite prepared)
                    return prepared;
                if (!File.Exists(fullPath))
                    continue;

                byte[] bytes = File.ReadAllBytes(fullPath);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!texture.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    continue;
                }

                texture.name = Path.GetFileNameWithoutExtension(names[i]);
                Rect spriteRect = ShouldTrimSprite(names[i])
                    ? MenuArtworkBounds.GetRect(key, texture)
                    : new Rect(0f, 0f, texture.width, texture.height);
                Sprite sprite = Sprite.Create(texture, spriteRect, new Vector2(0.5f, 0.5f), 100f);
                sprite.name = texture.name;
                MenuTextureSampling.FinishRuntimeTexture(texture);
                runtimeAssets.Add(texture);
                runtimeAssets.Add(sprite);
                sprites[key] = sprite;
                return sprite;
            }

            return null;
        }

        private static bool ShouldTrimSprite(string spriteName)
        {
            return !spriteName.EndsWith("Blank.png", StringComparison.OrdinalIgnoreCase) &&
                   !spriteName.EndsWith("Design.png", StringComparison.OrdinalIgnoreCase);
        }

        private static Vector2 D(float x, float y)
        {
            return new Vector2(x - DesignWidth * 0.5f, DesignHeight * 0.5f - y);
        }

        private static Color GetServerColor(string health)
        {
            if (string.Equals(health, "Online", StringComparison.OrdinalIgnoreCase))
                return new Color(0.06f, 0.45f, 0.16f, 1f);
            if (string.Equals(health, "Maintenance", StringComparison.OrdinalIgnoreCase))
                return new Color(0.85f, 0.48f, 0.04f, 1f);
            if (string.Equals(health, "Checking", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(health, "Reconnecting", StringComparison.OrdinalIgnoreCase))
                return new Color(0.1f, 0.28f, 0.78f, 1f);
            return new Color(0.72f, 0.08f, 0.08f, 1f);
        }
    }

    private static GUIStyle GetTitleStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold
        };
    }

    private static GUIStyle GetSectionStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
    }

    private static GUIStyle GetBodyStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            wordWrap = true
        };
    }

    private static GUIStyle GetHudStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
    }

    private static GUIStyle GetStatusStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Italic,
            wordWrap = true
        };
    }

    private static float GetGuiScale()
    {
        return ResponsiveUi.GetFitScale(ReferenceWidth, ReferenceHeight);
    }

    private static Texture2D LoadProjectTexture(string projectRelativePath)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath);
        if (!File.Exists(fullPath))
            return null;

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        if (!texture.LoadImage(bytes))
        {
            UnityEngine.Object.Destroy(texture);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(projectRelativePath);
        MenuTextureSampling.FinishRuntimeTexture(texture);
        return texture;
    }
}
