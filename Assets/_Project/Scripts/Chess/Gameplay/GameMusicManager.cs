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
    Default = 0,
    // Preserve the original ID: removed packs must not become Kingdom Rush
    // when an older saved selection contains its numeric enum value.
    KingdomRush = 5
}

public enum GameMusicContext
{
    Auth,
    MainMenuPrimary,
    MainMenuHub,
    Gacha,
    InGame,
    Result,
    Shop
}

public sealed class GameMusicManager : MonoBehaviour
{
    private const string SaveKey = "chess_but_weird_music_pack";
    private const float MenuVolume = 0.72f;
    private const float InGameVolume = 0.44f;
    private const float TransitionFadeSeconds = 0.45f;
    private const float EndFadeSeconds = 1.5f;
    private const string MusicRoot = "Assets/_Project/Audio/MusicPackage";
    private const string DefaultFolder = "DefaultMusic";
    private const string KingdomRushFolder = "KingdomRushCollab";

    private static GameMusicManager instance;

    private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> playlistIndices = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> lastRandomTracks = new Dictionary<string, string>(StringComparer.Ordinal);
    private AudioSource source;
    private Coroutine transitionCoroutine;
    private Coroutine playbackCoroutine;
    private MusicRequest currentRequest;
    private MusicRequest lastRequestedRequest;
    private MusicRequest volumeRequest;
    private GameMusicPack activePack = GameMusicPack.Default;
    private float playbackEnvelope;
    private bool playbackCompleted;
    private bool initialized;

    public static GameMusicPack ActivePack => Ensure().activePack;

    public static void SetActivePack(GameMusicPack pack)
    {
        if (!Enum.IsDefined(typeof(GameMusicPack), pack))
            return;
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

        if (string.Equals(packId, "kingdom_rush", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(packId, "kingdomrush", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(packId, "kingdomrushcollab", StringComparison.OrdinalIgnoreCase))
        {
            pack = GameMusicPack.KingdomRush;
            return true;
        }

        pack = GameMusicPack.Default;
        return false;
    }

    public static string GetPackDisplayName(GameMusicPack pack)
    {
        switch (pack)
        {
            case GameMusicPack.KingdomRush:
                return "Kingdom Rush";
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

    public static void PlayShopMusic()
    {
        Ensure().Play(MusicRequest.For(GameMusicContext.Shop));
    }

    public static void PlayInGameMusic(bool isBotGame, StockfishDifficulty difficulty, bool isOnline = false, bool isAram = false)
    {
        Ensure().Play(MusicRequest.ForInGame(isBotGame, difficulty, isOnline, isAram));
    }

    public static void PlayResultMusic(ResultMenuView.ResultKind resultKind)
    {
        Ensure().Play(MusicRequest.ForResult(resultKind));
    }

    public static void RefreshVolumeFromSettings()
    {
        GameMusicManager manager = Ensure();
        if (!manager.source)
            return;

        manager.ApplySourceVolume();
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
        if (initialized)
            return;
        initialized = true;
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
        if (Enum.TryParse(saved, true, out GameMusicPack pack) && Enum.IsDefined(typeof(GameMusicPack), pack))
            return pack;
        PlayerPrefs.SetString(SaveKey, GameMusicPack.Default.ToString());
        PlayerPrefs.Save();
        return GameMusicPack.Default;
    }

    private void ReplayCurrentContext()
    {
        if (string.IsNullOrEmpty(lastRequestedRequest.key))
            return;

        Play(lastRequestedRequest, true);
    }

    private void Play(MusicRequest request, bool forceRestart = false)
    {
        Initialize();
        if (!forceRestart)
            lastRequestedRequest = request;
        // Screens sharing a soundtrack also share playback, including an in-progress fade.
        if (activePack == GameMusicPack.KingdomRush)
        {
            if (request.context == GameMusicContext.MainMenuPrimary)
                request = MusicRequest.For(GameMusicContext.MainMenuHub);
            else if (request.context == GameMusicContext.Shop)
                request = MusicRequest.For(GameMusicContext.Gacha);
        }
        else if (request.context == GameMusicContext.MainMenuPrimary)
            request = MusicRequest.For(GameMusicContext.Auth);
        else if (request.context == GameMusicContext.Shop)
            request = MusicRequest.For(GameMusicContext.MainMenuHub);

        if (!forceRestart && request.Equals(currentRequest))
        {
            currentRequest = request;
            if (playbackCompleted)
                return;
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
        playbackCompleted = false;

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }
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
            yield return FadeSource(playbackEnvelope, 0f, TransitionFadeSeconds);

        source.Stop();
        playbackEnvelope = 0f;
        ApplySourceVolume();

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
                source.clip = null;
                transitionCoroutine = null;
                yield break;
            }
        }

        source.Stop();
        source.clip = clip;
        volumeRequest = request;
        source.loop = false;
        source.volume = 0f;
        source.Play();
        Debug.Log($"[GameMusic] Playing pack={activePack}, context={request.context}, clip=\"{track.fileName}\"");

        yield return FadeSource(0f, 1f, TransitionFadeSeconds);
        transitionCoroutine = null;
        playbackCoroutine = StartCoroutine(WatchClipEnd(request));
    }

    private IEnumerator WatchClipEnd(MusicRequest request)
    {
        while (request.Equals(currentRequest))
        {
            if (!source || !source.clip)
            {
                playbackCoroutine = null;
                yield break;
            }

            if (AudioListener.pause)
            {
                yield return null;
                continue;
            }

            // A short clip or a stalled frame can pass its end before the fade starts.
            if (!source.isPlaying)
                break;

            if (source.clip.length <= EndFadeSeconds)
                break;

            float remaining = source.clip.length - source.time;
            if (remaining <= EndFadeSeconds)
                break;

            ApplySourceVolume();
            yield return null;
        }

        if (!source || !source.clip)
        {
            playbackCoroutine = null;
            yield break;
        }

        if (source.isPlaying)
            yield return FadeSource(playbackEnvelope, 0f, Mathf.Min(EndFadeSeconds, source.clip.length - source.time));
        if (!request.Equals(currentRequest))
        {
            playbackCoroutine = null;
            yield break;
        }

        playbackCoroutine = null;
        if (ShouldStopAfterClip(request))
        {
            playbackCompleted = true;
            source.Stop();
            source.clip = null;
            yield break;
        }

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
        if (!source || !source.clip || string.IsNullOrEmpty(request.key) || playbackCompleted ||
            transitionCoroutine != null || playbackCoroutine != null)
            return;

        ApplySourceVolume();
        if (!source.isPlaying)
            source.UnPause();
    }

    private void ApplySourceVolume()
    {
        if (source)
            source.volume = playbackEnvelope * GetTargetVolume(volumeRequest);
    }

    private static float GetTargetVolume(MusicRequest request)
    {
        float contextVolume = request.context == GameMusicContext.InGame ? InGameVolume : MenuVolume;
        return contextVolume * GameRuntimeSettings.MusicVolume01;
    }

    private static bool ShouldStopAfterClip(MusicRequest request)
    {
        return request.context == GameMusicContext.Result &&
            (request.resultKind == ResultMenuView.ResultKind.Win ||
             request.resultKind == ResultMenuView.ResultKind.Lose);
    }

    private IEnumerator FadeSource(float from, float to, float duration)
    {
        if (!source)
            yield break;

        if (duration <= 0f)
        {
            playbackEnvelope = to;
            ApplySourceVolume();
            yield break;
        }

        float startedAt = Time.unscaledTime;
        while (Time.unscaledTime - startedAt < duration)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
            playbackEnvelope = Mathf.Lerp(from, to, t);
            ApplySourceVolume();
            yield return null;
        }

        playbackEnvelope = to;
        ApplySourceVolume();
    }

    private MusicTrack PickTrack(MusicRequest request)
    {
        MusicTrack[] playlist = GetPlaylist(request);
        if (playlist == null || playlist.Length == 0)
            return MusicTrack.Default("CBW_MainMenuTheme1.mp3");

        if (playlist.Length == 1)
            return playlist[0];

        if (request.sequential || activePack == GameMusicPack.KingdomRush)
        {
            string key = activePack == GameMusicPack.KingdomRush ? "KingdomRush:Forest" : $"{activePack}:{request.key}";
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
        // CBW keeps its original shop (hub menu) soundtrack.
        if (request.context == GameMusicContext.Shop && activePack != GameMusicPack.KingdomRush)
            request = MusicRequest.For(GameMusicContext.MainMenuHub);
        switch (activePack)
        {
            case GameMusicPack.KingdomRush:
                return GetKingdomRushPlaylist(request);
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

    private static MusicTrack[] One(MusicTrack track)
    {
        return new[] { track };
    }

    private MusicTrack[] GetKingdomRushPlaylist(MusicRequest request)
    {
        switch (request.context)
        {
            case GameMusicContext.Auth:
                return One(MusicTrack.KingdomRush("Kingdom Rush OST - Main Menu Theme.mp3"));
            case GameMusicContext.MainMenuPrimary:
            case GameMusicContext.MainMenuHub:
                return One(MusicTrack.KingdomRush("Kingdom Rush OST - Map Theme.mp3"));
            case GameMusicContext.Shop:
            case GameMusicContext.Gacha:
                return One(MusicTrack.KingdomRush("Kingdom Rush UNUSED OST - Theme of Linirea.mp3"));
            case GameMusicContext.InGame:
                if (request.isAram)
                    return One(MusicTrack.KingdomRush("Kingdom Rush OST - The Dark Tower.mp3"));
                if (request.isOnline)
                    return One(MusicTrack.KingdomRush("Kingdom Rush OST - Wasteland Battle.mp3"));
                if (request.isBotGame)
                {
                    int level = StockfishDifficultyProfiles.Get(request.difficulty).Level;
                    int highestLevel = 0;
                    foreach (var profile in StockfishDifficultyProfiles.All)
                        highestLevel = Math.Max(highestLevel, profile.Level);
                    if (level == highestLevel)
                        return One(MusicTrack.KingdomRush("Kingdom Rush OST - Boss Battle.mp3"));
                    if (level > 8)
                        return One(MusicTrack.KingdomRush("Kingdom Rush OST - Wasteland Battle.mp3"));
                }
                return new[]
                {
                    MusicTrack.KingdomRush("Kingdom Rush OST - Forest BattleI.mp3"),
                    MusicTrack.KingdomRush("Kingdom Rush OST - Forest BattleII.mp3")
                };
            default:
                return GetDefaultPlaylist(request);
        }
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

        // Serialized references keep the new album available in standalone builds.
        if (track.folder == KingdomRushFolder || track.folder == DefaultFolder)
        {
            GameMusicAlbum album = Resources.Load<GameMusicAlbum>("Chess/KingdomRushMusicAlbum");
            AudioClip bundledClip = album ? album.FindClip(track.fileName) : null;
            if (bundledClip)
            {
                clipCache[assetPath] = bundledClip;
                callback(bundledClip);
                yield break;
            }
        }

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

        public static MusicTrack KingdomRush(string fileName)
        {
            return new MusicTrack(KingdomRushFolder, fileName);
        }
    }

    private struct MusicRequest : IEquatable<MusicRequest>
    {
        public GameMusicContext context;
        public bool isBotGame;
        public bool isOnline;
        public bool isAram;
        public StockfishDifficulty difficulty;
        public ResultMenuView.ResultKind resultKind;
        public bool sequential;
        public string key;

        public static MusicRequest For(GameMusicContext context)
        {
            bool isMainMenuHub = context == GameMusicContext.MainMenuHub || context == GameMusicContext.Shop;
            return new MusicRequest
            {
                context = context,
                difficulty = StockfishDifficulty.Medium,
                resultKind = ResultMenuView.ResultKind.Draw,
                sequential = isMainMenuHub,
                key = context.ToString()
            };
        }

        public static MusicRequest ForInGame(bool isBotGame, StockfishDifficulty difficulty, bool isOnline = false, bool isAram = false)
        {
            return new MusicRequest
            {
                context = GameMusicContext.InGame,
                isBotGame = isBotGame,
                isOnline = isOnline,
                isAram = isAram,
                difficulty = difficulty,
                resultKind = ResultMenuView.ResultKind.Draw,
                key = $"{GameMusicContext.InGame}:{isBotGame}:{difficulty}:{isOnline}:{isAram}"
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
                   isOnline == other.isOnline &&
                   isAram == other.isAram &&
                   isBotGame == other.isBotGame &&
                   difficulty == other.difficulty &&
                   resultKind == other.resultKind &&
                   string.Equals(key, other.key, StringComparison.Ordinal);
        }
    }
}
