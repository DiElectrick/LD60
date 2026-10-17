using System;
using UnityEngine;

/// <summary>
/// One resource drop of an enemy: how much of a resource it can give,
/// rolled inside the [minAmount, maxAmount] range.
/// </summary>
[Serializable]
public class ResourceDrop
{
    public ResourceType type;
    public int minAmount = 1;
    public int maxAmount = 3;

    /// <summary>Random amount for this drop (inclusive on both ends).</summary>
    public int Roll()
    {
        int min = Mathf.Min(minAmount, maxAmount);
        int max = Mathf.Max(minAmount, maxAmount);
        return UnityEngine.Random.Range(min, max + 1);
    }
}
