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

    [SerializeField] Image initiativeFill;
    [SerializeField] Image healthFill;

    int curentTakeDamage;

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

        Debug.Log($"[City] Stats: atk {stats.atack}, hp {stats.health}, def {stats.def}, spd {stats.speed}");

        UpdateHealthUI();
    }

    // ------------------------------------------------------------------ turns

    public void OnTurn()
    {
        // TEMP (testing): on its turn the city attacks one random living enemy.
        var manager = G.Get<EnemyManager>();
        if (manager == null) return;

        var target = manager.GetRandomEnemy();
        if (target == null)
        {
            Debug.Log("City turn: no enemies left");
            return;
        }

        Debug.Log($"City attacks {target.name}");
        target.TakeDamage(stats.atack);
    }

    public void AddFill(float addedFill)
    {
        turnFill += addedFill * (float)stats.speed / 100f;
        UpdateInitiativeUI();
        if (turnFill >= 1f) {
            turnFill -= 1f;
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
        curentTakeDamage += Mathf.Max(damage-stats.def,1);

        Debug.Log(curentTakeDamage);

        UpdateHealthUI();
    }
}
