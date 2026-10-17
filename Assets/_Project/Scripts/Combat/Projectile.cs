using UnityEngine;

/// <summary>
/// Decorative projectile fired by a cannon module: it flies to its target and
/// disappears. It deals no damage - CityController applies the city's damage itself.
/// Configure speed/lifetime/hit radius on the prefab.
/// </summary>
public class Projectile : MonoBehaviour
{
    [SerializeField] float speed = 30f;
    [SerializeField] float lifetime = 3f;
    [SerializeField] float hitRadius = 0.25f;
    [Tooltip("Rotate the sprite towards its flight direction.")]
    [SerializeField] bool faceDirection = true;

    BaseEnemy target;

    public void Launch(BaseEnemy enemy, float speedOverride = -1f)
    {
        target = enemy;

        if (speedOverride > 0f) speed = speedOverride;
    }

    void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f || target == null || !target.IsAlive)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        float step = speed * Time.deltaTime;

        if (toTarget.magnitude <= Mathf.Max(step, hitRadius))
        {
            Destroy(gameObject);
            return;
        }

        Vector3 direction = toTarget.normalized;
        transform.position += direction * step;

        if (faceDirection)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }
}
