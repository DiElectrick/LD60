using UnityEngine;

/// <summary>
/// Boss that looks after its swarm: every <see cref="healEveryTurns"/>-th turn it patches up
/// itself and its living allies instead of attacking the city. All other turns it fights like a
/// normal enemy (<see cref="BaseEnemy.OnTurn"/>).
/// <para>
/// The prefab decides everything: stats come from the <see cref="EnemyData"/> it is spawned with,
/// and the healing pattern is tuned right here in the inspector. Put this component on the boss
/// prefab instead of a plain <see cref="BaseEnemy"/>.
/// </para>
/// </summary>
public class HealingBoss : BaseEnemy
{
    [Header("Support")]
    [Tooltip("Every N-th turn is a healing turn instead of an attack. 1 = heal every turn, 0 = never.")]
    [Min(0)]
    [SerializeField] int healEveryTurns = 3;

    [Tooltip("Health restored to every unit it heals.")]
    [Min(0)]
    [SerializeField] int healAmount = 18;

    [Tooltip("Heals itself as well as its allies.")]
    [SerializeField] bool healSelf = true;

    [Tooltip("Heals the other enemies of the encounter.")]
    [SerializeField] bool healAllies = true;

    int turnsTaken;

    /// <summary>Turns the boss has taken in this battle (used to pick the healing turns).</summary>
    public int TurnsTaken => turnsTaken;

    public override void Restart()
    {
        base.Restart();

        turnsTaken = 0;
    }

    public override void OnTurn()
    {
        turnsTaken++;

        if (IsHealingTurn() && Support())
        {
            DevLog.Info($"[Boss] {name} patches the swarm up (+{healAmount})");
            return;
        }

        base.OnTurn();
    }

    bool IsHealingTurn()
    {
        return healEveryTurns > 0 && turnsTaken % healEveryTurns == 0;
    }

    /// <summary>Heals everything it can; false when nothing needed healing (then it just attacks).</summary>
    bool Support()
    {
        bool healed = false;

        if (healSelf && Stats.health < MaxHealth)
        {
            Heal(healAmount);
            healed = true;
        }

        if (!healAllies) return healed;

        var manager = G.Get<EnemyManager>();
        if (manager == null) return healed;

        // Enemies list is stable while healing (nothing is added or removed here).
        var allies = manager.Enemies;
        for (int i = 0; i < allies.Count; i++)
        {
            var ally = allies[i];

            if (ally == null || ally == this || !ally.IsAlive) continue;
            if (ally.Stats.health >= ally.MaxHealth) continue;

            ally.Heal(healAmount);
            healed = true;
        }

        return healed;
    }
}
