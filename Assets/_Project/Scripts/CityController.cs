using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CityController : MonoBehaviour, ITurner, IBootstraped
{
    public bool isInitialised { get; set; }
    public float turnFill { get; set; }

    [Header("Starting city")]
    [Tooltip("Modules placed on the city when the game starts.")]
    [SerializeField] List<ModuleData> startingModules = new();

    // Platforms are found in the scene, sorted left to right -> they form the grid columns.
    List<Platform> platforms = new();

    Stats baseStats = new Stats();
    Stats stats = new Stats();

    public IReadOnlyList<Platform> Platforms => platforms;

    /// <summary>Current stats: base stats + bonuses from all placed modules.</summary>
    public Stats Stats => stats;

    /// <summary>Raised after a module was added or removed (stats already recalculated).</summary>
    public event System.Action ModulesChanged;

    /// <summary>Raised every time the city takes damage (shakers listen to this).</summary>
    public event System.Action Damaged;

    [SerializeField] Image initiativeFill;
    [SerializeField] Image healthFill;

    int curentTakeDamage;

    /// <summary>True while the city is stunned: its next turn is skipped.</summary>
    bool isStunned;

    /// <summary>Health left right now (max health minus accumulated damage).</summary>
    public int CurrentHealth => Mathf.Max(0, stats.health - curentTakeDamage);

    public void Init()
    {
        DiscoverPlatforms();
        curentTakeDamage = 0;
        RecalculateStats();

        foreach (var data in startingModules)
        {
            if (AddModule(data) == null)
                Debug.LogWarning($"[City] Could not place starting module '{data.name}'");
        }
    }

    void DiscoverPlatforms()
    {
        platforms.Clear();
        platforms.AddRange(FindObjectsByType<Platform>());
        platforms.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
    }

    // ------------------------------------------------------------------ modules

    /// <summary>Adds a module to the first platform that still has a free slot.</summary>
    public Module AddModule(ModuleData data)
    {
        if (data == null || data.prefab == null)
        {
            Debug.LogWarning("[City] ModuleData (or its prefab) is missing");
            return null;
        }

        foreach (var platform in platforms)
        {
            if (!platform.IsFull) return AddModule(data, platform);
        }

        Debug.LogWarning($"[City] No free slot for module '{data.displayName}'");
        return null;
    }

    /// <summary>Instantiates the module prefab and puts it on the given platform.</summary>
    public Module AddModule(ModuleData data, Platform platform)
    {
        if (data == null || data.prefab == null || platform == null) return null;

        var module = Instantiate(data.prefab);
        module.Bind(data);

        if (!platform.TryAdd(module))
        {
            Destroy(module.gameObject);
            return null;
        }

        Debug.Log($"[City] Added module '{data.displayName}' to {platform.name} ({platform.Count}/{Platform.MaxModules})");
        RecalculateStats();
        ModulesChanged?.Invoke();
        return module;
    }

    public bool RemoveModule(Module module)
    {
        if (module == null) return false;

        foreach (var platform in platforms)
        {
            if (!platform.Remove(module)) continue;

            Debug.Log($"[City] Removed module '{module.name}' from {platform.name} ({platform.Count}/{Platform.MaxModules})");
            Destroy(module.gameObject);
            RecalculateStats();
            ModulesChanged?.Invoke();
            return true;
        }

        return false;
    }

    // ------------------------------------------------------------------ stats

    public void RecalculateStats()
    {
        // Start from the base values, then add every bonus of every placed module.
        stats = baseStats.Clone();

        foreach (var platform in platforms)
        {
            foreach (var module in platform.Modules)
            {
                module.Bonus?.AddTo(stats);
            }
        }

        Debug.Log($"[City] Stats: atk {stats.atack}, hp {stats.health}, def {stats.def}, spd {stats.speed}, " +
                  $"regen {stats.regen}%, stun {stats.stanChance}%");

        UpdateHealthUI();
    }

    // ------------------------------------------------------------------ turns

    public void OnTurn()
    {
        Regenerate();

        // On its turn the city attacks one random living enemy.
        var manager = G.Get<EnemyManager>();
        if (manager == null) return;

        var target = manager.GetRandomEnemy();
        if (target == null)
        {
            Debug.Log("City turn: no enemies left");
            return;
        }

        Debug.Log($"City attacks {target.name}");

        // Damage happens exactly once, here - independent of any visual.
        target.TakeDamage(stats.atack);

        // Weapon modules only play their shot for show.
        foreach (var attacker in CollectAttackers())
        {
            attacker.PlayAttack(target);
        }

        if (CombatRules.RollChance(stats.stanChance))
        {
            Debug.Log($"[Stun] City stuns {target.name} ({stats.stanChance}%)");
            target.Stun();
        }
    }

    /// <summary>Every placed module with a visible attack effect, in platform order.</summary>
    List<AttackingModule> CollectAttackers()
    {
        var attackers = new List<AttackingModule>();

        foreach (var platform in platforms)
        {
            foreach (var module in platform.Modules)
            {
                var attacker = module.GetComponent<AttackingModule>();
                if (attacker != null) attackers.Add(attacker);
            }
        }

        return attackers;
    }

    /// <summary>Regeneration: restores a percentage of max health at the start of the turn.</summary>
    void Regenerate()
    {
        int heal = CombatRules.RegenAmount(stats.health, stats.regen);
        if (heal <= 0) return;

        curentTakeDamage = Mathf.Max(0, curentTakeDamage - heal);
        UpdateHealthUI();

        Debug.Log($"[Regen] City restored {heal} hp ({curentTakeDamage} damage taken)");
    }

    /// <summary>Marks the city as stunned: it will skip its next turn.</summary>
    public void Stun()
    {
        isStunned = true;
    }

    /// <summary>Reset for a new battle (also drops any stun).</summary>
    public void Restart()
    {
        turnFill = 0f;
        isStunned = false;
    }

    public void AddFill(float addedFill)
    {
        turnFill += addedFill * (float)stats.speed / 100f;
        UpdateInitiativeUI();
        if (turnFill >= 1f) {
            turnFill -= 1f;

            if (isStunned)
            {
                // The whole turn is lost, then the stun wears off.
                isStunned = false;
                UpdateInitiativeUI();
                Debug.Log("[Stun] City is stunned and skips a turn");
                return;
            }

            OnTurn();
        }
    }

    public void UpdateInitiativeUI()
    {
        initiativeFill.fillAmount = turnFill;
    }

    public void UpdateHealthUI()
    {
        if (stats.health <= 0) return;

        healthFill.fillAmount = (float)(stats.health-curentTakeDamage)/((float)stats.health);
    }

    public void TakeDamage(int damage) { 
        int applied = Mathf.Max(damage-stats.def,1);
        curentTakeDamage += applied;

        Debug.Log(curentTakeDamage);

        G.Get<DamagePopupSpawner>()?.ShowCityDamage(applied, transform.position);

        UpdateHealthUI();
        Damaged?.Invoke();
    }
}
