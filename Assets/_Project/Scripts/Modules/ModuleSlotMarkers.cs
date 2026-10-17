using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows the NomadCity placeholder icon on the stage while a module is being dragged,
/// in the lowest free slot of every platform column.
/// Markers are pooled: they are created once and only enabled/moved afterwards.
/// </summary>
public class ModuleSlotMarkers : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    [Tooltip("Icon drawn in the next free install slot.")]
    public Sprite markerSprite;

    [Range(0f, 1f)] public float alpha = 0.45f;
    public int sortingOrder = -2;

    readonly List<GameObject> markers = new();

    CityController city;
    ModuleDragController drag;

    /// <summary>Number of markers currently visible (handy for debugging).</summary>
    public int Count
    {
        get
        {
            int count = 0;
            foreach (var marker in markers)
            {
                if (marker != null && marker.activeSelf) count++;
            }

            return count;
        }
    }

    public void Init()
    {
        city = G.Get<CityController>();
        drag = FindAnyObjectByType<ModuleDragController>(FindObjectsInactive.Include);

        if (city != null) city.ModulesChanged += Refresh;

        if (drag != null)
        {
            drag.DragStarted += Refresh;
            drag.DragEnded += Clear;
        }
    }

    void OnDestroy()
    {
        if (city != null) city.ModulesChanged -= Refresh;

        if (drag != null)
        {
            drag.DragStarted -= Refresh;
            drag.DragEnded -= Clear;
        }
    }

    /// <summary>Shows a marker in the lowest free slot of every platform (drag in progress only).</summary>
    public void Refresh()
    {
        if (city == null || markerSprite == null || (drag != null && !drag.IsDragging))
        {
            Clear();
            return;
        }

        int used = 0;

        foreach (var platform in city.Platforms)
        {
            // Only the lowest free slot of the column gets a marker.
            int slot = platform.Count;
            if (slot >= Platform.MaxModules) continue;

            var marker = GetOrCreateMarker(used);
            marker.name = $"SlotMarker {platform.name} {slot}";
            marker.SetActive(true);
            marker.transform.position = platform.transform.TransformPoint(platform.GetSlotLocalPosition(slot));

            used++;
        }

        // Hide the leftovers from a previous refresh.
        for (int i = used; i < markers.Count; i++)
        {
            markers[i].SetActive(false);
        }
    }

    public void Clear()
    {
        foreach (var marker in markers)
        {
            if (marker != null) marker.SetActive(false);
        }
    }

    GameObject GetOrCreateMarker(int index)
    {
        while (markers.Count <= index)
        {
            var marker = new GameObject("SlotMarker");
            marker.transform.SetParent(transform, false);

            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = markerSprite;
            renderer.sortingOrder = sortingOrder;

            var color = renderer.color;
            color.a = alpha;
            renderer.color = color;

            markers.Add(marker);
        }

        return markers[index];
    }
}
