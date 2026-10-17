using System.Collections;
using UnityEngine;

/// <summary>
/// Turret module: the barrel idly swings around out of combat, turns towards the
/// target on attack, fires a projectile prefab and kicks back from the recoil.
/// </summary>
public class CannonModule : AttackingModule
{
    [Header("Barrel")]
    [Tooltip("Child object that rotates. Falls back to this transform.")]
    [SerializeField] Transform barrel;

    [SerializeField] float idleSpinMax = 45f;       // degrees per second while idle
    [SerializeField] float idleSpinMinDelay = 1.5f; // how long one spin direction lasts
    [SerializeField] float idleSpinMaxDelay = 3.5f;
    [SerializeField] float aimSpeed = 540f;         // degrees per second while aiming

    [Header("Recoil")]
    [Tooltip("How far the barrel kicks away from the aim direction when firing.")]
    [SerializeField] float recoilAngle = 7f;
    [SerializeField] float recoilKickTime = 0.05f;
    [SerializeField] float recoilReturnTime = 0.18f;

    [Header("Firing")]
    [Tooltip("Projectile prefab (must have a Projectile component).")]
    [SerializeField] Projectile projectilePrefab;
    [SerializeField] float muzzleDistance = 0.7f;

    TurnManager turnManager;

    float idleSpin;
    float nextSpinChange;
    bool isBusy;

    void Awake()
    {
        if (barrel == null) barrel = transform;
    }

    void Start()
    {
        turnManager = G.Get<TurnManager>();
        PickIdleSpin();
    }

    void Update()
    {
        if (isBusy) return;

        // Random swinging only out of combat - in combat the turret holds its angle
        // and only moves when it aims and fires.
        if (IsInCombat) return;

        if (Time.time >= nextSpinChange) PickIdleSpin();

        barrel.Rotate(0f, 0f, idleSpin * Time.deltaTime);
    }

    bool IsInCombat => turnManager != null && turnManager.IsBattleActive;

    void PickIdleSpin()
    {
        nextSpinChange = Time.time + Random.Range(idleSpinMinDelay, idleSpinMaxDelay);
        idleSpin = Random.Range(-idleSpinMax, idleSpinMax);
    }

    public override void PlayAttack(BaseEnemy target)
    {
        if (target == null || !target.IsAlive) return;

        StartCoroutine(AimFireAndRecoil(target));
    }

    IEnumerator AimFireAndRecoil(BaseEnemy target)
    {
        isBusy = true;

        // Aim in the barrel's parent space, so a rocking platform doesn't break it.
        float angle = barrel.localEulerAngles.z;

        while (target != null && target.IsAlive)
        {
            Vector3 localTarget = barrel.parent.InverseTransformPoint(target.transform.position);
            Vector3 direction = localTarget - barrel.localPosition;
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            float delta = Mathf.DeltaAngle(angle, targetAngle);
            if (Mathf.Abs(delta) <= 2f) break;

            angle = Mathf.MoveTowardsAngle(angle, targetAngle, aimSpeed * Time.deltaTime);
            SetBarrelAngle(angle);

            yield return null;
        }

        if (target == null || !target.IsAlive)
        {
            isBusy = false;
            PickIdleSpin();
            yield break;
        }

        angle = barrel.localEulerAngles.z;
        Fire(target);

        yield return Recoil(angle);

        isBusy = false;
        PickIdleSpin();
    }

    IEnumerator Recoil(float aimAngle)
    {
        if (recoilAngle <= 0f) yield break;

        float kickAngle = aimAngle - recoilAngle;

        for (float t = 0f; t < recoilKickTime; t += Time.deltaTime)
        {
            SetBarrelAngle(Mathf.LerpAngle(aimAngle, kickAngle, t / recoilKickTime));
            yield return null;
        }

        for (float t = 0f; t < recoilReturnTime; t += Time.deltaTime)
        {
            SetBarrelAngle(Mathf.LerpAngle(kickAngle, aimAngle, t / recoilReturnTime));
            yield return null;
        }

        SetBarrelAngle(aimAngle);
    }

    void Fire(BaseEnemy target)
    {
        if (projectilePrefab == null) return;

        Vector3 spawnPosition = barrel.position + barrel.right * muzzleDistance;
        var projectile = Instantiate(projectilePrefab, spawnPosition, barrel.rotation);
        projectile.Launch(target);
    }

    void SetBarrelAngle(float angle)
    {
        barrel.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
