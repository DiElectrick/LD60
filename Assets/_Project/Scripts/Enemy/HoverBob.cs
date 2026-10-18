using UnityEngine;

/// <summary>
/// Gentle up/down hover for floating enemies.
/// <para>
/// It moves only its own (child) transform, so everything that moves the enemy's root - the
/// fly-in, the attack twitch and the death fall - keeps working untouched. Every instance
/// randomises its phase, and slightly its period, so a group of identical enemies never bobs
/// in lockstep.
/// </para>
/// </summary>
public class HoverBob : MonoBehaviour
{
    [Tooltip("How far it floats above and below its rest position (local units).")]
    [SerializeField] float amplitude = 0.12f;

    [Tooltip("Seconds for one full up-down cycle.")]
    [Min(0.05f)]
    [SerializeField] float period = 2.4f;

    [Tooltip("Random spread of the period, so identical enemies drift apart. 0.15 = ±15%.")]
    [Range(0f, 0.5f)]
    [SerializeField] float periodJitter = 0.15f;

    [Tooltip("Stops the hover once the owner dies - a corpse should only fall.")]
    [SerializeField] bool stopWhenDead = true;

    Vector3 restPosition;
    BaseEnemy owner;
    float phase;
    float currentPeriod;
    float time;

    void Awake()
    {
        restPosition = transform.localPosition;
        owner = GetComponentInParent<BaseEnemy>();

        phase = Random.Range(0f, 1f);
        currentPeriod = period * Random.Range(1f - periodJitter, 1f + periodJitter);
    }

    void Update()
    {
        if (stopWhenDead && owner != null && !owner.IsAlive)
        {
            transform.localPosition = restPosition;
            return;
        }

        time += Time.deltaTime;

        float offset = Mathf.Sin((time / Mathf.Max(0.05f, currentPeriod) + phase) * Mathf.PI * 2f) * amplitude;

        transform.localPosition = restPosition + new Vector3(0f, offset, 0f);
    }
}
