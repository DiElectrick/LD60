using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's module storage: 6 slots, null = empty.
/// Registered in G (by Bootstrap) so the UI and the drag system can use it.
/// </summary>
public class ModuleInventory : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    public const int SlotCount = 6;

    [Tooltip("Modules the player owns when the game starts.")]
    public List<ModuleData> startingModules = new();

    readonly ModuleData[] slots = new ModuleData[SlotCount];

    /// <summary>Raised whenever any slot changes.</summary>
    public event Action Changed;

    public void Init()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            slots[i] = startingModules.Count > i ? startingModules[i] : null;
        }
    }

    public ModuleData Get(int index)
    {
        if (index < 0 || index >= SlotCount) return null;

        return slots[index];
    }

    /// <summary>Removes the module from the slot and returns it (null if the slot is empty).</summary>
    public ModuleData Take(int index)
    {
        var data = Get(index);
        if (data == null) return null;

        slots[index] = null;
        Changed?.Invoke();
        return data;
    }

    /// <summary>Puts the module into the first free slot.</summary>
    public bool TryAdd(ModuleData data)
    {
        if (data == null) return false;

        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] != null) continue;

            slots[i] = data;
            Changed?.Invoke();
            return true;
        }

        return false;
    }
}
