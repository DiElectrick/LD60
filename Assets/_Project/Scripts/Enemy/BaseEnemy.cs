using UnityEngine;
using UnityEngine.UI;

public class BaseEnemy : MonoBehaviour, ITurner
{
    public float turnFill { get; set; }

    public Stats Stats => enemyStats;

    [SerializeField] Stats enemyStats = new();

    /// <summary>Health the enemy had when it was spawned (for the HP bar ratio).</summary>
    int maxHealth;

    [SerializeField] Image initiativeFill;
    [SerializeField] Image healthFill;

    bool isAlive = true;

    /// <summary>True while stunned: the next turn is skipped.</summary>
    bool isStunned;

    /// <summary>The data this enemy was spawned from (used for its loot table).</summary>
    EnemyData data;

    void Awake()
    {
        maxHealth = enemyStats.health;
    }

    /// <summary>Called by the spawner right after the prefab was instantiated.</summary>
    public void Bind(EnemyData enemyData)
    {
        data = enemyData;
        enemyStats = enemyData.stats.Clone();
        maxHealth = enemyStats.health;
        isAlive = true;
        isStunned = false;
        turnFill = 0f;
        name = enemyData.displayName;

        UpdateHealthUI();
        UpdateInitiativeUI();
    }

    void OnEnable()
    {
        // An enemy spawns/enables itself -> it is a valid target for the city.
        if (G.TryGet<EnemyManager>(out var manager)) manager.Register(this);
    }

    void OnDisable()
    {
        if (G.TryGet<EnemyManager>(out var manager)) manager.Unregister(this);
    }

    public void AddFill(float addedFill)
    {
        if (!isAlive) return;

        turnFill += addedFill * (float)enemyStats.speed / 100f;
        UpdateInitiativeUI();
        if (turnFill >= 1f)
        {
            turnFill -= 1f;

            if (isStunned)
            {
                // The whole turn is lost, then the stun wears off.
                isStunned = false;
                UpdateInitiativeUI();
                Debug.Log($"[Stun] {name} is stunned and skips a turn");
                return;
            }

            OnTurn();
        }
    }

    public void OnTurn()
    {
        Regenerate();

        var city = G.Get<CityController>();
        if (city == null) return;

        city.TakeDamage(enemyStats.atack);

        if (CombatRules.RollChance(enemyStats.stanChance))
        {
            Debug.Log($"[Stun] {name} stuns the city ({enemyStats.stanChance}%)");
            city.Stun();
        }
    }

    /// <summary>Regeneration: restores a percentage of max health at the start of the turn.</summary>
    void Regenerate()
    {
        int heal = CombatRules.RegenAmount(maxHealth, enemyStats.regen);
        if (heal <= 0) return;

        enemyStats.health = Mathf.Min(enemyStats.health + heal, maxHealth);
        UpdateHealthUI();

        Debug.Log($"[Regen] {name} restored {heal} hp (hp left: {enemyStats.health})");
    }

    /// <summary>Marks the enemy as stunned: it will skip its next turn.</summary>
    public void Stun()
    {
        if (!isAlive) return;

        isStunned = true;
    }

    /// <summary>Reset for a new battle (also drops any stun).</summary>
    public void Restart()
    {
        turnFill = 0f;
        isStunned = false;
    }

    public void TakeDamage(int damage)
    {
        if (!isAlive) return;

        enemyStats.health -= Mathf.Max(damage - enemyStats.def, 1);
        Debug.Log($"{name} took {damage} damage, hp left: {enemyStats.health}");

        UpdateHealthUI();

        if (enemyStats.health <= 0) Die();
    }

    void Die()
    {
        isAlive = false;
        Debug.Log($"{name} died");

        DropLoot();

        // Deactivating fires OnDisable, which removes the enemy from the EnemyManager.
        gameObject.SetActive(false);
    }

    void DropLoot()
    {
        if (data == null || data.drops.Count == 0) return;

        G.Get<ResourceManager>()?.AddDrops(data.drops);
    }

    public void UpdateInitiativeUI()
    {
        initiativeFill.fillAmount = turnFill;
    }

    public void UpdateHealthUI()
    {
        if (healthFill == null) return;

        healthFill.fillAmount = maxHealth > 0 ? (float)enemyStats.health / maxHealth : 0f;
    }
}
