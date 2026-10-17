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

    [Tooltip("Engine modules vibrate while the city is running.")]
    public bool isEngine;

    [Tooltip("Optional icon for the inventory UI. Falls back to the prefab sprite.")]
    public Sprite icon;

    /// <summary>Sprite shown in the inventory slot (and dragged around).</summary>
    public Sprite Icon
    {
        get
        {
            if (icon != null) return icon;

            var renderer = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>(true) : null;
            return renderer != null ? renderer.sprite : null;
        }
    }
}
