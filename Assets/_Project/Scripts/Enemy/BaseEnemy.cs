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

    void Awake()
    {
        maxHealth = enemyStats.health;
    }

    /// <summary>Called by the spawner right after the prefab was instantiated.</summary>
    public void Bind(EnemyData data)
    {
        enemyStats = data.stats.Clone();
        maxHealth = enemyStats.health;
        isAlive = true;
        turnFill = 0f;
        name = data.displayName;

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
            OnTurn();
        }
    }

    public void OnTurn()
    {
        G.Get<CityController>().TakeDamage(enemyStats.atack);
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

        // Deactivating fires OnDisable, which removes the enemy from the EnemyManager.
        gameObject.SetActive(false);
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
