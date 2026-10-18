using DG.Tweening;
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

    /// <summary>False once the enemy died (used by projectiles).</summary>
    public bool IsAlive => isAlive;

    /// <summary>True while stunned: the next turn is skipped.</summary>
    bool isStunned;

    /// <summary>The data this enemy was spawned from (used for its loot table).</summary>
    EnemyData data;

    [Header("Death")]
    [Tooltip("Animator state played when the enemy dies. Empty = no animation (static sprite enemies).")]
    [SerializeField] string deathState = "Die";

    [Tooltip("Sprite swapped in when the enemy dies. Empty = keep the living sprite.")]
    [SerializeField] Sprite deathSprite;

    [Tooltip("Turns the sprite upside down on death, for enemies without a death animation.")]
    [SerializeField] bool flipOnDeath;

    [Tooltip("Degrees the corpse rotates while it falls off screen. 0 = it falls without spinning.")]
    [SerializeField] float deathSpin;

    [Tooltip("How long the death spin takes.")]
    [SerializeField] float deathSpinTime = 1.1f;

    [Tooltip("Gravity applied to the falling corpse.")]
    [SerializeField] float fallGravity = -22f;

    [Tooltip("Upward kick when the death animation starts.")]
    [SerializeField] float fallKickUp = 2.5f;

    [Tooltip("Horizontal drift while falling (negative = to the left).")]
    [SerializeField] float fallDrift = -1.5f;

    [Tooltip("How far below the camera bottom the corpse is destroyed.")]
    [SerializeField] float destroyBelowMargin = 2f;

    [Header("Attack twitch")]
    [Tooltip("How far the enemy jumps towards the city when it attacks.")]
    [SerializeField] float attackTwitchDistance = 0.45f;

    [Tooltip("Duration of the lunge towards the city.")]
    [SerializeField] float attackTwitchOutTime = 0.08f;

    [Tooltip("Duration of the spring back into its slot.")]
    [SerializeField] float attackTwitchBackTime = 0.22f;

    Animator animator;
    SpriteRenderer view;
    Canvas healthCanvas;
    Vector2 fallVelocity;
    int deathStateHash;

    // Death tumble: angle it started from and how far it has turned since.
    float deathStartAngle;
    float deathSpinProgress;

    // Slot the enemy attacks from - the twitch always returns here.
    Vector3 homePosition;
    bool homeCaptured;
    Tween attackTwitch;

    void Awake()
    {
        maxHealth = enemyStats.health;

        animator = GetComponent<Animator>();
        view = GetComponentInChildren<SpriteRenderer>(true);
        deathStateHash = Animator.StringToHash(deathState);

        var canvasTransform = transform.Find("Canvas");
        if (canvasTransform != null) healthCanvas = canvasTransform.GetComponent<Canvas>();
    }

    void Update()
    {
        if (isAlive) return;

        // Dead enemies become a falling corpse that leaves the screen on its own.
        fallVelocity.y += fallGravity * Time.deltaTime;
        transform.position += new Vector3(fallDrift * Time.deltaTime, fallVelocity.y * Time.deltaTime, 0f);

        // Some enemies tumble (or roll over) while they fall.
        SpinWhileFalling();

        var cam = Camera.main;
        float bottom = cam != null ? cam.transform.position.y - cam.orthographicSize : -10f;

        if (transform.position.y < bottom - destroyBelowMargin) Destroy(gameObject);
    }

    /// <summary>Turns the corpse over the configured angle, easing out as it falls.</summary>
    void SpinWhileFalling()
    {
        if (deathSpin == 0f) return;

        deathSpinProgress += Time.deltaTime / Mathf.Max(0.01f, deathSpinTime);
        float progress = Mathf.Clamp01(deathSpinProgress);
        float eased = 1f - (1f - progress) * (1f - progress);

        transform.rotation = Quaternion.Euler(0f, 0f, deathStartAngle + deathSpin * eased);
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
                DevLog.Info($"[Stun] {name} is stunned and skips a turn");
                return;
            }

            OnTurn();
        }
    }

    public virtual void OnTurn()
    {
        Regenerate();

        var city = G.Get<CityController>();
        if (city == null) return;

        // Strike: twitch towards the city the moment the hit lands.
        PlayAttackTwitch(city);

        city.TakeDamage(enemyStats.atack);

        if (CombatRules.RollChance(enemyStats.stanChance))
        {
            DevLog.Info($"[Stun] {name} stuns the city ({enemyStats.stanChance}%)");
            city.Stun();
        }
    }

    /// <summary>
    /// Quick lunge towards the city, played the frame the enemy attacks, followed by a
    /// spring back into the slot it attacked from. Purely cosmetic - the damage is
    /// applied by <see cref="OnTurn"/> no matter what happens to this tween.
    /// </summary>
    void PlayAttackTwitch(CityController city)
    {
        // The battle only starts after the fly-in, so the first attack is made from the
        // slot - that is the anchor the enemy springs back to after every lunge.
        if (!homeCaptured)
        {
            homePosition = transform.position;
            homeCaptured = true;
        }

        // A twitch can still be running if the turns come faster than the animation.
        attackTwitch?.Kill();
        attackTwitch = null;
        transform.position = homePosition;

        if (attackTwitchDistance <= 0f) return;

        Vector3 direction = city != null ? city.transform.position - homePosition : Vector3.left;

        // Fall back to "to the left" if the city sits on top of the enemy.
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.left;
        direction.Normalize();

        attackTwitch = DOTween.Sequence()
            .Append(transform.DOMove(homePosition + direction * attackTwitchDistance,
                    Mathf.Max(0.01f, attackTwitchOutTime))
                .SetEase(Ease.OutQuad))
            .Append(transform.DOMove(homePosition, Mathf.Max(0.01f, attackTwitchBackTime))
                .SetEase(Ease.OutBack))
            .SetLink(gameObject)
            .OnComplete(() => attackTwitch = null);
    }

    /// <summary>Regeneration: restores a percentage of max health at the start of the turn.</summary>
    void Regenerate()
    {
        int heal = CombatRules.RegenAmount(maxHealth, enemyStats.regen);
        if (heal <= 0) return;

        Heal(heal);

        DevLog.Info($"[Regen] {name} restored {heal} hp (hp left: {enemyStats.health})");
    }

    /// <summary>Health the enemy was spawned with; healing never goes above it.</summary>
    public int MaxHealth => maxHealth;

    /// <summary>
    /// Restores health (never above <see cref="MaxHealth"/>) and shows the green number.
    /// Used by regeneration and by enemies that heal themselves / their allies.
    /// </summary>
    public void Heal(int amount)
    {
        if (amount <= 0 || !isAlive) return;

        int before = enemyStats.health;
        enemyStats.health = Mathf.Min(enemyStats.health + amount, maxHealth);

        int restored = enemyStats.health - before;
        if (restored <= 0) return;

        UpdateHealthUI();
        G.Get<PopupSpawner>()?.ShowEnemyHeal(restored, transform.position);
    }

    /// <summary>Marks the enemy as stunned: it will skip its next turn.</summary>
    public void Stun()
    {
        if (!isAlive) return;

        isStunned = true;
    }

    /// <summary>Reset for a new battle (also drops any stun).</summary>
    public virtual void Restart()
    {
        turnFill = 0f;
        isStunned = false;
    }

    public void TakeDamage(int damage)
    {
        if (!isAlive) return;

        int applied = Mathf.Max(damage - enemyStats.def, 1);
        enemyStats.health -= applied;
        DevLog.Info($"{name} took {damage} damage, hp left: {enemyStats.health}");

        G.Get<PopupSpawner>()?.ShowEnemyDamage(applied, transform.position);
        G.Get<AudioManager>()?.PlaySfx("enemyDamage");

        UpdateHealthUI();

        if (enemyStats.health <= 0) Die();
    }

    void Die()
    {
        isAlive = false;
        DevLog.Info($"{name} died");

        G.Get<AudioManager>()?.PlaySfx("death");

        DropLoot();

        // The battle systems drop the enemy right away; the corpse is only a visual.
        G.Get<EnemyManager>()?.Unregister(this);
        G.Get<EnemySpawner>()?.Forget(this);

        // Stop the fly-in / attack twitch tweens, otherwise they would keep fighting the
        // fall. The corpse then drops from its slot rather than from the middle of a lunge.
        transform.DOKill();
        attackTwitch = null;
        if (homeCaptured) transform.position = homePosition;

        PlayDeathPose();

        if (healthCanvas != null) healthCanvas.gameObject.SetActive(false);

        // Kick it slightly up, then let gravity take over in Update.
        fallVelocity = new Vector2(fallDrift, fallKickUp);
    }

    /// <summary>Death animation / sprite of the corpse: an animator state, another sprite, a flip or a spin.</summary>
    void PlayDeathPose()
    {
        if (animator != null && !string.IsNullOrEmpty(deathState)) animator.Play(deathStateHash, 0, 0f);

        if (deathSprite != null && view != null) view.sprite = deathSprite;

        if (flipOnDeath && view != null) view.flipY = !view.flipY;

        if (deathSpin != 0f)
        {
            deathStartAngle = transform.eulerAngles.z;
            deathSpinProgress = 0f;
        }
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
