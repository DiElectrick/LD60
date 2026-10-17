using UnityEngine;

/// <summary>
/// Runtime instance of a module, living on the spawned prefab.
/// Holds a reference to its definition (ModuleData) so the city can read its bonus.
/// </summary>
public class Module : MonoBehaviour
{
    [SerializeField] ModuleData data;

    public ModuleData Data => data;

    public StatsBonus Bonus => data != null ? data.bonus : null;

    Shaker shaker;

    void Awake()
    {
        shaker = GetComponent<Shaker>();
    }

    /// <summary>Called by the city right after the prefab was instantiated.</summary>
    public void Bind(ModuleData moduleData)
    {
        data = moduleData;
        name = moduleData.displayName;

        // Engine modules vibrate all the time while the city runs.
        if (shaker != null) shaker.SetIdleEnabled(moduleData.isEngine);
    }
}
