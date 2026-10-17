using UnityEngine;

/// <summary>
/// Position shake for world objects: a continuous vibration (working engines) plus
/// short decaying impulses (the city taking damage).
/// </summary>
public class Shaker : MonoBehaviour
{
    [Tooltip("Amplitude of the constant vibration while idle shake is enabled.")]
    [SerializeField] float idleAmplitude = 0.035f;

    [Tooltip("How many times per second the shake changes direction. Lower = slower, heavier shake.")]
    [SerializeField] float shakeRate = 8f;

    float idleTarget;
    float impulseAmplitude;
    float impulseTime;
    float impulseDuration = 1f;

    Vector3 baseLocal;
    Vector3 lastApplied;
    bool initialised;

    Vector2 shakeDirection;
    float nextShakeTime;

    /// <summary>Starts or stops the continuous vibration (used by engine modules).</summary>
    public void SetIdleEnabled(bool enabled)
    {
        idleTarget = enabled ? idleAmplitude : 0f;
    }

    /// <summary>One-shot impulse that fades out over <paramref name="duration"/>.</summary>
    public void Shake(float amplitude, float duration = 0.25f)
    {
        if (amplitude <= 0f) return;

        impulseAmplitude = Mathf.Max(impulseAmplitude, amplitude);
        impulseDuration = Mathf.Max(0.01f, duration);
        impulseTime = impulseDuration;
    }

    void LateUpdate()
    {
        if (!initialised)
        {
            baseLocal = transform.localPosition;
            lastApplied = baseLocal;
            initialised = true;
        }
        else if ((transform.localPosition - lastApplied).sqrMagnitude > 1e-8f)
        {
            // Something else moved us (e.g. the platform layout) - use that as the new base.
            baseLocal = transform.localPosition;
        }

        float amplitude = idleTarget;

        if (impulseTime > 0f)
        {
            impulseTime -= Time.deltaTime;
            amplitude += impulseAmplitude * Mathf.Clamp01(impulseTime / impulseDuration);
        }
        else
        {
            impulseAmplitude = 0f;
        }

        // Pick a new direction a few times per second instead of every frame,
        // so the shake reads as a slower, heavier wobble.
        if (amplitude > 0f)
        {
            if (Time.time >= nextShakeTime)
            {
                nextShakeTime = Time.time + 1f / EffectiveRate;
                shakeDirection = Random.insideUnitCircle;
            }
        }
        else
        {
            shakeDirection = Vector2.zero;
        }

        Vector3 offset = shakeDirection * amplitude;

        lastApplied = baseLocal + offset;
        transform.localPosition = lastApplied;
    }

    float EffectiveRate => shakeRate > 0.01f ? shakeRate : 8f;
}
