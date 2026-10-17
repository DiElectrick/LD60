using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Instantiates enemies from EnemyData in a column of slots on the right side of the city.
/// Enemies fly in from outside the screen: even slots from the right, odd slots from the top.
/// </summary>
public class EnemySpawner : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    [Tooltip("World offset of the first enemy slot (relative to this object).")]
    public Vector2 firstSlot = new(4.6f, 1.6f);

    [Tooltip("Vertical distance between slots.")]
    public float slotSpacing = 2.8f;

    [Tooltip("How long the fly-in takes.")]
    [SerializeField] float flyInDuration = 1.4f;

    [Tooltip("How far outside the screen the fly-in starts.")]
    [SerializeField] float flyInMargin = 3f;

    /// <summary>Time an enemy needs to reach its slot (used to delay the battle start).</summary>
    public float FlyInDuration => flyInDuration;

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

        Vector3 targetPosition = transform.position +
            new Vector3(firstSlot.x, firstSlot.y - slotIndex * slotSpacing, 0f);

        enemy.transform.position = targetPosition + EntryOffset(slotIndex);
        enemy.transform.DOMove(targetPosition, flyInDuration)
            .SetEase(Ease.OutCubic)
            .SetLink(enemy.gameObject);

        enemy.Bind(data);
        spawned.Add(enemy);
        return enemy;
    }

    /// <summary>Even slots fly in from the right edge, odd slots from the top edge.</summary>
    Vector3 EntryOffset(int slotIndex)
    {
        var cam = Camera.main;
        float halfHeight = cam != null ? cam.orthographicSize : 5f;
        float halfWidth = cam != null ? halfHeight * cam.aspect : 9f;

        return slotIndex % 2 == 0
            ? new Vector3(halfWidth + flyInMargin, 0f, 0f)
            : new Vector3(0f, halfHeight + flyInMargin, 0f);
    }

    /// <summary>Stops tracking an enemy (used by corpses, which clean themselves up).</summary>
    public void Forget(BaseEnemy enemy)
    {
        if (enemy != null) spawned.Remove(enemy);
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
