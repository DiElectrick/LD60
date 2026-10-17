using UnityEngine;

/// <summary>
/// Spawns floating damage numbers. Registered in G so the city and the enemies
/// can report damage without knowing about the UI details.
/// </summary>
public class DamagePopupSpawner : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    [SerializeField] DamagePopup prefab;

    [Tooltip("Popup anchor offset from an enemy's position.")]
    [SerializeField] Vector2 enemyOffset = new(0f, 0.8f);

    [Tooltip("Popup anchor offset from the city's position (above the city).")]
    [SerializeField] Vector2 cityOffset = new(0f, 2.6f);

    public void Init() { }

    public void ShowEnemyDamage(int amount, Vector3 enemyPosition)
    {
        Spawn(amount, enemyPosition + (Vector3)enemyOffset);
    }

    public void ShowCityDamage(int amount, Vector3 cityPosition)
    {
        Spawn(amount, cityPosition + (Vector3)cityOffset);
    }

    void Spawn(int amount, Vector3 position)
    {
        if (prefab == null || amount <= 0) return;

        var popup = Instantiate(prefab, position, Quaternion.identity);
        popup.Show(amount);
    }
}
