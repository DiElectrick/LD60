using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's path through one run: the stops in travel order and the boss fought after
/// the last one.
/// <para>
/// This asset <b>is</b> the map. Every stop is a <see cref="MapNode"/>, which decides the
/// battle fought on the way there (<see cref="MapNode.encounter"/>) and the station that
/// opens afterwards (<see cref="MapNode.stationPanels"/>, e.g. shop or repair). To change
/// the path, reorder / add / remove stops in the inspector - no code change. The boss is a
/// plain <see cref="EncounterData"/>: from the game's point of view it is just one more
/// encounter, and winning it ends the run.
/// </para>
/// </summary>
[CreateAssetMenu(menuName = "LD60/Map Route", fileName = "MapRoute")]
public class MapRoute : ScriptableObject
{
    /// <summary>Resources folder the route is loaded from when the battle cycle has none.</summary>
    public const string ResourceFolder = "Map";

    public string displayName = "Run";

    [Tooltip("Stops of the run, in travel order. A stop = one battle (its encounter) " +
             "followed by its station (shop / repair panel).")]
    public List<MapNode> stops = new();

    [Tooltip("Encounter fought after the last stop. It is a normal encounter; " +
             "winning it ends the run.")]
    public EncounterData bossEncounter;

    /// <summary>How many stops the path has.</summary>
    public int StopCount => stops != null ? stops.Count : 0;

    public bool HasStops => StopCount > 0;

    /// <summary>Stop at the given index, or null when the index is outside the route.</summary>
    public MapNode GetStop(int index)
    {
        if (index < 0 || index >= StopCount) return null;

        return stops[index];
    }

    /// <summary>How many stops open the given station panel (e.g. "shop", "repair").</summary>
    public int CountStopsWith(string panelId)
    {
        int count = 0;

        for (int i = 0; i < StopCount; i++)
        {
            var node = stops[i];
            if (node == null) continue;

            foreach (var panel in node.stationPanels)
            {
                if (panel != panelId) continue;

                count++;
                break;
            }
        }

        return count;
    }

    /// <summary>
    /// Route used when nothing is assigned: the only (or first) route in Resources/Map,
    /// mirroring how the rest of the game falls back to Resources.
    /// </summary>
    public static MapRoute LoadDefault()
    {
        var routes = Resources.LoadAll<MapRoute>(ResourceFolder);
        if (routes == null || routes.Length == 0) return null;

        if (routes.Length > 1)
        {
            Debug.LogWarning($"[Map] {routes.Length} routes in Resources/{ResourceFolder} - using '{routes[0].name}'");
        }

        return routes[0];
    }
}
