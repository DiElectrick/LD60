using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps track of every enemy that is currently alive on the scene.
/// Registered in G (by Bootstrap), so any system can ask it for targets.
/// </summary>
public class EnemyManager : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    readonly List<BaseEnemy> enemies = new();

    public IReadOnlyList<BaseEnemy> Enemies => enemies;

    public void Init()
    {
        enemies.Clear();

        // Enemies already present in the scene when the game starts.
        // (Enemies spawned later register themselves in BaseEnemy.OnEnable.)
        foreach (var enemy in FindObjectsByType<BaseEnemy>())
        {
            Register(enemy);
        }
    }

    public void Register(BaseEnemy enemy)
    {
        if (enemy == null || enemies.Contains(enemy)) return;

        enemies.Add(enemy);
    }

    public void Unregister(BaseEnemy enemy)
    {
        enemies.Remove(enemy);
    }

    public BaseEnemy GetRandomEnemy()
    {
        if (enemies.Count == 0) return null;

        return enemies[Random.Range(0, enemies.Count)];
    }
}
