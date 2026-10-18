using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// One rolled affix on a generated module: which stat it buffs, by how much, and
/// whether it is the item's main stat or an extra roll.
/// </summary>
[Serializable]
public struct StatRoll
{
    public StatType stat;
    public int value;
    public bool isMain;

    public StatRoll(StatType stat, int value, bool isMain)
    {
        this.stat = stat;
        this.value = value;
        this.isMain = isMain;
    }

    public string Label => isMain ? "main" : "extra";
}

/// <summary>
/// A concrete module the player owns: the archetype (<see cref="ModuleData"/>) plus the
/// level and the stats rolled for this very copy. Two "Gun" modules can be completely
/// different items - this is what the city places and what the inventory stores.
/// </summary>
[Serializable]
public class ModuleInstance
{
    [SerializeField] ModuleData data;

    [Tooltip("Item level, 1..ModuleGenerationConfig.MaxLevel. Drives the size of the rolls.")]
    [SerializeField] int level = 1;

    [Tooltip("Sum of every rolled affix - this is what the module adds to the city.")]
    [SerializeField] StatsBonus bonus = new();

    [SerializeField] List<StatRoll> rolls = new();

    public ModuleData Data => data;
    public int Level => level;
    public StatsBonus Bonus => bonus;
    public IReadOnlyList<StatRoll> Rolls => rolls;

    /// <summary>Name shown in the UI / hierarchy, e.g. "Gun Lv.7".</summary>
    public string DisplayName => data != null ? $"{data.displayName} Lv.{level}" : $"Module Lv.{level}";

    public Sprite Icon => data != null ? data.Icon : null;

    public ModuleInstance(ModuleData data, int level)
    {
        this.data = data;
        this.level = level;
    }

    /// <summary>Registers one rolled affix (keeps both the roll list and the totals in sync).</summary>
    public void AddRoll(StatType stat, int value, bool isMain)
    {
        if (value == 0) return;

        rolls.Add(new StatRoll(stat, value, isMain));
        bonus.Add(stat, value);
    }

    /// <summary>"Gun Lv.7 (main ATK +9, extra SPD +2)" - used by the generator log.</summary>
    public override string ToString()
    {
        var text = new StringBuilder(DisplayName);
        text.Append(" (");

        for (int i = 0; i < rolls.Count; i++)
        {
            if (i > 0) text.Append(", ");

            text.Append(rolls[i].isMain ? "main " : "extra ");
            text.Append(rolls[i].stat.ShortLabel());
            text.Append(rolls[i].value >= 0 ? " +" : " -");
            text.Append(Mathf.Abs(rolls[i].value));
        }

        text.Append(")");
        return text.ToString();
    }
}
