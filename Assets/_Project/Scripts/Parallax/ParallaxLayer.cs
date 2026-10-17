using UnityEngine;

/// <summary>
/// Base class for one parallax layer. Knows its scroll speed and how to read the
/// camera view. Movement is driven by ParallaxSystem (so it can be paused/scaled).
/// </summary>
public abstract class ParallaxLayer : MonoBehaviour
{
    [Tooltip("World units per second this layer scrolls to the left.")]
    public float speed = 1f;

    protected Camera cam;

    protected float ViewLeft => cam.transform.position.x - cam.orthographicSize * cam.aspect;
    protected float ViewRight => cam.transform.position.x + cam.orthographicSize * cam.aspect;

    protected virtual void Awake()
    {
        cam = Camera.main;
    }

    /// <summary>Called every frame by the ParallaxSystem.</summary>
    public void Tick(float deltaTime)
    {
        if (cam == null) return;

        Move(speed * deltaTime);
    }

    protected abstract void Move(float distance);
}
