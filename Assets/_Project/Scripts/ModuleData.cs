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
