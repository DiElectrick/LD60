using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One row of the level table: everything that scales with the level of a generated
/// module. Ranges are INCLUSIVE, so {x: 2, y: 4} rolls 2, 3 or 4.
/// </summary>
[Serializable]
public class LevelTuning
{
    [Tooltip("The item level this row describes (1..maxLevel).")]
    public int level = 1;

    [Tooltip("Range of the main affix at this level, before the stat's value scale.")]
    public Vector2Int mainRange = new(2, 4);

    [Tooltip("Chance (0..1) that this item also rolls extra affixes.")]
    [Range(0f, 1f)]
    public float extraChance = 0.15f;

    [Tooltip("How many extra affixes are rolled when the chance succeeds.")]
    public Vector2Int extraCount = new(1, 1);

    [Tooltip("Range of every extra affix at this level, before the stat's value scale.")]
    public Vector2Int extraValue = new(1, 2);

    [Tooltip("Relative chance for this level to be rolled. 0 = never.")]
    [Min(0f)]
    public float weight = 1f;

    public int RollMainValue() => UnityEngine.Random.Range(Smaller(mainRange), Bigger(mainRange) + 1);

    public int RollExtraValue() => UnityEngine.Random.Range(Smaller(extraValue), Bigger(extraValue) + 1);

    public int RollExtraCount() => UnityEngine.Random.Range(Smaller(extraCount), Bigger(extraCount) + 1);

    static int Smaller(Vector2Int range) => Mathf.Min(range.x, range.y);

    static int Bigger(Vector2Int range) => Mathf.Max(range.x, range.y);
}

/// <summary>
/// How one stat behaves in the generator: how often it is picked and how big its
/// numbers are compared to the rolled range (a % stat like regen needs a different
/// scale than a big pool like health).
/// </summary>
[Serializable]
public class StatRule
{
    [Tooltip("The stat this rule describes.")]
    public StatType stat = StatType.Atack;

    [Tooltip("Relative chance to be picked as the main stat or as an extra affix.")]
    [Min(0f)]
    public float weight = 1f;

    [Tooltip("Multiplies the rolled value. 1 = raw range, 0.5 = half, 3 = triple. Never rolls below 1.")]
    [Min(0f)]
    public float valueScale = 1f;

    [Tooltip("Extra multiplier for every level above 1: 1.35 = +35% per level. Damage and speed " +
             "grow much faster than flat stats like regen, so a level feels different per stat.")]
    [Min(0.01f)]
    public float levelScale = 1.25f;

    [Tooltip("Can this stat be the main stat of an item?")]
    public bool canBeMain = true;

    [Tooltip("Can this stat drop as an extra affix?")]
    public bool canBeExtra = true;
}

/// <summary>
/// Every knob of the procedural module generator: the level table (main buff range,
/// extra-buff chance and ranges per level) and the per-stat rules.
/// Create one with Assets > Create > LD60 > Module Generation Config and assign it to
/// the ModuleGenerator (or drop it in a Resources folder so it is found automatically).
/// </summary>
[CreateAssetMenu(menuName = "LD60/Module Generation Config", fileName = "ModuleGenerationConfig")]
public class ModuleGenerationConfig : ScriptableObject
{
    public const int DefaultMaxLevel = 3;

    [Header("Levels")]
    [Tooltip("Lowest level an item can roll.")]
    [Min(1)]
    [SerializeField] int minLevel = 1;

    [Tooltip("Highest level an item can roll.")]
    [Min(1)]
    [SerializeField] int maxLevel = DefaultMaxLevel;

    [Tooltip("One row per level. An item rolls its main buff and extra buffs from the row of its level.")]
    [SerializeField] List<LevelTuning> levels = new();

    [Header("Stats")]
    [Tooltip("Which stats can be rolled, how often and how big.")]
    [SerializeField] List<StatRule> stats = new();

    public int MinLevel => Mathf.Max(1, minLevel);
    public int MaxLevel => Mathf.Max(MinLevel, maxLevel);

    public IReadOnlyList<LevelTuning> Levels => levels;
    public IReadOnlyList<StatRule> Stats => stats;

    /// <summary>Row of the level table closest to <paramref name="level"/> (never null).</summary>
    public LevelTuning GetTuning(int level)
    {
        if (levels.Count == 0) return Defaults.Tuning(level);

        LevelTuning best = null;
        int bestDistance = int.MaxValue;

        foreach (var tuning in levels)
        {
            if (tuning == null) continue;

            int distance = Mathf.Abs(tuning.level - level);

            // Closest row wins; on a tie the lower level wins, so an unlisted level
            // never rolls a stronger item than the table says.
            if (best == null || distance < bestDistance ||
                (distance == bestDistance && tuning.level < best.level))
            {
                best = tuning;
                bestDistance = distance;
            }
        }

        return best ?? Defaults.Tuning(level);
    }

    /// <summary>Rule of one stat; a neutral fallback rule when it is missing from the table.</summary>
    public StatRule GetRule(StatType stat)
    {
        foreach (var rule in stats)
        {
            if (rule != null && rule.stat == stat) return rule;
        }

        return Defaults.Rule(stat);
    }

    public bool TryGetRule(StatType stat, out StatRule rule)
    {
        foreach (var existing in stats)
        {
            if (existing != null && existing.stat == stat)
            {
                rule = existing;
                return true;
            }
        }

        rule = null;
        return false;
    }

    /// <summary>Fills the level table (one row per level) and the stat rules with sane values.</summary>
    [ContextMenu("Fill default level table")]
    public void ResetToDefaults()
    {
        minLevel = 1;
        maxLevel = DefaultMaxLevel;

        levels.Clear();
        for (int level = minLevel; level <= maxLevel; level++)
        {
            levels.Add(Defaults.Tuning(level));
        }

        stats.Clear();
        stats.AddRange(Defaults.Rules());

        LogDefaults();
    }

    /// <summary>Unity calls this when the asset is created through the Create menu.</summary>
    void Reset()
    {
        ResetToDefaults();
    }

    void OnValidate()
    {
        maxLevel = Mathf.Max(minLevel, maxLevel);

        var seen = new HashSet<StatType>();
        foreach (var rule in stats)
        {
            if (rule == null) continue;
            if (rule.stat == StatType.None) continue;
            if (!seen.Add(rule.stat))
            {
                Debug.LogWarning($"[Generator] '{name}' has more than one rule for {rule.stat} - the first one wins.", this);
            }
        }
    }

    void LogDefaults()
    {
        Debug.Log($"[Generator] '{name}' filled with {levels.Count} level rows and {stats.Count} stat rules");
    }

    /// <summary>Built-in values used by a fresh config and as a fallback for missing rows.</summary>
    public static class Defaults
    {
        /// <summary>
        /// The shipped balance curve: the raw roll grows a little with the level, the real growth
        /// comes from each stat's own <see cref="StatRule.levelScale"/> (damage and speed scale
        /// much faster than regen), and extras get more likely with the level.
        /// </summary>
        public static LevelTuning Tuning(int level)
        {
            int clamped = Mathf.Clamp(level, 1, DefaultMaxLevel);
            float progress = DefaultMaxLevel > 1 ? (clamped - 1) / (float)(DefaultMaxLevel - 1) : 0f;

            // Raw roll: 3..4 at level 1, 6..8 at level 2, 10..13 at level 3.
            int mainMin = clamped <= 1 ? 3 : (clamped == 2 ? 6 : 10);
            int mainMax = clamped <= 1 ? 4 : (clamped == 2 ? 8 : 13);

            return new LevelTuning
            {
                level = clamped,
                mainRange = new Vector2Int(mainMin, mainMax),
                extraChance = Mathf.Lerp(0.1f, 0.55f, progress),
                extraCount = clamped <= 1 ? new Vector2Int(1, 1)
                    : (clamped == 2 ? new Vector2Int(1, 2) : new Vector2Int(2, 3)),
                extraValue = new Vector2Int(1, 2),
                weight = clamped <= 1 ? 35f : (clamped == 2 ? 40f : 25f),
            };
        }

        public static StatRule Rule(StatType stat)
        {
            switch (stat)
            {
                // Damage and speed are the "carry" stats: they scale hardest with the level.
                case StatType.Atack: return new StatRule { stat = stat, weight = 1f, valueScale = 1f, levelScale = 1.35f, canBeMain = true, canBeExtra = true };
                case StatType.Speed: return new StatRule { stat = stat, weight = 0.9f, valueScale = 0.45f, levelScale = 1.35f, canBeMain = true, canBeExtra = true };
                case StatType.Health: return new StatRule { stat = stat, weight = 0.9f, valueScale = 4f, levelScale = 1.3f, canBeMain = true, canBeExtra = true };
                case StatType.Def: return new StatRule { stat = stat, weight = 0.7f, valueScale = 0.45f, levelScale = 1.3f, canBeMain = true, canBeExtra = true };
                // Regen, stun and scrap stay close to flat: they are utility, not damage.
                case StatType.Regen: return new StatRule { stat = stat, weight = 0.5f, valueScale = 0.25f, levelScale = 1.1f, canBeMain = true, canBeExtra = true };
                case StatType.StanChance: return new StatRule { stat = stat, weight = 0.4f, valueScale = 0.2f, levelScale = 1.15f, canBeMain = true, canBeExtra = true };
                case StatType.Harvest: return new StatRule { stat = stat, weight = 0.35f, valueScale = 0.25f, levelScale = 1.1f, canBeMain = true, canBeExtra = true };
                default: return new StatRule { stat = stat, weight = 0f, valueScale = 1f, levelScale = 1f, canBeMain = false, canBeExtra = false };
            }
        }

        public static List<StatRule> Rules()
        {
            var rules = new List<StatRule>();
            foreach (var stat in StatTypes.All) rules.Add(Rule(stat));
            return rules;
        }
    }
}
