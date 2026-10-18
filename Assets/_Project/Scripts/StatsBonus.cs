using System;

/// <summary>
/// Additive stat deltas provided by a module (or any other buff source).
/// Sum all bonuses, then add them on top of the base stats.
/// </summary>
[Serializable]
public class StatsBonus
{
    public int atack;
    public int speed;
    public int health;
    public int def;
    public int regen;
    public int stanChance;

    public void AddTo(Stats target)
    {
        target.atack += atack;
        target.speed += speed;
        target.health += health;
        target.def += def;
        target.regen += regen;
        target.stanChance += stanChance;
    }

    /// <summary>Value of one stat, so generators can iterate over <see cref="StatTypes.All"/>.</summary>
    public int Get(StatType stat)
    {
        switch (stat)
        {
            case StatType.Atack: return atack;
            case StatType.Speed: return speed;
            case StatType.Health: return health;
            case StatType.Def: return def;
            case StatType.Regen: return regen;
            case StatType.StanChance: return stanChance;
            default: return 0;
        }
    }

    public void Set(StatType stat, int value)
    {
        switch (stat)
        {
            case StatType.Atack: atack = value; break;
            case StatType.Speed: speed = value; break;
            case StatType.Health: health = value; break;
            case StatType.Def: def = value; break;
            case StatType.Regen: regen = value; break;
            case StatType.StanChance: stanChance = value; break;
        }
    }

    public void Add(StatType stat, int delta)
    {
        Set(stat, Get(stat) + delta);
    }
}
