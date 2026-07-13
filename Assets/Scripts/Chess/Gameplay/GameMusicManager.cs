using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum GameMusicPack
{
    Default,
    AngryBirdsEpic,
    EpicSeven
}

public enum GameMusicContext
{
    Auth,
    MainMenuPrimary,
    MainMenuHub,
    Gacha,
    InGame,
    Result
}

public sealed class GameMusicManager : MonoBehaviour
{
    private const string SaveKey = "chess_but_weird_music_pack";
    private const float TargetVolume = 0.72f;
    private const float TransitionFadeSeconds = 0.45f;
    private const float EndFadeSeconds = 1.5f;
    private const string MusicRoot = "Assets/Audio/MusicPackage";
    private const string DefaultFolder = "DefaultMusic";
    private const string AngryBirdsFolder = "AngryBirdsCollab";
    private const string EpicSevenFolder = "EpicSevenCollab";

    private static GameMusicManager instance;

    private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> playlistIndices = new Dictionary<string, int>(StringComparer.Ordinal);
    private AudioSource source;
    private Coroutine transitionCoroutine;
    private Coroutine playbackCoroutine;
    private MusicRequest currentRequest;
    private GameMusicPack activePack = GameMusicPack.Default;

    public static GameMusicPack ActivePack => Ensure().activePack;

    public static void SetActivePack(GameMusicPack pack)
    {
        GameMusicManager manager = Ensure();
        if (manager.activePack == pack)
            return;

        manager.activePack = pack;
        PlayerPrefs.SetString(SaveKey, pack.ToString());
        PlayerPrefs.Save();
        manager.ReplayCurrentContext();
    }

    public static bool TryParsePackId(string packId, out GameMusicPack pack)
    {
        if (string.Equals(packId, "cbw", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(packId, "default", StringComparison.OrdinalIgnoreCase))
        {
            pack = GameMusicPack.Default;
            return true;
        }

        if (string.Equals(packId, "angry_birds_epic", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(packId, "angrybirdsepic", StringComparison.OrdinalIgnoreCase))
        {
            pack = GameMusicPack.AngryBirdsEpic;
            return true;
        }

        if (string.Equals(packId, "epic_seven", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(packId, "epicseven", StringComparison.OrdinalIgnoreCase))
        {
            pack = GameMusicPack.EpicSeven;
            return true;
        }

        pack = GameMusicPack.Default;
        return false;
    }

    public static string GetPackDisplayName(GameMusicPack pack)
    {
        switch (pack)
        {
            case GameMusicPack.AngryBirdsEpic:
                return "Angry Birds Epic";
            case GameMusicPack.EpicSeven:
                return "Epic Seven";
            default:
                return "CBW Official";
        }
    }

    public static void PlayAuthMusic()
    {
        Ensure().Play(MusicRequest.For(GameMusicContext.Auth));
    }

    public static void PlayMainMenuPrimaryMusic()
    {
        Ensure().Play(MusicRequest.For(GameMusicContext.MainMenuPrimary));
    }

    public static void PlayMainMenuHubMusic()
    {
        Ensure().Play(MusicRequest.For(GameMusicContext.MainMenuHub));
    }

    public static void PlayGachaMusic()
    {
        Ensure().Play(MusicRequest.For(GameMusicContext.Gacha));
    }

    public static void PlayInGameMusic(bool isBotGame, StockfishDifficulty difficulty)
    {
        Ensure().Play(MusicRequest.ForInGame(isBotGame, difficulty));
    }

    public static void PlayResultMusic(ResultMenuView.ResultKind resultKind)
    {
        Ensure().Play(MusicRequest.ForResult(resultKind));
    }

    private static GameMusicManager Ensure()
    {
        if (instance)
            return instance;

        GameObject existing = GameObject.Find("Game Music Manager");
        if (existing)
            instance = existing.GetComponent<GameMusicManager>();

        if (!instance)
        {
            GameObject root = new GameObject("Game Music Manager");
            instance = root.AddComponent<GameMusicManager>();
            DontDestroyOnLoad(root);
        }

        instance.Initialize();
        return instance;
    }

    private void Initialize()
    {
        if (!source)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
        }

        activePack = LoadSavedPack();
    }

    private GameMusicPack LoadSavedPack()
    {
        string saved = PlayerPrefs.GetString(SaveKey, GameMusicPack.Default.ToString());
        if (Enum.TryParse(saved, true, out GameMusicPack pack))
            return pack;
        return GameMusicPack.Default;
    }

    private void ReplayCurrentContext()
    {
        if (currentRequest.context == 0 && string.IsNullOrEmpty(currentRequest.key))
            return;

        Play(currentRequest, true);
    }

    private void Play(MusicRequest request, bool forceRestart = false)
    {
        Initialize();
        if (!forceRestart && request.Equals(currentRequest) && source && source.isPlaying)
            return;

        currentRequest = request;

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);
        if (playbackCoroutine != null)
        {
            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;
        }

        transitionCoroutine = StartCoroutine(SwitchToRequest(request));
    }

    private IEnumerator SwitchToRequest(MusicRequest request)
    {
        if (source && source.isPlaying)
            yield return FadeSource(source.volume, 0f, TransitionFadeSeconds);

        MusicTrack track = PickTrack(request);
        AudioClip clip = null;
        yield return LoadClip(track, loaded => clip = loaded);

        if (!clip)
        {
            Debug.LogWarning($"[GameMusic] Missing music clip: {track.AssetPath}");
            transitionCoroutine = null;
            yield break;
        }

        source.Stop();
        source.clip = clip;
        source.loop = false;
        source.volume = 0f;
        source.Play();
        Debug.Log($"[GameMusic] Playing pack={activePack}, context={request.context}, clip=\"{track.fileName}\"");

        yield return FadeSource(0f, TargetVolume, TransitionFadeSeconds);
        transitionCoroutine = null;
        playbackCoroutine = StartCoroutine(WatchClipEnd(request));
    }

    private IEnumerator WatchClipEnd(MusicRequest request)
    {
        while (source && source.clip && source.isPlaying && source.clip.length > EndFadeSeconds)
        {
            float remaining = source.clip.length - source.time;
            if (remaining <= EndFadeSeconds)
                break;
            yield return null;
        }

        if (!source || !source.clip)
        {
            playbackCoroutine = null;
            yield break;
        }

        yield return FadeSource(source.volume, 0f, EndFadeSeconds);
        if (!request.Equals(currentRequest))
        {
            playbackCoroutine = null;
            yield break;
        }

        playbackCoroutine = null;
        Play(request, true);
    }

    private IEnumerator FadeSource(float from, float to, float duration)
    {
        if (!source)
            yield break;

        if (duration <= 0f)
        {
            source.volume = to;
            yield break;
        }

        float startedAt = Time.unscaledTime;
        while (Time.unscaledTime - startedAt < duration)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
            source.volume = Mathf.Lerp(from, to, t);
            yield return null;
        }

        source.volume = to;
    }

    private MusicTrack PickTrack(MusicRequest request)
    {
        MusicTrack[] playlist = GetPlaylist(request);
        if (playlist == null || playlist.Length == 0)
            return MusicTrack.Default("CBW_MainMenuTheme1.mp3");

        if (playlist.Length == 1)
            return playlist[0];

        if (request.sequential)
        {
            string key = $"{activePack}:{request.key}";
            if (!playlistIndices.TryGetValue(key, out int index))
                index = 0;
            MusicTrack track = playlist[index % playlist.Length];
            playlistIndices[key] = (index + 1) % playlist.Length;
            return track;
        }

        return playlist[UnityEngine.Random.Range(0, playlist.Length)];
    }

    private MusicTrack[] GetPlaylist(MusicRequest request)
    {
        switch (activePack)
        {
            case GameMusicPack.AngryBirdsEpic:
                return GetAngryBirdsPlaylist(request);
            case GameMusicPack.EpicSeven:
                return GetEpicSevenPlaylist(request);
            default:
                return GetDefaultPlaylist(request);
        }
    }

    private MusicTrack[] GetDefaultPlaylist(MusicRequest request)
    {
        switch (request.context)
        {
            case GameMusicContext.Gacha:
                return One(MusicTrack.Default("CBW_GachaMenu.mp3"));
            case GameMusicContext.InGame:
                return One(MusicTrack.Default("CBW_IngameTheme.mp3"));
            case GameMusicContext.Result:
                if (request.resultKind == ResultMenuView.ResultKind.Win)
                    return One(MusicTrack.Default("CBW_WinTheme.mp3"));
                if (request.resultKind == ResultMenuView.ResultKind.Lose)
                    return One(MusicTrack.Default("CBW_LoseTheme.mp3"));
                return One(MusicTrack.Default("CBW_DrawTheme.mp3"));
            default:
                request.sequential = true;
                return new[]
                {
                    MusicTrack.Default("CBW_MainMenuTheme1.mp3"),
                    MusicTrack.Default("CBW_MainMenuTheme2.mp3")
                };
        }
    }

    private MusicTrack[] GetAngryBirdsPlaylist(MusicRequest request)
    {
        switch (request.context)
        {
            case GameMusicContext.Auth:
            case GameMusicContext.MainMenuPrimary:
                return One(MusicTrack.AngryBirds("Angry Birds Epic music - Main theme.m4a"));
            case GameMusicContext.MainMenuHub:
                return One(MusicTrack.AngryBirds("Angry Birds Epic music extended - Map of Piggy Island (Map 2).m4a"));
            case GameMusicContext.Gacha:
                return One(MusicTrack.AngryBirds("Angry Birds Epic music extended - Camp Ca- Caw.m4a"));
            case GameMusicContext.InGame:
                if (request.isBotGame && request.difficulty == StockfishDifficulty.Expert)
                    return One(MusicTrack.AngryBirds("Angry Birds Epic music extended - King Pig and His Manic Minions (Boss battle).m4a"));
                return new[]
                {
                    MusicTrack.AngryBirds("Angry Birds Epic music extended - Battle of Birds and Pigs (Battle 2).m4a"),
                    MusicTrack.AngryBirds("Angry Birds Epic music extended - You Call THAT a Stick (Battle 1).m4a"),
                    MusicTrack.AngryBirds("Angry Birds Epic music extended - Moar boars! (Battle 3).m4a")
                };
            case GameMusicContext.Result:
                if (request.resultKind == ResultMenuView.ResultKind.Win)
                    return One(MusicTrack.AngryBirds("Angry Birds Epic music - win.m4a"));
                if (request.resultKind == ResultMenuView.ResultKind.Lose)
                    return One(MusicTrack.Default("CBW_LoseTheme.mp3"));
                return One(MusicTrack.Default("CBW_DrawTheme.mp3"));
            default:
                return GetDefaultPlaylist(request);
        }
    }

    private MusicTrack[] GetEpicSevenPlaylist(MusicRequest request)
    {
        switch (request.context)
        {
            case GameMusicContext.Auth:
            case GameMusicContext.MainMenuPrimary:
                return One(MusicTrack.EpicSeven("Epic Seven - First Step Towards an Epic History.mp3"));
            case GameMusicContext.MainMenuHub:
                return new[]
                {
                    MusicTrack.EpicSeven("Epic Seven - A Gift from the Heart.mp3"),
                    MusicTrack.EpicSeven("Epic Seven - Love, Remembrance, Eternity.mp3"),
                    MusicTrack.EpicSeven("Epic Seven - The Pub on a Sleepy Afternoon MP3.mp3")
                };
            case GameMusicContext.Gacha:
                return One(MusicTrack.EpicSeven("Epic Seven OST Summon Theme 1 - Osvald.mp3"));
            case GameMusicContext.InGame:
                if (request.isBotGame && request.difficulty == StockfishDifficulty.Expert)
                    return One(MusicTrack.EpicSeven("Epic Seven OST Notos  Theme - Osvald.mp3"));
                if (request.isBotGame && request.difficulty >= StockfishDifficulty.Hard)
                {
                    return new[]
                    {
                        MusicTrack.EpicSeven("Epic Seven OST World Arena (RTA)  Battle Theme 13.m4a"),
                        MusicTrack.EpicSeven("Epic Seven OST World Arena (RTA)  Battle Theme 12.m4a")
                    };
                }
                return new[]
                {
                    MusicTrack.EpicSeven("Epic Seven OST Salome\u00b4s Theme - Episode 6 Theme 2.m4a"),
                    MusicTrack.EpicSeven("Epic Seven OST Rhianna and Luciella\u00b4 Theme - Episode 6 Theme 6.m4a")
                };
            case GameMusicContext.Result:
                return One(MusicTrack.EpicSeven("Epic Seven OST Battle Results Theme - TheOrang.mp3"));
            default:
                return GetDefaultPlaylist(request);
        }
    }

    private static MusicTrack[] One(MusicTrack track)
    {
        return new[] { track };
    }

    private IEnumerator LoadClip(MusicTrack track, Action<AudioClip> callback)
    {
        string assetPath = track.AssetPath;
        if (clipCache.TryGetValue(assetPath, out AudioClip cached) && cached)
        {
            callback(cached);
            yield break;
        }

#if UNITY_EDITOR
        AudioClip editorClip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        if (editorClip)
        {
            clipCache[assetPath] = editorClip;
            callback(editorClip);
            yield break;
        }
#endif

        string fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
        if (!File.Exists(fullPath))
        {
            callback(null);
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(fullPath).AbsoluteUri, AudioType.UNKNOWN))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[GameMusic] Failed to load audio: {request.error} ({fullPath})");
                callback(null);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip)
            {
                clip.name = Path.GetFileNameWithoutExtension(fullPath);
                clipCache[assetPath] = clip;
            }
            callback(clip);
        }
    }

    private readonly struct MusicTrack
    {
        public readonly string folder;
        public readonly string fileName;

        private MusicTrack(string folder, string fileName)
        {
            this.folder = folder;
            this.fileName = fileName;
        }

        public string AssetPath => Path.Combine(MusicRoot, folder, fileName).Replace('\\', '/');

        public static MusicTrack Default(string fileName)
        {
            return new MusicTrack(DefaultFolder, fileName);
        }

        public static MusicTrack AngryBirds(string fileName)
        {
            return new MusicTrack(AngryBirdsFolder, fileName);
        }

        public static MusicTrack EpicSeven(string fileName)
        {
            return new MusicTrack(EpicSevenFolder, fileName);
        }
    }

    private struct MusicRequest : IEquatable<MusicRequest>
    {
        public GameMusicContext context;
        public bool isBotGame;
        public StockfishDifficulty difficulty;
        public ResultMenuView.ResultKind resultKind;
        public bool sequential;
        public string key;

        public static MusicRequest For(GameMusicContext context)
        {
            bool isMainMenu = context == GameMusicContext.MainMenuPrimary || context == GameMusicContext.MainMenuHub;
            return new MusicRequest
            {
                context = context,
                difficulty = StockfishDifficulty.Medium,
                resultKind = ResultMenuView.ResultKind.Draw,
                sequential = isMainMenu,
                key = isMainMenu ? "MainMenu" : context.ToString()
            };
        }

        public static MusicRequest ForInGame(bool isBotGame, StockfishDifficulty difficulty)
        {
            return new MusicRequest
            {
                context = GameMusicContext.InGame,
                isBotGame = isBotGame,
                difficulty = difficulty,
                resultKind = ResultMenuView.ResultKind.Draw,
                key = $"{GameMusicContext.InGame}:{isBotGame}:{difficulty}"
            };
        }

        public static MusicRequest ForResult(ResultMenuView.ResultKind resultKind)
        {
            return new MusicRequest
            {
                context = GameMusicContext.Result,
                difficulty = StockfishDifficulty.Medium,
                resultKind = resultKind,
                key = $"{GameMusicContext.Result}:{resultKind}"
            };
        }

        public bool Equals(MusicRequest other)
        {
            return context == other.context &&
                   isBotGame == other.isBotGame &&
                   difficulty == other.difficulty &&
                   resultKind == other.resultKind &&
                   string.Equals(key, other.key, StringComparison.Ordinal);
        }
    }
}
