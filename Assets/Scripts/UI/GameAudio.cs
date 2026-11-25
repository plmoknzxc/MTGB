using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System.Collections;

public enum TableSound { Press, Cast, Impact, Victory, Defeat, Draw, Land, Attack, Block }

public sealed class GameAudio : MonoBehaviour
{
    const string Folder = "GameAudio/";
    const string MusicKey = "Audio.MusicVolume", EffectsKey = "Audio.EffectsVolume";
    static GameAudio current;
    public static float MusicVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, .55f));
    public static float EffectsVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsKey, .8f));

    AudioMixer mixer;
    AudioSource music, effects;
    bool ownsMusic, started;
    MenuThemeStyle requestedTheme;
    bool requestedBattle;
    Coroutine pending;
    UnityWebRequest request;
    AudioClip[] cues;
    float[] nextCueTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (current != null || Application.isBatchMode || ReplayExportWorker.IsWorker) return;
        var root = new GameObject("Game Audio");
        DontDestroyOnLoad(root);
        current = root.AddComponent<GameAudio>();
    }

    void Awake()
    {
        mixer = Resources.Load<AudioMixer>(Folder + "Tabletop");
        var musicBus = mixer.FindMatchingGroups("Master/Music")[0];
        music = Source("Theme music", musicBus, true);
        effects = Source("Sound effects", mixer.FindMatchingGroups("Master/Effects")[0]);
        var names = System.Enum.GetNames(typeof(TableSound));
        cues = new AudioClip[names.Length];
        nextCueTime = new float[names.Length];
        for (int i = 0; i < names.Length; i++) cues[i] = Resources.Load<AudioClip>(Folder + names[i]);
        SceneManager.activeSceneChanged += SceneChanged;
        MenuThemeCatalog.Changed += RefreshMusic;
    }

    AudioSource Source(string label, AudioMixerGroup bus, bool music = false)
    {
        var child = new GameObject(label);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.outputAudioMixerGroup = bus;
        if (music)
        {
            source.loop = true;
            source.volume = 0;
        }
        return source;
    }

    void Start()
    {
        ApplyVolume("MusicGain", MusicVolume);
        ApplyVolume("EffectsGain", EffectsVolume);
        started = true;
        RefreshMusic();
    }

    void SceneChanged(Scene previous, Scene scene) => RefreshMusic();

    void RefreshMusic()
    {
        if (!started) return;
        string id = SceneManager.GetActiveScene().name;
        bool battle = id == ForgeBattlefieldController.SceneName ||
            id == ForgeBattlefieldController.FourPlayerSceneName;
        var theme = MenuThemeCatalog.Current;
        if (requestedTheme == theme && requestedBattle == battle) return;
        CancelLoad();
        StopMusic();
        requestedTheme = theme;
        requestedBattle = battle;
        string key = battle ? theme.BattleMusic : theme.MenuMusic;
        string fallback = battle ? MenuThemeCatalog.Default.BattleMusic : MenuThemeCatalog.Default.MenuMusic;
        if (key != null && key.StartsWith("./",System.StringComparison.Ordinal))
        {
            var uri = theme.Package?.MusicUri(key);
            if (uri != null) { pending = StartCoroutine(LoadTrack(uri, fallback)); return; }
        }
        var clip = string.IsNullOrEmpty(key) || key.StartsWith("./",System.StringComparison.Ordinal) ? null : theme.Load<AudioClip>(key);
        StartTrack(clip != null ? clip : Resources.Load<AudioClip>(fallback), false);
    }

    IEnumerator LoadTrack(string uri, string fallback)
    {
        request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS);
        ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio = true;
        yield return request.SendWebRequest();
        AudioClip clip = request.result == UnityWebRequest.Result.Success ? DownloadHandlerAudioClip.GetContent(request) : null;
        if (clip != null) clip.name = System.IO.Path.GetFileNameWithoutExtension(new System.Uri(uri).LocalPath);
        else Debug.LogWarning("主题音乐无法读取，已使用月夜配乐：" + request.error);
        request.Dispose(); request = null; pending = null;
        StartTrack(clip != null ? clip : Resources.Load<AudioClip>(fallback), clip != null);
    }

    void CancelLoad()
    {
        if (pending != null) { StopCoroutine(pending); pending = null; }
        if (request != null) { request.Abort(); request.Dispose(); request = null; }
    }

    void StartTrack(AudioClip clip, bool owned)
    {
        if (clip == null) return;
        music.clip = clip;
        ownsMusic = owned;
        music.Play();
    }

    void StopMusic()
    {
        if (music == null) return;
        var clip = music.clip;
        music.Stop();
        music.clip = null;
        music.volume = 0;
        if (ownsMusic && clip != null) Destroy(clip);
        ownsMusic = false;
    }

    void Update()
    {
        if (music.clip != null && music.volume < 1)
            music.volume = Mathf.MoveTowards(music.volume, 1, Time.unscaledDeltaTime / .22f);
    }

    public static void SetVolume(bool music, float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(music ? MusicKey : EffectsKey, value);
        if (current != null) current.ApplyVolume(music ? "MusicGain" : "EffectsGain", value);
    }

    void ApplyVolume(string parameter, float gain)
    {
        mixer.SetFloat(parameter, gain > .0001f ? 20 * Mathf.Log10(gain) : -80);
        if (parameter == "MusicGain") music.mute = gain == 0;
        else effects.mute = gain == 0;
    }

    public static void Play(TableSound cue)
    {
        if (current == null || current.effects.mute) return;
        int i = (int)cue;
        float now = Time.unscaledTime;
        if (now < current.nextCueTime[i]) return;
        current.nextCueTime[i] = now + .085f;
        current.effects.PlayOneShot(current.cues[i]);
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= SceneChanged;
        MenuThemeCatalog.Changed -= RefreshMusic;
        CancelLoad();
        StopMusic();
        if (current == this) current = null;
    }
}
