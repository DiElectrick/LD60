using DG.Tweening;
using UnityEngine;

/// <summary>
/// Turret module: always aims at the city's current target, and on attack fires a
/// projectile timed to land together with the city's damage. The shot is decoration.
/// </summary>
public class CannonModule : AttackingModule
{
    [Header("Barrel")]
    [Tooltip("Child object that rotates. Falls back to this transform.")]
    [SerializeField] Transform barrel;

    [SerializeField] float aimSpeed = 540f; // degrees per second

    [Header("Recoil")]
    [Tooltip("How far the barrel kicks away from the aim direction when firing.")]
    [SerializeField] float recoilAngle = 7f;
    [SerializeField] float recoilKickTime = 0.05f;
    [SerializeField] float recoilReturnTime = 0.18f;

    [Header("Firing")]
    [Tooltip("Projectile prefab (must have a Projectile component).")]
    [SerializeField] Projectile projectilePrefab;
    [SerializeField] float muzzleDistance = 0.7f;

    [Tooltip("How long the projectile flies before it reaches the target.")]
    [SerializeField] float shotTime = 0.35f;

    CityController city;
    bool isBusy;

    void Awake()
    {
        if (barrel == null) barrel = transform;
    }

    void Start()
    {
        city = G.Get<CityController>();
    }

    void OnDestroy()
    {
        barrel.DOKill();
    }

    void Update()
    {
        if (isBusy) return;

        var target = city != null ? city.CurrentTarget : null;
        if (target == null || !target.IsAlive) return;

        // Always face the current target - there is no idle randomness.
        float angle = Mathf.MoveTowardsAngle(barrel.localEulerAngles.z, AimAngle(target),
            aimSpeed * Time.deltaTime);
        SetBarrelAngle(angle);
    }

    public override float GetAttackDelay(BaseEnemy target)
    {
        if (target == null || !target.IsAlive) return 0f;

        return AimTime(target) + shotTime;
    }

    public override void PlayAttack(BaseEnemy target, float hitDelay)
    {
        if (target == null || !target.IsAlive) return;

        barrel.DOKill();

        float aimTime = AimTime(target);
        float targetAngle = AimAngle(target);

        // Fire early enough that the shot lands together with the city's damage.
        float wait = Mathf.Max(0f, hitDelay - aimTime - shotTime);

        isBusy = true;

        var sequence = DOTween.Sequence();
        sequence.Append(barrel.DOLocalRotate(new Vector3(0f, 0f, targetAngle), aimTime).SetEase(Ease.OutQuad));

        if (wait > 0f) sequence.AppendInterval(wait);

        sequence.AppendCallback(() => Fire(target));
        sequence.Append(barrel.DOLocalRotate(new Vector3(0f, 0f, targetAngle - recoilAngle), recoilKickTime)
            .SetEase(Ease.OutQuad));
        sequence.Append(barrel.DOLocalRotate(new Vector3(0f, 0f, targetAngle), recoilReturnTime)
            .SetEase(Ease.InOutQuad));
        sequence.OnComplete(() => isBusy = false);
    }

    float AimTime(BaseEnemy target)
    {
        float delta = Mathf.Abs(Mathf.DeltaAngle(barrel.localEulerAngles.z, AimAngle(target)));
        return delta / Mathf.Max(1f, aimSpeed);
    }

    float AimAngle(BaseEnemy target)
    {
        // Aim in the barrel's parent space, so a rocking platform doesn't break it.
        Vector3 localTarget = barrel.parent.InverseTransformPoint(target.transform.position);
        Vector3 direction = localTarget - barrel.localPosition;
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    void Fire(BaseEnemy target)
    {
        if (projectilePrefab == null || target == null || !target.IsAlive) return;

        Vector3 spawnPosition = barrel.position + barrel.right * muzzleDistance;
        var projectile = Instantiate(projectilePrefab, spawnPosition, barrel.rotation);

        // Speed chosen so the flight takes exactly shotTime seconds.
        float distance = Vector3.Distance(spawnPosition, target.transform.position);
        projectile.Launch(target, distance / Mathf.Max(0.01f, shotTime));
    }

    void SetBarrelAngle(float angle)
    {
        barrel.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
