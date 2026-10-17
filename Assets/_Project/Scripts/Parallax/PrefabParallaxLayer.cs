using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Layer made of prefabs: objects spawn just outside the right edge of the view in a
/// random order, scroll left together with the layer and are destroyed once they
/// leave the view, so the scene never grows.
/// </summary>
public class PrefabParallaxLayer : ParallaxLayer
{
    [Tooltip("One of these prefabs is picked at random for every spawn.")]
    public List<GameObject> prefabs = new();

    [Tooltip("Random horizontal gap between two neighbouring objects.")]
    public float minGap = 0.5f;
    public float maxGap = 2f;

    [Tooltip("How far outside the view objects are spawned / destroyed.")]
    public float spawnMargin = 2f;
    public float despawnMargin = 2f;

    readonly List<Transform> spawned = new();

    // World X where the next object will be placed.
    float nextSpawnX;

    void Start()
    {
        nextSpawnX = ViewLeft - spawnMargin;
        Fill();
    }

    protected override void Move(float distance)
    {
        // The layer itself moves - its children follow along.
        transform.position += Vector3.left * distance;

        // The spawn cursor lives in world space, so it has to travel with the layer too.
        nextSpawnX -= distance;

        Fill();
        Despawn();
    }

    void Fill()
    {
        if (prefabs.Count == 0) return;

        // Spawn until the right side of the view is covered.
        for (int i = 0; i < 32 && nextSpawnX < ViewRight + spawnMargin; i++)
        {
            Spawn();
        }
    }

    void Spawn()
    {
        var prefab = prefabs[Random.Range(0, prefabs.Count)];

        var instance = Instantiate(prefab);
        instance.transform.position = new Vector3(nextSpawnX, transform.position.y, 0f);
        instance.transform.SetParent(transform, true);

        spawned.Add(instance.transform);

        nextSpawnX += MeasureWidth(instance.transform) + Random.Range(minGap, maxGap);
    }

    void Despawn()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            var instance = spawned[i];

            if (instance == null)
            {
                spawned.RemoveAt(i);
                continue;
            }

            if (RightEdge(instance) > ViewLeft - despawnMargin) continue; // still (partly) visible

            Destroy(instance.gameObject);
            spawned.RemoveAt(i);
        }
    }

    static float RightEdge(Transform instance)
    {
        var renderer = instance.GetComponentInChildren<Renderer>();
        return renderer != null ? renderer.bounds.max.x : instance.position.x;
    }

    static float MeasureWidth(Transform instance)
    {
        var renderer = instance.GetComponentInChildren<Renderer>();
        return renderer != null ? renderer.bounds.size.x : 1f;
    }
}
