using UnityEngine;

/// <summary>
/// Base class for modules that visibly fire when the city attacks (shot, beam, ...).
/// Damage itself is applied once by CityController - modules only play the effect and their
/// own shot sound.
/// </summary>
public abstract class AttackingModule : MonoBehaviour
{
    [Header("Shot sound")]
    [Tooltip("Clip played the moment this weapon fires (cannon, laser, ...).")]
    [SerializeField] AudioClip shotSound;

    [Range(0f, 1f)]
    [SerializeField] float shotVolume = 0.8f;

    [Tooltip("Random pitch spread, so several guns firing together do not sound identical.")]
    [Range(0f, 0.5f)]
    [SerializeField] float shotPitchSpread = 0.06f;

    /// <summary>
    /// How long this module needs before its shot can land (aim/wind-up + travel).
    /// CityController takes the longest value so all weapons land together.
    /// </summary>
    public abstract float GetAttackDelay(BaseEnemy target);

    /// <summary>
    /// Plays the attack so that the effect lands after <paramref name="hitDelay"/> seconds.
    /// </summary>
    public abstract void PlayAttack(BaseEnemy target, float hitDelay);

    /// <summary>Plays the weapon's own shot sound; nothing happens when no clip is set.</summary>
    protected void PlayShotSound()
    {
        if (shotSound == null) return;

        // bypassInterval: two weapons firing in the same frame must both be heard.
        G.Get<AudioManager>()?.PlaySfxClip(shotSound, shotVolume, shotPitchSpread, true);
    }
}
