using UnityEngine;

/// <summary>
/// Rocks a platform around its pivot like a suspension on uneven ground.
/// The rocking SPEED (and therefore the period) slowly changes over time,
/// and this change is shared by every platform - so all platforms rock
/// in sync in terms of tempo, while their phases differ by world X.
/// Rocking fades out and settles level while the world is stopped.
/// </summary>
public class PlatformSuspension : MonoBehaviour
{
    [Header("Rocking")]
    [Tooltip("Maximum tilt in degrees.")]
    public float amplitude = 2f;

    [Tooltip("Base rocking speed (radians per second). This is the center of the speed modulation.")]
    public float baseSpeed = 1.2f;

    [Tooltip("Phase shift per world unit of X.")]
    public float phasePerUnitX = 0.45f;

    [Header("Speed (period) modulation - global & synchronized")]
    [Tooltip("How much the speed deviates from baseSpeed, as a fraction. 0.5 = speed varies between 50% and 150% of baseSpeed.")]
    [Range(0f, 1f)]
    public float speedModDepth = 0.5f;

    [Tooltip("How fast the speed itself oscillates (radians per second). Keep it much slower than baseSpeed.")]
    public float speedModRate = 0.15f;

    [Tooltip("World speed below this fraction the tilt fades out to level.")]
    [Range(0.01f, 1f)]
    public float amplitudeRampSpeed = 0.2f;

    Vector3 baseEuler;
    float phase;        // per-platform phase offset from world X

    ParallaxSystem parallax;

    // Shared integrated speed - same for every instance, so all platforms
    // experience identical tempo changes at identical moments.
    static float integratedSpeed;
    static float lastTime;

    void Awake()
    {
        baseEuler = transform.localEulerAngles;
        phase = transform.position.x * phasePerUnitX;
        parallax = FindAnyObjectByType<ParallaxSystem>();
    }

    void LateUpdate()
    {
        // The parallax multiplier doubles as "is the city moving at all": the rocking
        // tempo follows the world speed and settles level when the world stops.
        float worldSpeed = parallax != null ? Mathf.Max(0f, parallax.speedMultiplier) : 1f;

        if (worldSpeed <= 0.0001f)
        {
            transform.localRotation = Quaternion.Euler(baseEuler);
            return;
        }

        // --- Shared speed modulation, integrated over time ---
        // This makes speed(t) = baseSpeed * (1 + depth * sin(rate * t)) *without* phase jumps.
        // We use a static so every instance advances the same accumulator only once per frame.
        if (Time.time > lastTime)
        {
            float dt = Time.time - lastTime;
            float mod = 1f + speedModDepth * Mathf.Sin(Time.time * speedModRate);
            integratedSpeed += baseSpeed * mod * dt * worldSpeed;
            lastTime = Time.time;
        }

        // Amplitude fades out only in the last part of the braking, so a slower world
        // tempo doesn't just shrink the tilt.
        float amplitudeScale = Mathf.Clamp01(worldSpeed / amplitudeRampSpeed);

        // --- Rocking with per-platform phase from world X ---
        float angle = amplitude * Mathf.Sin(integratedSpeed + phase) * amplitudeScale;

        transform.localRotation = Quaternion.Euler(baseEuler.x, baseEuler.y, baseEuler.z + angle);
    }
}
