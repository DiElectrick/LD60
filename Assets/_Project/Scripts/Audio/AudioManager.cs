using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central audio: background music, one-shot sound effects and a continuous engine loop.
/// <para>
/// <b>SFX are throttled:</b> only one one-shot may start every <see cref="sfxMinInterval"/>
/// seconds (0.1s by default), so many sounds never fire on the same frame. A cue can opt
/// out of the throttle with <see cref="SoundCue.bypassInterval"/>.
/// </para>
/// <para>
/// <b>The engine is separate:</b> it is a looping source of its own, never delayed and never
/// cut off by SFX. It can follow the world speed automatically (the city is "driving" while
/// the parallax scrolls) or be started/stopped by hand.
/// </para>
/// Every cue has its own <see cref="SoundCue.volume"/>; the channel volumes and a master
/// volume are multiplied on top.
/// </summary>
public class AudioManager : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    [Header("Mix")]
    [Range(0f, 1f)]
    [SerializeField] float masterVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] float musicVolume = 0.5f;

    [Range(0f, 1f)]
    [SerializeField] float sfxVolume = 0.5f;

    [Tooltip("Extra gain applied to the music on top of its channel volume, so the soundtrack " +
             "sits below the sound effects (music * game * mix).")]
    [Range(0f, 1f)]
    [SerializeField] float musicGain = 0.7f;

    [Range(0f, 1f)]
    [SerializeField] float engineVolume = 1f;

    [Header("Music")]
    [Tooltip("Tracks that can be played by id.")]
    [SerializeField] List<SoundCue> music = new();

    [Tooltip("Track played when the game starts (id from the music list). Empty = silence.")]
    [SerializeField] string startMusicId = "driftWestland";

    [Tooltip("Default cross-fade when music starts or stops.")]
    [SerializeField] float musicFade = 0.6f;

    [Header("SFX")]
    [Tooltip("One-shot sounds that can be played by id.")]
    [SerializeField] List<SoundCue> sfx = new();

    [Tooltip("Minimum time between two one-shot sounds - keeps them from stacking up.")]
    [Min(0f)]
    [SerializeField] float sfxMinInterval = 0.1f;

    [Tooltip("How many one-shot sounds may overlap.")]
    [Min(1)]
    [SerializeField] int sfxVoices = 8;

    [Tooltip("Log every one-shot that was skipped because of the interval.")]
    [SerializeField] bool logSkippedSfx;

    [Header("Engine")]
    [Tooltip("Loop that plays while the city is driving. Leave the clip empty until the sound exists.")]
    [SerializeField] SoundCue engine = new() { id = "engine" };

    [Tooltip("One-shot played the moment the city pulls away from a standstill (the station closes " +
             "and the world starts to roll again). Leave the clip empty until the sound exists.")]
    [SerializeField] SoundCue engineStart = new() { id = "engineStart" };

    [Tooltip("Start / stop the engine loop automatically together with the world speed.")]
    [SerializeField] bool engineFollowsWorld = true;

    [Tooltip("World speed (fraction of the authored speed) below which the engine turns off.")]
    [Range(0f, 1f)]
    [SerializeField] float engineStopSpeed = 0.05f;

    [Tooltip("Fade used when the engine starts or stops.")]
    [SerializeField] float engineFade = 0.35f;

    AudioSource musicSource;
    AudioSource engineSource;
    AudioSource[] sfxSources;

    int nextSfxVoice;
    float lastSfxTime = float.NegativeInfinity;

    string musicId;
    string engineId;

    float currentMusicScale = 1f;
    float currentEngineScale = 1f;

    Coroutine musicFadeRoutine;
    Coroutine engineFadeRoutine;

    ParallaxSystem parallax;

    public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;
    public bool IsEnginePlaying => engineSource != null && engineSource.isPlaying;

    public string CurrentMusicId => musicId;
    public string CurrentEngineId => engineId;

    /// <summary>Minimum time between two one-shot sounds.</summary>
    public float SfxMinInterval
    {
        get => sfxMinInterval;
        set => sfxMinInterval = Mathf.Max(0f, value);
    }

    // ------------------------------------------------------------------ volumes

    public float MasterVolume
    {
        get => masterVolume;
        set
        {
            masterVolume = Mathf.Clamp01(value);
            ApplyVolumes();
        }
    }

    public float MusicChannelVolume
    {
        get => musicVolume;
        set
        {
            musicVolume = Mathf.Clamp01(value);
            ApplyVolumes();
        }
    }

    public float SfxChannelVolume
    {
        get => sfxVolume;
        set
        {
            sfxVolume = Mathf.Clamp01(value);
            ApplyVolumes();
        }
    }

    public float EngineChannelVolume
    {
        get => engineVolume;
        set
        {
            engineVolume = Mathf.Clamp01(value);
            ApplyVolumes();
        }
    }

    // ------------------------------------------------------------------ lifecycle

    void Awake()
    {
        CreateSources();
    }

    public void Init()
    {
        CreateSources();
        parallax = FindAnyObjectByType<ParallaxSystem>();
        WarnAboutDuplicates(music, "music");
        WarnAboutDuplicates(sfx, "sfx");

        if (!string.IsNullOrEmpty(startMusicId)) PlayMusic(startMusicId);
    }

    void OnDestroy()
    {
        StopAllCoroutines();
    }

    void Update()
    {
        if (!engineFollowsWorld || parallax == null) return;

        float baseSpeed = Mathf.Max(0.0001f, Mathf.Abs(parallax.baseSpeed));
        bool driving = Mathf.Abs(parallax.speedMultiplier) > baseSpeed * engineStopSpeed;

        if (driving)
        {
            if (!IsEnginePlaying) StartEngine();
        }
        else if (IsEnginePlaying)
        {
            StopEngine();
        }
    }

    void CreateSources()
    {
        if (musicSource == null) musicSource = AddSource(true);
        if (engineSource == null) engineSource = AddSource(true);

        if (sfxSources != null && sfxSources.Length == Mathf.Max(1, sfxVoices)) return;

        sfxSources = new AudioSource[Mathf.Max(1, sfxVoices)];

        for (int i = 0; i < sfxSources.Length; i++)
        {
            sfxSources[i] = AddSource(false);
        }
    }

    AudioSource AddSource(bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f; // 2D
        return source;
    }

    // ------------------------------------------------------------------ music

    /// <summary>Plays one of the configured tracks by id (cross-fades from the current one).</summary>
    public void PlayMusic(string id, float fade = -1f)
    {
        var cue = FindCue(music, id);
        if (cue == null || cue.clip == null)
        {
            Debug.LogWarning($"[Audio] No music clip for '{id}'");
            return;
        }

        PlayMusicClip(cue.clip, cue.volume, fade);
        musicId = id;
    }

    public void PlayMusicClip(AudioClip clip, float volume = 1f, float fade = -1f)
    {
        if (clip == null || musicSource == null) return;

        if (musicSource.clip == clip && musicSource.isPlaying) return;

        float duration = fade >= 0f ? fade : musicFade;

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = 0f;
        musicSource.Play();

        currentMusicScale = Mathf.Clamp01(volume);
        FadeSource(musicSource, MusicEffectiveVolume(currentMusicScale), duration, false, ref musicFadeRoutine);
    }

    public void StopMusic(float fade = -1f)
    {
        if (musicSource == null || !musicSource.isPlaying) return;

        float duration = fade >= 0f ? fade : musicFade;

        musicId = null;
        FadeSource(musicSource, 0f, duration, true, ref musicFadeRoutine);
    }

    // ------------------------------------------------------------------ one-shot SFX

    /// <summary>Plays a configured one-shot by id. Returns false when it was skipped.</summary>
    public bool PlaySfx(string id)
    {
        var cue = FindCue(sfx, id);
        if (cue == null)
        {
            Debug.LogWarning($"[Audio] No sfx for '{id}'");
            return false;
        }

        if (cue.clip == null) return false;

        return PlaySfxClip(cue.clip, cue.volume, cue.pitchSpread, cue.bypassInterval);
    }

    /// <summary>
    /// Plays a one-shot clip. Skipped when another one-shot started less than
    /// <see cref="sfxMinInterval"/> ago - that is what keeps many sounds from stacking.
    /// </summary>
    public bool PlaySfxClip(AudioClip clip, float volume = 1f, float pitchSpread = 0f, bool bypassInterval = false)
    {
        if (clip == null || sfxSources == null || sfxSources.Length == 0) return false;

        float now = Time.unscaledTime;

        if (!bypassInterval && now - lastSfxTime < sfxMinInterval)
        {
            if (logSkippedSfx) Debug.Log($"[Audio] '{clip.name}' skipped (interval {sfxMinInterval:F2}s)");
            return false;
        }

        var source = sfxSources[nextSfxVoice];
        nextSfxVoice = (nextSfxVoice + 1) % sfxSources.Length;

        source.pitch = pitchSpread > 0f ? 1f + Random.Range(-pitchSpread, pitchSpread) : 1f;
        source.PlayOneShot(clip, EffectiveVolume(sfxVolume, Mathf.Clamp01(volume)));

        lastSfxTime = now;
        return true;
    }

    // ------------------------------------------------------------------ engine loop

    /// <summary>
    /// Starts the engine loop. It runs until <see cref="StopEngine"/> and is never affected
    /// by the SFX interval.
    /// </summary>
    public void StartEngine()
    {
        var clip = engine != null ? engine.clip : null;
        if (clip == null) return; // no engine sound yet

        StartEngineClip(clip, engine.volume);
        engineId = engine.id;
    }

    /// <summary>
    /// The "engine cranks up" one-shot: played when the city pulls away from a station, right
    /// before the world starts to roll (<see cref="BattleCycle"/>). Returns false while the cue
    /// has no clip assigned.
    /// </summary>
    public bool PlayEngineStart()
    {
        var cue = engineStart;
        if (cue == null || cue.clip == null) return false;

        bool played = PlaySfxClip(cue.clip, cue.volume, cue.pitchSpread, cue.bypassInterval);
        if (played) Debug.Log("[Audio] Engine start");

        return played;
    }

    /// <summary>Starts a looping engine clip (or switches to another one).</summary>
    public void StartEngineClip(AudioClip clip, float volume = 1f)
    {
        if (clip == null || engineSource == null) return;

        if (engineSource.isPlaying && engineSource.clip == clip) return;

        engineSource.Stop();
        engineSource.clip = clip;
        engineSource.loop = true;
        engineSource.volume = 0f;
        engineSource.Play();

        currentEngineScale = Mathf.Clamp01(volume);
        FadeSource(engineSource, EffectiveVolume(engineVolume, currentEngineScale), engineFade, false, ref engineFadeRoutine);
    }

    public void StopEngine()
    {
        if (engineSource == null || !engineSource.isPlaying) return;

        engineId = null;
        FadeSource(engineSource, 0f, engineFade, true, ref engineFadeRoutine);
    }

    // ------------------------------------------------------------------ fades

    /// <summary>
    /// Fades a source over <paramref name="duration"/> seconds of unscaled time.
    /// <para>
    /// A plain coroutine instead of a DOTween tween on purpose: audio is started from
    /// Bootstrap.Awake, and a tween created that early never advances here (the music came
    /// up at volume 0 and stayed there).
    /// </para>
    /// </summary>
    void FadeSource(AudioSource source, float target, float duration, bool stopAfter,
        ref Coroutine routine)
    {
        if (routine != null) StopCoroutine(routine);

        routine = StartCoroutine(FadeRoutine(source, target, duration, stopAfter));
    }

    IEnumerator FadeRoutine(AudioSource source, float target, float duration, bool stopAfter)
    {
        if (source == null) yield break;

        float start = source.volume;

        if (duration > 0f)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                if (source == null) yield break;

                source.volume = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
        }

        if (source == null) yield break;

        source.volume = target;

        if (stopAfter) source.Stop();
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Final volume of a cue: master * channel * cue.</summary>
    float EffectiveVolume(float channel, float cueScale)
    {
        return Mathf.Clamp01(masterVolume) * Mathf.Clamp01(channel) * Mathf.Clamp01(cueScale);
    }

    /// <summary>
    /// Final volume of the music: the regular mix times <see cref="musicGain"/>, so the
    /// soundtrack always sits a little below the sound effects.
    /// </summary>
    float MusicEffectiveVolume(float cueScale)
    {
        return EffectiveVolume(musicVolume * musicGain, cueScale);
    }

    /// <summary>Re-applies the volumes to the sources that are playing right now.</summary>
    void ApplyVolumes()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.volume = MusicEffectiveVolume(currentMusicScale);
        }

        if (engineSource != null && engineSource.isPlaying)
        {
            engineSource.volume = EffectiveVolume(engineVolume, currentEngineScale);
        }
    }

    static SoundCue FindCue(List<SoundCue> cues, string id)
    {
        if (cues == null || string.IsNullOrEmpty(id)) return null;

        foreach (var cue in cues)
        {
            if (cue != null && cue.id == id) return cue;
        }

        return null;
    }

    static void WarnAboutDuplicates(List<SoundCue> cues, string channel)
    {
        if (cues == null) return;

        var seen = new HashSet<string>();

        foreach (var cue in cues)
        {
            if (cue == null || string.IsNullOrEmpty(cue.id)) continue;

            if (!seen.Add(cue.id))
            {
                Debug.LogWarning($"[Audio] Duplicate {channel} id '{cue.id}' - the first one wins");
            }
        }
    }
}
