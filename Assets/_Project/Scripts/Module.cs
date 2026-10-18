using System;
using UnityEngine;

/// <summary>
/// Runtime instance of a module, living on the spawned prefab. Holds the generated
/// <see cref="ModuleInstance"/> (level + rolled stats) so the city can read its bonus.
/// </summary>
public class Module : MonoBehaviour
{
    [SerializeField] ModuleData data;

    // The generated item this object displays. Runtime only - prefabs carry no level.
    [NonSerialized] ModuleInstance instance;

    public ModuleInstance Instance => instance;

    public ModuleData Data => instance != null ? instance.Data : data;

    /// <summary>Level of this copy (1 when the object was placed without a generated item).</summary>
    public int Level => instance != null ? instance.Level : 1;

    /// <summary>Generated bonus of this copy; falls back to the archetype's authored bonus.</summary>
    public StatsBonus Bonus
    {
        get
        {
            if (instance != null) return instance.Bonus;
            return data != null ? data.bonus : null;
        }
    }

    Shaker shaker;
    SpriteRenderer view;

    /// <summary>Sprite that draws this module (build-mode label and hover hit test use it).</summary>
    public SpriteRenderer View => view;

    void Awake()
    {
        shaker = GetComponent<Shaker>();
        view = GetComponentInChildren<SpriteRenderer>(true);
    }

    /// <summary>Called by the city right after the prefab was instantiated.</summary>
    public void Bind(ModuleInstance moduleInstance)
    {
        instance = moduleInstance;
        if (moduleInstance != null && moduleInstance.Data != null) data = moduleInstance.Data;

        name = moduleInstance != null
            ? moduleInstance.DisplayName
            : (data != null ? data.displayName : "Module");

        // Engine modules vibrate all the time while the city runs.
        if (shaker != null && data != null) shaker.SetIdleEnabled(data.isEngine);
    }
}
