using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One column of the city grid. Holds up to MaxModules modules stacked vertically.
/// </summary>
public class Platform : MonoBehaviour
{
    public const int MaxModules = 3;

    [Tooltip("Vertical distance between two modules in the column.")]
    [SerializeField] float moduleStep = 1.5f;
    [SerializeField] float startOffset = 1f;

    readonly List<Module> modules = new();

    public IReadOnlyList<Module> Modules => modules;
    public int Count => modules.Count;
    public bool IsFull => modules.Count >= MaxModules;

    public bool TryAdd(Module module)
    {
        if (module == null || IsFull) return false;

        modules.Add(module);
        module.transform.SetParent(transform, false);
        LayoutModules();
        return true;
    }

    public bool Remove(Module module)
    {
        if (!modules.Remove(module)) return false;

        LayoutModules();
        return true;
    }

    void LayoutModules()
    {
        for (int i = 0; i < modules.Count; i++)
        {
            modules[i].transform.localPosition = GetSlotLocalPosition(i);
        }
    }

    /// <summary>Local position a module sits at in the given slot.</summary>
    public Vector3 GetSlotLocalPosition(int slotIndex)
    {
        return new Vector3(0f, startOffset + slotIndex * moduleStep, 0f);
    }
}
