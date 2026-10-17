using UnityEngine;

/// <summary>
/// Everything needed to spawn a specific enemy: its view prefab and its stats.
/// Create new ones with Assets > Create > LD60 > Enemy Data.
/// </summary>
[CreateAssetMenu(menuName = "LD60/Enemy Data", fileName = "EnemyData")]
public class EnemyData : ScriptableObject
{
    public string displayName = "Enemy";

    [Tooltip("Prefab instantiated by the EnemySpawner.")]
    public BaseEnemy prefab;

    public Stats stats = new();
}
