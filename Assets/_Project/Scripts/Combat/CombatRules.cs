using UnityEngine;

/// <summary>
/// Shared rules for the stat effects that aren't plain damage/speed maths.
/// </summary>
public static class CombatRules
{
    /// <summary>Health restored by one regeneration tick (regen is a percent of max health).</summary>
    public static int RegenAmount(int maxHealth, int regenPercent)
    {
        if (maxHealth <= 0 || regenPercent <= 0) return 0;

        return Mathf.RoundToInt(maxHealth * Mathf.Clamp(regenPercent, 0, 100) / 100f);
    }

    /// <summary>Rolls a percent chance (1 = 1%, 100 = always).</summary>
    public static bool RollChance(int percent)
    {
        if (percent <= 0) return false;

        return Random.Range(0, 100) < Mathf.Clamp(percent, 0, 100);
    }
}
