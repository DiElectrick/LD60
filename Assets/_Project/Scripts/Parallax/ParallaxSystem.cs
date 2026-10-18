using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Root of the parallax effect. Collects all layers below it and ticks them in order.
/// <see cref="baseSpeed"/> is the authored world speed; <see cref="speedMultiplier"/>
/// is the live value that brakes to 0 when a battle ends and returns to baseSpeed after.
/// </summary>
[DefaultExecutionOrder(-100)]
public class ParallaxSystem : MonoBehaviour
{
    [Tooltip("Normal world scrolling speed - every layer scrolls at its own speed times this.")]
    public float baseSpeed = 1f;

    /// <summary>Live multiplier applied to every layer (baseSpeed while running, 0 while stopped).</summary>
    public float speedMultiplier { get; private set; }

    readonly List<ParallaxLayer> layers = new();

    public IReadOnlyList<ParallaxLayer> Layers => layers;

    Tween speedTween;

    void Awake()
    {
        speedMultiplier = baseSpeed;

        layers.Clear();
        layers.AddRange(GetComponentsInChildren<ParallaxLayer>(true));
    }

    void Update()
    {
        float deltaTime = Time.deltaTime * speedMultiplier;

        foreach (var layer in layers)
        {
            layer.Tick(deltaTime);
        }
    }

    /// <summary>Eases the scrolling to a full stop (played when the battle ends).</summary>
    public void BrakeToStop(float duration)
    {
        speedTween?.Kill();

        speedTween = DOTween.To(() => speedMultiplier, value => speedMultiplier = value, 0f,
                Mathf.Max(0.01f, duration))
            .SetEase(Ease.OutQuad);
    }

    /// <summary>Returns to the authored world speed (used when the next battle starts).</summary>
    public void ResumeScrolling()
    {
        speedTween?.Kill();
        speedMultiplier = baseSpeed;
    }

    /// <summary>Eases the scrolling back up to the authored speed (used while travelling on).</summary>
    public void ResumeScrolling(float duration)
    {
        speedTween?.Kill();

        if (duration <= 0f)
        {
            speedMultiplier = baseSpeed;
            return;
        }

        speedTween = DOTween.To(() => speedMultiplier, value => speedMultiplier = value, baseSpeed, duration)
            .SetEase(Ease.InQuad);
    }
}
