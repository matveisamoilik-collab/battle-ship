using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Global audio: one-shot SFX pool + looping ocean ambience + automatic UI click sounds.
// Self-bootstrapping (no scene wiring, no Build All needed): created before the first scene loads
// and kept alive with DontDestroyOnLoad. Clips are loaded from Assets/Resources/Sounds/<name>.ogg.
// All sources play while Time.timeScale == 0, so the victory/defeat jingles work on the End Panel.
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    const int   PoolSize        = 12;
    const float AmbienceVolume  = 0.25f;
    const float FalloffDistance = 160f;  // PlayAt(): volume reaches the floor at this distance from the listener
    const float FalloffFloor    = 0.3f;  // distant sounds stay audible — the arena is only ±105 units

    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly List<AudioSource> pool = new List<AudioSource>();
    AudioSource ambience;
    int next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("SoundManager");
        DontDestroyOnLoad(go);
        go.AddComponent<SoundManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < PoolSize; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake  = false;
            src.spatialBlend = 0f;
            pool.Add(src);
        }

        ambience = gameObject.AddComponent<AudioSource>();
        ambience.clip        = Clip("ocean_loop");
        ambience.loop        = true;
        ambience.volume      = AmbienceVolume;
        ambience.playOnAwake = false;
        if (ambience.clip != null) ambience.Play();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Every Button in the scene (including inactive panels: shop, map, end panel) gets a click sound.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            b.onClick.AddListener(() => Play("click", 0.8f, 0f));
    }

    AudioClip Clip(string name)
    {
        if (!clips.TryGetValue(name, out var clip))
        {
            clip = Resources.Load<AudioClip>("Sounds/" + name);
            if (clip == null) Debug.LogWarning("SoundManager: missing clip Resources/Sounds/" + name);
            clips[name] = clip;
        }
        return clip;
    }

    void PlayInternal(string name, float volume, float pitchJitter, float pitch)
    {
        var clip = Clip(name);
        if (clip == null) return;

        // Round-robin, preferring an idle source; steals the oldest one if all are busy.
        AudioSource src = null;
        for (int i = 0; i < pool.Count; i++)
        {
            var s = pool[(next + i) % pool.Count];
            if (!s.isPlaying) { src = s; next = (next + i + 1) % pool.Count; break; }
        }
        if (src == null) { src = pool[next]; next = (next + 1) % pool.Count; }

        src.clip   = clip;
        src.volume = Mathf.Clamp01(volume);
        src.pitch  = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
        src.Play();
    }

    // ---- Static API -------------------------------------------------------------------------

    public static void Play(string name, float volume = 1f, float pitchJitter = 0.05f, float pitch = 1f)
    {
        if (Instance != null) Instance.PlayInternal(name, volume, pitchJitter, pitch);
    }

    // Same as Play(), but quieter the farther the world position is from the camera/listener.
    public static void PlayAt(string name, Vector3 position, float volume = 1f, float pitchJitter = 0.05f, float pitch = 1f)
    {
        if (Instance == null) return;
        Transform listener = Camera.main != null ? Camera.main.transform : null;
        if (listener != null)
        {
            float d = Vector3.Distance(listener.position, position);
            volume *= Mathf.Lerp(1f, FalloffFloor, d / FalloffDistance);
        }
        Instance.PlayInternal(name, volume, pitchJitter, pitch);
    }
}
