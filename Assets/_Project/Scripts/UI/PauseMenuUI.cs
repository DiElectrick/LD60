using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Pause menu with the music and SFX volume sliders.
/// <para>
/// The window is a normal Canvas child authored in the scene (opened from a HUD button),
/// so it can be laid out and styled by hand. Opening it freezes the game with
/// <see cref="Time.timeScale"/>; the audio keeps running so the sliders can be heard while
/// they are dragged.
/// </para>
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Pause window in the scene (its root object is switched on while paused).")]
    [SerializeField] GameObject panel;

    [Tooltip("HUD button that opens the pause menu.")]
    [SerializeField] Button pauseButton;

    [Tooltip("Button inside the window that resumes the game.")]
    [SerializeField] Button resumeButton;

    [Header("Volumes")]
    [Tooltip("Slider (0..1) that sets the music channel volume.")]
    [SerializeField] Slider musicSlider;

    [Tooltip("Slider (0..1) that sets the SFX channel volume.")]
    [SerializeField] Slider sfxSlider;

    [Header("Input")]
    [Tooltip("Escape opens / closes the pause menu as well.")]
    [SerializeField] bool escapeKeyToggles = true;

    bool isOpen;

    /// <summary>True while the pause window is up.</summary>
    public bool IsOpen => isOpen;

    // ------------------------------------------------------------------ lifecycle

    void Awake()
    {
        if (pauseButton != null) pauseButton.onClick.AddListener(Open);
        if (resumeButton != null) resumeButton.onClick.AddListener(Close);

        if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

        if (panel != null) panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (pauseButton != null) pauseButton.onClick.RemoveListener(Open);
        if (resumeButton != null) resumeButton.onClick.RemoveListener(Close);

        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);

        // Never leave the next scene frozen.
        if (isOpen) Time.timeScale = 1f;
    }

    void Update()
    {
        if (!escapeKeyToggles) return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Toggle();
    }

    // ------------------------------------------------------------------ pause

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (isOpen) return;

        isOpen = true;

        SyncSliders();

        if (panel != null) panel.SetActive(true);

        Time.timeScale = 0f;

        var audio = Audio();
        if (audio != null) audio.PlaySfx("blipSelect");
    }

    public void Close()
    {
        if (!isOpen) return;

        isOpen = false;

        if (panel != null) panel.SetActive(false);

        Time.timeScale = 1f;

        var audio = Audio();
        if (audio != null) audio.PlaySfx("blipSelect");
    }

    // ------------------------------------------------------------------ volumes

    void SyncSliders()
    {
        var audio = Audio();
        if (audio == null) return;

        if (musicSlider != null) musicSlider.SetValueWithoutNotify(audio.MusicChannelVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(audio.SfxChannelVolume);
    }

    void OnMusicVolumeChanged(float value)
    {
        var audio = Audio();
        if (audio != null) audio.MusicChannelVolume = value;
    }

    void OnSfxVolumeChanged(float value)
    {
        var audio = Audio();
        if (audio != null) audio.SfxChannelVolume = value;
    }

    static AudioManager Audio()
    {
        return G.Get<AudioManager>();
    }
}
