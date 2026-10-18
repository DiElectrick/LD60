using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One stop of the world map: the encounter fought on the way there and the station the
/// player gets when the city stops afterwards.
/// <para>
/// A node decides <b>which</b> station panels open (<see cref="stationPanels"/>) and at
/// <b>what level</b> (<see cref="stationLevel"/>), so a "shop" node can show the shop while
/// a "repair" node shows something else - no code changes needed. Which nodes the run
/// actually visits, and in what order, is defined by a <see cref="MapRoute"/>.
/// </para>
/// </summary>
[CreateAssetMenu(menuName = "LD60/Map Node", fileName = "MapNode")]
public class MapNode : ScriptableObject
{
    [Tooltip("Folder used when a node is loaded without one being assigned.")]
    public const string ResourceFolder = "Map";

    public string displayName = "Node";

    [Tooltip("Battle fought on the way to this stop. Empty = the battle cycle's fallback encounter.")]
    public EncounterData encounter;

    [Tooltip("Station panels opened when the battle stops (StationPanel ids). " +
             "Typical stop: 'module' + 'shop'.")]
    public List<string> stationPanels = new() { "module", "shop" };

    [Tooltip("Level of everything the station offers - the shop rolls its items ±1 around it.")]
    [Range(1, 10)]
    public int stationLevel = 3;
}
