using UnityEngine;

/// <summary>
/// Base class for modules that visibly fire when the city attacks (shot, beam, ...).
/// Damage itself is applied once by CityController - modules only play the effect.
/// </summary>
public abstract class AttackingModule : MonoBehaviour
{
    /// <summary>
    /// How long this module needs before its shot can land (aim/wind-up + travel).
    /// CityController takes the longest value so all weapons land together.
    /// </summary>
    public abstract float GetAttackDelay(BaseEnemy target);

    /// <summary>
    /// Plays the attack so that the effect lands after <paramref name="hitDelay"/> seconds.
    /// </summary>
    public abstract void PlayAttack(BaseEnemy target, float hitDelay);
}
