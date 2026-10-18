using System;
using UnityEngine;

/// <summary>
/// One sound the <see cref="AudioManager"/> can play: an id to look it up by, the clip and
/// its own volume. The same struct is used for music, one-shot SFX and the engine loop.
/// </summary>
[Serializable]
public class SoundCue
{
    [Tooltip("Name used by code to play this sound (e.g. 'shot', 'hit', 'click').")]
    public string id = "sound";

    public AudioClip clip;

    [Tooltip("Volume of this sound (0..1). Multiplied by the channel volume and the master volume.")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("Random pitch spread (0 = none) so repeated one-shots do not sound identical.")]
    [Range(0f, 0.5f)]
    public float pitchSpread = 0f;

    [Tooltip("Play this one-shot even when the global SFX interval has not passed yet.")]
    public bool bypassInterval;
}
