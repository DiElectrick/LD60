using UnityEngine;

/// <summary>
/// Cannon module: aims like every turret module and fires a projectile timed to land
/// together with the city's damage. The shot is decoration.
/// </summary>
public class CannonModule : AimingModule
{
    [Header("Firing")]
    [Tooltip("Projectile prefab (must have a Projectile component).")]
    [SerializeField] Projectile projectilePrefab;

    [Tooltip("Distance in front of the barrel where the projectile appears.")]
    [SerializeField] float muzzleDistance = 0.7f;

    [Tooltip("How long the projectile flies before it reaches the target.")]
    [SerializeField] float shotTime = 0.35f;

    protected override float EffectDelay => shotTime;

    protected override void Fire(BaseEnemy target)
    {
        if (projectilePrefab == null || target == null || !target.IsAlive) return;

        Vector3 spawnPosition = Barrel.position + Barrel.right * muzzleDistance;
        var projectile = Instantiate(projectilePrefab, spawnPosition, Barrel.rotation);

        // Speed chosen so the flight takes exactly shotTime seconds.
        float distance = Vector3.Distance(spawnPosition, target.transform.position);
        projectile.Launch(target, distance / Mathf.Max(0.01f, shotTime));
    }
}
