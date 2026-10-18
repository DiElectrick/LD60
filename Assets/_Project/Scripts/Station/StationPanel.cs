using UnityEngine;

/// <summary>
/// One panel of a map-node station. A <see cref="MapNode"/> lists which panels open when
/// the battle stops, by <see cref="PanelId"/>, so a new panel type only has to derive from
/// this class and get an id - the controller does not need to know about it.
/// </summary>
public abstract class StationPanel : MonoBehaviour
{
    [Tooltip("Id a map node uses to open this panel (e.g. 'module', 'shop').")]
    [SerializeField] string panelId = "panel";

    public string PanelId => panelId;

    /// <summary>True while the panel is on screen (or sliding in/out).</summary>
    public abstract bool IsOpen { get; }

    /// <summary>Opens the panel for a station of the given level (0 = the panel's own default).</summary>
    public abstract void Open(int level);

    /// <summary>Closes the panel - the station is over.</summary>
    public abstract void Close();
}
