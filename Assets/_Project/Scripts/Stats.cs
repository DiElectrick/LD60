using System;

[Serializable]
public class Stats {
    public int speed = 100;
    public int health = 100;
    public int def = 0;
    public int atack = 10;
    public int regen = 0;

    public int stanChance = 0;

    /// <summary>Copy of these values, used as the starting point when recounting buffs.</summary>
    public Stats Clone() {
        return new Stats {
            speed = speed,
            health = health,
            def = def,
            atack = atack,
            regen = regen,
            stanChance = stanChance,
        };
    }

}
