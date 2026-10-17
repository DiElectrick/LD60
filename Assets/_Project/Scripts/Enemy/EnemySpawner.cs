using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Instantiates enemies from EnemyData in a column of slots on the right side of the city.
/// </summary>
public class EnemySpawner : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    [Tooltip("World offset of the first enemy slot (relative to this object).")]
    public Vector2 firstSlot = new(4.6f, 1.6f);

    [Tooltip("Vertical distance between slots.")]
    public float slotSpacing = 2.8f;

    readonly List<BaseEnemy> spawned = new();

    public void Init() { }

    public List<BaseEnemy> SpawnEncounter(IReadOnlyList<EnemyData> enemies)
    {
        DespawnAll();

        var result = new List<BaseEnemy>();
        for (int i = 0; i < enemies.Count; i++)
        {
            var enemy = Spawn(enemies[i], i);
            if (enemy != null) result.Add(enemy);
        }

        return result;
    }

    public BaseEnemy Spawn(EnemyData data, int slotIndex)
    {
        if (data == null || data.prefab == null)
        {
            Debug.LogWarning("[Spawner] EnemyData (or its prefab) is missing");
            return null;
        }

        var enemy = Instantiate(data.prefab, transform);
        enemy.transform.position = transform.position +
            new Vector3(firstSlot.x, firstSlot.y - slotIndex * slotSpacing, 0f);

        enemy.Bind(data);
        spawned.Add(enemy);
        return enemy;
    }

    public void DespawnAll()
    {
        foreach (var enemy in spawned)
        {
            if (enemy != null) Destroy(enemy.gameObject);
        }

        spawned.Clear();
    }
}
