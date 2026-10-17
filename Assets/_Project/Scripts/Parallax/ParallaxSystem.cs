using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Root of the parallax effect. Collects all layers below it and ticks them in order.
/// </summary>
[DefaultExecutionOrder(-100)]
public class ParallaxSystem : MonoBehaviour
{
    [Tooltip("Multiplier applied to every layer speed (0 = frozen).")]
    public float speedMultiplier = 1f;

    readonly List<ParallaxLayer> layers = new();

    public IReadOnlyList<ParallaxLayer> Layers => layers;

    void Awake()
    {
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
}
