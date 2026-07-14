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
    EpicSeven,
    CounterStrikeGO,
    EyeOfTheDragon
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
    private const float MenuVolume = 0.72f;
    private const float InGameVolume = 0.44f;
    private const float TransitionFadeSeconds = 0.45f;
    private const float EndFadeSeconds = 1.5f;
    private const string MusicRoot = "Assets/Audio/MusicPackage";
    private const string DefaultFolder = "DefaultMusic";
    private const string AngryBirdsFolder = "AngryBirdsCollab";
    private const string EpicSevenFolder = "EpicSevenCollab";
    private const string CsgoFolder = "CSGOcollab";

    private static GameMusicManager instance;

    private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> playlistIndices = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> lastRandomTracks = new Dictionary<string, string>(StringComparer.Ordinal);
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

        if (string.Equals(packId, "csgo", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(packId, "counter_strike_go", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(packId, "counterstrikego", StringComparison.OrdinalIgnoreCase))
        {
            pack = GameMusicPack.CounterStrikeGO;
            return true;
        }

        if (string.Equals(packId, "eye_of_the_dragon", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(packId, "eyeofthedragon", StringComparison.OrdinalIgnoreCase))
        {
            pack = GameMusicPack.EyeOfTheDragon;
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
            case GameMusicPack.CounterStrikeGO:
                return "CSGO Main Theme";
            case GameMusicPack.EyeOfTheDragon:
                return "Eye of the Dragon";
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
        if (!forceRestart && request.Equals(currentRequest))
        {
            currentRequest = request;
            if (IsPlaybackBusy)
            {
                ResumeCurrentClipIfNeeded(request);
                return;
            }

            if (source && source.clip)
            {
                ResumeCurrentClipIfNeeded(request);
                playbackCoroutine = StartCoroutine(WatchClipEnd(request));
                return;
            }
        }

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
        float targetVolume = GetTargetVolume(request);
        if (source && source.isPlaying)
            yield return FadeSource(source.volume, 0f, TransitionFadeSeconds);

        MusicTrack track = PickTrack(request);
        AudioClip clip = null;
        yield return LoadClip(track, loaded => clip = loaded);

        if (!clip)
        {
            Debug.LogWarning($"[GameMusic] Missing music clip: {track.AssetPath}");
            if (TryGetFallbackTrack(request, track, out MusicTrack fallbackTrack))
            {
                Debug.LogWarning($"[GameMusic] Falling back to default music clip: {fallbackTrack.AssetPath}");
                track = fallbackTrack;
                yield return LoadClip(track, loaded => clip = loaded);
            }

            if (!clip)
            {
                transitionCoroutine = null;
                yield break;
            }
        }

        source.Stop();
        source.clip = clip;
        source.loop = false;
        source.volume = 0f;
        source.Play();
        Debug.Log($"[GameMusic] Playing pack={activePack}, context={request.context}, clip=\"{track.fileName}\"");

        yield return FadeSource(0f, targetVolume, TransitionFadeSeconds);
        transitionCoroutine = null;
        playbackCoroutine = StartCoroutine(WatchClipEnd(request));
    }

    private IEnumerator WatchClipEnd(MusicRequest request)
    {
        float targetVolume = GetTargetVolume(request);
        while (request.Equals(currentRequest))
        {
            if (!source || !source.clip)
            {
                playbackCoroutine = null;
                yield break;
            }

            if (!source.isPlaying)
            {
                yield return null;
                continue;
            }

            if (source.clip.length <= EndFadeSeconds)
                break;

            float remaining = source.clip.length - source.time;
            if (remaining <= EndFadeSeconds)
                break;

            if (Mathf.Abs(source.volume - targetVolume) > 0.01f)
                source.volume = Mathf.MoveTowards(source.volume, targetVolume, Time.unscaledDeltaTime);
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

    private bool IsPlaybackBusy => source && (source.isPlaying || transitionCoroutine != null || playbackCoroutine != null);

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ResumeCurrentClipIfNeeded(currentRequest);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
            ResumeCurrentClipIfNeeded(currentRequest);
    }

    private void ResumeCurrentClipIfNeeded(MusicRequest request)
    {
        if (!source || !source.clip || string.IsNullOrEmpty(request.key))
            return;

        source.volume = GetTargetVolume(request);
        if (!source.isPlaying)
            source.Play();
    }

    private static float GetTargetVolume(MusicRequest request)
    {
        return request.context == GameMusicContext.InGame ? InGameVolume : MenuVolume;
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

        return PickRandomTrack(playlist, $"{activePack}:{request.key}");
    }

    private MusicTrack PickRandomTrack(MusicTrack[] playlist, string key)
    {
        if (playlist == null || playlist.Length == 0)
            return MusicTrack.Default("CBW_MainMenuTheme1.mp3");
        if (playlist.Length == 1)
            return playlist[0];

        string lastPath = lastRandomTracks.TryGetValue(key, out string rememberedPath) ? rememberedPath : null;
        MusicTrack track = playlist[UnityEngine.Random.Range(0, playlist.Length)];
        for (int attempt = 0; attempt < 6 && string.Equals(track.AssetPath, lastPath, StringComparison.OrdinalIgnoreCase); attempt++)
            track = playlist[UnityEngine.Random.Range(0, playlist.Length)];

        if (string.Equals(track.AssetPath, lastPath, StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < playlist.Length; i++)
            {
                if (!string.Equals(playlist[i].AssetPath, lastPath, StringComparison.OrdinalIgnoreCase))
                {
                    track = playlist[i];
                    break;
                }
            }
        }

        lastRandomTracks[key] = track.AssetPath;
        return track;
    }

    private MusicTrack[] GetPlaylist(MusicRequest request)
    {
        switch (activePack)
        {
            case GameMusicPack.AngryBirdsEpic:
                return GetAngryBirdsPlaylist(request);
            case GameMusicPack.EpicSeven:
                return GetEpicSevenPlaylist(request);
            case GameMusicPack.CounterStrikeGO:
                return GetCounterStrikePlaylist(request);
            case GameMusicPack.EyeOfTheDragon:
                return GetEyeOfTheDragonPlaylist(request);
            default:
                return GetDefaultPlaylist(request);
        }
    }

    private MusicTrack[] GetDefaultPlaylist(MusicRequest request)
    {
        switch (request.context)
        {
            case GameMusicContext.Auth:
            case GameMusicContext.MainMenuPrimary:
                return One(MusicTrack.Default("CBW_MainMenuTheme1.mp3"));
            case GameMusicContext.MainMenuHub:
                return One(MusicTrack.Default("CBW_MainMenuTheme2.mp3"));
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
                return One(MusicTrack.Default("CBW_MainMenuTheme1.mp3"));
        }
    }

    private bool TryGetFallbackTrack(MusicRequest request, MusicTrack failedTrack, out MusicTrack fallbackTrack)
    {
        fallbackTrack = default;
        if (activePack == GameMusicPack.Default || string.Equals(failedTrack.folder, DefaultFolder, StringComparison.Ordinal))
            return false;

        MusicTrack[] fallbackPlaylist = GetDefaultPlaylist(request);
        if (fallbackPlaylist == null || fallbackPlaylist.Length == 0)
            return false;

        fallbackTrack = fallbackPlaylist[0];
        return true;
    }

    private MusicTrack[] GetAngryBirdsPlaylist(MusicRequest request)
    {
        switch (request.context)
        {
            case GameMusicContext.Auth:
            case GameMusicContext.MainMenuPrimary:
                return One(MusicTrack.AngryBirds("Angry Birds Epic music - Main theme.mp3"));
            case GameMusicContext.MainMenuHub:
                return One(MusicTrack.AngryBirds("Angry Birds Epic music extended - Map of Piggy Island Map 2.mp3"));
            case GameMusicContext.Gacha:
                return One(MusicTrack.AngryBirds("Angry Birds Epic music extended - Camp Ca- Caw.mp3"));
            case GameMusicContext.InGame:
                if (request.isBotGame && request.difficulty == StockfishDifficulty.Expert)
                    return One(MusicTrack.AngryBirds("Angry Birds Epic music extended - King Pig and His Manic Minions Boss battle.mp3"));
                return new[]
                {
                    MusicTrack.AngryBirds("Angry Birds Epic music extended - Battle of Birds and Pigs Battle 2.mp3"),
                    MusicTrack.AngryBirds("Angry Birds Epic music extended - You Call THAT a Stick Battle 1.mp3"),
                    MusicTrack.AngryBirds("Angry Birds Epic music extended - Moar boars! Battle 3.mp3")
                };
            case GameMusicContext.Result:
                if (request.resultKind == ResultMenuView.ResultKind.Win)
                    return One(MusicTrack.AngryBirds("Angry Birds Epic music - win.mp3"));
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
                    return One(MusicTrack.EpicSeven("Epic Seven OST Notos Theme - Osvald.mp3"));
                if (request.isBotGame && request.difficulty >= StockfishDifficulty.Hard)
                {
                    return new[]
                    {
                        MusicTrack.EpicSeven("Epic Seven OST World Arena RTA  Battle Theme 13.mp3"),
                        MusicTrack.EpicSeven("Epic Seven OST World Arena RTA  Battle Theme 12.mp3")
                    };
                }
                return new[]
                {
                    MusicTrack.EpicSeven("Epic Seven OST Salome Theme - Episode 6 Theme 2.mp3"),
                    MusicTrack.EpicSeven("Epic Seven OST Rhianna and Luciella Theme - Episode 6 Theme 6.mp3")
                };
            case GameMusicContext.Result:
                return One(MusicTrack.EpicSeven("Epic Seven OST Battle Results Theme - TheOrang.mp3"));
            default:
                return GetDefaultPlaylist(request);
        }
    }

    private MusicTrack[] GetCounterStrikePlaylist(MusicRequest request)
    {
        switch (request.context)
        {
            case GameMusicContext.Auth:
            case GameMusicContext.MainMenuPrimary:
                return One(MusicTrack.Csgo("Counter-Strike- Global Offensive Main Menu.mp3"));
            case GameMusicContext.MainMenuHub:
                return One(MusicTrack.Csgo("Counter-Strike- Global Offensive main menu 2.mp3"));
            case GameMusicContext.Gacha:
                return new[]
                {
                    MusicTrack.Csgo("Counter-Strike- Global Offensive Gacha.mp3"),
                    MusicTrack.Csgo("Counter-Strike- Global Offensive gacha 2.mp3")
                };
            case GameMusicContext.InGame:
                return GetDefaultPlaylist(request);
            case GameMusicContext.Result:
                if (request.resultKind == ResultMenuView.ResultKind.Win)
                {
                    return new[]
                    {
                        MusicTrack.Csgo("Counter-Strike- Global Offensive Win.mp3"),
                        MusicTrack.Csgo("Counter-Strike- Global Offensive win 2.mp3")
                    };
                }
                if (request.resultKind == ResultMenuView.ResultKind.Lose)
                {
                    return new[]
                    {
                        MusicTrack.Csgo("Counter-Strike- Global Offensive Lose.mp3"),
                        MusicTrack.Csgo("Counter-Strike- Global Offensive lose 2.mp3")
                    };
                }
                return One(MusicTrack.Csgo("Counter-Strike- Global Offensive main menu 2.mp3"));
            default:
                return One(MusicTrack.Csgo("Counter-Strike- Global Offensive main menu 2.mp3"));
        }
    }

    private MusicTrack[] GetEyeOfTheDragonPlaylist(MusicRequest request)
    {
        switch (request.context)
        {
            case GameMusicContext.InGame:
                return GetDefaultPlaylist(request);
            case GameMusicContext.Result:
                if (request.resultKind == ResultMenuView.ResultKind.Win)
                    return One(MusicTrack.Csgo("Eyes of the dragon win.mp3"));
                if (request.resultKind == ResultMenuView.ResultKind.Lose)
                    return One(MusicTrack.Csgo("Eyes of the dragon lose.mp3"));
                return One(MusicTrack.Csgo("Eyes of the dragon main menu.mp3"));
            default:
                return One(MusicTrack.Csgo("Eyes of the dragon main menu.mp3"));
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

        AudioType audioType = GetAudioTypeForPath(fullPath);
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(fullPath).AbsoluteUri, audioType))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[GameMusic] Failed to load audio: {request.error} ({fullPath})");
                callback(null);
                yield break;
            }

            AudioClip clip = null;
            try
            {
                clip = DownloadHandlerAudioClip.GetContent(request);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[GameMusic] Failed to decode audio as {audioType}: {exception.Message} ({fullPath})");
                callback(null);
                yield break;
            }

            if (clip)
            {
                clip.name = Path.GetFileNameWithoutExtension(fullPath);
                clipCache[assetPath] = clip;
            }
            callback(clip);
        }
    }

    private static AudioType GetAudioTypeForPath(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        switch (extension)
        {
            case ".mp3":
                return AudioType.MPEG;
            case ".m4a":
            case ".aac":
                return AudioType.ACC;
            case ".wav":
                return AudioType.WAV;
            case ".ogg":
                return AudioType.OGGVORBIS;
            case ".aif":
            case ".aiff":
                return AudioType.AIFF;
            default:
                return AudioType.UNKNOWN;
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

        public static MusicTrack Csgo(string fileName)
        {
            return new MusicTrack(CsgoFolder, fileName);
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
            bool isMainMenuHub = context == GameMusicContext.MainMenuHub;
            bool isAuthOrPrimaryMenu = context == GameMusicContext.Auth || context == GameMusicContext.MainMenuPrimary;
            return new MusicRequest
            {
                context = context,
                difficulty = StockfishDifficulty.Medium,
                resultKind = ResultMenuView.ResultKind.Draw,
                sequential = isMainMenuHub,
                key = isAuthOrPrimaryMenu ? "AuthMainMenuPrimary" : context.ToString()
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
            return isBotGame == other.isBotGame &&
                   difficulty == other.difficulty &&
                   resultKind == other.resultKind &&
                   string.Equals(key, other.key, StringComparison.Ordinal);
        }
    }
}
