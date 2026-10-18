using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's module storage: 6 slots, null = empty.
/// Slots hold generated <see cref="ModuleInstance"/> items (level + rolled stats),
/// not archetypes - two guns occupy two different items.
/// Registered in G (by Bootstrap) so the UI and the drag system can use it.
/// </summary>
public class ModuleInventory : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    public const int SlotCount = 6;

    [Tooltip("Archetypes the player owns when the game starts. Each one is rolled into an item.")]
    public List<ModuleData> startingModules = new();

    [Tooltip("Level of the starting items. 0 = roll a level from the generation table.")]
    [SerializeField] int startingLevel = 1;

    readonly ModuleInstance[] slots = new ModuleInstance[SlotCount];

    /// <summary>Raised whenever any slot changes.</summary>
    public event Action Changed;

    public void Init()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            var data = startingModules.Count > i ? startingModules[i] : null;
            slots[i] = data != null ? ModuleGenerator.Instance.Generate(data, startingLevel) : null;
        }

        Changed?.Invoke();
    }

    public ModuleInstance Get(int index)
    {
        if (index < 0 || index >= SlotCount) return null;

        return slots[index];
    }

    /// <summary>Index of the first empty slot, or -1 when the storage is full.</summary>
    public int FreeSlotIndex
    {
        get
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] == null) return i;
            }

            return -1;
        }
    }

    /// <summary>True while at least one slot can take an item.</summary>
    public bool HasFreeSlot => FreeSlotIndex >= 0;

    /// <summary>Removes the item from the slot and returns it (null if the slot is empty).</summary>
    public ModuleInstance Take(int index)
    {
        var item = Get(index);
        if (item == null) return null;

        slots[index] = null;
        Changed?.Invoke();
        return item;
    }

    /// <summary>Puts the item into the first free slot.</summary>
    public bool TryAdd(ModuleInstance item)
    {
        if (item == null) return false;

        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] != null) continue;

            slots[i] = item;
            Changed?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>Puts the item into one specific slot (fails when the slot is taken).</summary>
    public bool TryAdd(ModuleInstance item, int index)
    {
        if (item == null || index < 0 || index >= SlotCount) return false;
        if (slots[index] != null) return false;

        slots[index] = item;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Rolls a random module and stores it in the first free slot (loot hook).</summary>
    public bool TryAddRandom(int level = 0)
    {
        var item = ModuleGenerator.Instance.GenerateRandom(level);
        if (item == null) return false;

        if (TryAdd(item)) return true;

        Debug.LogWarning($"[Inventory] No free slot for '{item.DisplayName}'");
        return false;
    }
}
