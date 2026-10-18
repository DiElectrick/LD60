using UnityEngine;

/// <summary>
/// Spawns the floating numbers the game shows over units: damage over whoever was hit and heals
/// over whatever healed. Registered in G, so gameplay code reports a number without knowing
/// anything about the popup prefab.
/// <para>
/// Resource gains are not handled here: they pop out of their HUD counter instead
/// (see <see cref="ResourceCounterUI"/> and <see cref="FloatingResourcePopup"/>).
/// </para>
/// </summary>
public class PopupSpawner : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    [SerializeField] FloatingPopup prefab;

    [Tooltip("Popup anchor offset from an enemy's position.")]
    [SerializeField] Vector2 enemyOffset = new(0f, 0.8f);

    [Tooltip("Heal numbers sit above the damage anchor, so a heal is never mistaken for a hit.")]
    [SerializeField] Vector2 enemyHealOffset = new(0f, 1.6f);

    [Tooltip("Popup anchor offset from the city's position (above the city).")]
    [SerializeField] Vector2 cityOffset = new(0f, 2.6f);

    [Tooltip("Heal numbers sit above the damage anchor, so a heal is never mistaken for a hit.")]
    [SerializeField] Vector2 cityHealOffset = new(0f, 3.4f);

    [Tooltip("Heal number offset from the position of the module that heals (auto-repair).")]
    [SerializeField] Vector2 moduleHealOffset = new(0f, 0.7f);

    public void Init() { }

    /// <summary>"-N" over an enemy that was hit.</summary>
    public void ShowEnemyDamage(int amount, Vector3 enemyPosition)
    {
        Spawn(amount, PopupKind.Damage, enemyPosition + (Vector3)enemyOffset);
    }

    /// <summary>"+N" in green over an enemy that healed itself (or was healed by a boss).</summary>
    public void ShowEnemyHeal(int amount, Vector3 enemyPosition)
    {
        Spawn(amount, PopupKind.Heal, enemyPosition + (Vector3)enemyHealOffset);
    }

    /// <summary>"-N" over the city that was hit.</summary>
    public void ShowCityDamage(int amount, Vector3 cityPosition)
    {
        Spawn(amount, PopupKind.Damage, cityPosition + (Vector3)cityOffset);
    }

    /// <summary>
    /// "+N" in green over the city. Used when the city heals without a repair module involved,
    /// e.g. the repair station.
    /// </summary>
    public void ShowCityHeal(int amount, Vector3 cityPosition)
    {
        Spawn(amount, PopupKind.Heal, cityPosition + (Vector3)cityHealOffset);
    }

    /// <summary>"+N" in green over the module that repaired the city (auto-repair).</summary>
    public void ShowModuleHeal(int amount, Vector3 modulePosition)
    {
        Spawn(amount, PopupKind.Heal, modulePosition + (Vector3)moduleHealOffset);
    }

    void Spawn(int amount, PopupKind kind, Vector3 position)
    {
        if (prefab == null || amount <= 0) return;

        var popup = Instantiate(prefab, position, Quaternion.identity);
        popup.Show(amount, kind);
    }
}
