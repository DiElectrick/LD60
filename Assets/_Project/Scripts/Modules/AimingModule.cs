using DG.Tweening;
using UnityEngine;

/// <summary>
/// Base class for turret modules: they swing towards the city's current target and fire when
/// the city attacks. The damage itself is applied once by <see cref="CityController"/> -
/// subclasses only play their effect (a projectile, a beam, ...).
/// </summary>
public abstract class AimingModule : AttackingModule
{
    [Header("Barrel")]
    [Tooltip("Child object that rotates. Falls back to this transform.")]
    [SerializeField] protected Transform barrel;

    [Tooltip("How fast the barrel swings to the target (degrees per second).")]
    [SerializeField] float aimSpeed = 540f;

    [Header("Recoil")]
    [Tooltip("How far the barrel kicks away from the aim direction when firing.")]
    [SerializeField] float recoilAngle = 7f;
    [SerializeField] float recoilKickTime = 0.05f;
    [SerializeField] float recoilReturnTime = 0.18f;

    /// <summary>The rotating part, aimed at the target. Never null after Awake.</summary>
    protected Transform Barrel => barrel;

    CityController city;
    bool isBusy;

    /// <summary>How long the effect needs to reach the target after it was fired.</summary>
    protected abstract float EffectDelay { get; }

    protected virtual void Awake()
    {
        if (barrel == null) barrel = transform;
    }

    protected virtual void Start()
    {
        city = G.Get<CityController>();
    }

    protected virtual void OnDestroy()
    {
        if (barrel != null) barrel.DOKill();
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

        return AimTime(target) + EffectDelay;
    }

    public override void PlayAttack(BaseEnemy target, float hitDelay)
    {
        if (target == null || !target.IsAlive) return;

        barrel.DOKill();

        float aimTime = AimTime(target);
        float targetAngle = AimAngle(target);

        // Fire early enough that the effect lands together with the city's damage.
        float wait = Mathf.Max(0f, hitDelay - aimTime - EffectDelay);

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

    /// <summary>Plays the shot of the concrete module (projectile, beam, ...).</summary>
    protected abstract void Fire(BaseEnemy target);

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

    void SetBarrelAngle(float angle)
    {
        barrel.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
