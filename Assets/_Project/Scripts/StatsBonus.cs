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
}
