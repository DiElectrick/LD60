/// <summary>
/// One of the city's characteristics. Generated modules roll their bonuses per stat.
/// </summary>
public enum StatType
{
    /// <summary>"No explicit stat" - used by <see cref="ModuleData"/> to mean "pick the biggest authored one".</summary>
    None = 0,

    Atack = 1,
    Speed = 2,
    Health = 3,
    Def = 4,
    Regen = 5,
    StanChance = 6,
}

/// <summary>Helpers for iterating and labelling <see cref="StatType"/> values.</summary>
public static class StatTypes
{
    /// <summary>Every real stat (without <see cref="StatType.None"/>), in the order the UI shows them.</summary>
    public static readonly StatType[] All =
    {
        StatType.Atack,
        StatType.Speed,
        StatType.Health,
        StatType.Def,
        StatType.Regen,
        StatType.StanChance,
    };

    public static bool IsReal(this StatType stat)
    {
        return stat != StatType.None;
    }

    /// <summary>Short label used in logs and tooltips (matches the stats panel).</summary>
    public static string ShortLabel(this StatType stat)
    {
        switch (stat)
        {
            case StatType.Atack: return "ATK";
            case StatType.Speed: return "SPD";
            case StatType.Health: return "HP";
            case StatType.Def: return "DEF";
            case StatType.Regen: return "REGEN";
            case StatType.StanChance: return "STUN";
            default: return stat.ToString();
        }
    }
}
