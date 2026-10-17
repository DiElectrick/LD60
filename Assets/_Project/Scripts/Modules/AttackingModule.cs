using UnityEngine;

/// <summary>
/// Base class for modules that visibly react when the city attacks (shot, beam, ...).
/// Damage itself is applied once by CityController - modules only play the effect.
/// </summary>
public abstract class AttackingModule : MonoBehaviour
{
    /// <summary>Called by CityController on the city's turn, after the damage was dealt.</summary>
    public abstract void PlayAttack(BaseEnemy target);
}
