using UnityEngine;

/// <summary>
/// A building the city stops at (shop / repair bay). It travels left with the ground - the
/// <see cref="ParallaxSystem"/> ticks it like any other layer, so it brakes and resumes with the
/// world and never slides against the road - and <see cref="BattleCycle"/> times the stop so the
/// city comes to a halt right in front of it.
/// <para>
/// The prefab carries everything that is tuned by hand: the sprite, its sorting order, the
/// ground height of the root (the spawner only moves it along X) and its scale.
/// </para>
/// </summary>
public class StopBuilding : ParallaxLayer
{
    [Tooltip("Scrolled this far past the left edge of the view, the building is removed.")]
    [SerializeField] float despawnMargin = 4f;

    ParallaxSystem system;
    Renderer art;

    /// <summary>World-space box of the building art (works on the prefab asset too).</summary>
    public Bounds Art
    {
        get
        {
            if (art == null) art = GetComponentInChildren<Renderer>(true);

            return art != null ? art.bounds : new Bounds(transform.position, Vector3.zero);
        }
    }

    /// <summary>Half of the building's width - how far it reaches either side of its pivot.</summary>
    public float HalfWidth => Art.size.x * 0.5f;

    /// <summary>Right edge of the camera view: where a building is spawned.</summary>
    public float ViewRightEdge => cam != null ? ViewRight : 0f;

    protected override void Awake()
    {
        base.Awake();

        art = GetComponentInChildren<Renderer>(true);
    }

    void OnEnable()
    {
        // Layers spawned at runtime are not in the system's initial scan, so they join it here.
        system = FindAnyObjectByType<ParallaxSystem>();
        system?.RegisterLayer(this);
    }

    void OnDisable()
    {
        system?.UnregisterLayer(this);
        system = null;
    }

    protected override void Move(float distance)
    {
        transform.position += Vector3.left * distance;

        // The city rolled on: the building stays behind.
        if (Art.max.x < ViewLeft - despawnMargin) Destroy(gameObject);
    }
}
