using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores the amounts of every resource the player owns.
/// Registered in G (by Bootstrap) so enemies can drop loot and UI can subscribe.
/// </summary>
public class ResourceManager : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    readonly Dictionary<ResourceType, int> resources = new();

    /// <summary>(changed resource, new total)</summary>
    public event Action<ResourceType, int> Changed;

    public void Init()
    {
        resources.Clear();

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            resources[type] = 0;
        }
    }

    public int Get(ResourceType type)
    {
        return resources.TryGetValue(type, out var amount) ? amount : 0;
    }

    public void Add(ResourceType type, int amount)
    {
        if (amount <= 0) return;

        resources[type] = Get(type) + amount;
        Changed?.Invoke(type, resources[type]);
    }

    /// <summary>Spends resources if the bank has enough; returns false when it does not.</summary>
    public bool TrySpend(ResourceType type, int amount)
    {
        if (amount <= 0) return true;
        if (Get(type) < amount) return false;

        resources[type] = Get(type) - amount;
        Changed?.Invoke(type, resources[type]);
        return true;
    }

    /// <summary>Rolls every drop of a defeated enemy and adds it to the bank.</summary>
    public void AddDrops(IReadOnlyList<ResourceDrop> drops)
    {
        if (drops == null) return;

        foreach (var drop in drops)
        {
            int amount = drop.Roll();
            if (amount <= 0) continue;

            Add(drop.type, amount);
            DevLog.Info($"[Resources] +{amount} {drop.type} (total {Get(drop.type)})");
        }
    }
}
