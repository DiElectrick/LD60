using UnityEngine;

/// <summary>
/// The explosion used when the city falls apart: a puff of the very same smoke particles the city
/// breathes - same material, same particle size - but coloured like fire, shorter lived and thrown
/// outwards. Registered in G, so any system can blow something up without knowing the details.
/// <para>
/// The look is copied from the scene's smoke particle system (<see cref="smokeTemplate"/>); if it
/// is not assigned, the first particle system in the scene is used.
/// </para>
/// </summary>
public class ExplosionFX : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    [Header("Sound")]
    [Tooltip("Clip played with every explosion.")]
    [SerializeField] AudioClip sound;

    [Range(0f, 1f)]
    [SerializeField] float volume = 0.9f;

    [Header("Particles")]
    [Tooltip("Smoke particle system the look is copied from. Empty = the first one in the scene.")]
    [SerializeField] ParticleSystem smokeTemplate;

    [Tooltip("Particles per explosion.")]
    [Min(1)]
    [SerializeField] int particleCount = 22;

    [Tooltip("How long one fire puff lives.")]
    [Min(0.05f)]
    [SerializeField] float particleLifetime = 0.55f;

    [Tooltip("Particle size relative to the smoke. 1 = exactly the same size.")]
    [Min(0.01f)]
    [SerializeField] float sizeScale = 1f;

    [Tooltip("Start speed of the puffs (they are thrown outwards).")]
    [Min(0f)]
    [SerializeField] float puffSpeed = 5f;

    [Tooltip("How far from the centre the puffs are born.")]
    [Min(0f)]
    [SerializeField] float puffRadius = 0.35f;

    [Tooltip("Sorting order of the particles - above the city sprites.")]
    [SerializeField] int sortingOrder = 12;

    [Header("Colours")]
    [Tooltip("Fire colour at birth.")]
    [SerializeField] Color fireColor = new(1f, 0.82f, 0.35f, 1f);

    [Tooltip("Colour the puffs fade to (dark smoke, fully transparent at the end).")]
    [SerializeField] Color smokeColor = new(0.28f, 0.24f, 0.22f, 0f);

    float smokeSize = 0.19f;
    Material smokeMaterial;

    public void Init()
    {
        if (smokeTemplate == null)
            smokeTemplate = FindAnyObjectByType<ParticleSystem>(FindObjectsInactive.Include);

        if (smokeTemplate == null)
        {
            Debug.LogWarning("[FX] No smoke particle system to copy the explosion look from");
            return;
        }

        var templateRenderer = smokeTemplate.GetComponent<ParticleSystemRenderer>();
        if (templateRenderer != null) smokeMaterial = templateRenderer.sharedMaterial;

        // "Same particles, same size": the smoke's own start size becomes the explosion's.
        float size = smokeTemplate.main.startSize.constant;
        if (size > 0.0001f) smokeSize = size;
    }

    /// <summary>Plays the explosion (sound + fire puff) at a world position.</summary>
    public void Play(Vector3 position)
    {
        PlaySound();
        SpawnParticles(position);

        Debug.Log($"[FX] Explosion at {position}");
    }

    void PlaySound()
    {
        if (sound == null) return;

        var audio = G.Get<AudioManager>();
        if (audio != null) audio.PlaySfxClip(sound, volume, 0.12f, true);
    }

    void SpawnParticles(Vector3 position)
    {
        var go = new GameObject("Explosion");
        go.transform.position = position;

        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.duration = 0.2f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = particleLifetime;
        main.startSpeed = puffSpeed;
        main.startSize = smokeSize * sizeScale;
        main.startColor = fireColor;
        main.gravityModifier = 0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(1, particleCount);

        var emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, particleCount)) });

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.01f, puffRadius);
        shape.radiusThickness = 1f;

        // Fire at birth, then the puff turns into dark smoke and fades away.
        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(fireColor, 0f), new GradientColorKey(smokeColor, 1f) },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.25f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        // Puffs grow while they fade, like the smoke does.
        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.6f)));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            if (smokeMaterial != null) renderer.sharedMaterial = smokeMaterial;

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = sortingOrder;
        }

        particles.Play();

        // The puff is done after its lifetime - remove the object a bit later.
        Destroy(go, particleLifetime + 1.5f);
    }
}
