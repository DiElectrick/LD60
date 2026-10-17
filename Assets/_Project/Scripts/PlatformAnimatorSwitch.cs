using UnityEngine;

/// <summary>
/// Switches a platform's animator between the rolling clip and the stopped clip,
/// depending on whether the world is moving. The rolling clip is also slowed down
/// while the parallax brakes, so the animation matches the world speed.
/// </summary>
public class PlatformAnimatorSwitch : MonoBehaviour
{
    [Tooltip("Animator state played while the city is rolling.")]
    [SerializeField] string movingState = "PlatformRunAnimation";

    [Tooltip("Animator state played while the world is stopped.")]
    [SerializeField] string stoppedState = "PlatformStop";

    [Tooltip("Fraction of the authored parallax speed below which the world counts as stopped.")]
    [SerializeField] float stopThreshold = 0.1f;

    [Tooltip("Slow the rolling clip down together with the world.")]
    [SerializeField] bool scaleWithWorldSpeed = true;

    Animator animator;
    ParallaxSystem parallax;

    int movingHash;
    int stoppedHash;
    bool isStopped;

    void Awake()
    {
        animator = GetComponent<Animator>();
        parallax = FindAnyObjectByType<ParallaxSystem>();

        movingHash = Animator.StringToHash(movingState);
        stoppedHash = Animator.StringToHash(stoppedState);
    }

    void Start()
    {
        if (animator == null) return;

        isStopped = false;
        animator.Play(movingHash, 0, 0f);
    }

    void Update()
    {
        if (animator == null) return;

        float worldSpeed = parallax != null ? Mathf.Max(0f, parallax.speedMultiplier) : 1f;
        float baseSpeed = parallax != null ? Mathf.Max(0.0001f, Mathf.Abs(parallax.baseSpeed)) : 1f;

        // "Stopped" is relative to the authored speed, so a slow world still animates.
        bool stopped = worldSpeed < baseSpeed * stopThreshold;

        if (stopped != isStopped)
        {
            isStopped = stopped;
            animator.speed = 1f;
            animator.Play(stopped ? stoppedHash : movingHash, 0, 0f);
        }

        if (scaleWithWorldSpeed && !isStopped)
        {
            animator.speed = Mathf.Max(0.02f, worldSpeed);
        }
    }
}
