using DG.Tweening;
using UnityEngine;

/// <summary>
/// Railgun module: aims like a cannon, but instead of a projectile it fires a beam - the beam
/// sprite is stretched from the muzzle all the way to the target, so the bolt is a line rather
/// than a flying object. The damage itself is applied by <see cref="CityController"/>.
/// </summary>
public class RailgunModule : AimingModule
{
    [Header("Beam")]
    [Tooltip("Sprite stretched along the shot (UI_5 - a thin strip works best).")]
    [SerializeField] Sprite beamSprite;

    [Tooltip("Tint of the beam.")]
    [SerializeField] Color beamColor = new(0.45f, 0.95f, 1f, 1f);

    [Tooltip("Thickness multiplier of the stretched beam.")]
    [SerializeField] float beamWidth = 0.8f;

    [Tooltip("How long the beam takes to reach the target.")]
    [SerializeField] float beamGrowTime = 0.06f;

    [Tooltip("How long the beam stays on screen before it fades out.")]
    [SerializeField] float beamLife = 0.18f;

    [Tooltip("Distance in front of the barrel where the beam starts.")]
    [SerializeField] float muzzleOffset = 1.2f;

    [Tooltip("Sorting order of the beam. Keep it below the HUD canvas (552).")]
    [SerializeField] int beamSortingOrder = 520;

    protected override float EffectDelay => beamGrowTime;

    protected override void Fire(BaseEnemy target)
    {
        if (target == null || !target.IsAlive || beamSprite == null) return;

        Vector3 origin = Barrel.position + Barrel.right * muzzleOffset;
        Vector3 direction = target.transform.position - origin;

        if (direction.sqrMagnitude < 0.0001f) return;

        float distance = direction.magnitude;
        Vector3 unit = direction / distance;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // The sprite's own width at scale 1 decides how much it has to stretch.
        float spriteWidth = Mathf.Max(0.001f, beamSprite.bounds.size.x);
        float growTime = Mathf.Max(0.01f, beamGrowTime);

        var beam = new GameObject("RailgunBeam");

        var renderer = beam.AddComponent<SpriteRenderer>();
        renderer.sprite = beamSprite;
        renderer.color = beamColor;
        renderer.sortingOrder = beamSortingOrder;

        // The beam sprite is centred on its pivot (it is used elsewhere too), so the object
        // sits in the MIDDLE of the trajectory and the scale stretches it over the whole
        // distance - placing it at the muzzle would push half of the beam behind the barrel.
        beam.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        beam.transform.position = origin;
        beam.transform.localScale = new Vector3(0.001f, beamWidth, 1f);

        // Shoot out from the muzzle to the target, then fade away.
        DOVirtual.Float(0f, 1f, growTime, progress =>
            {
                if (beam == null) return;

                float length = Mathf.Lerp(0.001f, distance, progress);
                beam.transform.position = origin + unit * (length * 0.5f);
                beam.transform.localScale = new Vector3(length / spriteWidth, beamWidth, 1f);
            })
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                if (beam == null || renderer == null) return;

                renderer.DOFade(0f, Mathf.Max(0.01f, beamLife)).OnComplete(() =>
                {
                    if (beam != null) Destroy(beam);
                });
            });
    }
}
