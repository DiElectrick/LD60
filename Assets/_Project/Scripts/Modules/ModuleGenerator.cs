using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural module generator. Turns a <see cref="ModuleData"/> archetype into a concrete
/// <see cref="ModuleInstance"/>: it rolls a level, then the main buff of that level's range
/// and - with the configured chance - extra affixes on other stats.
/// <para>
/// Every number comes from a <see cref="ModuleGenerationConfig"/> asset. The config and the
/// archetype pool are loaded from Resources when they are not assigned in the inspector,
/// so the generator works with zero scene setup.
/// </para>
/// </summary>
[DefaultExecutionOrder(-50)]
public class ModuleGenerator : MonoBehaviour, IBootstraped
{
    public const string ConfigResourcePath = "ModuleGenerationConfig";
    public const string ModuleResourceFolder = "Modules";

    public bool isInitialised { get; set; }

    [Tooltip("Level table and per-stat rules. Left empty = loaded from Resources/" + ConfigResourcePath + ".")]
    [SerializeField] ModuleGenerationConfig config;

    [Tooltip("Archetypes picked by GenerateRandom. Left empty = every ModuleData in Resources/" + ModuleResourceFolder + ".")]
    [SerializeField] List<ModuleData> modulePool = new();

    static ModuleGenerator instance;

    readonly List<ModuleData> loadedPool = new();

    /// <summary>The generator of this scene. Creates a hidden one if the scene has none.</summary>
    public static ModuleGenerator Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<ModuleGenerator>(FindObjectsInactive.Include);
            }

            if (instance == null)
            {
                instance = CreateDefault();
            }

            return instance;
        }
    }

    /// <summary>The generation rules; falls back to Resources and then to built-in defaults.</summary>
    public ModuleGenerationConfig Config
    {
        get
        {
            if (config == null) config = LoadConfig();
            return config;
        }
    }

    /// <summary>Archetypes that <see cref="GenerateRandom"/> can pick from.</summary>
    public IReadOnlyList<ModuleData> Pool
    {
        get
        {
            if (modulePool.Count > 0) return modulePool;

            if (loadedPool.Count == 0)
            {
                loadedPool.AddRange(Resources.LoadAll<ModuleData>(ModuleResourceFolder));
                loadedPool.RemoveAll(data => data == null);
                Debug.Log($"[Generator] Loaded {loadedPool.Count} module archetypes from Resources/{ModuleResourceFolder}");

                // The UI icon comes from ModuleData.icon, or from the prefab art when no icon
                // is assigned - stating it once makes a mixed set of icons easy to spot.
                foreach (var data in loadedPool)
                {
                    if (data.icon != null) continue;

                    Debug.Log($"[Generator] '{data.name}' has no icon assigned - its UI icon is the prefab art");
                }
            }

            return loadedPool;
        }
    }

    void Awake()
    {
        // A scene generator wins over a temporary one created by the Instance getter.
        if (instance == null || instance == this) instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void Init()
    {
        // Touch the config at boot so a missing asset is reported once, before the first roll.
        var cfg = Config;
        Debug.Log($"[Generator] Ready: levels {cfg.MinLevel}-{cfg.MaxLevel}, {cfg.Levels.Count} rows, {cfg.Stats.Count} stat rules");
    }

    // ------------------------------------------------------------------ rolls

    /// <summary>Rolls an item level, weighted by the level table (rare high levels).</summary>
    public int RollLevel()
    {
        var cfg = Config;

        float total = 0f;
        foreach (var tuning in cfg.Levels)
        {
            if (IsRollable(tuning, cfg)) total += tuning.weight;
        }

        if (total <= 0f) return Random.Range(cfg.MinLevel, cfg.MaxLevel + 1);

        float pick = Random.value * total;
        foreach (var tuning in cfg.Levels)
        {
            if (!IsRollable(tuning, cfg)) continue;

            pick -= tuning.weight;
            if (pick <= 0f) return tuning.level;
        }

        return cfg.MaxLevel;
    }

    /// <summary>
    /// Rolls one item of the given archetype. Pass <paramref name="level"/> &gt; 0 to force a
    /// level, or 0 to roll one from the level table.
    /// </summary>
    public ModuleInstance Generate(ModuleData data, int level = 0)
    {
        if (data == null)
        {
            Debug.LogWarning("[Generator] Cannot roll an item without a ModuleData archetype");
            return null;
        }

        var cfg = Config;
        int itemLevel = Mathf.Clamp(level > 0 ? level : RollLevel(), cfg.MinLevel, cfg.MaxLevel);
        var tuning = cfg.GetTuning(itemLevel);

        var instance = new ModuleInstance(data, itemLevel);

        // --- main stat: the characteristic this archetype is about ---
        var mainStat = ResolveMainStat(data);
        var mainRule = cfg.GetRule(mainStat);
        int mainValue = Scale(tuning.RollMainValue(), mainRule.valueScale);

        if (mainValue > 0) instance.AddRoll(mainStat, mainValue, true);

        // --- extra affixes: rolled against the level's chance, never on the main stat ---
        if (Random.value < tuning.extraChance)
        {
            var candidates = ExtraCandidates(data, cfg, mainStat);
            int count = tuning.RollExtraCount();

            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                var rule = TakeWeighted(candidates);
                if (rule == null) break;

                int value = Scale(tuning.RollExtraValue(), rule.valueScale);
                instance.AddRoll(rule.stat, value, false);
            }
        }

        Debug.Log($"[Generator] Rolled {instance}");
        return instance;
    }

    /// <summary>Rolls a random archetype from the pool.</summary>
    public ModuleInstance GenerateRandom(int level = 0)
    {
        var pool = Pool;

        var candidates = new List<ModuleData>();
        foreach (var data in pool)
        {
            if (data != null) candidates.Add(data);
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[Generator] No module archetypes available to roll");
            return null;
        }

        var picked = candidates[Random.Range(0, candidates.Count)];
        return Generate(picked, level);
    }

    // ------------------------------------------------------------------ internals

    /// <summary>Main stat of an archetype: explicit on the data, else the biggest authored stat.</summary>
    public static StatType ResolveMainStat(ModuleData data)
    {
        if (data == null) return StatType.None;
        if (data.mainStat.IsReal()) return data.mainStat;

        // No explicit main stat: the archetype's authored bonus says what it is about
        // (a gun buffs attack, an engine buffs speed), so use its biggest stat.
        StatType best = StatType.None;
        int bestValue = 0;

        foreach (var stat in StatTypes.All)
        {
            int value = data.bonus != null ? data.bonus.Get(stat) : 0;
            if (value <= bestValue) continue;

            best = stat;
            bestValue = value;
        }

        return best.IsReal() ? best : StatType.Atack;
    }

    /// <summary>Stats an item of this archetype may roll as extras, weighted by the config.</summary>
    List<StatRule> ExtraCandidates(ModuleData data, ModuleGenerationConfig cfg, StatType mainStat)
    {
        var candidates = new List<StatRule>();

        foreach (var rule in cfg.Stats)
        {
            if (!IsExtraCandidate(rule, data, mainStat)) continue;
            candidates.Add(rule);
        }

        // Stats the archetype allows but the config does not list still get a chance,
        // with the neutral default rule.
        if (data.extraStatPool.Count > 0)
        {
            foreach (var stat in data.extraStatPool)
            {
                if (!stat.IsReal() || stat == mainStat) continue;
                if (ContainsStat(candidates, stat)) continue;

                var fallback = cfg.GetRule(stat);
                if (fallback.canBeExtra && fallback.weight > 0f) candidates.Add(fallback);
            }
        }

        return candidates;
    }

    static bool IsExtraCandidate(StatRule rule, ModuleData data, StatType mainStat)
    {
        if (rule == null || !rule.stat.IsReal() || rule.stat == mainStat) return false;
        if (!rule.canBeExtra || rule.weight <= 0f) return false;

        // An archetype may restrict which "other" stats it can roll.
        if (data.extraStatPool.Count > 0 && !data.extraStatPool.Contains(rule.stat)) return false;

        return true;
    }

    static bool ContainsStat(List<StatRule> rules, StatType stat)
    {
        foreach (var rule in rules)
        {
            if (rule.stat == stat) return true;
        }

        return false;
    }

    /// <summary>Picks a rule by weight and removes it, so one item never rolls a stat twice.</summary>
    static StatRule TakeWeighted(List<StatRule> candidates)
    {
        float total = 0f;
        foreach (var rule in candidates) total += rule.weight;

        if (total <= 0f) return null;

        float pick = Random.value * total;
        for (int i = 0; i < candidates.Count; i++)
        {
            pick -= candidates[i].weight;
            if (pick > 0f) continue;

            var chosen = candidates[i];
            candidates.RemoveAt(i);
            return chosen;
        }

        var last = candidates[candidates.Count - 1];
        candidates.RemoveAt(candidates.Count - 1);
        return last;
    }

    /// <summary>Applies the stat's value scale; a successful roll is never worth 0.</summary>
    static int Scale(int rolled, float valueScale)
    {
        if (rolled <= 0 || valueScale <= 0f) return 0;

        return Mathf.Max(1, Mathf.RoundToInt(rolled * valueScale));
    }

    static bool IsRollable(LevelTuning tuning, ModuleGenerationConfig cfg)
    {
        if (tuning == null || tuning.weight <= 0f) return false;

        return tuning.level >= cfg.MinLevel && tuning.level <= cfg.MaxLevel;
    }

    static ModuleGenerator CreateDefault()
    {
        var go = new GameObject("[ModuleGenerator]");
        var generator = go.AddComponent<ModuleGenerator>();

        Debug.LogWarning("[Generator] No ModuleGenerator in the scene - using a temporary one");
        return generator;
    }

    static ModuleGenerationConfig LoadConfig()
    {
        var loaded = Resources.Load<ModuleGenerationConfig>(ConfigResourcePath);
        if (loaded != null) return loaded;

        // Still playable without an asset: the built-in curve is used.
        var fallback = ScriptableObject.CreateInstance<ModuleGenerationConfig>();
        fallback.name = ConfigResourcePath;
        fallback.ResetToDefaults();

        Debug.LogWarning($"[Generator] No config at Resources/{ConfigResourcePath} - using built-in defaults");
        return fallback;
    }
}
