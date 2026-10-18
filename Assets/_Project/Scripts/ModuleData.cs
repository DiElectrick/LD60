using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Definition (archetype) of a module: what it is called, which prefab displays it and
/// what kind of characteristic it is about. The actual numbers of an item are rolled by
/// <see cref="ModuleGenerator"/> into a <see cref="ModuleInstance"/>.
/// Create new ones with Assets > Create > LD60 > Module Data.
/// </summary>
[CreateAssetMenu(menuName = "LD60/Module Data", fileName = "ModuleData")]
public class ModuleData : ScriptableObject
{
    public string displayName = "Module";

    [Tooltip("One line shown under the name in the item tooltip: what the module is for.")]
    [TextArea(1, 3)]
    public string description = "";

    [Tooltip("Prefab that is instantiated in the scene to display this module.")]
    public Module prefab;

    [Tooltip("Archetype stats. Used to guess the main stat when none is set below, and as the " +
             "fallback bonus for a module placed without a generated instance.")]
    public StatsBonus bonus = new();

    [Header("Generation")]
    [Tooltip("Characteristic this archetype buffs. None = the biggest stat of the bonus above " +
             "(a gun becomes attack, an engine becomes speed).")]
    public StatType mainStat = StatType.None;

    [Tooltip("Restricts which other stats may drop as extra affixes. Empty = any stat the " +
             "generation config allows.")]
    public List<StatType> extraStatPool = new();

    [Header("Presentation")]
    [Tooltip("Engine modules vibrate while the city is running.")]
    public bool isEngine;

    [Tooltip("Optional icon for the inventory UI. Falls back to the prefab sprite.")]
    public Sprite icon;

    [Header("Recycling")]
    [Tooltip("Resources this module yields when scrapped at level 1. The level scales every amount.")]
    public List<ResourceDrop> recycleRecipe = new();

    [Tooltip("Yield growth per level above 1: amount * (1 + recyclePerLevel * (level - 1)).")]
    [Min(0f)]
    public float recyclePerLevel = 0.35f;

    /// <summary>
    /// Sprite shown in the inventory, the shop and while dragging.
    /// Assign <see cref="icon"/> to use a dedicated icon image; otherwise the module's own
    /// art is used - the top-most sprite of the prefab, so a gun icon shows the cannon
    /// instead of the empty mount.
    /// </summary>
    public Sprite Icon
    {
        get
        {
            if (icon != null) return icon;

            return PrefabIcon();
        }
    }

    /// <summary>
    /// Picks the sprite that represents the module from its prefab: the art drawn on top
    /// (the cannon on a gun module), ignoring sprite masks and backgrounds that sit below
    /// the art (negative sorting order).
    /// </summary>
    Sprite PrefabIcon()
    {
        if (prefab == null) return null;

        var renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);

        Sprite best = null;
        int bestOrder = int.MinValue;

        foreach (var renderer in renderers)
        {
            if (renderer == null || renderer.sprite == null) continue;
            if (renderer.GetComponent<SpriteMask>() != null) continue;

            // Backgrounds / masks sit below the art - they are not the module's look.
            int order = renderer.sortingOrder;
            if (order < 0) continue;

            if (order < bestOrder) continue;

            best = renderer.sprite;
            bestOrder = order;
        }

        if (best != null) return best;

        // Everything was a background: fall back to the first sprite there is.
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.sprite != null) return renderer.sprite;
        }

        return null;
    }
}
