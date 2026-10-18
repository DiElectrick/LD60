using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Opens the station panels of the map node the city just arrived at.
/// <para>
/// The map only has to call <see cref="Open"/> with the node: the controller closes every
/// panel, then opens the ones the node lists, passing the node's station level. Panels are
/// discovered automatically (<see cref="StationPanel"/>), so adding a new panel type is a
/// matter of adding a component with a unique <see cref="StationPanel.PanelId"/>.
/// </para>
/// </summary>
public class StationController : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    readonly List<StationPanel> panels = new();

    public IReadOnlyList<StationPanel> Panels => panels;

    public bool IsOpen
    {
        get
        {
            foreach (var panel in panels)
            {
                if (panel != null && panel.IsOpen) return true;
            }

            return false;
        }
    }

    public void Init()
    {
        panels.Clear();
        panels.AddRange(FindObjectsByType<StationPanel>(FindObjectsInactive.Include));

        var ids = new List<string>();
        foreach (var panel in panels) ids.Add(panel.PanelId);

        Debug.Log($"[Station] {panels.Count} panel(s): {string.Join(", ", ids)}");
    }

    /// <summary>Opens the station of a map node (null = just the module panel).</summary>
    public void Open(MapNode node)
    {
        CloseAll();

        if (node == null)
        {
            // No map yet: keep the build loop working with the module panel only.
            Open("module", 0);
            return;
        }

        foreach (var panelId in node.stationPanels)
        {
            Open(panelId, node.stationLevel);
        }
    }

    /// <summary>Opens one panel of the station; returns it (null when the id is unknown).</summary>
    public StationPanel Open(string panelId, int level)
    {
        var panel = Find(panelId);
        if (panel == null)
        {
            Debug.LogWarning($"[Station] No panel with id '{panelId}'");
            return null;
        }

        panel.Open(level);
        return panel;
    }

    public void CloseAll()
    {
        foreach (var panel in panels)
        {
            if (panel != null) panel.Close();
        }
    }

    public StationPanel Find(string panelId)
    {
        foreach (var panel in panels)
        {
            if (panel != null && panel.PanelId == panelId) return panel;
        }

        return null;
    }
}
