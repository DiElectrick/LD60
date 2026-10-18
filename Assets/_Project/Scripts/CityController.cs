using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class CityController : MonoBehaviour, ITurner, IBootstraped
{
    public bool isInitialised { get; set; }
    public float turnFill { get; set; }

    [Header("Starting city")]
    [Tooltip("Modules placed on the city when the game starts.")]
    [SerializeField] List<ModuleData> startingModules = new();

    [Tooltip("Level the starting modules are generated at. 0 = roll a level from the table.")]
    [SerializeField] int startingModuleLevel = 1;

    // Platforms are found in the scene, sorted left to right -> they form the grid columns.
    List<Platform> platforms = new();

    [Header("Starting stats")]
    [Tooltip("Stats the city has before any module is placed. Modules add on top of these.")]
    [SerializeField]
    Stats baseStats = new Stats();

    Stats stats = new Stats();

    /// <summary>
    /// Scrap the city collects at the start of every turn: the sum of the harvest bonuses of
    /// all placed modules. Kept apart from <see cref="stats"/> on purpose - it is a city
    /// economy stat that no enemy can read (see <see cref="StatType.Harvest"/>).
    /// </summary>
    int harvestPerTurn;

    public IReadOnlyList<Platform> Platforms => platforms;

    /// <summary>Current stats: base stats + bonuses from all placed modules.</summary>
    public Stats Stats => stats;

    /// <summary>Scrap metal gained at the start of every city turn (harvester modules).</summary>
    public int HarvestPerTurn => harvestPerTurn;

    /// <summary>Raised after a module was added or removed (stats already recalculated).</summary>
    public event System.Action ModulesChanged;

    /// <summary>Raised every time the city takes damage (shakers listen to this).</summary>
    public event System.Action Damaged;

    /// <summary>Raised once, when the city's health reaches zero.</summary>
    public event System.Action Destroyed;

    /// <summary>True once the city has been reduced to zero health - it takes no more turns.</summary>
    public bool IsDestroyed { get; private set; }

    /// <summary>The enemy the city is currently aiming at (weapon modules track it).</summary>
    public BaseEnemy CurrentTarget { get; private set; }

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
            var instance = ModuleGenerator.Instance.Generate(data, startingModuleLevel);
            if (instance == null || AddModule(instance) == null)
                Debug.LogWarning($"[City] Could not place starting module '{data?.name}'");
        }
    }

    void DiscoverPlatforms()
    {
        platforms.Clear();
        platforms.AddRange(FindObjectsByType<Platform>());
        platforms.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
    }

    // ------------------------------------------------------------------ modules

    /// <summary>Adds a generated module to the first platform that still has a free slot.</summary>
    public Module AddModule(ModuleInstance instance)
    {
        if (instance == null || instance.Data == null)
        {
            Debug.LogWarning("[City] Module instance (or its data) is missing");
            return null;
        }

        foreach (var platform in platforms)
        {
            if (!platform.IsFull) return AddModule(instance, platform);
        }

        Debug.LogWarning($"[City] No free slot for module '{instance.DisplayName}'");
        return null;
    }

    /// <summary>Instantiates the module prefab and puts it on the given platform.</summary>
    public Module AddModule(ModuleInstance instance, Platform platform)
    {
        if (instance == null || instance.Data == null || platform == null) return null;

        var data = instance.Data;
        if (data.prefab == null)
        {
            Debug.LogWarning($"[City] ModuleData '{data.name}' has no prefab");
            return null;
        }

        var module = Instantiate(data.prefab);
        module.Bind(instance);

        if (!platform.TryAdd(module))
        {
            Destroy(module.gameObject);
            return null;
        }

        DevLog.Info($"[City] Added module '{instance.DisplayName}' to {platform.name} ({platform.Count}/{Platform.MaxModules})");
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

            DevLog.Info($"[City] Removed module '{module.name}' from {platform.name} ({platform.Count}/{Platform.MaxModules})");
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
        harvestPerTurn = 0;

        foreach (var platform in platforms)
        {
            foreach (var module in platform.Modules)
            {
                var bonus = module.Bonus;
                if (bonus == null) continue;

                bonus.AddTo(stats);

                // Income is not a battle stat: StatsBonus.AddTo skips it, so it is summed here.
                harvestPerTurn += bonus.harvest;
            }
        }

        DevLog.Info($"[City] Stats: atk {stats.atack}, hp {stats.health}, def {stats.def}, spd {stats.speed}, " +
                  $"regen {stats.regen}%, stun {stats.stanChance}%, scrap +{harvestPerTurn}/turn");

        UpdateHealthUI();
    }

    // ------------------------------------------------------------------ turns

    public void OnTurn()
    {
        Regenerate();
        Harvest();

        // On its turn the city attacks one random living enemy.
        var manager = G.Get<EnemyManager>();
        if (manager == null) return;

        var target = manager.GetRandomEnemy();
        if (target == null)
        {
            DevLog.Info("City turn: no enemies left");
            return;
        }

        CurrentTarget = target;

        // Every weapon fires; the slowest one decides when the hit lands, so all
        // projectiles arrive together with the damage.
        var attackers = CollectAttackers();
        float hitDelay = 0f;

        foreach (var attacker in attackers)
        {
            hitDelay = Mathf.Max(hitDelay, attacker.GetAttackDelay(target));
        }

        foreach (var attacker in attackers)
        {
            attacker.PlayAttack(target, hitDelay);
        }

        DevLog.Info($"City attacks {target.name} with {attackers.Count} weapon(s), hit in {hitDelay:F2}s");

        // Damage is applied exactly once, independent of the visual shots.
        if (hitDelay <= 0f) ApplyHit(target);
        else DOVirtual.DelayedCall(hitDelay, () => ApplyHit(target));
    }

    void ApplyHit(BaseEnemy target)
    {
        // A shot still travelling when the city fell must not land after the death sequence.
        if (IsDestroyed) return;

        // The chosen enemy may have died while the shot was travelling - hit another
        // living one instead, so a filled initiative bar always results in an attack.
        if (target == null || !target.IsAlive) target = G.Get<EnemyManager>()?.GetRandomEnemy();
        if (target == null) return;

        CurrentTarget = target;
        target.TakeDamage(stats.atack);

        if (CombatRules.RollChance(stats.stanChance))
        {
            DevLog.Info($"[Stun] City stuns {target.name} ({stats.stanChance}%)");
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

        int before = curentTakeDamage;
        curentTakeDamage = Mathf.Max(0, curentTakeDamage - heal);
        int restored = before - curentTakeDamage;

        UpdateHealthUI();

        if (restored > 0) ShowHealPopups(restored);

        DevLog.Info($"[Regen] City restored {restored} hp ({curentTakeDamage} damage taken)");
    }

    /// <summary>
    /// Reports the regeneration on the modules that provide it: the "+N" pops out of every
    /// repair module (auto-repair), split by its share of the total regeneration. A city that
    /// heals without such a module still reports the heal over itself.
    /// </summary>
    void ShowHealPopups(int restored)
    {
        var spawner = G.Get<PopupSpawner>();
        if (spawner == null) return;

        var sources = new List<Module>();
        int totalRegen = 0;

        foreach (var platform in platforms)
        {
            foreach (var module in platform.Modules)
            {
                var bonus = module.Bonus;
                if (bonus == null || bonus.regen <= 0) continue;

                sources.Add(module);
                totalRegen += bonus.regen;
            }
        }

        if (sources.Count == 0)
        {
            spawner.ShowCityHeal(restored, transform.position);
            return;
        }

        if (sources.Count == 1)
        {
            spawner.ShowModuleHeal(restored, sources[0].transform.position);
            return;
        }

        // Several repair modules: each one reports its part of the healed amount, and the
        // last one shows whatever rounding is left so the numbers still add up.
        int left = restored;

        for (int i = 0; i < sources.Count && left > 0; i++)
        {
            int regen = sources[i].Bonus.regen;
            int share = i == sources.Count - 1
                ? left
                : Mathf.Clamp(Mathf.RoundToInt(restored * (float)regen / totalRegen), 0, left);

            if (share > 0) spawner.ShowModuleHeal(share, sources[i].transform.position);

            left -= share;
        }
    }

    /// <summary>
    /// Harvesting: every placed harvester adds its scrap to the bank at the start of the turn.
    /// The gain pops out of the HUD scrap counter (see <see cref="ResourceCounterUI"/>).
    /// </summary>
    void Harvest()
    {
        if (harvestPerTurn <= 0) return;

        var resources = G.Get<ResourceManager>();
        resources?.Add(ResourceType.ScrapMetal, harvestPerTurn);

        DevLog.Info($"[Harvest] City collected {harvestPerTurn} {ResourceType.ScrapMetal} " +
                  $"(total {resources?.Get(ResourceType.ScrapMetal) ?? 0})");
    }

    /// <summary>Marks the city as stunned: it will skip its next turn.</summary>
    public void Stun()
    {
        isStunned = true;
    }

    /// <summary>
    /// Repairs the city: lowers the damage it has taken (never below zero).
    /// Used by the repair station.
    /// </summary>
    public void Repair(int amount)
    {
        if (amount <= 0 || IsDestroyed) return;

        int before = curentTakeDamage;
        curentTakeDamage = Mathf.Max(0, curentTakeDamage - amount);
        int restored = before - curentTakeDamage;

        UpdateHealthUI();

        if (restored > 0)
        {
            G.Get<PopupSpawner>()?.ShowCityHeal(restored, transform.position);
        }

        DevLog.Info($"[Repair] Restored {restored} hp ({CurrentHealth}/{stats.health})");
    }

    /// <summary>Reset for a new battle (also drops any stun).</summary>
    public void Restart()
    {
        turnFill = 0f;
        isStunned = false;
    }

    public void AddFill(float addedFill)
    {
        // A destroyed city does not take turns any more - the death sequence owns the screen.
        if (IsDestroyed) return;

        turnFill += addedFill * (float)stats.speed / 100f;
        UpdateInitiativeUI();
        if (turnFill >= 1f) {
            turnFill -= 1f;

            if (isStunned)
            {
                // The whole turn is lost, then the stun wears off.
                isStunned = false;
                UpdateInitiativeUI();
                DevLog.Info("[Stun] City is stunned and skips a turn");
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
        if (IsDestroyed) return;

        int applied = Mathf.Max(damage-stats.def,1);
        curentTakeDamage += applied;

        G.Get<PopupSpawner>()?.ShowCityDamage(applied, transform.position);
        G.Get<AudioManager>()?.PlaySfx("hitHurt");

        UpdateHealthUI();
        Damaged?.Invoke();

        if (CurrentHealth > 0) return;

        // Health is gone: the city is destroyed once, and the run goes to its death sequence.
        IsDestroyed = true;
        turnFill = 0f;
        UpdateInitiativeUI();

        DevLog.Info("[City] Destroyed - the city ran out of health");
        Destroyed?.Invoke();
    }
}
