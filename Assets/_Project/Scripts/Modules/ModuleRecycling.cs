using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Turns a generated module into resources.
/// Every archetype defines its own recipe (<see cref="ModuleData.recycleRecipe"/>) and how
/// fast the yield grows with the item level (<see cref="ModuleData.recyclePerLevel"/>), so a
/// level 10 gun is worth much more scrap than a level 1 one.
/// </summary>
public static class ModuleRecycling
{
    /// <summary>Level multiplier applied to every recipe amount (exactly 1 at level 1).</summary>
    public static float LevelScale(int level, float perLevel)
    {
        return 1f + Mathf.Max(0f, perLevel) * Mathf.Max(0, level - 1);
    }

    /// <summary>
    /// Rolls the archetype's recipe and scales it by the item level. Every entry comes back
    /// as a fixed amount (min == max) so it can be handed to <see cref="ResourceManager.AddDrops"/>.
    /// An empty list means the archetype has no recipe.
    /// </summary>
    public static List<ResourceDrop> Resolve(ModuleInstance item)
    {
        var result = new List<ResourceDrop>();

        if (item == null || item.Data == null) return result;

        var recipe = item.Data.recycleRecipe;
        if (recipe == null || recipe.Count == 0) return result;

        float scale = LevelScale(item.Level, item.Data.recyclePerLevel);

        foreach (var entry in recipe)
        {
            if (entry == null) continue;

            int amount = Mathf.Max(1, Mathf.RoundToInt(entry.Roll() * scale));

            result.Add(new ResourceDrop
            {
                type = entry.type,
                minAmount = amount,
                maxAmount = amount,
            });
        }

        return result;
    }

    /// <summary>"3 ScrapMetal, 1 Oil" - used by the recycling log.</summary>
    public static string Describe(IReadOnlyList<ResourceDrop> drops)
    {
        if (drops == null || drops.Count == 0) return "nothing";

        var text = new StringBuilder();

        foreach (var drop in drops)
        {
            if (text.Length > 0) text.Append(", ");

            text.Append(Mathf.Max(drop.minAmount, drop.maxAmount));
            text.Append(' ');
            text.Append(drop.type);
        }

        return text.ToString();
    }
}
