using UnityEngine;

/// <summary>
/// Definition of a module: what it is called, what it buffs and which prefab displays it.
/// Create new ones with Assets > Create > LD60 > Module Data.
/// </summary>
[CreateAssetMenu(menuName = "LD60/Module Data", fileName = "ModuleData")]
public class ModuleData : ScriptableObject
{
    public string displayName = "Module";

    [Tooltip("Prefab that is instantiated in the scene to display this module.")]
    public Module prefab;

    public StatsBonus bonus = new();
}
