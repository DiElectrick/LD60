using UnityEngine;

/// <summary>
/// Shakes this object every time the city takes damage.
/// Put it on a module prefab or on a scenery object such as the tube.
/// </summary>
[RequireComponent(typeof(Shaker))]
public class DamageShaker : MonoBehaviour
{
    [SerializeField] float amplitude = 0.18f;
    [SerializeField] float duration = 0.25f;

    CityController city;
    Shaker shaker;

    void Awake()
    {
        shaker = GetComponent<Shaker>();
    }

    void Start()
    {
        city = G.Get<CityController>();
        if (city != null) city.Damaged += OnCityDamaged;
    }

    void OnDestroy()
    {
        if (city != null) city.Damaged -= OnCityDamaged;
    }

    void OnCityDamaged()
    {
        shaker.Shake(amplitude, duration);
    }
}
