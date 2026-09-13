using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ResultMenuView : MonoBehaviour
{
    public enum ResultKind
    {
        Win,
        Lose,
        Draw,
        Stalemate
    }

    private const string AssetFolder = "Assets/Materials/Result_Menu";
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    private static readonly Vector2 PanelSize = new Vector2(760f, 950f);

    private readonly List<UnityEngine.Object> runtimeAssets = new List<UnityEngine.Object>();
    private ChessTurnSelectionUI owner;
    private ChessPauseMenu pauseMenu;
    private GameObject canvasRoot;
    private RectTransform safeArea;
    private RectTransform contentRoot;

    public bool IsReady => canvasRoot != null;

    public void Initialize(ChessTurnSelectionUI newOwner, ChessPauseMenu newPauseMenu, ChessGame chessGame)
    {
        owner = newOwner;
        pauseMenu = newPauseMenu;

        canvasRoot = new GameObject("Result Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(chessGame ? chessGame.transform : null, false);

        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 2000;

        ResponsiveUi.ConfigureCanvasScaler(canvasRoot.GetComponent<CanvasScaler>(), ReferenceResolution);

        RectTransform canvasRect = canvasRoot.transform as RectTransform;
        Stretch(canvasRect);

        Image dim = CreateImage(canvasRect, "Result Dim", null, Vector2.zero, Vector2.zero);
        dim.color = new Color(0f, 0f, 0f, 0.58f);
        dim.preserveAspect = false;
        Stretch(dim.rectTransform);

        safeArea = CreateRect(canvasRect, "Result Safe Area");
        Stretch(safeArea);
        safeArea.gameObject.AddComponent<ResponsiveSafeArea>();

        EnsureEventSystem();
        canvasRoot.SetActive(false);
    }

    public void Show(ResultKind kind)
    {
        if (!IsReady)
            return;

        GameMusicManager.PlayResultMusic(kind);
        pauseMenu?.SetResultSpectating(false);
        Rebuild(kind);
        canvasRoot.SetActive(true);
    }

    public void Hide()
    {
        if (canvasRoot)
            canvasRoot.SetActive(false);
    }

    private void Rebuild(ResultKind kind)
    {
        if (contentRoot)
            Destroy(contentRoot.gameObject);
        ClearRuntimeAssets();

        contentRoot = CreateRect(safeArea, $"{kind} Result");
        Stretch(contentRoot);

        ResultProfile profile = ResultProfile.For(kind);
        Sprite blank = LoadSprite(profile.blankFile, false);
        Sprite decoration = LoadSprite(profile.decorationFile, true);
        Sprite spectate = LoadSprite(profile.spectateFile, true);
        Sprite newGame = LoadSprite(profile.newGameFile, true);
        Sprite mainMenu = LoadSprite(profile.mainMenuFile, true);

        Image panel = CreateImage(contentRoot, "Result Panel", blank, Vector2.zero, PanelSize);
        panel.raycastTarget = false;

        Image decorationImage = CreateImage(contentRoot, "Result Decoration", decoration, profile.decorationPosition, profile.decorationSize);
        decorationImage.raycastTarget = false;
        HandDrawnIdleWiggle wiggle = decorationImage.gameObject.AddComponent<HandDrawnIdleWiggle>();
        wiggle.Configure(1.6f, 0.55f, 0.008f, 0.12f);

        CreateButton(contentRoot, "Spectated Game", spectate, profile.spectatePosition, profile.buttonSize, owner.SpectateFinishedGame);
        CreateButton(contentRoot, "New Game", newGame, profile.newGamePosition, profile.buttonSize, owner.StartNewGameFromResult);
        CreateButton(contentRoot, "Main Menu", mainMenu, profile.mainMenuPosition, profile.buttonSize, owner.ReturnToMainMenuFromResult);
    }

    private Button CreateButton(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Action action)
    {
        Image image = CreateImage(parent, name, sprite, position, size);
        image.raycastTarget = true;

        Button button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        if (action != null)
            button.onClick.AddListener(() => action());

        HandDrawnPressable pressable = image.gameObject.AddComponent<HandDrawnPressable>();
        pressable.Configure(1.035f, 0.965f, 0.45f, new Color(1f, 0.97f, 0.88f, 1f));
        return button;
    }

    private Sprite LoadSprite(string fileName, bool trimTransparency)
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), AssetFolder, fileName);
        if (CoreArtworkCache.GetSprite(path) is Sprite prepared) return prepared;
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[ResultMenu] Missing result asset: {fileName}");
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = Path.GetFileNameWithoutExtension(fileName),
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        if (!texture.LoadImage(File.ReadAllBytes(path)))
        {
            Destroy(texture);
            return null;
        }

        Rect sourceRect = trimTransparency ? FindVisibleRect(texture) : new Rect(0f, 0f, texture.width, texture.height);
        Sprite sprite = Sprite.Create(texture, sourceRect, new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
        texture.Apply(false, true);
        runtimeAssets.Add(sprite);
        runtimeAssets.Add(texture);
        return sprite;
    }

    private static Rect FindVisibleRect(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        int minX = texture.width;
        int minY = texture.height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < texture.height; y++)
        {
            int row = y * texture.width;
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[row + x].a <= 8)
                    continue;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
            return new Rect(0f, 0f, texture.width, texture.height);

        const int padding = 2;
        minX = Mathf.Max(0, minX - padding);
        minY = Mathf.Max(0, minY - padding);
        maxX = Mathf.Min(texture.width - 1, maxX + padding);
        maxY = Mathf.Min(texture.height - 1, maxY + padding);
        return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private void ClearRuntimeAssets()
    {
        for (int i = 0; i < runtimeAssets.Count; i++)
            if (runtimeAssets[i])
                Destroy(runtimeAssets[i]);
        runtimeAssets.Clear();
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        RectTransform rect = result.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current)
            return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        EventSystem.current = eventSystem.GetComponent<EventSystem>();
    }

    private void OnDestroy()
    {
        ClearRuntimeAssets();
        if (canvasRoot)
            Destroy(canvasRoot);
    }

    private readonly struct ResultProfile
    {
        public readonly string blankFile;
        public readonly string decorationFile;
        public readonly string spectateFile;
        public readonly string newGameFile;
        public readonly string mainMenuFile;
        public readonly Vector2 decorationPosition;
        public readonly Vector2 decorationSize;
        public readonly Vector2 spectatePosition;
        public readonly Vector2 newGamePosition;
        public readonly Vector2 mainMenuPosition;
        public readonly Vector2 buttonSize;

        private ResultProfile(
            string blank,
            string decoration,
            string spectate,
            string newGame,
            string mainMenu,
            Vector2 decorationPosition,
            Vector2 decorationSize,
            Vector2 spectatePosition,
            Vector2 newGamePosition,
            Vector2 mainMenuPosition)
        {
            blankFile = blank;
            decorationFile = decoration;
            spectateFile = spectate;
            newGameFile = newGame;
            mainMenuFile = mainMenu;
            this.decorationPosition = decorationPosition;
            this.decorationSize = decorationSize;
            this.spectatePosition = spectatePosition;
            this.newGamePosition = newGamePosition;
            this.mainMenuPosition = mainMenuPosition;
            buttonSize = new Vector2(520f, 104f);
        }

        public static ResultProfile For(ResultKind kind)
        {
            switch (kind)
            {
                case ResultKind.Win:
                    return new ResultProfile(
                        "WinUIBlank.png", "WinChampion.png", "SpectatedWin.png", "NewGameWin.png", "MainMenuWin.png",
                        new Vector2(0f, 72f), new Vector2(270f, 225f),
                        new Vector2(0f, -82f), new Vector2(0f, -200f), new Vector2(0f, -320f));
                case ResultKind.Lose:
                    return new ResultProfile(
                        "LoseUIBlank.png", "LoseBroken.png", "SpectatedLose.png", "NewGameLose.png", "MainMenuLose.png",
                        new Vector2(0f, 50f), new Vector2(330f, 245f),
                        new Vector2(0f, -116f), new Vector2(0f, -236f), new Vector2(0f, -352f));
                case ResultKind.Stalemate:
                    return new ResultProfile(
                        "StalemateUIBlank.png", "StalemateKing.png", "SpectatedDraw.png", "NewGameDraw.png", "MainMenuDraw.png",
                        new Vector2(0f, 82f), new Vector2(420f, 255f),
                        new Vector2(0f, -112f), new Vector2(0f, -232f), new Vector2(0f, -350f));
                default:
                    return new ResultProfile(
                        "DrawUIBlank.png", "DrawDraw.png", "SpectatedDraw.png", "NewGameDraw.png", "MainMenuDraw.png",
                        new Vector2(0f, 92f), new Vector2(390f, 240f),
                        new Vector2(0f, -88f), new Vector2(0f, -208f), new Vector2(0f, -328f));
            }
        }
    }
}
